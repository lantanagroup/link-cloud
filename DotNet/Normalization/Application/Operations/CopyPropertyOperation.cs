using Hl7.Fhir.ElementModel;
using Hl7.Fhir.FhirPath;
using Hl7.Fhir.Model;
using Hl7.FhirPath;
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

            CopyFhirPathValue(resourceCopy, SourceFhirPath, TargetFhirPath);
            return resourceCopy;
        }

        private static void CopyFhirPathValue(DomainResource resource, string sourceFhirPath, string targetFhirPath)
        {
            var scopedNode = resource.ToTypedElement();

            // Extract source value using FHIRPath or fallback
            object targetValue = ExtractValueFromFhirPath(scopedNode, sourceFhirPath);
            if (targetValue == null && (sourceFhirPath.EndsWith("value") || sourceFhirPath.EndsWith("value.value")))
            {
                targetValue = GetValueReflectively(resource);
            }

            if (targetValue == null)
            {
                throw new InvalidOperationException($"No values found at source FHIRPath: {sourceFhirPath}");
            }

            // Handle primitive types (from old version)
            if (targetValue is string || targetValue is int || targetValue is bool || targetValue is decimal || targetValue is DateTime)
            {
                // Try setting via FHIRPath
                SetValueViaFhirPath(resource, targetFhirPath, targetValue, scopedNode);

                var scopedHasTargetAtPath = scopedNode.Select(targetFhirPath).Any();
                // Fallback for component targets using reflection
                if (!scopedHasTargetAtPath && targetFhirPath.Contains("component") && (targetFhirPath.EndsWith("value") || targetFhirPath.EndsWith("value.value")))
                {
                    SetComponentValuesReflectively(resource, targetValue);
                }
                // If no target elements found, create them
                else if (!scopedHasTargetAtPath)
                {
                    SetTargetValue(resource, targetFhirPath, targetValue);
                }
            }
            // Handle complex types (from old version)
            else if (targetValue is Base complexValue)
            {
                var copiedObject = complexValue.DeepCopy() as Base;

                var pathParts = targetFhirPath.Split('.');
                if (pathParts.Length >= 2)
                {
                    var parentPath = string.Join(".", pathParts.Take(pathParts.Length - 1));
                    var propertyName = pathParts.Last().Split('[')[0];

                    var parentNode = scopedNode.Select(parentPath).FirstOrDefault();
                    if (parentNode != null)
                    {
                        var parentPoco = parentNode.ToPoco() as Base;
                        var property = parentPoco?.GetType().GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);

                        if (property != null
                            && !property.PropertyType.IsAssignableFrom(copiedObject.GetType())
                            && !(typeof(IList).IsAssignableFrom(property.PropertyType)
                                 && property.PropertyType.GenericTypeArguments.Length > 0
                                 && property.PropertyType.GenericTypeArguments[0].IsAssignableFrom(copiedObject.GetType())))
                        {
                            throw new InvalidOperationException(
                                $"Target property {propertyName} of type {property.PropertyType.Name} " +
                                $"cannot accept source object of type {copiedObject.GetType().Name}.");
                        }
                    }
                }

                // Try setting via FHIRPath
                SetValueViaFhirPath(resource, targetFhirPath, copiedObject, scopedNode);

                // If no target elements found, create them
                if (!scopedNode.Select(targetFhirPath).Any())
                {
                    SetTargetValue(resource, targetFhirPath, copiedObject);
                }
            }
            else
            {
                throw new InvalidOperationException("Source type is not supported.");
            }
        }

        private static object ExtractValueFromFhirPath(ITypedElement scopedNode, string fhirPath)
        {
            var values = scopedNode.Select(fhirPath).ToList();
            if (!values.Any())
            {
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

        private static object GetValueReflectively(object resource, string propertyName = "Value")
        {
            var valueProperty = resource.GetType().GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
            if (valueProperty == null)
            {
                return null;
            }

            var value = valueProperty.GetValue(resource);
            if (value is Quantity quantity)
            {
                if (quantity.Value == null)
                {
                    throw new InvalidOperationException("Quantity value is null.");
                }
                return quantity.Value;
            }
            else if (value is PrimitiveType primitive)
            {
                if (primitive.ObjectValue == null)
                {
                    throw new InvalidOperationException("Primitive value is null.");
                }
                return primitive.ObjectValue;
            }
            else if (value is Base complexValue)
            {
                return complexValue.DeepCopy() as Base;
            }

            return null;
        }

        private static void SetValueViaFhirPath(DomainResource resource, string targetFhirPath, object targetValue, ITypedElement scopedNode)
        {
            var targetElements = scopedNode.Select(targetFhirPath).ToList();
            if (!targetElements.Any())
            {
                return; // Let the caller handle creation or fallback
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
                    propertyName = part;
                    arrayIndex = null;

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
                                propertyName = part.Substring(0, indexStart);
                            }
                        }
                    }

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
                            list.Add(Activator.CreateInstance(itemType));
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
                if (propertyToSet == null)
                {
                    throw new InvalidOperationException($"Property {propertyName} not found on parent type {parentPoco.GetType().Name}.");
                }

                // Convert the target value to the appropriate FHIR type
                if (targetValue is string strValue && propertyToSet.PropertyType == typeof(FhirString))
                {
                    targetValue = new FhirString(strValue);
                }
                else if (targetValue is int intValue && propertyToSet.PropertyType == typeof(Integer))
                {
                    targetValue = new Integer(intValue);
                }
                else if (targetValue is bool boolValue && propertyToSet.PropertyType == typeof(FhirBoolean))
                {
                    targetValue = new FhirBoolean(boolValue);
                }
                else if (targetValue is decimal decValue && propertyToSet.PropertyType == typeof(FhirDecimal))
                {
                    targetValue = new FhirDecimal(decValue);
                }
                else if (targetValue is DateTime dateValue && propertyToSet.PropertyType == typeof(FhirDateTime))
                {
                    targetValue = new FhirDateTime(dateValue);
                }
                else if (targetValue is Base complexValue && !propertyToSet.PropertyType.IsAssignableFrom(complexValue.GetType()))
                {
                    throw new InvalidOperationException($"Cannot assign complex value of type {targetValue.GetType().Name} to property {propertyName} of type {propertyToSet.PropertyType.Name}.");
                }

                propertyToSet.SetValue(parentPoco, targetValue);
            }
        }

        private static void SetComponentValuesReflectively(DomainResource resource, object targetValue)
        {
            var componentsProperty = resource.GetType().GetProperty("Component", BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
            if (componentsProperty == null)
            {
                return;
            }

            var components = componentsProperty.GetValue(resource) as IList;
            if (components == null)
            {
                return;
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
                        valueProp.SetValue(quantity, targetValue);
                    }
                }
            }
        }

        private static void SetTargetValue(Resource resource, string targetFhirPath, object newValue)
        {
            var pathParts = targetFhirPath.Split('.');
            if (pathParts.Length < 2)
            {
                throw new InvalidOperationException($"Target FHIRPath {targetFhirPath} is too short to resolve parent and property.");
            }

            var parentPath = string.Join(".", pathParts.Take(pathParts.Length - 1));
            var propertyName = pathParts.Last();

            int? arrayIndex = null;
            if (propertyName.Contains("[") && propertyName.EndsWith("]"))
            {
                var indexStart = propertyName.IndexOf('[');
                var indexEnd = propertyName.IndexOf(']');
                if (indexStart < indexEnd)
                {
                    var indexStr = propertyName.Substring(indexStart + 1, indexEnd - indexStart - 1);
                    if (int.TryParse(indexStr, out int index))
                    {
                        arrayIndex = index;
                        propertyName = propertyName.Substring(0, indexStart);
                    }
                }
            }

            // Resolve or create the parent structure
            var parentPoco = CreateParentStructure(resource, parentPath);
            if (parentPoco == null)
            {
                throw new InvalidOperationException($"Could not create parent structure for {parentPath}.");
            }

            var property = parentPoco.GetType().GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
            if (property == null)
            {
                throw new InvalidOperationException($"Property {propertyName} not found on parent type {parentPoco.GetType().Name}.");
            }

            object targetValue = newValue;
            // Apply type conversion (similar to SetValueViaFhirPath)
            if (newValue is string strValue && property.PropertyType == typeof(FhirString))
            {
                targetValue = new FhirString(strValue);
            }
            else if (newValue is int intValue && property.PropertyType == typeof(Integer))
            {
                targetValue = new Integer(intValue);
            }
            else if (newValue is bool boolValue && property.PropertyType == typeof(FhirBoolean))
            {
                targetValue = new FhirBoolean(boolValue);
            }
            else if (newValue is decimal decValue && property.PropertyType == typeof(FhirDecimal))
            {
                targetValue = new FhirDecimal(decValue);
            }
            else if (newValue is DateTime dateValue && property.PropertyType == typeof(FhirDateTime))
            {
                targetValue = new FhirDateTime(dateValue);
            }
            else if (newValue is Base complexValue && !property.PropertyType.IsAssignableFrom(complexValue.GetType()))
            {
                throw new InvalidOperationException($"Cannot assign complex value of type {newValue.GetType().Name} to property {propertyName} of type {property.PropertyType.Name}.");
            }

            // Set the value, handling lists if necessary
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
                        list.Add(Activator.CreateInstance(itemType));
                    }
                    list[arrayIndex.Value] = targetValue;
                }
                else
                {
                    if (list.Count == 0)
                    {
                        list.Add(targetValue);
                    }
                    else
                    {
                        list[0] = targetValue;
                    }
                }
            }
            else
            {
                property.SetValue(parentPoco, targetValue);
            }
        }

        private static Base CreateParentStructure(Resource resource, string parentPath)
        {
            if (string.IsNullOrEmpty(parentPath))
            {
                return resource;
            }

            var pathParts = parentPath.Split('.');
            Base current = resource;
            foreach (var part in pathParts)
            {
                string propertyName = part;
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
                            propertyName = part.Substring(0, indexStart);
                        }
                    }
                }

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
                        list.Add(Activator.CreateInstance(itemType));
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
                    throw new InvalidOperationException($"Failed to create or access object at path part {part}.");
                }
            }

            return current;
        }
    }
}