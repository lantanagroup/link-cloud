using Hl7.Fhir.ElementModel;
using Hl7.Fhir.FhirPath;
using Hl7.Fhir.Model;
using Hl7.FhirPath;
using System.Collections;
using System.Reflection;

namespace LantanaGroup.Link.Normalization.Application.Operations
{
    /// <summary>
    /// An operation to copy a value from a source FHIRPath to a target FHIRPath on a FHIR resource.
    /// </summary>
    public class CopyPropertyOperation : IOperation
    {
        public OperationType OperationType => OperationType.CopyProperty;
        public string Name { get; private set; }
        public string SourceFhirPath { get; private set; }
        public string TargetFhirPath { get; private set; }

        // Explicit mapping of FHIR resource name to C# Properties
        private static readonly Dictionary<string, string> FhirPathToPropertyMappings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {

        };

        // Common FHIR suffixes to strip when mapping FHIRPath to property names
        private static readonly string[] CommonFhirSuffixes = { "DateTime", "Quantity", "String", "Boolean", "Decimal", "Integer", "Code" };

        // Cache for mapped property names
        private static readonly Dictionary<(Type, string), string> _propertyNameCache = new Dictionary<(Type, string), string>();

        /// <summary>
        /// Initializes a new instance of the <see cref="CopyPropertyOperation"/> class.
        /// </summary>
        /// <param name="name">The name of the operation.</param>
        /// <param name="sourceFhirPath">The source FHIRPath expression.</param>
        /// <param name="targetFhirPath">The target FHIRPath expression.</param>
        public CopyPropertyOperation(string name, string sourceFhirPath, string targetFhirPath)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("Name must not be null or whitespace.", nameof(name));
            }
            if (string.IsNullOrWhiteSpace(sourceFhirPath))
            {
                throw new ArgumentException("SourceFhirPath must not be null or whitespace.", nameof(sourceFhirPath));
            }
            if (string.IsNullOrWhiteSpace(targetFhirPath))
            {
                throw new ArgumentException("TargetFhirPath must not be null or whitespace.", nameof(targetFhirPath));
            }

            Name = name;
            SourceFhirPath = sourceFhirPath;
            TargetFhirPath = targetFhirPath;
        }

        /// <summary>
        /// Executes the copy operation on the provided FHIR resource.
        /// </summary>
        /// <param name="resource">The FHIR resource to operate on.</param>
        /// <returns>A new resource with the copied value.</returns>
        /// <exception cref="ArgumentException">Thrown when resource or FHIRPaths are invalid.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the operation cannot be completed.</exception>
        public DomainResource Execute(DomainResource resource)
        {
            if (resource == null)
            {
                throw new ArgumentException("Resource must not be null.", nameof(resource));
            }
            if (string.IsNullOrEmpty(SourceFhirPath) || string.IsNullOrEmpty(TargetFhirPath))
            {
                throw new ArgumentException("SourceFhirPath and TargetFhirPath must not be null or empty.");
            }

            var resourceCopy = resource.DeepCopy() as DomainResource
                ?? throw new InvalidOperationException($"Failed to create a deep copy of the resource of type {resource.GetType().Name}.");

            CopyFhirPathValue(resourceCopy, SourceFhirPath, TargetFhirPath, resource);
            return resourceCopy;
        }

        /// <summary>
        /// Copies a value from the source FHIRPath to the target FHIRPath on the resource.
        /// </summary>
        /// <param name="resource">The resource to modify.</param>
        /// <param name="sourceFhirPath">The source FHIRPath.</param>
        /// <param name="targetFhirPath">The target FHIRPath.</param>
        /// <param name="originalResource">The original resource for context.</param>
        private void CopyFhirPathValue(DomainResource resource, string sourceFhirPath, string targetFhirPath, DomainResource originalResource)
        {
            var scopedNode = resource.ToTypedElement();

            // Extract source values (supporting collections)
            var targetValues = ExtractValueFromFhirPath(scopedNode, sourceFhirPath)
                ?? GetValueReflectively(resource, sourceFhirPath)
                ?? throw new InvalidOperationException($"No values found at source FHIRPath: {sourceFhirPath} for resource type {resource.TypeName}.");

            // Check if target exists
            bool targetExists = scopedNode.Select(targetFhirPath).Any();

            if ((targetValues is string || targetValues is int || targetValues is bool || targetValues is decimal || targetValues is DateTime) //Is primitive
                || (targetValues is IList valueList && valueList.Cast<object>().All(v => v is string || v is int || v is bool || v is decimal || v is DateTime))) //or is list of primitives
            {
                if (targetFhirPath.Contains("component") && (targetFhirPath.EndsWith("value") || targetFhirPath.EndsWith("value.value")))
                {
                    SetComponentValuesReflectively(resource, targetValues, targetFhirPath);
                }
                else
                {
                    if (targetExists)
                    {
                        SetValueViaFhirPath(resource, targetFhirPath, targetValues, scopedNode, originalResource);
                    }
                    else
                    {
                        SetTargetValue(resource, targetFhirPath, targetValues, originalResource);
                    }
                }
            }
            else if (targetValues is Base complexValue)
            {
                var copiedObject = complexValue.DeepCopy() as Base;
                ValidateComplexTypeCompatibility(scopedNode, targetFhirPath, copiedObject);

                SetValueViaFhirPath(resource, targetFhirPath, copiedObject, scopedNode, originalResource);
                if (!targetExists)
                {
                    SetTargetValue(resource, targetFhirPath, copiedObject, originalResource);
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
        }

        /// <summary>
        /// Extracts a value from the specified FHIRPath.
        /// </summary>
        /// <param name="scopedNode">The scoped node to query.</param>
        /// <param name="fhirPath">The FHIRPath expression.</param>
        /// <returns>The extracted value, or null if not found.</returns>
        private object ExtractValueFromFhirPath(ITypedElement scopedNode, string fhirPath)
        {
            var values = scopedNode.Select(fhirPath).ToList();
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

        /// <summary>
        /// Retrieves a value reflectively from the resource using the FHIRPath.
        /// </summary>
        /// <param name="resource">The resource to query.</param>
        /// <param name="fhirPath">The FHIRPath expression.</param>
        /// <returns>The retrieved value, or Facade for null values.</returns>
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

                // Get possible property names
                var possiblePropertyNames = MapFhirPathToPropertyName(propertyName, current.GetType());

                PropertyInfo property = null;
                foreach (var name in possiblePropertyNames)
                {
                    property = current.GetType().GetProperty(name, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
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

            // Handle primitive values directly
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
                var valueProp = quantity.GetType().GetProperty("Value", BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
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

        /// <summary>
        /// Sets a value at the target FHIRPath using FHIRPath evaluation.
        /// </summary>
        /// <param name="resource">The resource to modify.</param>
        /// <param name="targetFhirPath">The target FHIRPath.</param>
        /// <param name="targetValue">The value to set.</param>
        /// <param name="scopedNode">The scoped node for FHIRPath evaluation.</param>
        /// <param name="originalResource">The original resource for context.</param>
        private void SetValueViaFhirPath(DomainResource resource, string targetFhirPath, object targetValue, ITypedElement scopedNode, DomainResource originalResource)
        {
            var targetElements = scopedNode.Select(targetFhirPath).ToList();
            if (!targetElements.Any())
            {
                return;
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

                    var property = current.GetType().GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase)
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
                            if (newItem is Extension ext)
                            {
                                ext.Url = InferExtensionUrl(originalResource, SourceFhirPath, TargetFhirPath);
                            }
                            list.Add(newItem);
                        }
                        current = list[arrayIndex.Value];
                    }
                    else
                    {
                        current = property.GetValue(current);
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

                var propertyToSet = parentPoco.GetType().GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase)
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

        /// <summary>
        /// Sets component values reflectively for Observation resources.
        /// </summary>
        /// <param name="resource">The resource to modify.</param>
        /// <param name="targetValue">The value to set.</param>
        /// <param name="targetFhirPath">The target FHIRPath for error reporting.</param>
        private void SetComponentValuesReflectively(DomainResource resource, object targetValue, string targetFhirPath)
        {
            var componentsProperty = resource.GetType().GetProperty("Component", BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase)
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

            if (!(targetValue is decimal || targetValue is int))
            {
                throw new InvalidOperationException($"Target value of type {targetValue.GetType().Name} is not compatible with Quantity.value for FHIRPath {targetFhirPath}.");
            }

            foreach (var component in components)
            {
                var valueProperty = component.GetType().GetProperty("Value", BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
                if (valueProperty == null)
                {
                    continue;
                }

                var value = valueProperty.GetValue(component);
                if (value is Quantity quantity)
                {
                    var valueProp = quantity.GetType().GetProperty("Value", BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
                    if (valueProp != null)
                    {
                        var convertedValue = targetValue is int i ? (decimal)i : (decimal)targetValue;
                        valueProp.SetValue(quantity, convertedValue);
                    }
                }
            }
        }

        /// <summary>
        /// Sets a value at the target FHIRPath, creating parent structures if necessary.
        /// </summary>
        /// <param name="resource">The resource to modify.</param>
        /// <param name="targetFhirPath">The target FHIRPath.</param>
        /// <param name="newValue">The value to set.</param>
        /// <param name="originalResource">The original resource for context.</param>
        private void SetTargetValue(Resource resource, string targetFhirPath, object newValue, DomainResource originalResource)
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
            var parentPoco = CreateParentStructure(resource, parentPath, originalResource)
                ?? throw new InvalidOperationException($"Could not create parent structure for {parentPath} in resource type {resource.TypeName}.");

            propertyName = MapFhirPathToPropertyName(propertyName, parentPoco.GetType()).First();

            var property = parentPoco.GetType().GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase)
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
                            if (newItem is Extension ext)
                            {
                                ext.Url = InferExtensionUrl(originalResource, SourceFhirPath, TargetFhirPath);
                            }
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

        /// <summary>
        /// Creates the parent structure for the target FHIRPath.
        /// </summary>
        /// <param name="resource">The resource to modify.</param>
        /// <param name="parentPath">The parent FHIRPath.</param>
        /// <param name="originalResource">The original resource for context.</param>
        /// <returns>The parent Base object.</returns>
        private Base CreateParentStructure(Resource resource, string parentPath, DomainResource originalResource)
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

                var property = current.GetType().GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase)
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
                        if (newItem is Extension ext)
                        {
                            ext.Url = InferExtensionUrl(originalResource, SourceFhirPath, TargetFhirPath);
                        }
                        list.Add(newItem);
                    }

                    current = list[arrayIndex ?? 0] as Base;
                }
                else
                {
                    var value = property.GetValue(current) as Base;
                    if (value == null)
                    {
                        if (property.PropertyType == typeof(Extension))
                        {
                            value = new Extension { Url = InferExtensionUrl(originalResource, SourceFhirPath, TargetFhirPath) };
                        }
                        else
                        {
                            value = Activator.CreateInstance(property.PropertyType) as Base;
                        }
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

        /// <summary>
        /// Validates that a complex type is compatible with the target FHIRPath.
        /// </summary>
        /// <param name="scopedNode">The scoped node for FHIRPath evaluation.</param>
        /// <param name="targetFhirPath">The target FHIRPath.</param>
        /// <param name="copiedObject">The copied complex object.</param>
        private void ValidateComplexTypeCompatibility(ITypedElement scopedNode, string targetFhirPath, Base copiedObject)
        {
            var pathParts = targetFhirPath.Split('.');
            if (pathParts.Length < 2) return;

            var parentPath = string.Join(".", pathParts.Take(pathParts.Length - 1));
            var propertyName = pathParts.Last().Split('[')[0];
            propertyName = MapFhirPathToPropertyName(propertyName, null).First();

            var parentNode = scopedNode.Select(parentPath).FirstOrDefault();
            if (parentNode != null)
            {
                var parentPoco = parentNode.ToPoco() as Base;
                var property = parentPoco?.GetType().GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
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

        /// <summary>
        /// Maps a FHIRPath name to possible C# property names.
        /// </summary>
        /// <param name="fhirPathName">The FHIRPath name.</param>
        /// <param name="parentType">The parent type for context.</param>
        /// <returns>An enumerable of possible property names.</returns>
        private IEnumerable<string> MapFhirPathToPropertyName(string fhirPathName, Type parentType)
        {
            if (parentType != null && _propertyNameCache.TryGetValue((parentType, fhirPathName), out var cachedName))
            {
                yield return cachedName;
                yield break;
            }

            string normalizedFhirPathName = fhirPathName.ToLower();

            // Check for explicit mapping
            if (FhirPathToPropertyMappings.TryGetValue(normalizedFhirPathName, out string mappedName))
            {
                if (parentType != null)
                {
                    _propertyNameCache[(parentType, fhirPathName)] = mappedName;
                }
                yield return mappedName;
            }

            // Special case for Extension type
            if (parentType == typeof(Extension) && normalizedFhirPathName == "valuestring")
            {
                if (parentType != null)
                {
                    _propertyNameCache[(parentType, fhirPathName)] = "Value";
                }
                yield return "Value";
            }

            // Original FHIR path name in PascalCase
            var pascalCase = char.ToUpper(fhirPathName[0]) + (fhirPathName.Length > 1 ? fhirPathName.Substring(1) : string.Empty);
            yield return pascalCase;

            // Remove common FHIR suffixes and convert to PascalCase
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

            // Original FHIR path name as-is
            yield return fhirPathName;
        }

        /// <summary>
        /// Parses a FHIRPath part into property name and array index.
        /// </summary>
        /// <param name="part">The FHIRPath part.</param>
        /// <returns>A tuple containing the property name and optional array index.</returns>
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

        /// <summary>
        /// Checks if a value is assignable to a property.
        /// </summary>
        /// <param name="property">The property info.</param>
        /// <param name="value">The value to check.</param>
        /// <returns>True if assignable, false otherwise.</returns>
        private bool IsAssignableToProperty(PropertyInfo property, object value)
        {
            var propertyType = property.PropertyType;
            if (value == null)
            {
                return !propertyType.IsValueType || Nullable.GetUnderlyingType(propertyType) != null;
            }

            var valueType = value.GetType();
            return propertyType.IsAssignableFrom(valueType) ||
                   (propertyType == typeof(decimal) && value is decimal) ||
                   (propertyType == typeof(decimal?) && value is decimal) ||
                   (propertyType == typeof(FhirString) && (value is string || value is decimal || value is int)) ||
                   (propertyType == typeof(string) && (value is decimal || value is int || value is bool || value is DateTime)) ||
                   (propertyType == typeof(Code) && value is string) ||
                   (propertyType == typeof(Integer) && value is int) ||
                   (propertyType == typeof(FhirBoolean) && value is bool) ||
                   (propertyType == typeof(FhirDecimal) && value is decimal) ||
                   (propertyType == typeof(FhirDateTime) && value is DateTime) ||
                   (propertyType == typeof(DataType) && value is string && property.DeclaringType == typeof(Extension)) ||
                   (propertyType == typeof(IList) && value is IList && property.PropertyType.GenericTypeArguments.Length > 0 &&
                    valueType.GenericTypeArguments.Length > 0 &&
                    property.PropertyType.GenericTypeArguments[0].IsAssignableFrom(valueType.GenericTypeArguments[0]));
        }

        /// <summary>
        /// Converts a value to the appropriate FHIR type for the target property.
        /// </summary>
        /// <param name="newValue">The value to convert.</param>
        /// <param name="propertyType">The target property type.</param>
        /// <param name="parentPoco">The parent FHIR object.</param>
        /// <param name="propertyName">The property name.</param>
        /// <returns>The converted value.</returns>
        private object ConvertToFhirType(object newValue, Type propertyType, Base parentPoco, string propertyName)
        {
            if (newValue == null) return null;

            if (newValue is string strValue)
            {
                if (propertyType == typeof(FhirString)) return new FhirString(strValue);
                if (propertyType == typeof(string)) return strValue;
                if (propertyType == typeof(Code)) return new Code(strValue);
                if (propertyType == typeof(DataType) && parentPoco is Extension) return new FhirString(strValue);
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
            else if (newValue is Base complexValue && !propertyType.IsAssignableFrom(complexValue.GetType()))
            {
                throw new InvalidOperationException($"Cannot assign complex value of type {newValue.GetType().Name} to property {propertyName} of type {propertyType.Name}.");
            }

            return newValue;
        }

        /// <summary>
        /// Infers an extension URL based on the resource and FHIRPaths.
        /// </summary>
        /// <param name="resource">The resource for context.</param>
        /// <param name="sourceFhirPath">The source FHIRPath.</param>
        /// <param name="targetFhirPath">The target FHIRPath.</param>
        /// <returns>The inferred extension URL.</returns>
        private string InferExtensionUrl(DomainResource resource, string sourceFhirPath, string targetFhirPath)
        {
            string resourceType = resource.TypeName;
            string sourceProperty = sourceFhirPath.Split('.').Last().Split('[')[0];
            sourceProperty = char.ToUpper(sourceProperty[0]) + sourceProperty.Substring(1);
            string inferredUrl = $"http://example.org/fhir/extension/{resourceType}-{sourceProperty}";
            if (sourceProperty.Equals("Given", StringComparison.OrdinalIgnoreCase))
            {
                inferredUrl = $"http://example.org/fhir/extension/{resourceType}-GivenName";
            }
            return inferredUrl;
        }
    }
}