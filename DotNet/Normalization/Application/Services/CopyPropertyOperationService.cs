using Hl7.Fhir.ElementModel;
using Hl7.Fhir.FhirPath;
using Hl7.Fhir.Model;
using Hl7.FhirPath;
using LantanaGroup.Link.Normalization.Application.Models.Operations;
using System.Collections;
using System.Collections.Concurrent;
using System.Reflection;
using Task = System.Threading.Tasks.Task;

namespace LantanaGroup.Link.Normalization.Application.Operations
{
    /// <summary>
    /// A background service that executes copy operations on FHIR resources asynchronously via a queue.
    /// </summary>
    public class CopyPropertyOperationService : BackgroundService
    {
        // Thread-safe queue for operations with result tasks
        private readonly ConcurrentQueue<(CopyPropertyOperation Operation, DomainResource Resource, TaskCompletionSource<OperationResult> Result)> _operationQueue = new();

        // Dictionary for future FHIRPath-to-property mappings; currently unused pending specific requirements
        private static readonly Dictionary<string, string> FhirPathToPropertyMappings = new(StringComparer.OrdinalIgnoreCase);

        // Common FHIR suffixes to strip when mapping FHIRPath to property names
        private static readonly string[] CommonFhirSuffixes = { "DateTime", "Quantity", "String", "Boolean", "Decimal", "Integer", "Code" };

        // Cache for mapped property names
        private static readonly ConcurrentDictionary<(string, Type), string> _propertyNameCache = new();

        // Configurable timeout for operations
        private readonly TimeSpan _operationTimeout;

        // Logger for diagnostic and error logging
        private readonly ILogger<CopyPropertyOperationService> _logger;

        // Metadata registry for FHIR resource properties (thread-safe)
        private static class FhirMetadataRegistry
        {
            private static readonly ConcurrentDictionary<Type, Dictionary<string, PropertyInfo>> _propertyCache = new();

            /// <summary>
            /// Retrieves a property's metadata for a given type and name.
            /// </summary>
            /// <param name="type">The type to inspect.</param>
            /// <param name="propertyName">The name of the property.</param>
            /// <returns>The PropertyInfo if found; otherwise, null.</returns>
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
        /// Initializes a new instance of the <see cref="CopyPropertyOperationService"/> class.
        /// </summary>
        /// <param name="logger">The logger for diagnostic and error logging.</param>
        /// <param name="operationTimeout">The timeout for queued operations. Defaults to 120 seconds.</param>
        public CopyPropertyOperationService(ILogger<CopyPropertyOperationService> logger, TimeSpan? operationTimeout = null)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _operationTimeout = operationTimeout ?? TimeSpan.FromSeconds(120);
        }

        /// <summary>
        /// Enqueues a copy operation for asynchronous execution and returns a task to await the result.
        /// </summary>
        /// <param name="operation">The copy operation to execute.</param>
        /// <param name="resource">The FHIR resource to operate on.</param>
        /// <returns>A task that completes with the operation result.</returns>
        public async Task<OperationResult> EnqueueOperationAsync(CopyPropertyOperation operation, DomainResource resource)
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
                _logger.LogError(tex, "Copy operation '{OperationName}' timed out after {Timeout}.", operation.Name, _operationTimeout);
                return OperationResult.Failure($"Copy operation '{operation.Name}' timed out after {_operationTimeout}.");
            }
        }

        /// <summary>
        /// Executes the background service, processing queued copy operations in batches.
        /// </summary>
        /// <param name="stoppingToken">Cancellation token to stop the service.</param>
        /// <returns>A task representing the service's execution.</returns>
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                var batch = new List<(CopyPropertyOperation Operation, DomainResource Resource, TaskCompletionSource<OperationResult> Result)>();
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
                        _logger.LogError("Failed operation {OperationName}: {ErrorMessage}", item.Operation.Name, result.ErrorMessage);
                    }
                }

                if (batch.Count == 0)
                {
                    await Task.Delay(100, stoppingToken);
                }
            }
        }

        /// <summary>
        /// Processes a single copy operation.
        /// </summary>
        /// <param name="operation">The copy operation to execute.</param>
        /// <param name="resource">The FHIR resource to operate on.</param>
        /// <returns>The result of the operation.</returns>
        private OperationResult ProcessOperation(CopyPropertyOperation operation, DomainResource resource)
        {
            var resourceCopy = resource.DeepCopy() as DomainResource;
            if (resourceCopy == null)
            {
                return OperationResult.Failure($"Failed to create a deep copy of the resource of type {resource.GetType().Name}.");
            }

            return CopyFhirPathValue(resourceCopy, operation.SourceFhirPath, operation.TargetFhirPath, resource);
        }

        /// <summary>
        /// Copies a value from a source FHIRPath to a target FHIRPath on a resource.
        /// </summary>
        /// <param name="resource">The resource to modify.</param>
        /// <param name="sourceFhirPath">The source FHIRPath expression.</param>
        /// <param name="targetFhirPath">The target FHIRPath expression.</param>
        /// <param name="originalResource">The original resource for reference.</param>
        /// <returns>The result of the copy operation.</returns>
        private OperationResult CopyFhirPathValue(DomainResource resource, string sourceFhirPath, string targetFhirPath, DomainResource originalResource)
        {
            if (!ValidateFhirPath(sourceFhirPath, out var sourceValidationError))
            {
                return OperationResult.Failure($"Invalid source FHIRPath expression: {sourceFhirPath}. {sourceValidationError}", resource);
            }

            if (!ValidateFhirPath(targetFhirPath, out var targetValidationError))
            {
                return OperationResult.Failure($"Invalid target FHIRPath expression: {targetFhirPath}. {targetValidationError}", resource);
            }

            var scopedNode = resource.ToTypedElement();
            var sourceValueResult = ExtractValueFromFhirPath(scopedNode, sourceFhirPath);
            object sourceValue;

            if (sourceValueResult.Result)
            {
                sourceValue = sourceValueResult.Value;
            }
            else
            {
                var reflectiveValue = GetValueReflectively(resource, sourceFhirPath);
                if (reflectiveValue == null)
                {
                    return OperationResult.Failure($"No values found at source FHIRPath: {sourceFhirPath} for resource type {resource.TypeName}.", resource);
                }
                sourceValue = reflectiveValue;
            }

            if ((sourceValue is string || sourceValue is int || sourceValue is bool || sourceValue is decimal || sourceValue is DateTime)
                || (sourceValue is IList valueList && valueList.Cast<object>().All(v => v is string || v is int || v is bool || v is decimal || v is DateTime)))
            {
                var setResult = SetValueViaFhirPath(resource, targetFhirPath, sourceValue, scopedNode, sourceFhirPath, originalResource);
                if (setResult.Result)
                {
                    return OperationResult.Success(resource);
                }

                var reflectiveSetResult = ResolveAndSetValueReflectively(resource, targetFhirPath, sourceValue);
                if (reflectiveSetResult.Result)
                {
                    return OperationResult.Success(resource);
                }

                var createSetResult = CreateAndSetTargetElement(resource, targetFhirPath, sourceValue, sourceFhirPath, originalResource);
                return createSetResult.Result
                    ? OperationResult.Success(resource)
                    : OperationResult.Failure(createSetResult.ErrorMessage, resource);
            }
            else if (sourceValue is Base complexValue)
            {
                var validationResult = ValidateComplexTypeCompatibility(scopedNode, targetFhirPath, complexValue);
                if (!validationResult.Result)
                {
                    return OperationResult.Failure(validationResult.ErrorMessage, resource);
                }

                var setResult = SetValueViaFhirPath(resource, targetFhirPath, complexValue, scopedNode, sourceFhirPath, originalResource);
                if (setResult.Result)
                {
                    return OperationResult.Success(resource);
                }

                var reflectiveSetResult = ResolveAndSetValueReflectively(resource, targetFhirPath, complexValue);
                if (reflectiveSetResult.Result)
                {
                    return OperationResult.Success(resource);
                }

                var createSetResult = CreateAndSetTargetElement(resource, targetFhirPath, complexValue, sourceFhirPath, originalResource);
                return createSetResult.Result
                    ? OperationResult.Success(resource)
                    : OperationResult.Failure(createSetResult.ErrorMessage, resource);
            }
            else
            {
                return OperationResult.Failure($"Source type {sourceValue.GetType().Name} is not supported at source FHIRPath: {sourceFhirPath}.", resource);
            }
        }

        /// <summary>
        /// Result of extracting a value from a FHIRPath expression.
        /// </summary>
        private class ExtractValueResult
        {
            public bool Result { get; }
            public string ErrorMessage { get; }
            public object Value { get; }

            public ExtractValueResult(bool success, string errorMessage, object value)
            {
                Result = success;
                ErrorMessage = errorMessage ?? string.Empty;
                Value = value;
            }

            public static ExtractValueResult Success(object value) =>
                new ExtractValueResult(true, string.Empty, value);

            public static ExtractValueResult Failure(string errorMessage) =>
                new ExtractValueResult(false, errorMessage, null);
        }

        /// <summary>
        /// Extracts a value from a FHIRPath expression on a scoped node.
        /// </summary>
        /// <param name="scopedNode">The node to evaluate the FHIRPath against.</param>
        /// <param name="fhirPath">The FHIRPath expression.</param>
        /// <returns>The result of the extraction.</returns>
        private ExtractValueResult ExtractValueFromFhirPath(ITypedElement scopedNode, string fhirPath)
        {
            try
            {
                var values = scopedNode.Select(fhirPath).ToList();
                if (!values.Any())
                {
                    return ExtractValueResult.Failure("No values found.");
                }

                var pocos = values
                    .Where(v => v != null)
                    .Select(v => v.ToPoco())
                    .Where(p => p is Base)
                    .ToList();

                if (!pocos.Any() && values.Any())
                {
                    return ExtractValueResult.Failure("No valid FHIR types converted.");
                }

                if (pocos.Count == 1)
                {
                    var poco = pocos[0];
                    if (poco is PrimitiveType primitive)
                    {
                        return ExtractValueResult.Success(primitive.ObjectValue ?? null);
                    }
                    else if (poco is Quantity quantity)
                    {
                        return ExtractValueResult.Success(quantity.Value ?? null);
                    }
                    else if (poco is Base complex)
                    {
                        return ExtractValueResult.Success(complex);
                    }
                }
                else
                {
                    var result = new List<object>();
                    foreach (var poco in pocos)
                    {
                        if (poco is PrimitiveType primitive && primitive.ObjectValue != null)
                        {
                            result.Add(primitive.ObjectValue);
                        }
                        else if (poco is Quantity quantity && quantity.Value != null)
                        {
                            result.Add(quantity.Value);
                        }
                        else if (poco is Base complex)
                        {
                            result.Add(complex);
                        }
                    }
                    return result.Any() ? ExtractValueResult.Success(result) : ExtractValueResult.Failure("No valid values extracted.");
                }

                return ExtractValueResult.Failure("Unexpected POCO processing failure.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to evaluate FHIRPath '{FhirPath}' for resource type {ResourceType}.", fhirPath, scopedNode.Name);
                return ExtractValueResult.Failure($"Failed to evaluate FHIRPath '{fhirPath}': {ex.Message}");
            }
        }

        /// <summary>
        /// Retrieves a value from a resource using reflection based on a FHIRPath.
        /// </summary>
        /// <param name="resource">The resource to inspect.</param>
        /// <param name="fhirPath">The FHIRPath expression.</param>
        /// <returns>The retrieved value, or null if not found.</returns>
        private object GetValueReflectively(object resource, string fhirPath)
        {
            var pathParts = fhirPath.Split('.');
            object currentObject = resource;

            foreach (var part in pathParts)
            {
                string propertyName = part.Split('[')[0];
                int? arrayIndex = null;

                if (part.Contains("[") && part.EndsWith("]"))
                {
                    (propertyName, arrayIndex) = ParseFhirPathPart(part);
                }

                if (currentObject == null)
                {
                    return null;
                }

                propertyName = MapFhirPathToPropertyName(propertyName, currentObject.GetType());
                var property = FhirMetadataRegistry.GetProperty(currentObject.GetType(), propertyName);
                if (property == null)
                {
                    return null;
                }

                currentObject = property.GetValue(currentObject);
                if (currentObject == null)
                {
                    return null;
                }

                if (arrayIndex.HasValue && currentObject is IList list)
                {
                    if (list.Count <= arrayIndex.Value)
                    {
                        return null;
                    }
                    currentObject = list[arrayIndex.Value];
                }
            }

            if (currentObject is string || currentObject is int || currentObject is bool || currentObject is decimal || currentObject is DateTime)
            {
                return currentObject;
            }
            else if (currentObject is FhirDateTime fhirDateTime)
            {
                return fhirDateTime.Value ?? null;
            }
            else if (currentObject is Quantity quantity)
            {
                var valueProp = FhirMetadataRegistry.GetProperty(quantity.GetType(), "Value");
                if (valueProp != null)
                {
                    return valueProp.GetValue(quantity);
                }
            }
            else if (currentObject is PrimitiveType primitive)
            {
                return primitive.ObjectValue;
            }
            else if (currentObject is Base complexValue)
            {
                return complexValue;
            }

            return null;
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

            public static SetValueResult Success() =>
                new SetValueResult(true, string.Empty);

            public static SetValueResult Failure(string errorMessage) =>
                new SetValueResult(false, errorMessage);
        }

        /// <summary>
        /// Sets a value at a target FHIRPath using FHIRPath evaluation.
        /// </summary>
        /// <param name="resource">The resource to modify.</param>
        /// <param name="targetFhirPath">The target FHIRPath expression.</param>
        /// <param name="targetValue">The value to set.</param>
        /// <param name="scopedNode">The typed element for FHIRPath evaluation.</param>
        /// <param name="sourceFhirPath">The source FHIRPath for context.</param>
        /// <param name="originalResource">The original resource for reference.</param>
        /// <returns>The result of the set operation.</returns>
        private SetValueResult SetValueViaFhirPath(DomainResource resource, string targetFhirPath, object targetValue, ITypedElement scopedNode, string sourceFhirPath, DomainResource originalResource)
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

                    if (targetValue is IList valueList && typeof(IList).IsAssignableFrom(propertyToSet.PropertyType))
                    {
                        var list = (IList)Activator.CreateInstance(propertyToSet.PropertyType);
                        foreach (var item in valueList)
                        {
                            var convertedItem = ConvertToFhirType(item, propertyToSet.PropertyType.GenericTypeArguments[0], parentPoco, propertyToSet.Name);
                            list.Add(convertedItem);
                        }
                        propertyToSet.SetValue(parentPoco, list);
                    }
                    else
                    {
                        var convertedValue = ConvertToFhirType(targetValue, propertyToSet.PropertyType, parentPoco, propertyToSet.Name);
                        propertyToSet.SetValue(parentPoco, convertedValue);
                    }
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
        /// <param name="resource">The resource to modify.</param>
        /// <param name="targetFhirPath">The target FHIRPath expression.</param>
        /// <param name="targetValue">The value to set.</param>
        /// <returns>The result of the set operation.</returns>
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

                if (targetValue is IList valueList && typeof(IList).IsAssignableFrom(property.PropertyType))
                {
                    var vList = (IList)Activator.CreateInstance(property.PropertyType);
                    foreach (var item in valueList)
                    {
                        var convertedItem = ConvertToFhirType(item, property.PropertyType.GenericTypeArguments[0], parentPoco, property.Name);
                        vList.Add(convertedItem);
                    }
                    property.SetValue(parentPoco, vList);
                }
                else
                {
                    var convertedValue = ConvertToFhirType(targetValue, property.PropertyType, parentPoco, property.Name);
                    if (pathParts.Last().Contains("[") && typeof(IList).IsAssignableFrom(property.PropertyType))
                    {
                        var (_, arrayIndex) = ParseFhirPathPart(pathParts.Last());
                        var vList = property.GetValue(parentPoco) as IList;
                        if (vList == null)
                        {
                            vList = (IList)Activator.CreateInstance(property.PropertyType);
                            property.SetValue(parentPoco, vList);
                        }

                        while (vList.Count <= arrayIndex.Value)
                        {
                            var itemType = property.PropertyType.GenericTypeArguments[0];
                            var newItem = Activator.CreateInstance(itemType);
                            vList.Add(newItem);
                        }
                        vList[arrayIndex.Value] = convertedValue;
                    }
                    else
                    {
                        property.SetValue(parentPoco, convertedValue);
                    }
                }
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
        /// <param name="resource">The resource to modify.</param>
        /// <param name="targetFhirPath">The target FHIRPath expression.</param>
        /// <param name="newValue">The value to set.</param>
        /// <param name="sourceFhirPath">The source FHIRPath for context.</param>
        /// <param name="originalResource">The original resource for reference.</param>
        /// <returns>The result of the set operation.</returns>
        private SetValueResult CreateAndSetTargetElement(Resource resource, string targetFhirPath, object newValue, string sourceFhirPath, DomainResource originalResource)
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
                if (newValue is IList valueList && typeof(IList).IsAssignableFrom(property.PropertyType))
                {
                    var list = (IList)Activator.CreateInstance(property.PropertyType);
                    foreach (var item in valueList)
                    {
                        var convertedItem = ConvertToFhirType(item, property.PropertyType.GenericTypeArguments[0], parentPoco, propertyName);
                        list.Add(convertedItem);
                    }
                    property.SetValue(parentPoco, list);
                }
                else
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
        /// <param name="resource">The resource to navigate.</param>
        /// <param name="parentPath">The FHIRPath to the parent object.</param>
        /// <returns>The parent object, or the resource if the path is empty, or null on failure.</returns>
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
        /// <param name="resource">The resource to navigate.</param>
        /// <param name="fhirPath">The FHIRPath expression.</param>
        /// <param name="createIfMissing">Whether to create missing objects in the path.</param>
        /// <returns>A tuple containing the parent object and the target property.</returns>
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
        /// Validates the compatibility of a complex type for a target FHIRPath.
        /// </summary>
        /// <param name="scopedNode">The node for FHIRPath evaluation.</param>
        /// <param name="targetFhirPath">The target FHIRPath expression.</param>
        /// <param name="copiedObject">The complex object to validate.</param>
        /// <returns>The result of the validation.</returns>
        private SetValueResult ValidateComplexTypeCompatibility(ITypedElement scopedNode, string targetFhirPath, Base copiedObject)
        {
            try
            {
                var pathParts = targetFhirPath.Split('.');
                if (pathParts.Length < 2)
                {
                    return SetValueResult.Success();
                }

                var parentPath = string.Join(".", pathParts.Take(pathParts.Length - 1));
                var parentNodes = scopedNode.Select(parentPath).ToList();
                var parentNode = parentNodes.FirstOrDefault();
                if (parentNode == null)
                {
                    return SetValueResult.Success();
                }

                var parentPoco = parentNode.ToPoco() as Base;
                if (parentPoco == null)
                {
                    return SetValueResult.Success();
                }

                var propertyName = pathParts.Last().Split('[')[0];
                propertyName = MapFhirPathToPropertyName(propertyName, parentPoco.GetType());
                var property = FhirMetadataRegistry.GetProperty(parentPoco.GetType(), propertyName);
                if (property != null && !property.PropertyType.IsAssignableFrom(copiedObject.GetType()) &&
                    !(typeof(IList).IsAssignableFrom(property.PropertyType) &&
                      property.PropertyType.GenericTypeArguments.Length > 0 &&
                      property.PropertyType.GenericTypeArguments[0].IsAssignableFrom(copiedObject.GetType())))
                {
                    return SetValueResult.Failure(
                        $"Target property {propertyName} of type {property.PropertyType.Name} cannot accept source object of type {copiedObject.GetType().Name} for FHIRPath {targetFhirPath}.");
                }

                return SetValueResult.Success();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to validate complex type compatibility for FHIRPath '{TargetFhirPath}' for resource type {ResourceType}.", targetFhirPath, scopedNode.Name);
                return SetValueResult.Failure($"Failed to validate complex type compatibility for FHIRPath '{targetFhirPath}': {ex.Message}");
            }
        }

        /// <summary>
        /// Maps a FHIRPath property name to a .NET property name, accounting for FHIR conventions and suffixes.
        /// </summary>
        /// <param name="fhirPathName">The FHIRPath property name to map.</param>
        /// <param name="parentType">The type of the parent object, used for context-specific mapping.</param>
        /// <returns>The mapped property name.</returns>
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
        /// <param name="part">The FHIRPath part to parse.</param>
        /// <returns>A tuple containing the property name and array index, if present.</returns>
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
        /// <param name="fhirPath">The FHIRPath expression to validate.</param>
        /// <param name="errorMessage">The error message if validation fails.</param>
        /// <returns>True if valid; otherwise, false.</returns>
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
        /// <param name="newValue">The value to convert.</param>
        /// <param name="propertyType">The target property type.</param>
        /// <param name="parentPoco">The parent FHIR object.</param>
        /// <param name="propertyName">The name of the property.</param>
        /// <returns>The converted value.</returns>
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
                else if (newValue is CodeableConcept codeableConcept && propertyType == typeof(CodeableConcept))
                {
                    return codeableConcept;
                }
                else if (newValue is Coding coding && propertyType == typeof(Coding))
                {
                    return coding;
                }
                else if (newValue is Period period && propertyType == typeof(Period))
                {
                    return period;
                }
                else if (newValue is ResourceReference resourceReference && propertyType == typeof(ResourceReference))
                {
                    return resourceReference;
                }
                else if (newValue is Base complexValue && !propertyType.IsAssignableFrom(complexValue.GetType()))
                {
                    return null;
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