using Hl7.Fhir.ElementModel;
using Hl7.Fhir.FhirPath;
using Hl7.Fhir.Model;
using Hl7.FhirPath;
using System.Collections;
using System.Collections.Concurrent;
using System.Reflection;
using System.Text.Json;

namespace LantanaGroup.Link.Normalization.Application.Services.Operations
{
    public static class OperationServiceHelper
    {
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

        public static bool ValidateFhirPath(string fhirPath, out string errorMessage, ILogger logger)
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
                logger.LogError(ex, "Invalid FHIRPath expression: {FhirPath}.", fhirPath);
                errorMessage = ex.Message;
                return false;
            }
        }

        public static ITypedElement EvaluateFhirPath(ITypedElement scopedNode, string fhirPath, ILogger logger)
        {
            try
            {
                var elements = scopedNode.Select(fhirPath).ToList();
                return elements.FirstOrDefault();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to evaluate FHIRPath '{FhirPath}' for resource type {ResourceType}.", fhirPath, scopedNode.Name);
                return null;
            }
        }

        public static (bool Success, string ErrorMessage, object Value) ExtractValueFromFhirPath(ITypedElement scopedNode, string fhirPath, ILogger logger)
        {
            try
            {
                var values = scopedNode.Select(fhirPath).ToList();
                if (!values.Any())
                {
                    return (false, "No values found.", null);
                }

                var pocos = values
                    .Where(v => v != null)
                    .Select(v => v.ToPoco())
                    .Where(p => p is Base)
                    .ToList();

                if (!pocos.Any() && values.Any())
                {
                    return (false, "No valid FHIR types converted.", null);
                }

                if (pocos.Count == 1)
                {
                    var poco = pocos[0];
                    if (poco is PrimitiveType primitive)
                    {
                        return (true, string.Empty, primitive.ObjectValue ?? null);
                    }
                    else if (poco is Quantity quantity)
                    {
                        return (true, string.Empty, quantity.Value ?? null);
                    }
                    else if (poco is Base complex)
                    {
                        return (true, string.Empty, complex);
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
                    return result.Any() ? (true, string.Empty, result) : (false, "No valid values extracted.", null);
                }

                return (false, "Unexpected POCO processing failure.", null);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to evaluate FHIRPath '{FhirPath}' for resource type {ResourceType}.", fhirPath, scopedNode.Name);
                return (false, $"Failed to evaluate FHIRPath '{fhirPath}': {ex.Message}", null);
            }
        }

        public static (Base Parent, PropertyInfo Property) NavigateFhirPath(object resource, string fhirPath, bool createIfMissing = false, ILogger logger = null)
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

                targetProperty = GetProperty(currentObject.GetType(), propertyName);
                if (targetProperty == null)
                {
                    logger?.LogWarning("Property {PropertyName} not found for FHIRPath {FhirPath}.", propertyName, fhirPath);
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

        public static Base CreateParentStructure(Resource resource, string parentPath, ILogger logger = null)
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

        private static object ConvertJsonElementToFhirType(JsonElement jsonElement, Type propertyType, Base parentPoco, string propertyName, ILogger logger)
        {
            if (propertyType.IsEnum || propertyType.IsGenericType && propertyType.GetGenericTypeDefinition() == typeof(Nullable<>) && propertyType.GetGenericArguments()[0].IsEnum)
            {
                var enumType = propertyType.IsEnum ? propertyType : propertyType.GetGenericArguments()[0];
                if (jsonElement.ValueKind == JsonValueKind.Number && jsonElement.TryGetInt32(out int intValue))
                {
                    return Enum.ToObject(enumType, intValue);
                }
                else if (jsonElement.ValueKind == JsonValueKind.String)
                {
                    return Enum.Parse(enumType, jsonElement.GetString(), ignoreCase: true);
                }
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
            logger?.LogWarning("Unsupported JsonElement type {ValueKind} for property {PropertyName}.", jsonElement.ValueKind, propertyName);
            return null;
        }

        public static object ConvertToFhirType(object newValue, Type propertyType, Base parentPoco, string propertyName, ILogger logger)
        {
            if (newValue == null) return null;

            try
            {
                if (newValue is JsonElement jsonElement)
                {
                    return ConvertJsonElementToFhirType(jsonElement, propertyType, parentPoco, propertyName, logger);
                }

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
                else if (newValue is Enum enumValue && (propertyType.IsEnum || propertyType.IsGenericType && propertyType.GetGenericTypeDefinition() == typeof(Nullable<>) && propertyType.GetGenericArguments()[0].IsEnum))
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

        public static string MapFhirPathToPropertyName(string fhirPathName, Type parentType)
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
                    if (!string.IsNullOrEmpty(baseName))
                    {
                        var basePascalCase = char.ToUpper(baseName[0]) + (baseName.Length > 1 ? baseName.Substring(1) : string.Empty);
                        if (parentType != null && GetProperty(parentType, basePascalCase) != null)
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
    }
}