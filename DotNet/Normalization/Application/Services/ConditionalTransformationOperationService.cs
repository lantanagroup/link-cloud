using Hl7.Fhir.ElementModel;
using Hl7.Fhir.FhirPath;
using Hl7.Fhir.Model;
using Hl7.FhirPath;
using LantanaGroup.Link.Normalization.Application.Models.Operations;
using LantanaGroup.Link.Normalization.Application.Operations;
using System.Collections;
using System.Collections.Concurrent;
using System.Reflection;
using Task = System.Threading.Tasks.Task;

namespace LantanaGroup.Link.Normalization.Application.Services
{
    /// <summary>
    /// A background service that executes conditional transform operations on FHIR resources asynchronously via a queue.
    /// </summary>
    public class ConditionalTransformOperationService : BackgroundService
    {
        // Thread-safe queue for operations with result tasks
        private readonly ConcurrentQueue<(ConditionalTransformOperation<object> Operation, DomainResource Resource, TaskCompletionSource<OperationResult> Result)> _operationQueue = new();

        // Dictionary for future FHIRPath-to-property mappings; currently unused pending specific requirements
        private static readonly Dictionary<string, string> FhirPathToPropertyMappings = new(StringComparer.OrdinalIgnoreCase);

        // Common FHIR suffixes to strip when mapping FHIRPath to property names
        private static readonly string[] CommonFhirSuffixes = { "DateTime", "Quantity", "String", "Boolean", "Decimal", "Integer", "Code" };

        // Cache for mapped property names
        private static readonly ConcurrentDictionary<(string, Type), string> _propertyNameCache = new();

        // Configurable timeout for operations
        private readonly TimeSpan _operationTimeout;

        // Logger for diagnostic and error logging
        private readonly ILogger<ConditionalTransformOperationService> _logger;

        // Metadata registry for FHIR resource properties (thread-safe)
        private static class FhirMetadataRegistry
        {
            private static readonly ConcurrentDictionary<Type, Dictionary<string, PropertyInfo>> _propertyCache = new();

            /// <summary>
            /// Retrieves a property's metadata for a given type and name.
            /// </summary>
            public static PropertyInfo GetProperty(Type type, string propertyName)
            {
                if (!_propertyCache.TryGetValue(type, out var properties))
                {
                    properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase)
                        .ToDictionary(p => p.Name.ToLower(), p => p, StringComparer.OrdinalIgnoreCase);
                    _propertyCache.TryAdd(type, properties);
                }
                return properties.TryGetValue(propertyName.ToLower(), out var property) ? property : null;
            }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ConditionalTransformOperationService"/> class.
        /// </summary>
        public ConditionalTransformOperationService(ILogger<ConditionalTransformOperationService> logger, TimeSpan? operationTimeout = null)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _operationTimeout = operationTimeout ?? TimeSpan.FromSeconds(120);
        }

        /// <summary>
        /// Enqueues a conditional transform operation for asynchronous execution and returns a task to await the result.
        /// </summary>
        public async Task<OperationResult> EnqueueOperationAsync(ConditionalTransformOperation<object> operation, DomainResource resource)
        {
            if (operation == null)
            {
                return OperationResult.Failure("Operation cannot be null.");
            }

            if (resource == null)
            {
                return OperationResult.Failure("Resource cannot be null.");
            }

            var tcs = new TaskCompletionSource<OperationResult>(TaskCreationOptions.RunContinuationsAsynchronously);
            _operationQueue.Enqueue((operation, resource, tcs));

            try
            {
                return await tcs.Task.WaitAsync(_operationTimeout, CancellationToken.None);
            }
            catch (TimeoutException tex)
            {
                _logger.LogError(tex, "Conditional transform operation timed out after {Timeout}.", _operationTimeout);
                return OperationResult.Failure($"Conditional transform operation timed out after {_operationTimeout}.");
            }
        }

        /// <summary>
        /// Executes the background service, processing queued conditional transform operations in batches.
        /// </summary>
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                var batch = new List<(ConditionalTransformOperation<object> Operation, DomainResource Resource, TaskCompletionSource<OperationResult> Result)>();
                while (_operationQueue.TryDequeue(out var item) && batch.Count < 10)
                {
                    batch.Add(item);
                }

                foreach (var item in batch)
                {
                    var result = ProcessOperation(item.Operation, item.Resource);
                    item.Result.SetResult(result);
                    if (result.SuccessCode != OperationStatus.Success)
                    {
                        _logger.LogError("Failed conditional transform operation: {ErrorMessage}", result.ErrorMessage);
                    }
                }

                if (batch.Count == 0)
                {
                    await Task.Delay(100, stoppingToken);
                }
            }
        }

        /// <summary>
        /// Processes a single conditional transform operation.
        /// </summary>
        private OperationResult ProcessOperation(ConditionalTransformOperation<object> operation, DomainResource resource)
        {
            var resourceCopy = resource.DeepCopy() as DomainResource;
            if (resourceCopy == null)
            {
                return OperationResult.Failure($"Failed to create a deep copy of the resource of type {resource.GetType().Name}.");
            }

            return ExecuteConditionalTransform(resourceCopy, operation);
        }

        /// <summary>
        /// Executes the conditional transform logic, checking conditions and applying the transformation if all conditions pass.
        /// </summary>
        private OperationResult ExecuteConditionalTransform(DomainResource resource, ConditionalTransformOperation<object> operation)
        {
            // Check all conditions
            foreach (var condition in operation.Conditions)
            {
                if (!condition.Is_Passed(resource))
                {
                    return OperationResult.Success(resource); // Conditions not met, return unchanged resource
                }
            }

            // All conditions passed, apply transformation
            return SetTransformValue(resource, operation.TargetFhirPath, operation.TargetValue);
        }

        /// <summary>
        /// Sets the target value at the specified FHIRPath.
        /// </summary>
        private OperationResult SetTransformValue(DomainResource resource, string targetFhirPath, object targetValue)
        {
            if (!ValidateFhirPath(targetFhirPath, out var targetValidationError))
            {
                return OperationResult.Failure($"Invalid target FHIRPath expression: {targetFhirPath}. {targetValidationError}", resource);
            }

            var scopedNode = resource.ToTypedElement();

            // Try setting value via FHIRPath
            var setResult = SetValueViaFhirPath(resource, targetFhirPath, targetValue, scopedNode);
            if (setResult.Result)
            {
                return OperationResult.Success(resource);
            }

            // Fallback to reflective setting
            var reflectiveSetResult = ResolveAndSetValueReflectively(resource, targetFhirPath, targetValue);
            if (reflectiveSetResult.Result)
            {
                return OperationResult.Success(resource);
            }

            // Final attempt: create and set target element
            var createSetResult = CreateAndSetTargetElement(resource, targetFhirPath, targetValue);
            return createSetResult.Result
                ? OperationResult.Success(resource)
                : OperationResult.Failure(createSetResult.ErrorMessage, resource);
        }

        /// <summary>
        /// Result of setting a value via FHIRPath or reflection.
        /// </summary>
        private class SetValueResult
        {
            public bool Result { get; }
            public string ErrorMessage { get; }

            public SetValueResult(bool success, string errorMessage)
            {
                Result = success;
                ErrorMessage = errorMessage ?? string.Empty;
            }

            public static SetValueResult Success() => new SetValueResult(true, string.Empty);
            public static SetValueResult Failure(string errorMessage) => new SetValueResult(false, errorMessage);
        }

        /// <summary>
        /// Sets a value at a target FHIRPath using FHIRPath evaluation.
        /// </summary>
        private SetValueResult SetValueViaFhirPath(DomainResource resource, string targetFhirPath, object targetValue, ITypedElement scopedNode)
        {
            try
            {
                var targetElements = scopedNode.Select(targetFhirPath).ToList();
                if (!targetElements.Any())
                {
                    return SetValueResult.Failure("No target elements found.");
                }

                foreach (var targetElement in targetElements)
                {
                    if (string.IsNullOrEmpty(targetElement.Location))
                    {
                        return SetValueResult.Failure($"Target element at FHIRPath {targetFhirPath} has no location.");
                    }

                    var targetPath = targetElement.Location;
                    var pathParts = targetPath.Split('.').Skip(1).ToArray();
                    var (parentPoco, propertyToSet) = NavigateFhirPath(resource, string.Join(".", pathParts), createIfMissing: true);

                    if (parentPoco == null || propertyToSet == null)
                    {
                        return SetValueResult.Failure($"Could not resolve parent or property for target path {targetPath} in resource type {resource.TypeName}.");
                    }

                    if (!propertyToSet.CanWrite)
                    {
                        return SetValueResult.Failure($"Property {propertyToSet.Name} on type {parentPoco.GetType().Name} is not writable for FHIRPath {targetFhirPath}.");
                    }

                    var convertedValue = ConvertToFhirType(targetValue, propertyToSet.PropertyType, parentPoco, propertyToSet.Name);
                    propertyToSet.SetValue(parentPoco, convertedValue);
                }
                return SetValueResult.Success();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to evaluate target FHIRPath '{TargetFhirPath}' for resource type {ResourceType}.", targetFhirPath, resource.TypeName);
                return SetValueResult.Failure($"Failed to evaluate target FHIRPath '{targetFhirPath}': {ex.Message}");
            }
        }

        /// <summary>
        /// Sets a value at a target FHIRPath using reflection.
        /// </summary>
        private SetValueResult ResolveAndSetValueReflectively(DomainResource resource, string targetFhirPath, object targetValue)
        {
            var pathParts = targetFhirPath.Split('.');
            if (pathParts.Length < 2)
            {
                return SetValueResult.Failure("Target FHIRPath is too short to resolve parent and property.");
            }

            try
            {
                var (parentPoco, property) = NavigateFhirPath(resource, targetFhirPath, createIfMissing: true);
                if (property == null || !property.CanWrite)
                {
                    return SetValueResult.Failure($"Property not found or not writable for FHIRPath {targetFhirPath}.");
                }

                var convertedValue = ConvertToFhirType(targetValue, property.PropertyType, parentPoco, property.Name);
                property.SetValue(parentPoco, convertedValue);
                return SetValueResult.Success();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to resolve and set value reflectively for FHIRPath '{TargetFhirPath}'.", targetFhirPath);
                return SetValueResult.Failure($"Failed to set value reflectively for FHIRPath '{targetFhirPath}': {ex.Message}");
            }
        }

        /// <summary>
        /// Creates a parent structure and sets a value at a target FHIRPath.
        /// </summary>
        private SetValueResult CreateAndSetTargetElement(Resource resource, string targetFhirPath, object newValue)
        {
            var pathParts = targetFhirPath.Split('.');
            if (pathParts.Length < 2)
            {
                return SetValueResult.Failure($"Target FHIRPath {targetFhirPath} is too short to resolve parent and property in resource type {resource.TypeName}.");
            }

            var propertyName = pathParts.Last();
            int? arrayIndex = null;
            if (propertyName.Contains("[") && propertyName.EndsWith("]"))
            {
                (propertyName, arrayIndex) = ParseFhirPathPart(propertyName);
            }

            var parentPath = string.Join(".", pathParts.Take(pathParts.Length - 1));
            var parentPoco = CreateParentStructure(resource, parentPath);
            if (parentPoco == null)
            {
                return SetValueResult.Failure($"Could not create parent structure for {parentPath} in resource type {resource.TypeName}.");
            }

            propertyName = MapFhirPathToPropertyName(propertyName, parentPoco.GetType());
            var property = FhirMetadataRegistry.GetProperty(parentPoco.GetType(), propertyName);
            if (property == null)
            {
                return SetValueResult.Failure($"Property {propertyName} not found on parent type {parentPoco.GetType().Name} for FHIRPath {targetFhirPath}.");
            }

            try
            {
                var convertedValue = ConvertToFhirType(newValue, property.PropertyType, parentPoco, propertyName);
                if (typeof(IList).IsAssignableFrom(property.PropertyType))
                {
                    var list = property.GetValue(parentPoco) as IList;
                    if (list == null)
                    {
                        list = (IList)Activator.CreateInstance(property.PropertyType);
                        property.SetValue(parentPoco, list);
                    }

                    if (arrayIndex.HasValue)
                    {
                        while (list.Count <= arrayIndex.Value)
                        {
                            var itemType = property.PropertyType.GenericTypeArguments[0];
                            var newItem = Activator.CreateInstance(itemType);
                            list.Add(newItem);
                        }
                        list[arrayIndex.Value] = convertedValue;
                    }
                    else
                    {
                        if (list.Count == 0)
                        {
                            list.Add(convertedValue);
                        }
                        else
                        {
                            list[0] = convertedValue;
                        }
                    }
                }
                else
                {
                    property.SetValue(parentPoco, convertedValue);
                }
                return SetValueResult.Success();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to set target element for FHIRPath '{TargetFhirPath}' in resource type {ResourceType}.", targetFhirPath, resource.TypeName);
                return SetValueResult.Failure($"Failed to set target element for FHIRPath '{targetFhirPath}': {ex.Message}");
            }
        }

        /// <summary>
        /// Creates the parent structure for a given FHIRPath, ensuring all intermediate objects exist.
        /// </summary>
        private Base CreateParentStructure(Resource resource, string parentPath)
        {
            if (string.IsNullOrEmpty(parentPath))
            {
                return resource;
            }

            var pathParts = parentPath.Split('.');
            Base currentObject = resource;
            object previousObject = null;
            PropertyInfo previousProperty = null;

            foreach (var part in pathParts)
            {
                var (propertyName, arrayIndex) = ParseFhirPathPart(part);
                propertyName = MapFhirPathToPropertyName(propertyName, currentObject.GetType());

                var property = FhirMetadataRegistry.GetProperty(currentObject.GetType(), propertyName);
                if (property == null)
                {
                    return null;
                }

                if (typeof(IList).IsAssignableFrom(property.PropertyType))
                {
                    var list = property.GetValue(currentObject) as IList;
                    if (list == null)
                    {
                        list = (IList)Activator.CreateInstance(property.PropertyType);
                        if (previousProperty != null && previousObject != null)
                        {
                            previousProperty.SetValue(previousObject, list);
                        }
                        else
                        {
                            property.SetValue(currentObject, list);
                        }
                    }

                    var itemType = property.PropertyType.GenericTypeArguments[0];
                    while (list.Count <= (arrayIndex ?? 0))
                    {
                        var newItem = Activator.CreateInstance(itemType);
                        list.Add(newItem);
                    }

                    previousObject = currentObject;
                    previousProperty = property;
                    currentObject = list[arrayIndex ?? 0] as Base;
                }
                else
                {
                    var value = property.GetValue(currentObject) as Base;
                    if (value == null)
                    {
                        value = Activator.CreateInstance(property.PropertyType) as Base;
                        if (previousProperty != null && previousObject != null)
                        {
                            previousProperty.SetValue(previousObject, value);
                        }
                        else
                        {
                            property.SetValue(currentObject, value);
                        }
                    }

                    previousObject = currentObject;
                    previousProperty = property;
                    currentObject = value;
                }

                if (currentObject == null)
                {
                    return null;
                }
            }

            return currentObject;
        }

        /// <summary>
        /// Navigates a FHIRPath to locate a parent object and its property.
        /// </summary>
        private (Base Parent, PropertyInfo Property) NavigateFhirPath(object resource, string fhirPath, bool createIfMissing = false)
        {
            var pathParts = fhirPath.Split('.');
            Base currentObject = resource as Base;
            object previousObject = null;
            PropertyInfo previousProperty = null;
            PropertyInfo targetProperty = null;
            string propertyName = null;
            int? arrayIndex = null;

            for (int i = 0; i < pathParts.Length; i++)
            {
                var part = pathParts[i];
                (propertyName, arrayIndex) = ParseFhirPathPart(part);
                propertyName = MapFhirPathToPropertyName(propertyName, currentObject?.GetType());

                targetProperty = FhirMetadataRegistry.GetProperty(currentObject.GetType(), propertyName);
                if (targetProperty == null)
                {
                    return (null, null);
                }

                if (i == pathParts.Length - 1)
                {
                    break;
                }

                if (typeof(IList).IsAssignableFrom(targetProperty.PropertyType))
                {
                    var list = targetProperty.GetValue(currentObject) as IList;
                    if (list == null && createIfMissing)
                    {
                        list = (IList)Activator.CreateInstance(targetProperty.PropertyType);
                        if (previousProperty != null && previousObject != null)
                        {
                            previousProperty.SetValue(previousObject, list);
                        }
                        else
                        {
                            targetProperty.SetValue(currentObject, list);
                        }
                    }

                    if (list != null && arrayIndex.HasValue)
                    {
                        var itemType = targetProperty.PropertyType.GenericTypeArguments[0];
                        while (list.Count <= arrayIndex.Value)
                        {
                            var newItem = Activator.CreateInstance(itemType);
                            list.Add(newItem);
                        }
                        previousObject = currentObject;
                        previousProperty = targetProperty;
                        currentObject = list[arrayIndex.Value] as Base;
                    }
                    else if (list != null)
                    {
                        previousObject = currentObject;
                        previousProperty = targetProperty;
                        currentObject = list.Count > 0 ? list[0] as Base : null;
                    }
                    else
                    {
                        return (null, null);
                    }
                }
                else
                {
                    var value = targetProperty.GetValue(currentObject) as Base;
                    if (value == null && createIfMissing)
                    {
                        value = Activator.CreateInstance(targetProperty.PropertyType) as Base;
                        if (previousProperty != null && previousObject != null)
                        {
                            previousProperty.SetValue(previousObject, value);
                        }
                        else
                        {
                            targetProperty.SetValue(currentObject, value);
                        }
                    }

                    previousObject = currentObject;
                    previousProperty = targetProperty;
                    currentObject = value;
                }

                if (currentObject == null)
                {
                    return (null, null);
                }
            }

            return (currentObject, targetProperty);
        }

        /// <summary>
        /// Maps a FHIRPath property name to a .NET property name, accounting for FHIR conventions and suffixes.
        /// </summary>
        private string MapFhirPathToPropertyName(string fhirPathName, Type parentType)
        {
            if (parentType != null && _propertyNameCache.TryGetValue((fhirPathName, parentType), out var cachedName))
            {
                return cachedName;
            }

            string normalizedFhirPathName = fhirPathName.ToLower();
            string result = null;

            if (FhirPathToPropertyMappings.TryGetValue(normalizedFhirPathName, out string mappedName))
            {
                result = mappedName;
            }
            else
            {
                var pascalCase = char.ToUpper(fhirPathName[0]) + (fhirPathName.Length > 1 ? fhirPathName.Substring(1) : string.Empty);
                if (parentType != null && FhirMetadataRegistry.GetProperty(parentType, pascalCase) != null)
                {
                    result = pascalCase;
                }
                else
                {
                    string baseName = fhirPathName;
                    foreach (var suffix in CommonFhirSuffixes)
                    {
                        if (baseName.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
                        {
                            baseName = baseName.Substring(0, baseName.Length - suffix.Length);
                            break;
                        }
                    }
                    if (!string.IsNullOrEmpty(baseName))
                    {
                        var basePascalCase = char.ToUpper(baseName[0]) + (baseName.Length > 1 ? baseName.Substring(1) : string.Empty);
                        if (parentType != null && FhirMetadataRegistry.GetProperty(parentType, basePascalCase) != null)
                        {
                            result = basePascalCase;
                        }
                    }
                }
            }

            result ??= fhirPathName;
            if (parentType != null)
            {
                _propertyNameCache.TryAdd((fhirPathName, parentType), result);
            }

            return result;
        }

        /// <summary>
        /// Parses a FHIRPath part into a property name and optional array index.
        /// </summary>
        private (string propertyName, int? arrayIndex) ParseFhirPathPart(string part)
        {
            if (part.Contains("[") && part.EndsWith("]"))
            {
                var indexStart = part.IndexOf('[');
                var indexEnd = part.IndexOf(']');
                if (indexStart >= indexEnd || indexStart == part.Length - 1)
                {
                    return (part, null);
                }

                var indexStr = part.Substring(indexStart + 1, indexEnd - indexStart - 1);
                if (string.IsNullOrEmpty(indexStr) || !int.TryParse(indexStr, out int index) || index < 0)
                {
                    return (part, null);
                }
                return (part.Substring(0, indexStart), index);
            }
            return (part, null);
        }

        /// <summary>
        /// Validates a FHIRPath expression for basic syntax.
        /// </summary>
        private bool ValidateFhirPath(string fhirPath, out string errorMessage)
        {
            try
            {
                var compiler = new FhirPathCompiler();
                compiler.Compile(fhirPath);
                errorMessage = string.Empty;
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Invalid FHIRPath expression: {FhirPath}.", fhirPath);
                errorMessage = ex.Message;
                return false;
            }
        }

        /// <summary>
        /// Converts a value to a FHIR-compatible type for a given property.
        /// </summary>
        private object ConvertToFhirType(object newValue, Type propertyType, Base parentPoco, string propertyName)
        {
            if (newValue == null) return null;

            try
            {
                if (newValue is string strValue)
                {
                    if (propertyType == typeof(FhirString)) return new FhirString(strValue);
                    if (propertyType == typeof(string)) return strValue;
                    if (propertyType == typeof(Code)) return new Code(strValue);
                }
                else if (newValue is int intValue)
                {
                    if (propertyType == typeof(Integer)) return new Integer(intValue);
                    if (propertyType == typeof(decimal) || propertyType == typeof(decimal?)) return (decimal)intValue;
                    if (propertyType == typeof(FhirString)) return new FhirString(intValue.ToString(System.Globalization.CultureInfo.InvariantCulture));
                    if (propertyType == typeof(string)) return intValue.ToString(System.Globalization.CultureInfo.InvariantCulture);
                }
                else if (newValue is bool boolValue)
                {
                    if (propertyType == typeof(FhirBoolean)) return new FhirBoolean(boolValue);
                    if (propertyType == typeof(FhirString)) return new FhirString(boolValue.ToString());
                    if (propertyType == typeof(string)) return boolValue.ToString();
                }
                else if (newValue is decimal decValue)
                {
                    if (propertyType == typeof(FhirDecimal)) return new FhirDecimal(decValue);
                    if (propertyType == typeof(decimal) || propertyType == typeof(decimal?)) return decValue;
                    if (propertyType == typeof(FhirString)) return new FhirString(decValue.ToString(System.Globalization.CultureInfo.InvariantCulture));
                    if (propertyType == typeof(string)) return decValue.ToString(System.Globalization.CultureInfo.InvariantCulture);
                }
                else if (newValue is DateTime dateValue)
                {
                    if (propertyType == typeof(FhirDateTime)) return new FhirDateTime(dateValue);
                    if (propertyType == typeof(FhirString)) return new FhirString(dateValue.ToString("o", System.Globalization.CultureInfo.InvariantCulture));
                    if (propertyType == typeof(string)) return dateValue.ToString("o", System.Globalization.CultureInfo.InvariantCulture);
                }
                else if (newValue is Enum enumValue && propertyType.IsEnum)
                {
                    return Enum.Parse(propertyType, enumValue.ToString());
                }
                else if (newValue is Base complexValue && propertyType.IsAssignableFrom(complexValue.GetType()))
                {
                    return complexValue;
                }

                return newValue;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to convert value of type {ValueType} to property {PropertyName} of type {PropertyType}.", newValue.GetType().Name, propertyName, propertyType.Name);
                return null;
            }
        }
    }
}

