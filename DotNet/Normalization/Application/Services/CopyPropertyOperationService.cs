using Hl7.Fhir.ElementModel;
using Hl7.Fhir.FhirPath;
using Hl7.Fhir.Model;
using Hl7.FhirPath;
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
        private readonly ConcurrentQueue<(CopyPropertyOperation Operation, DomainResource Resource, TaskCompletionSource<DomainResource> Result)> _operationQueue = new ConcurrentQueue<(CopyPropertyOperation, DomainResource, TaskCompletionSource<DomainResource>)>();

        // Dictionary for future FHIRPath-to-property mappings; currently unused pending specific requirements
        private static readonly Dictionary<string, string> FhirPathToPropertyMappings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        // Common FHIR suffixes to strip when mapping FHIRPath to property names
        private static readonly string[] CommonFhirSuffixes = { "DateTime", "Quantity", "String", "Boolean", "Decimal", "Integer", "Code" };

        // Cache for mapped property names (non-concurrent since operations are one-off within execution)
        private static readonly Dictionary<(Type, string), string> _propertyNameCache = new Dictionary<(Type, string), string>();

        // Metadata registry for FHIR resource properties (thread-safe)
        private static class FhirMetadataRegistry
        {
            private static readonly ConcurrentDictionary<Type, Dictionary<string, PropertyInfo>> _propertyCache = new ConcurrentDictionary<Type, Dictionary<string, PropertyInfo>>();

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
        public CopyPropertyOperationService()
        {
        }

        /// <summary>
        /// Enqueues a copy operation for asynchronous execution and returns a task to await the result.
        /// </summary>
        /// <param name="operation">The copy operation to execute.</param>
        /// <param name="resource">The FHIR resource to operate on.</param>
        /// <returns>A task that completes with the modified resource.</returns>
        public async Task<DomainResource> EnqueueOperationAsync(CopyPropertyOperation operation, DomainResource resource)
        {
            if (operation == null)
            {
                throw new ArgumentNullException(nameof(operation));
            }
            if (resource == null)
            {
                throw new ArgumentNullException(nameof(resource));
            }

            Console.WriteLine($"Enqueuing operation: {operation.Name}");
            var tcs = new TaskCompletionSource<DomainResource>();
            _operationQueue.Enqueue((operation, resource, tcs));

            // Add timeout to prevent hanging
            return await tcs.Task.WaitAsync(TimeSpan.FromSeconds(5));
        }

        /// <summary>
        /// Executes the background service, processing queued copy operations.
        /// </summary>
        /// <param name="stoppingToken">Cancellation token to stop the service.</param>
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            Console.WriteLine("CopyPropertyOperationService started.");
            while (!stoppingToken.IsCancellationRequested)
            {
                if (_operationQueue.TryDequeue(out var item))
                {
                    Console.WriteLine($"Processing operation: {item.Operation.Name}");
                    try
                    {
                        var resourceCopy = item.Resource.DeepCopy() as DomainResource
                            ?? throw new InvalidOperationException($"Failed to create a deep copy of the resource of type {item.Resource.GetType().Name}.");
                        CopyFhirPathValue(resourceCopy, item.Operation.SourceFhirPath, item.Operation.TargetFhirPath, item.Resource);
                        item.Result.SetResult(resourceCopy);
                        Console.WriteLine($"Completed operation: {item.Operation.Name}");
                    }
                    catch (Exception ex)
                    {
                        item.Result.SetException(ex);
                        Console.WriteLine($"Failed to execute operation {item.Operation.Name}: {ex.Message}");
                    }
                }
                else
                {
                    await Task.Delay(10, stoppingToken);
                }
            }
            Console.WriteLine("CopyPropertyOperationService stopped.");
        }

        // Per-resource FHIRPath cache (cleared after each operation)
        private readonly Dictionary<string, List<ITypedElement>> _fhirPathCache = new Dictionary<string, List<ITypedElement>>();

        private void CopyFhirPathValue(DomainResource resource, string sourceFhirPath, string targetFhirPath, DomainResource originalResource)
        {
            var scopedNode = resource.ToTypedElement();

            // Extract source values (supporting collections)
            var targetValues = ExtractValueFromFhirPath(scopedNode, sourceFhirPath)
                ?? GetValueReflectively(resource, sourceFhirPath)
                ?? throw new InvalidOperationException($"No values found at source FHIRPath: {sourceFhirPath} for resource type {resource.TypeName}.");

            if ((targetValues is string || targetValues is int || targetValues is bool || targetValues is decimal || targetValues is DateTime) // Is primitive
                || (targetValues is IList valueList && valueList.Cast<object>().All(v => v is string || v is int || v is bool || v is decimal || v is DateTime))) // or is list of primitives
            {
                if (targetFhirPath.Contains("component") && (targetFhirPath.EndsWith("value") || targetFhirPath.EndsWith("value.value")))
                {
                    SetComponentValuesReflectively(resource, targetValues, targetFhirPath);
                }
                else
                {
                    var resolvedViaFhirPath = SetValueViaFhirPath(resource, targetFhirPath, targetValues, scopedNode, sourceFhirPath, originalResource);
                    if (!resolvedViaFhirPath)
                    {
                        SetTargetValue(resource, targetFhirPath, targetValues, sourceFhirPath, originalResource);
                    }
                }
            }
            else if (targetValues is Base complexValue)
            {
                var copiedObject = complexValue.DeepCopy() as Base;
                ValidateComplexTypeCompatibility(scopedNode, targetFhirPath, copiedObject);

                var resolvedViaFhirPath = SetValueViaFhirPath(resource, targetFhirPath, copiedObject, scopedNode, sourceFhirPath, originalResource);
                if(!resolvedViaFhirPath)
                {
                    SetTargetValue(resource, targetFhirPath, copiedObject, sourceFhirPath, originalResource);
                }
            }
            else
            {
                throw new InvalidOperationException($"Source type {targetValues.GetType().Name} is not supported at source FHIRPath: {sourceFhirPath}.");
            }

            // Verify component values for component targets
            if (targetFhirPath.Contains("component") && (targetFhirPath.EndsWith("value") || targetFhirPath.EndsWith("value.value")))
            {
                if (resource is Observation observation)
                {
                    if (observation.Component == null || !observation.Component.Any())
                    {
                        throw new InvalidOperationException($"Failed to set value at target FHIRPath: {targetFhirPath} for resource type {resource.TypeName}. No components found.");
                    }
                    foreach (var value in targetValues is IList list ? list.Cast<object>() : new[] { targetValues })
                    {
                        if (observation.Component.Any(c => c.Value is Quantity q && q.Value != (value is int i ? (decimal)i : (decimal)value)))
                        {
                            throw new InvalidOperationException($"Failed to set value at target FHIRPath: {targetFhirPath} for resource type {resource.TypeName}. Component value mismatch.");
                        }
                    }
                }
            }

            _fhirPathCache.Clear(); // Clear cache after operation
        }

        private object ExtractValueFromFhirPath(ITypedElement scopedNode, string fhirPath)
        {
            try
            {
                if (!_fhirPathCache.TryGetValue(fhirPath, out var values))
                {
                    values = scopedNode.Select(fhirPath).ToList();
                    _fhirPathCache[fhirPath] = values;
                }

                if (!values.Any())
                {
                    return null;
                }

                var pocos = values.Where(v => v != null).Select(v => v.ToPoco()).Where(p => p != null).ToList();
                if (!pocos.Any())
                {
                    return null;
                }

                if (pocos.Count == 1)
                {
                    var poco = pocos[0];
                    if (poco is PrimitiveType primitive)
                    {
                        return primitive.ObjectValue ?? null;
                    }
                    else if (poco is Quantity quantity)
                    {
                        return quantity.Value ?? null;
                    }
                    else if (poco is Base complex)
                    {
                        return complex.DeepCopy() as Base;
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
                            result.Add(complex.DeepCopy() as Base);
                        }
                    }
                    return result.Any() ? result : null;
                }

                return null;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to evaluate FHIRPath '{fhirPath}' for resource type {scopedNode.Name}: {ex.Message}", ex);
            }
        }

        private object GetValueReflectively(object resource, string fhirPath)
        {
            var pathParts = fhirPath.Split('.');
            object current = resource;

            foreach (var part in pathParts)
            {
                string propertyName = part.Split('[')[0];
                int? arrayIndex = null;

                if (part.Contains("[") && part.EndsWith("]"))
                {
                    (propertyName, arrayIndex) = ParseFhirPathPart(part);
                }

                if (current == null)
                {
                    return null;
                }

                var possiblePropertyNames = MapFhirPathToPropertyName(propertyName, current.GetType());

                PropertyInfo property = null;
                foreach (var name in possiblePropertyNames)
                {
                    property = FhirMetadataRegistry.GetProperty(current.GetType(), name);
                    if (property != null)
                    {
                        break;
                    }
                }

                if (property == null)
                {
                    return null;
                }

                current = property.GetValue(current);
                if (current == null)
                {
                    return null;
                }

                if (arrayIndex.HasValue && current is IList list)
                {
                    if (list.Count <= arrayIndex.Value)
                    {
                        return null;
                    }
                    current = list[arrayIndex.Value];
                }
            }

            if (current is string || current is int || current is bool || current is decimal || current is DateTime)
            {
                return current;
            }
            else if (current is FhirDateTime fhirDateTime)
            {
                return fhirDateTime.Value ?? null;
            }
            else if (current is Quantity quantity)
            {
                var valueProp = FhirMetadataRegistry.GetProperty(quantity.GetType(), "Value");
                if (valueProp != null)
                {
                    return valueProp.GetValue(quantity);
                }
            }
            else if (current is PrimitiveType primitive)
            {
                return primitive.ObjectValue;
            }
            else if (current is Base complexValue)
            {
                return complexValue.DeepCopy() as Base;
            }

            return null;
        }

        private bool SetValueViaFhirPath(DomainResource resource, string targetFhirPath, object targetValue, ITypedElement scopedNode, string sourceFhirPath, DomainResource originalResource)
        {
            try
            {
                if (!_fhirPathCache.TryGetValue(targetFhirPath, out var targetElements))
                {
                    targetElements = scopedNode.Select(targetFhirPath).ToList();
                    _fhirPathCache[targetFhirPath] = targetElements;
                }

                if (!targetElements.Any())
                {
                    return false;
                }

                foreach (var targetElement in targetElements)
                {
                    var targetPath = targetElement.Location
                        ?? throw new InvalidOperationException($"Target element at FHIRPath {targetFhirPath} does not have a valid location for resource type {resource.TypeName}.");

                    var pathParts = targetPath.Split('.').Skip(1).ToArray();
                    object current = resource;
                    Base parentPoco = null;
                    string propertyName = null;
                    int? arrayIndex = null;

                    for (int i = 0; i < pathParts.Length; i++)
                    {
                        var part = pathParts[i];
                        (propertyName, arrayIndex) = ParseFhirPathPart(part);
                        propertyName = MapFhirPathToPropertyName(propertyName, parentPoco?.GetType()).First();

                        var property = FhirMetadataRegistry.GetProperty(current.GetType(), propertyName)
                            ?? throw new InvalidOperationException($"Property {propertyName} not found on type {current.GetType().Name} for FHIRPath {targetFhirPath}.");

                        if (typeof(IList).IsAssignableFrom(property.PropertyType) && arrayIndex.HasValue)
                        {
                            var list = property.GetValue(current) as IList;
                            if (list == null)
                            {
                                list = (IList)Activator.CreateInstance(property.PropertyType);
                                property.SetValue(current, list);
                            }
                            while (list.Count <= arrayIndex.Value)
                            {
                                var itemType = property.PropertyType.GenericTypeArguments[0];
                                var newItem = Activator.CreateInstance(itemType);
                                list.Add(newItem);
                            }
                            current = list[arrayIndex.Value];
                        }
                        else
                        {
                            current = property.GetValue(current);
                            if (current == null && i < pathParts.Length - 1)
                            {
                                var newInstance = Activator.CreateInstance(property.PropertyType);
                                property.SetValue(current, newInstance);
                                current = newInstance;
                            }
                        }

                        if (i == pathParts.Length - 2)
                        {
                            parentPoco = current as Base
                                ?? throw new InvalidOperationException($"Parent object at {string.Join(".", pathParts.Take(i + 1))} is not a Base type for FHIRPath {targetFhirPath}.");
                        }
                    }

                    if (parentPoco == null || propertyName == null)
                    {
                        throw new InvalidOperationException($"Could not resolve parent or property for target path {targetPath} in resource type {resource.TypeName}.");
                    }

                    var propertyToSet = FhirMetadataRegistry.GetProperty(parentPoco.GetType(), propertyName)
                        ?? throw new InvalidOperationException($"Property {propertyName} not found on type {parentPoco.GetType().Name} for FHIRPath {targetFhirPath}.");
                    if (!propertyToSet.CanWrite)
                    {
                        throw new InvalidOperationException($"Property {propertyName} on type {parentPoco.GetType().Name} is not writable for FHIRPath {targetFhirPath}.");
                    }

                    if (targetValue is IList valueList && typeof(IList).IsAssignableFrom(propertyToSet.PropertyType))
                    {
                        var list = (IList)Activator.CreateInstance(propertyToSet.PropertyType);
                        foreach (var item in valueList)
                        {
                            var convertedItem = ConvertToFhirType(item, propertyToSet.PropertyType.GenericTypeArguments[0], parentPoco, propertyName);
                            list.Add(convertedItem);
                        }
                        propertyToSet.SetValue(parentPoco, list);
                    }
                    else
                    {
                        var convertedValue = ConvertToFhirType(targetValue, propertyToSet.PropertyType, parentPoco, propertyName);
                        propertyToSet.SetValue(parentPoco, convertedValue);
                    }
                }
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to evaluate target FHIRPath '{targetFhirPath}' for resource type {resource.TypeName}: {ex.Message}", ex);
            }

            return true;
        }

        private void SetComponentValuesReflectively(DomainResource resource, object targetValue, string targetFhirPath)
        {
            var componentsProperty = FhirMetadataRegistry.GetProperty(resource.GetType(), "Component")
                ?? throw new InvalidOperationException($"Component property not found on resource type {resource.GetType().Name} for FHIRPath {targetFhirPath}.");

            var components = componentsProperty.GetValue(resource) as IList;
            if (components == null)
            {
                components = (IList)Activator.CreateInstance(componentsProperty.PropertyType);
                componentsProperty.SetValue(resource, components);
            }

            if (components.Count == 0)
            {
                throw new InvalidOperationException($"No components exist to set values on for FHIRPath {targetFhirPath} in resource type {resource.TypeName}.");
            }

            var values = targetValue is IList list ? list.Cast<object>().ToList() : new[] { targetValue }.ToList();

            // If a single value, apply it to all components
            if (values.Count == 1)
            {
                var singleValue = values[0];
                if (!(singleValue is decimal || singleValue is int))
                {
                    throw new InvalidOperationException($"Target value of type {singleValue.GetType().Name} is not compatible with Quantity.value for FHIRPath {targetFhirPath}.");
                }

                var convertedValue = singleValue is int i ? (decimal)i : (decimal)singleValue;

                foreach (var component in components)
                {
                    var valueProperty = FhirMetadataRegistry.GetProperty(component.GetType(), "Value");
                    if (valueProperty == null)
                    {
                        continue;
                    }

                    var currentValue = valueProperty.GetValue(component);
                    if (currentValue is Quantity quantity)
                    {
                        var valueProp = FhirMetadataRegistry.GetProperty(quantity.GetType(), "Value");
                        if (valueProp != null)
                        {
                            valueProp.SetValue(quantity, convertedValue);
                        }
                    }
                    else
                    {
                        quantity = new Quantity { Value = convertedValue };
                        valueProperty.SetValue(component, quantity);
                    }
                }
            }
            else
            {
                // Multiple values: map to components, creating new ones if needed
                for (int componentIndex = 0; componentIndex < values.Count; componentIndex++)
                {
                    var value = values[componentIndex];
                    if (!(value is decimal || value is int))
                    {
                        throw new InvalidOperationException($"Target value of type {value.GetType().Name} is not compatible with Quantity.value for FHIRPath {targetFhirPath}.");
                    }

                    if (componentIndex >= components.Count)
                    {
                        var itemType = componentsProperty.PropertyType.GenericTypeArguments[0];
                        var newComponent = Activator.CreateInstance(itemType);
                        components.Add(newComponent);
                    }

                    var component = components[componentIndex];
                    var valueProperty = FhirMetadataRegistry.GetProperty(component.GetType(), "Value");
                    if (valueProperty == null)
                    {
                        continue;
                    }

                    var currentValue = valueProperty.GetValue(component);
                    if (currentValue is Quantity quantity)
                    {
                        var valueProp = FhirMetadataRegistry.GetProperty(quantity.GetType(), "Value");
                        if (valueProp != null)
                        {
                            var convertedValue = value is int i ? (decimal)i : (decimal)value;
                            valueProp.SetValue(quantity, convertedValue);
                        }
                    }
                    else
                    {
                        quantity = new Quantity { Value = value is int i ? (decimal)i : (decimal)value };
                        valueProperty.SetValue(component, quantity);
                    }
                }
            }
        }

        private void SetTargetValue(Resource resource, string targetFhirPath, object newValue, string sourceFhirPath, DomainResource originalResource)
        {
            var pathParts = targetFhirPath.Split('.');
            if (pathParts.Length < 2)
            {
                throw new InvalidOperationException($"Target FHIRPath {targetFhirPath} is too short to resolve parent and property in resource type {resource.TypeName}.");
            }

            var parentPath = string.Join(".", pathParts.Take(pathParts.Length - 1));
            var propertyName = pathParts.Last();
            int? arrayIndex = null;

            (propertyName, arrayIndex) = ParseFhirPathPart(propertyName);
            var parentPoco = CreateParentStructure(resource, parentPath, sourceFhirPath, targetFhirPath, originalResource)
                ?? throw new InvalidOperationException($"Could not create parent structure for {parentPath} in resource type {resource.TypeName}.");

            propertyName = MapFhirPathToPropertyName(propertyName, parentPoco.GetType()).First();

            var property = FhirMetadataRegistry.GetProperty(parentPoco.GetType(), propertyName)
                ?? throw new InvalidOperationException($"Property {propertyName} not found on parent type {parentPoco.GetType().Name} for FHIRPath {targetFhirPath}.");

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
        }

        private Base CreateParentStructure(Resource resource, string parentPath, string sourceFhirPath, string targetFhirPath, DomainResource originalResource)
        {
            if (string.IsNullOrEmpty(parentPath))
            {
                return resource;
            }

            var pathParts = parentPath.Split('.');
            Base current = resource;
            foreach (var part in pathParts)
            {
                var (propertyName, arrayIndex) = ParseFhirPathPart(part);
                propertyName = MapFhirPathToPropertyName(propertyName, current.GetType()).First();

                var property = FhirMetadataRegistry.GetProperty(current.GetType(), propertyName)
                    ?? throw new InvalidOperationException($"Property {propertyName} not found on type {current.GetType().Name} for parent path {parentPath}.");

                if (typeof(IList).IsAssignableFrom(property.PropertyType))
                {
                    var list = property.GetValue(current) as IList;
                    if (list == null)
                    {
                        list = (IList)Activator.CreateInstance(property.PropertyType);
                        property.SetValue(current, list);
                    }

                    var itemType = property.PropertyType.GenericTypeArguments[0];
                    while (list.Count <= (arrayIndex ?? 0))
                    {
                        var newItem = Activator.CreateInstance(itemType);
                        list.Add(newItem);
                    }

                    current = list[arrayIndex ?? 0] as Base;
                }
                else
                {
                    var value = property.GetValue(current) as Base;
                    if (value == null)
                    {
                        value = Activator.CreateInstance(property.PropertyType) as Base;
                        property.SetValue(current, value);
                    }
                    current = value;
                }

                if (current == null)
                {
                    throw new InvalidOperationException($"Failed to create or access object at path part {part} for parent path {parentPath}.");
                }
            }

            return current;
        }

        private void ValidateComplexTypeCompatibility(ITypedElement scopedNode, string targetFhirPath, Base copiedObject)
        {
            try
            {
                var pathParts = targetFhirPath.Split('.');
                if (pathParts.Length < 2) return;

                var parentPath = string.Join(".", pathParts.Take(pathParts.Length - 1));
                var propertyName = pathParts.Last().Split('[')[0];
                propertyName = MapFhirPathToPropertyName(propertyName, null).First();

                List<ITypedElement> parentNodes;
                if (!_fhirPathCache.TryGetValue(parentPath, out parentNodes))
                {
                    parentNodes = scopedNode.Select(parentPath).ToList();
                    _fhirPathCache[parentPath] = parentNodes;
                }

                var parentNode = parentNodes.FirstOrDefault();
                if (parentNode != null)
                {
                    var parentPoco = parentNode.ToPoco() as Base;
                    var property = FhirMetadataRegistry.GetProperty(parentPoco?.GetType(), propertyName);
                    if (property != null && !property.PropertyType.IsAssignableFrom(copiedObject.GetType()) &&
                        !(typeof(IList).IsAssignableFrom(property.PropertyType) &&
                          property.PropertyType.GenericTypeArguments.Length > 0 &&
                          property.PropertyType.GenericTypeArguments[0].IsAssignableFrom(copiedObject.GetType())))
                    {
                        throw new InvalidOperationException(
                            $"Target property {propertyName} of type {property.PropertyType.Name} cannot accept source object of type {copiedObject.GetType().Name} for FHIRPath {targetFhirPath}.");
                    }
                }
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to validate complex type compatibility for FHIRPath '{targetFhirPath}' on resource type {scopedNode.Name}: {ex.Message}", ex);
            }
        }

        private IEnumerable<string> MapFhirPathToPropertyName(string fhirPathName, Type parentType)
        {
            if (parentType != null && _propertyNameCache.TryGetValue((parentType, fhirPathName), out var cachedName))
            {
                yield return cachedName;
                yield break;
            }

            string normalizedFhirPathName = fhirPathName.ToLower();

            if (FhirPathToPropertyMappings.TryGetValue(normalizedFhirPathName, out string mappedName))
            {
                if (parentType != null)
                {
                    _propertyNameCache[(parentType, fhirPathName)] = mappedName;
                }
                yield return mappedName;
            }

            var pascalCase = char.ToUpper(fhirPathName[0]) + (fhirPathName.Length > 1 ? fhirPathName.Substring(1) : string.Empty);
            yield return pascalCase;

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
                if (parentType != null)
                {
                    _propertyNameCache[(parentType, fhirPathName)] = basePascalCase;
                }
                yield return basePascalCase;
            }

            yield return fhirPathName;
        }

        private (string propertyName, int? arrayIndex) ParseFhirPathPart(string part)
        {
            if (part.Contains("[") && part.EndsWith("]"))
            {
                var indexStart = part.IndexOf('[');
                var indexEnd = part.IndexOf(']');
                if (indexStart >= indexEnd || indexStart == part.Length - 1)
                {
                    throw new InvalidOperationException($"Invalid FHIRPath index expression: {part}. Index is empty or malformed.");
                }

                var indexStr = part.Substring(indexStart + 1, indexEnd - indexStart - 1);
                if (string.IsNullOrEmpty(indexStr) || !int.TryParse(indexStr, out int index) || index < 0)
                {
                    throw new InvalidOperationException($"Invalid FHIRPath index '{indexStr}' in expression: {part}. Index must be a non-negative integer.");
                }
                return (part.Substring(0, indexStart), index);
            }
            return (part, null);
        }

        private object ConvertToFhirType(object newValue, Type propertyType, Base parentPoco, string propertyName)
        {
            if (newValue == null) return null;

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
                return codeableConcept.DeepCopy() as CodeableConcept;
            }
            else if (newValue is Coding coding && propertyType == typeof(Coding))
            {
                return coding.DeepCopy() as Coding;
            }
            else if (newValue is Period period && propertyType == typeof(Period))
            {
                return period.DeepCopy() as Period;
            }
            // For FHIR STU3, use ResourceReference; for R4+, use Reference
            else if (newValue is ResourceReference resourceReference && propertyType == typeof(ResourceReference))
            {
                return resourceReference.DeepCopy() as ResourceReference;
            }
            else if (newValue is Base complexValue && !propertyType.IsAssignableFrom(complexValue.GetType()))
            {
                throw new InvalidOperationException($"Cannot assign complex value of type {newValue.GetType().Name} to property {propertyName} of type {propertyType.Name}.");
            }

            return newValue;
        }
    }
}