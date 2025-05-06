using Hl7.Fhir.ElementModel;
using Hl7.Fhir.FhirPath;
using Hl7.Fhir.Model;
using Hl7.FhirPath;
using System;
using System.Collections;
using System.Linq;
using System.Reflection;

namespace LantanaGroup.Link.Normalization.Application.Operations
{
    public class CopyPropertyOperation : IOperation
    {
        public OperationType OperationType => OperationType.CopyProperty;
        public string Name { get; private set; }
        public string SourceFhirPath { get; private set; }
        public string TargetFhirPath { get; private set; }

        public CopyPropertyOperation(string name, string sourceFhirPath, string targetFhirPath)
        {
            Name = name;
            SourceFhirPath = sourceFhirPath;
            TargetFhirPath = targetFhirPath;
        }

        public DomainResource Execute(DomainResource resource)
        {
            if (resource == null || string.IsNullOrEmpty(SourceFhirPath) || string.IsNullOrEmpty(TargetFhirPath))
            {
                throw new ArgumentException("Resource, SourceFhirPath, and TargetFhirPath must not be null or empty.");
            }

            var resourceCopy = resource.DeepCopy() as DomainResource;
            if (resourceCopy == null)
            {
                throw new InvalidOperationException("Failed to create a deep copy of the resource.");
            }

            CopyFhirPathValue(resourceCopy, SourceFhirPath, TargetFhirPath, resource);
            return resourceCopy;
        }

        private void CopyFhirPathValue(DomainResource resource, string sourceFhirPath, string targetFhirPath, DomainResource originalResource)
        {
            var scopedNode = resource.ToTypedElement();

            // Extract source value
            object targetValue = ExtractValueFromFhirPath(scopedNode, sourceFhirPath);
            if (targetValue == null)
            {
                // Try reflective access for quantity or value paths
                if (sourceFhirPath.Contains("Quantity") || sourceFhirPath.EndsWith("value") || sourceFhirPath.EndsWith("value.value"))
                {
                    targetValue = GetValueReflectively(resource, sourceFhirPath);
                }
            }

            if (targetValue == null)
            {
                throw new InvalidOperationException($"No values found at source FHIRPath: {sourceFhirPath}");
            }

            // Check if target exists
            bool targetExists = scopedNode.Select(targetFhirPath).Any();

            if (targetValue is string || targetValue is int || targetValue is bool || targetValue is decimal || targetValue is DateTime)
            {
                // Prioritize SetComponentValuesReflectively for component targets
                if (targetFhirPath.Contains("component") && (targetFhirPath.EndsWith("value") || targetFhirPath.EndsWith("value.value")))
                {
                    SetComponentValuesReflectively(resource, targetValue);
                }
                else
                {
                    SetValueViaFhirPath(resource, targetFhirPath, targetValue, scopedNode, originalResource);

                    // Fallback if target doesn't exist
                    if (!targetExists)
                    {
                        SetTargetValue(resource, targetFhirPath, targetValue, originalResource);
                    }
                }
            }
            else if (targetValue is Base complexValue)
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
                throw new InvalidOperationException($"Source type {targetValue.GetType().Name} is not supported.");
            }

            // Verify that component values were set for component targets
            if (targetFhirPath.Contains("component") && (targetFhirPath.EndsWith("value") || targetFhirPath.EndsWith("value.value")))
            {
                if (resource is Observation observation)
                {
                    if (observation.Component == null || !observation.Component.Any() ||
                        observation.Component.Any(c => c.Value is Quantity q && q.Value != (targetValue is int i ? (decimal)i : (decimal)targetValue)))
                    {
                        throw new InvalidOperationException($"Failed to set value at target FHIRPath: {targetFhirPath}");
                    }
                }
            }
        }

        private object ExtractValueFromFhirPath(ITypedElement scopedNode, string fhirPath)
        {
            var values = scopedNode.Select(fhirPath).ToList();
            if (!values.Any())
            {
                DebugPrint($"FHIRPath {fhirPath} returned no values.");
                return null;
            }

            var value = values.First();
            var poco = value?.ToPoco();
            if (poco == null)
            {
                throw new InvalidOperationException("Value could not be resolved to a FHIR object.");
            }

            if (poco is PrimitiveType primitive)
            {
                if (primitive.ObjectValue == null)
                {
                    throw new InvalidOperationException("Primitive value is null.");
                }
                return primitive.ObjectValue;
            }
            else if (poco is Quantity quantity)
            {
                if (quantity.Value == null)
                {
                    throw new InvalidOperationException("Quantity value is null.");
                }
                return quantity.Value;
            }
            else if (poco is Base complex)
            {
                return complex.DeepCopy() as Base;
            }
            else
            {
                throw new InvalidOperationException($"Type {poco.GetType().Name} is not supported.");
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
                    var indexStart = part.IndexOf('[');
                    var indexEnd = part.IndexOf(']');
                    if (indexStart < indexEnd)
                    {
                        var indexStr = part.Substring(indexStart + 1, indexEnd - indexStart - 1);
                        if (int.TryParse(indexStr, out int index))
                        {
                            arrayIndex = index;
                        }
                    }
                }

                propertyName = MapFhirPathToPropertyName(propertyName, current.GetType());
                var property = current.GetType().GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
                if (property == null)
                {
                    DebugPrint($"Property {propertyName} not found on type {current.GetType().Name}.");
                    return null;
                }

                current = property.GetValue(current);
                if (current == null)
                {
                    DebugPrint($"Value at {propertyName} is null.");
                    return null;
                }

                if (arrayIndex.HasValue && current is IList list)
                {
                    if (list.Count <= arrayIndex.Value)
                    {
                        DebugPrint($"Index {arrayIndex.Value} out of bounds for list {propertyName}.");
                        return null;
                    }
                    current = list[arrayIndex.Value];
                }
            }

            if (current is Quantity quantity)
            {
                var valueProp = quantity.GetType().GetProperty("Value", BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
                if (valueProp != null)
                {
                    var value = valueProp.GetValue(quantity);
                    if (value == null)
                    {
                        DebugPrint("Quantity value is null.");
                    }
                    return value;
                }
            }
            else if (current is PrimitiveType primitive)
            {
                if (primitive.ObjectValue == null)
                {
                    DebugPrint("Primitive value is null.");
                }
                return primitive.ObjectValue;
            }
            else if (current is Base complexValue)
            {
                return complexValue.DeepCopy() as Base;
            }

            DebugPrint($"No value found reflectively at {fhirPath}.");
            return null;
        }

        private void SetValueViaFhirPath(DomainResource resource, string targetFhirPath, object targetValue, ITypedElement scopedNode, DomainResource originalResource)
        {
            var targetElements = scopedNode.Select(targetFhirPath).ToList();
            if (!targetElements.Any())
            {
                return;
            }

            foreach (var targetElement in targetElements)
            {
                var targetPath = targetElement.Location;
                if (string.IsNullOrEmpty(targetPath))
                {
                    throw new InvalidOperationException($"Target element at {targetFhirPath} does not have a valid location.");
                }

                var pathParts = targetPath.Split('.').Skip(1).ToArray();
                object current = resource;
                Base parentPoco = null;
                string propertyName = null;
                int? arrayIndex = null;

                for (int i = 0; i < pathParts.Length; i++)
                {
                    var part = pathParts[i];
                    (propertyName, arrayIndex) = ParseFhirPathPart(part);
                    propertyName = MapFhirPathToPropertyName(propertyName, parentPoco?.GetType());

                    var property = current.GetType().GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
                    if (property == null)
                    {
                        throw new InvalidOperationException($"Property {propertyName} not found on type {current.GetType().Name}.");
                    }

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
                        parentPoco = current as Base;
                        if (parentPoco == null)
                        {
                            throw new InvalidOperationException($"Parent object at {string.Join(".", pathParts.Take(i + 1))} is not a Base type.");
                        }
                    }
                }

                if (parentPoco == null || propertyName == null)
                {
                    throw new InvalidOperationException($"Could not resolve parent or property for target path {targetPath}.");
                }

                var propertyToSet = parentPoco.GetType().GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
                if (propertyToSet == null || !propertyToSet.CanWrite)
                {
                    throw new InvalidOperationException($"Property {propertyName} on type {parentPoco.GetType().Name} is not writable.");
                }

                var convertedValue = ConvertToFhirType(targetValue, propertyToSet.PropertyType, parentPoco, propertyName);
                DebugPrint($"Assigning converted value of type {convertedValue?.GetType().Name ?? "null"} to property {propertyName} of type {propertyToSet.PropertyType.Name}");
                propertyToSet.SetValue(parentPoco, convertedValue);
            }
        }

        private void SetComponentValuesReflectively(DomainResource resource, object targetValue)
        {
            var componentsProperty = resource.GetType().GetProperty("Component", BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
            if (componentsProperty == null)
            {
                throw new InvalidOperationException("Component property not found on resource.");
            }

            var components = componentsProperty.GetValue(resource) as IList;
            if (components == null)
            {
                components = (IList)Activator.CreateInstance(componentsProperty.PropertyType);
                componentsProperty.SetValue(resource, components);
            }

            if (components.Count == 0)
            {
                throw new InvalidOperationException("No components exist to set values on.");
            }

            if (!(targetValue is decimal || targetValue is int))
            {
                throw new InvalidOperationException($"Target value of type {targetValue.GetType().Name} is not compatible with Quantity.value.");
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

        private void SetTargetValue(Resource resource, string targetFhirPath, object newValue, DomainResource originalResource)
        {
            var pathParts = targetFhirPath.Split('.');
            if (pathParts.Length < 2)
            {
                throw new InvalidOperationException($"Target FHIRPath {targetFhirPath} is too short to resolve parent and property.");
            }

            var parentPath = string.Join(".", pathParts.Take(pathParts.Length - 1));
            var propertyName = pathParts.Last();
            int? arrayIndex = null;

            (propertyName, arrayIndex) = ParseFhirPathPart(propertyName);
            var parentPoco = CreateParentStructure(resource, parentPath, originalResource);
            if (parentPoco == null)
            {
                throw new InvalidOperationException($"Could not create parent structure for {parentPath}.");
            }

            propertyName = MapFhirPathToPropertyName(propertyName, parentPoco.GetType());

            var property = parentPoco.GetType().GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
            if (property == null)
            {
                throw new InvalidOperationException($"Property {propertyName} not found on parent type {parentPoco.GetType().Name} for FHIRPath {targetFhirPath}.");
            }

            var convertedValue = ConvertToFhirType(newValue, property.PropertyType, parentPoco, propertyName);
            DebugPrint($"Assigning converted value of type {convertedValue?.GetType().Name ?? "null"} (original: {newValue?.GetType().Name ?? "null"}) to property {propertyName} of type {property.PropertyType.Name} at FHIRPath {targetFhirPath}");

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
                propertyName = MapFhirPathToPropertyName(propertyName, current.GetType());

                var property = current.GetType().GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
                if (property == null)
                {
                    throw new InvalidOperationException($"Property {propertyName} not found on type {current.GetType().Name}.");
                }

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
                    throw new InvalidOperationException($"Failed to create or access object at path part {part}.");
                }
            }

            return current;
        }

        private void ValidateComplexTypeCompatibility(ITypedElement scopedNode, string targetFhirPath, Base copiedObject)
        {
            var pathParts = targetFhirPath.Split('.');
            if (pathParts.Length < 2) return;

            var parentPath = string.Join(".", pathParts.Take(pathParts.Length - 1));
            var propertyName = pathParts.Last().Split('[')[0];
            propertyName = MapFhirPathToPropertyName(propertyName, null);

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
                        $"Target property {propertyName} of type {property.PropertyType.Name} cannot accept source object of type {copiedObject.GetType().Name}.");
                }
            }
        }

        private string MapFhirPathToPropertyName(string fhirPathName, Type parentType)
        {
            if (parentType == typeof(Extension) && fhirPathName.Equals("valueString", StringComparison.OrdinalIgnoreCase))
            {
                return "Value";
            }

            if (fhirPathName.Equals("doseQuantity", StringComparison.OrdinalIgnoreCase))
            {
                return "Dose";
            }

            return fhirPathName switch
            {
                "valueQuantity" => "ValueQuantity",
                "value" => "Value",
                "code" => "Code",
                "extension" => "Extension",
                _ => char.ToUpper(fhirPathName[0]) + fhirPathName.Substring(1) // Default to PascalCase
            };
        }

        private (string propertyName, int? arrayIndex) ParseFhirPathPart(string part)
        {
            if (part.Contains("[") && part.EndsWith("]"))
            {
                var indexStart = part.IndexOf('[');
                var indexEnd = part.IndexOf(']');
                if (indexStart < indexEnd)
                {
                    var indexStr = part.Substring(indexStart + 1, indexEnd - indexStart - 1);
                    if (int.TryParse(indexStr, out int index))
                    {
                        return (part.Substring(0, indexStart), index);
                    }
                    throw new InvalidOperationException($"Unsupported FHIRPath index expression: {part}");
                }
            }
            return (part, null);
        }

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
                   (propertyType == typeof(DataType) && value is string && property.DeclaringType == typeof(Extension));
        }

        private object ConvertToFhirType(object newValue, Type propertyType, Base parentPoco, string propertyName)
        {
            DebugPrint($"Converting value of type {newValue?.GetType().Name ?? "null"} to property type {propertyType.Name} for property {propertyName} on type {parentPoco?.GetType().Name}");

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
            else if (newValue is Base complexValue && !propertyType.IsAssignableFrom(complexValue.GetType()))
            {
                throw new InvalidOperationException($"Cannot assign complex value of type {newValue.GetType().Name} to property of type {propertyType.Name}.");
            }

            DebugPrint($"No conversion applied, returning original value of type {newValue.GetType().Name}.");
            return newValue;
        }

        private string InferExtensionUrl(DomainResource resource, string sourceFhirPath, string targetFhirPath)
        {
            // Get the resource type
            string resourceType = resource.TypeName;

            // Extract the source property from SourceFhirPath
            string sourceProperty = sourceFhirPath.Split('.').Last().Split('[')[0];
            sourceProperty = char.ToUpper(sourceProperty[0]) + sourceProperty.Substring(1); // PascalCase

            // Construct a context-specific URL
            string inferredUrl = $"http://example.org/fhir/extension/{resourceType}-{sourceProperty}";

            // For specific cases, map properties to meaningful names
            if (sourceProperty.Equals("Given", StringComparison.OrdinalIgnoreCase))
            {
                inferredUrl = $"http://example.org/fhir/extension/{resourceType}-GivenName";
            }

            return inferredUrl;
        }

        private void DebugPrint(string message)
        {
            // Simple console output for debugging
            Console.WriteLine($"[DEBUG] {message}");
        }
    }
}