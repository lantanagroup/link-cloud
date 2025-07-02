using Hl7.Fhir.ElementModel;
using Hl7.Fhir.FhirPath;
using Hl7.Fhir.Model;
using Hl7.FhirPath;
using LantanaGroup.Link.Normalization.Application.Operations;
using LantanaGroup.Link.Normalization.Application.Services.FhirPathValidation;
using System.Collections;
using System.Collections.Concurrent;
using System.Reflection;
using System.Text;
using System.Text.Json;

namespace LantanaGroup.Link.Normalization.Application.Services.Operations
{
    public static class OperationServiceHelper
    {
        private static readonly ConcurrentDictionary<string, DomainResource> _resourceCache = new();
        private static ConcurrentBag<string> resources = new ConcurrentBag<string>(Enum.GetNames(typeof(ResourceType)));
        private static readonly Dictionary<string, string> FhirPathToPropertyMappings = new(StringComparer.OrdinalIgnoreCase);
        private static readonly string[] CommonFhirSuffixes = { "DateTime", "Quantity", "String", "Boolean", "Decimal", "Integer", "Code" };
        private static readonly ConcurrentDictionary<(string, Type), string> _propertyNameCache = new();
        private static readonly ConcurrentDictionary<Type, Dictionary<string, PropertyInfo>> _propertyCache = new();

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

        public static object? GetOperationImplementation(IOperation operation)
        {
            return operation.OperationType switch
            {
                OperationType.CopyProperty => (object)(CopyPropertyOperation)operation,
                OperationType.CodeMap => (object)(CodeMapOperation)operation,
                OperationType.ConditionalTransform => (object)(ConditionalTransformOperation)operation,
                _ => null
            };
        }

        public static (bool Success, string ErrorMessage, object Value) ExtractValueFromFhirPath(ITypedElement scopedNode, string fhirPath, ILogger logger)
        {
            try
            {
                var values = scopedNode.Select(fhirPath).ToList();
                if (!values.Any())
                    return (false, "No values found.", null);

                var pocos = values
                    .Where(v => v != null)
                    .Select(v => v.ToPoco())
                    .OfType<Base>()
                    .ToList();

                if (!pocos.Any() && values.Any())
                    return (false, "No valid FHIR types converted.", null);

                if (pocos.Count == 1)
                {
                    var poco = pocos[0];
                    if (poco is PrimitiveType primitive)
                        return (true, string.Empty, primitive.ObjectValue ?? null);
                    if (poco is Quantity quantity)
                        return (true, string.Empty, quantity.Value ?? null);
                    return (true, string.Empty, poco);
                }

                var result = pocos
                    .Select(poco => poco switch
                    {
                        PrimitiveType primitive when primitive.ObjectValue != null => primitive.ObjectValue,
                        Quantity quantity when quantity.Value != null => quantity.Value,
                        Base complex => complex,
                        _ => null
                    })
                    .Where(v => v != null)
                    .ToList();

                return result.Any() ? (true, string.Empty, result) : (false, "No valid values extracted.", null);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to evaluate FHIRPath '{FhirPath}' for resource type {ResourceType}.", fhirPath, scopedNode.Name);
                return (false, $"Failed to evaluate FHIRPath '{fhirPath}': {ex.Message}", null);
            }
        }

        public static object GetValueReflectively(object resource, string fhirPath)
        {
            var pathParts = fhirPath.Split('.');
            object currentObject = resource;

            foreach (var part in pathParts)
            {
                string propertyName = part.Split('[')[0];
                int? arrayIndex = null;

                if (part.Contains("[") && part.EndsWith("]"))
                    (propertyName, arrayIndex) = ParseFhirPathPart(part);

                if (currentObject == null)
                    return null;

                propertyName = MapFhirPathToPropertyName(propertyName, currentObject.GetType());
                var property = GetProperty(currentObject.GetType(), propertyName);
                if (property == null)
                    return null;

                currentObject = property.GetValue(currentObject);
                if (currentObject == null)
                    return null;

                if (arrayIndex.HasValue && currentObject is IList list)
                {
                    if (list.Count <= arrayIndex.Value)
                        return null;
                    currentObject = list[arrayIndex.Value];
                }
            }

            return currentObject switch
            {
                string or int or bool or decimal or DateTime => currentObject,
                FhirDateTime fhirDateTime => fhirDateTime.Value ?? null,
                Quantity quantity => GetProperty(quantity.GetType(), "Value")?.GetValue(quantity),
                PrimitiveType primitive => primitive.ObjectValue,
                Base complexValue => complexValue,
                _ => null
            };
        }

        public static bool CanCreateFhirPath(object resource, string fhirPath, ILogger? logger = null)
        {
            if (resource is not Base currentObject)
                return false;

            var pathParts = fhirPath.Split('.');

            foreach (var part in pathParts)
            {
                var (propertyName, arrayIndex) = ParseFhirPathPart(part);
                propertyName = MapFhirPathToPropertyName(propertyName, currentObject.GetType());

                var targetProperty = GetProperty(currentObject.GetType(), propertyName);
                if (targetProperty == null)
                {
                    logger?.LogWarning("Property '{PropertyName}' not found on type '{TypeName}' for FHIRPath '{FhirPath}'.",
                        propertyName, currentObject.GetType().Name, fhirPath);
                    return false;
                }

                // Simulate traversal without requiring actual values
                if (typeof(IList).IsAssignableFrom(targetProperty.PropertyType))
                {
                    var itemType = targetProperty.PropertyType.GetGenericArguments().FirstOrDefault();
                    if (itemType == null || !typeof(Base).IsAssignableFrom(itemType))
                        return false;

                    currentObject = Activator.CreateInstance(itemType) as Base;
                }
                else if (!typeof(Base).IsAssignableFrom(targetProperty.PropertyType))
                {
                    bool isLastSegment = part == pathParts.Last();

                    if (isLastSegment)
                    {
                        // Allow primitive types at the end of the path
                        return true;
                    }

                    logger?.LogWarning("Property '{PropertyName}' is a primitive and cannot be traversed further.", propertyName);
                    return false;
                }
                else
                {
                    if (!typeof(Base).IsAssignableFrom(targetProperty.PropertyType))
                        return false;

                    currentObject = Activator.CreateInstance(targetProperty.PropertyType) as Base;
                }

                if (currentObject == null)
                    return false;
            }

            return true;
        }


        public static (Base Parent, PropertyInfo Property) NavigateFhirPath(object resource, string fhirPath, bool createIfMissing = false, ILogger? logger = null)
        {
            var pathParts = fhirPath.Split('.');
            Base currentObject = resource as Base;
            object previousObject = null;
            PropertyInfo previousProperty = null;
            PropertyInfo targetProperty = null;

            for (int i = 0; i < pathParts.Length; i++)
            {
                var (propertyName, arrayIndex) = ParseFhirPathPart(pathParts[i]);
                propertyName = MapFhirPathToPropertyName(propertyName, currentObject?.GetType());

                targetProperty = GetProperty(currentObject.GetType(), propertyName);
                if (targetProperty == null)
                {
                    logger?.LogWarning("Property {PropertyName} not found for FHIRPath {FhirPath}.", propertyName, fhirPath);
                    return (null, null);
                }

                if (i == pathParts.Length - 1)
                    break;

                if (typeof(IList).IsAssignableFrom(targetProperty.PropertyType))
                {
                    var list = targetProperty.GetValue(currentObject) as IList;
                    if (list == null && createIfMissing)
                    {
                        list = (IList)Activator.CreateInstance(targetProperty.PropertyType);
                        SetPropertyValue(previousProperty, previousObject, targetProperty, currentObject, list);
                    }

                    if (list != null && arrayIndex.HasValue)
                    {
                        var itemType = targetProperty.PropertyType.GenericTypeArguments[0];
                        while (list.Count <= arrayIndex.Value)
                            list.Add(Activator.CreateInstance(itemType));
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
                        SetPropertyValue(previousProperty, previousObject, targetProperty, currentObject, value);
                    }

                    previousObject = currentObject;
                    previousProperty = targetProperty;
                    currentObject = value;
                }

                if (currentObject == null)
                    return (null, null);
            }

            return (currentObject, targetProperty);
        }

        public static Base CreateParentStructure(Resource resource, string parentPath, ILogger logger = null)
        {
            if (string.IsNullOrEmpty(parentPath))
                return resource;

            var pathParts = parentPath.Split('.');
            Base currentObject = resource;
            object previousObject = null;
            PropertyInfo previousProperty = null;

            foreach (var part in pathParts)
            {
                var (propertyName, arrayIndex) = ParseFhirPathPart(part);
                propertyName = MapFhirPathToPropertyName(propertyName, currentObject.GetType());

                var property = GetProperty(currentObject.GetType(), propertyName);
                if (property == null)
                {
                    logger?.LogWarning("Property {PropertyName} not found for parent path {ParentPath}.", propertyName, parentPath);
                    return null;
                }

                if (typeof(IList).IsAssignableFrom(property.PropertyType))
                {
                    var list = property.GetValue(currentObject) as IList;
                    if (list == null)
                    {
                        list = (IList)Activator.CreateInstance(property.PropertyType);
                        SetPropertyValue(previousProperty, previousObject, property, currentObject, list);
                    }

                    var itemType = property.PropertyType.GenericTypeArguments[0];
                    while (list.Count <= (arrayIndex ?? 0))
                        list.Add(Activator.CreateInstance(itemType));

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
                        SetPropertyValue(previousProperty, previousObject, property, currentObject, value);
                    }

                    previousObject = currentObject;
                    previousProperty = property;
                    currentObject = value;
                }

                if (currentObject == null)
                    return null;
            }

            return currentObject;
        }

        public static object ConvertJsonElementToBaseType(JsonElement element)
        {
            return element.ValueKind switch
            {
                JsonValueKind.String => element.GetString(),
                JsonValueKind.Number => element.TryGetInt32(out int intValue) ? intValue :
                                        element.TryGetInt64(out long longValue) ? longValue :
                                        element.TryGetDouble(out double doubleValue) ? doubleValue :
                                        element.GetDecimal(),
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                JsonValueKind.Null => null,
                JsonValueKind.Array => element.EnumerateArray().Select(ConvertJsonElementToBaseType).ToList(),
                _ => throw new InvalidOperationException("Unknown or undefined JsonElement type.")
            };
        }

        public static object ConvertToFhirType(object newValue, Type propertyType, string propertyName, ILogger logger)
        {
            if (newValue == null)
                return null;

            try
            {
                if (newValue is JsonElement jsonElement)
                {
                    if (propertyType.IsEnum || (propertyType.IsGenericType && propertyType.GetGenericTypeDefinition() == typeof(Nullable<>) && propertyType.GetGenericArguments()[0].IsEnum))
                    {
                        var enumType = propertyType.IsEnum ? propertyType : propertyType.GetGenericArguments()[0];
                        if (jsonElement.ValueKind == JsonValueKind.Number && jsonElement.TryGetInt32(out int intValue))
                            return Enum.ToObject(enumType, intValue);
                        if (jsonElement.ValueKind == JsonValueKind.String)
                            return Enum.Parse(enumType, jsonElement.GetString(), ignoreCase: true);
                    }
                    else if (jsonElement.ValueKind == JsonValueKind.String)
                    {
                        var strValue = jsonElement.GetString();
                        if (propertyType == typeof(FhirString)) return new FhirString(strValue);
                        if (propertyType == typeof(string)) return strValue;
                        if (propertyType == typeof(Code)) return new Code(strValue);
                    }
                    else if (jsonElement.ValueKind == JsonValueKind.Number)
                    {
                        if (propertyType == typeof(int) || propertyType == typeof(int?)) return jsonElement.GetInt32();
                        if (propertyType == typeof(decimal) || propertyType == typeof(decimal?)) return jsonElement.GetDecimal();
                        if (propertyType == typeof(double) || propertyType == typeof(double?)) return jsonElement.GetDouble();
                        if (propertyType == typeof(FhirDecimal)) return new FhirDecimal(jsonElement.GetDecimal());
                        if (propertyType == typeof(Integer)) return new Integer(jsonElement.GetInt32());
                    }
                    else if (jsonElement.ValueKind == JsonValueKind.True || jsonElement.ValueKind == JsonValueKind.False)
                    {
                        var boolValue = jsonElement.GetBoolean();
                        if (propertyType == typeof(FhirBoolean)) return new FhirBoolean(boolValue);
                        if (propertyType == typeof(bool) || propertyType == typeof(bool?)) return boolValue;
                    }
                }
                else if (newValue is string strValue)
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
                else if (newValue is Enum enumValue && (propertyType.IsEnum || (propertyType.IsGenericType && propertyType.GetGenericTypeDefinition() == typeof(Nullable<>) && propertyType.GetGenericArguments()[0].IsEnum)))
                {
                    var enumType = propertyType.IsEnum ? propertyType : propertyType.GetGenericArguments()[0];
                    return Enum.Parse(enumType, enumValue.ToString());
                }
                else if (newValue is Base complexValue && propertyType.IsAssignableFrom(complexValue.GetType()))
                {
                    return complexValue;
                }

                logger?.LogWarning("Unsupported value type {ValueType} for property {PropertyName} of type {PropertyType}.", newValue.GetType().Name, propertyName, propertyType.Name);
                return newValue;
            }
            catch (Exception ex)
            {
                logger?.LogError(ex, "Failed to convert value of type {ValueType} to property {PropertyName} of type {PropertyType}.", newValue.GetType().Name, propertyName, propertyType.Name);
                return null;
            }
        }

        public static (string propertyName, int? arrayIndex) ParseFhirPathPart(string part)
        {
            if (part.Contains("[") && part.EndsWith("]"))
            {
                var indexStart = part.IndexOf('[');
                var indexEnd = part.IndexOf(']');
                if (indexStart >= indexEnd || indexStart == part.Length - 1)
                    return (part, null);

                var indexStr = part.Substring(indexStart + 1, indexEnd - indexStart - 1);
                if (string.IsNullOrEmpty(indexStr) || !int.TryParse(indexStr, out int index) || index < 0)
                    return (part, null);
                return (part.Substring(0, indexStart), index);
            }
            return (part, null);
        }

        public static string MapFhirPathToPropertyName(string fhirPathName, Type parentType)
        {
            if (parentType != null && _propertyNameCache.TryGetValue((fhirPathName, parentType), out var cachedName))
                return cachedName;

            string normalizedFhirPathName = fhirPathName.ToLower();
            string result;

            if (FhirPathToPropertyMappings.TryGetValue(normalizedFhirPathName, out string mappedName))
            {
                result = mappedName;
            }
            else
            {
                var pascalCase = char.ToUpper(fhirPathName[0]) + (fhirPathName.Length > 1 ? fhirPathName.Substring(1) : string.Empty);
                if (parentType != null && GetProperty(parentType, pascalCase) != null)
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
                    var basePascalCase = char.ToUpper(baseName[0]) + (baseName.Length > 1 ? baseName.Substring(1) : string.Empty);
                    result = parentType != null && GetProperty(parentType, basePascalCase) != null ? basePascalCase : fhirPathName;
                }
            }

            if (parentType != null)
                _propertyNameCache.TryAdd((fhirPathName, parentType), result);

            return result;
        }

        private static void SetPropertyValue(PropertyInfo previousProperty, object previousObject, PropertyInfo targetProperty, Base currentObject, object value)
        {
            if (previousProperty != null && previousObject != null)
                previousProperty.SetValue(previousObject, value);
            else
                targetProperty.SetValue(currentObject, value);
        }

        public static SetValueResult SetValueViaFhirPath(DomainResource resource, string targetFhirPath, object targetValue, ITypedElement scopedNode, ILogger? logger = null)
        {
            try
            {
                var targetElements = scopedNode.Select(targetFhirPath).ToList();
                if (!targetElements.Any())
                    return SetValueResult.Failure($"No target elements found for FHIRPath {targetFhirPath}.");

                foreach (var targetElement in targetElements)
                {
                    if (string.IsNullOrEmpty(targetElement.Location))
                        return SetValueResult.Failure($"Target element at FHIRPath {targetFhirPath} has no location.");

                    var targetPath = targetElement.Location;
                    var pathParts = targetPath.Split('.').Skip(1).ToArray();
                    var (parentPoco, propertyToSet) = pathParts.Length == 0
                        ? (resource, GetProperty(resource.GetType(), targetFhirPath))
                        : NavigateFhirPath(resource, string.Join(".", pathParts), createIfMissing: true, logger);

                    if (parentPoco == null || propertyToSet == null)
                        return SetValueResult.Failure($"Could not resolve parent or property for target path {targetPath} in resource type {resource.TypeName}.");

                    if (!propertyToSet.CanWrite)
                        return SetValueResult.Failure($"Property {propertyToSet.Name} on type {parentPoco.GetType().Name} is not writable for FHIRPath {targetFhirPath}.");

                    SetPropertyValue(parentPoco, propertyToSet, targetValue, logger);
                }
                return SetValueResult.Success();
            }
            catch (Exception ex)
            {
                logger?.LogError(ex, "Failed to evaluate target FHIRPath '{TargetFhirPath}' for resource type {ResourceType}.", targetFhirPath, resource.TypeName);
                return SetValueResult.Failure($"Failed to evaluate target FHIRPath '{targetFhirPath}': {ex.Message}");
            }
        }

        public static SetValueResult ResolveAndSetValueReflectively(DomainResource resource, string targetFhirPath, object targetValue, ILogger logger)
        {
            var pathParts = targetFhirPath.Split('.');
            try
            {
                if (pathParts.Length == 1)
                {
                    var property = GetProperty(resource.GetType(), pathParts[0]);
                    if (property == null || !property.CanWrite)
                        return SetValueResult.Failure($"Property {pathParts[0]} not found or not writable for FHIRPath {targetFhirPath}.");

                    SetPropertyValue(resource, property, targetValue, logger);
                }
                else
                {
                    var (parentPoco, property) = NavigateFhirPath(resource, targetFhirPath, createIfMissing: true, logger);
                    if (property == null || !property.CanWrite)
                        return SetValueResult.Failure($"Property not found or not writable for FHIRPath {targetFhirPath}.");

                    if (pathParts.Last().Contains("[") && typeof(IList).IsAssignableFrom(property.PropertyType))
                    {
                        var (_, arrayIndex) = ParseFhirPathPart(pathParts.Last());
                        var list = property.GetValue(parentPoco) as IList ?? (IList)Activator.CreateInstance(property.PropertyType);
                        property.SetValue(parentPoco, list);

                        while (list.Count <= arrayIndex.Value)
                            list.Add(Activator.CreateInstance(property.PropertyType.GenericTypeArguments[0]));

                        var convertedValue = ConvertToFhirType(targetValue, property.PropertyType.GenericTypeArguments[0], property.Name, logger);
                        list[arrayIndex.Value] = convertedValue;
                    }
                    else
                    {
                        SetPropertyValue(parentPoco, property, targetValue, logger);
                    }
                }

                return SetValueResult.Success();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to resolve and set value reflectively for FHIRPath '{TargetFhirPath}'.", targetFhirPath);
                return SetValueResult.Failure($"Failed to set value reflectively for FHIRPath '{targetFhirPath}': {ex.Message}");
            }
        }

        public static SetValueResult CreateAndSetTargetElement(Resource resource, string targetFhirPath, object newValue, ILogger? logger = null)
        {
            var pathParts = targetFhirPath.Split('.');
            if (pathParts.Length == 1)
            {
                var propertyName = pathParts[0];
                var property = GetProperty(resource.GetType(), propertyName);
                if (property == null)
                    return SetValueResult.Failure($"Property {propertyName} not found on type {resource.TypeName} for FHIRPath {targetFhirPath}.");

                try
                {
                    if (!property.CanWrite)
                        return SetValueResult.Failure($"Property {propertyName} on type {resource.TypeName} is not writable for FHIRPath {targetFhirPath}.");
                    SetPropertyValue(resource, property, newValue, logger);
                    return SetValueResult.Success();
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Failed to set target element for FHIRPath '{TargetFhirPath}' in resource type {ResourceType}.", targetFhirPath, resource.TypeName);
                    return SetValueResult.Failure($"Failed to set target element for FHIRPath '{targetFhirPath}': {ex.Message}");
                }
            }
            else
            {
                var propertyName = pathParts.Last();
                int? arrayIndex = null;
                if (propertyName.Contains("[") && propertyName.EndsWith("]"))
                    (propertyName, arrayIndex) = ParseFhirPathPart(propertyName);

                var parentPath = string.Join(".", pathParts.Take(pathParts.Length - 1));
                var parentPoco = CreateParentStructure(resource, parentPath, logger);
                if (parentPoco == null)
                    return SetValueResult.Failure($"Could not create parent structure for {parentPath} in resource type {resource.TypeName}.");

                propertyName = MapFhirPathToPropertyName(propertyName, parentPoco.GetType());
                var property = GetProperty(parentPoco.GetType(), propertyName);
                if (property == null)
                    return SetValueResult.Failure($"Property {propertyName} not found on parent type {parentPoco.GetType().Name} for FHIRPath {targetFhirPath}.");

                try
                {
                    if (newValue is IList valueList && typeof(IList).IsAssignableFrom(property.PropertyType))
                    {
                        var list = (IList)Activator.CreateInstance(property.PropertyType);
                        foreach (var item in valueList)
                        {
                            var convertedItem = ConvertToFhirType(item, property.PropertyType.GenericTypeArguments[0], propertyName, logger);
                            list.Add(convertedItem);
                        }
                        property.SetValue(parentPoco, list);
                    }
                    else
                    {
                        var convertedValue = ConvertToFhirType(newValue, property.PropertyType, propertyName, logger);
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
                                    list.Add(convertedValue);
                                else
                                    list[0] = convertedValue;
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
                    logger.LogError(ex, "Failed to set target element for FHIRPath '{TargetFhirPath}' in resource type {ResourceType}.", targetFhirPath, resource.TypeName);
                    return SetValueResult.Failure($"Failed to set target element for FHIRPath '{targetFhirPath}': {ex.Message}");
                }
            }
        }

        private static void SetPropertyValue(Base parentPoco, PropertyInfo property, object targetValue, ILogger logger)
        {
            if (targetValue is IList valueList && typeof(IList).IsAssignableFrom(property.PropertyType))
            {
                var list = (IList)Activator.CreateInstance(property.PropertyType);
                foreach (var item in valueList)
                    list.Add(ConvertToFhirType(item, property.PropertyType.GenericTypeArguments[0], property.Name, logger));
                property.SetValue(parentPoco, list);
            }
            else
            {
                var convertedValue = ConvertToFhirType(targetValue, property.PropertyType, property.Name, logger);
                property.SetValue(parentPoco, convertedValue);
            }
        }

        internal static async Task<(bool IsValid, string? ErrorMessage)> ValidateOperation(string operationType, string operationJson, List<string> resources)
        {
            try
            {
                var operation = OperationHelper.GetOperation(operationType, operationJson);

                if (operation is CopyPropertyOperation)
                {
                    var op = (CopyPropertyOperation)operation;
                    StringBuilder builder = new StringBuilder();
                    foreach(var resource in resources)
                    {
                        var result = await FhirPathValidator.IsFhirPathValidForResourceType(op.SourceFhirPath, resource);

                        if (!result.IsValid)
                        {
                            builder.AppendLine($"SourceFhirPath {op.SourceFhirPath} is not a valid path for {resource}: {result.ErrorMessage}");
                        }

                        result = await FhirPathValidator.IsFhirPathValidForResourceType(op.TargetFhirPath, resource);

                        if (!result.IsValid)
                        {
                            builder.AppendLine($"TargetFhirPath {op.TargetFhirPath} is not a valid path for {resource}: {result.ErrorMessage}");
                        }
                    }
                    
                    if(builder.Length > 0)
                    {
                        return (false, builder.ToString());
                    }
                }
                else if (operation is CodeMapOperation)
                {
                    var op = (CodeMapOperation)operation;
                    StringBuilder builder = new StringBuilder();
                    foreach (var resource in resources)
                    {
                        var result = await FhirPathValidator.IsFhirPathValidForResourceType(op.FhirPath, resource);

                        if (!result.IsValid)
                        {
                            builder.AppendLine($"FhirPath {op.FhirPath} is not a valid path for {resource}: {result.ErrorMessage}");
                        }
                    }

                    if (builder.Length > 0)
                    {
                        return (false, builder.ToString());
                    }
                }
                else if (operation is ConditionalTransformOperation)
                {
                    var op = (ConditionalTransformOperation)operation;
                    StringBuilder builder = new StringBuilder();
                    foreach (var resource in resources)
                    {
                        var result = await FhirPathValidator.IsFhirPathValidForResourceType(op.TargetFhirPath, resource);

                        if (!result.IsValid)
                        {
                            builder.AppendLine($"TargetFhirPath {op.TargetFhirPath} is not a valid path for {resource}: {result.ErrorMessage}");
                        }

                        foreach(var condition in op.Conditions)
                        {
                            var condResult = await FhirPathValidator.IsFhirPathValidForResourceType(condition.FhirPathSource, resource);

                            if (!condResult.IsValid)
                            {
                                builder.AppendLine($"Condition.FhirPathSource {condition.FhirPathSource} is not a valid path for {resource}: {condResult.ErrorMessage}");
                            }
                        }
                    }

                    if (builder.Length > 0)
                    {
                        return (false, builder.ToString());
                    }
                }
                else
                {
                    return (false, "Operation is not of a known type.");
                }

            }
            catch (Exception ex)
            {
                return (false, ex.Message);
            }

            return (true, null);
        }

        public class SetValueResult
        {
            public bool Result { get; }
            public string ErrorMessage { get; }

            public SetValueResult(bool success, string errorMessage)
            {
                Result = success;
                ErrorMessage = errorMessage ?? string.Empty;
            }

            public static SetValueResult Success() => new(true, string.Empty);
            public static SetValueResult Failure(string errorMessage) => new(false, errorMessage);
        }
    }
}