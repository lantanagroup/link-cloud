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

            var sourceValues = scopedNode.Select(sourceFhirPath).ToList();
            if (!sourceValues.Any())
            {
                throw new InvalidOperationException($"No values found at source FHIRPath: {sourceFhirPath}");
            }

            var sourceValue = sourceValues.First();
            var sourcePoco = sourceValue?.ToPoco();
            if (sourcePoco == null)
            {
                throw new InvalidOperationException("Source value could not be resolved to a FHIR object.");
            }

            var targetElements = scopedNode.Select(targetFhirPath).ToList();

            if (sourcePoco is PrimitiveType sourcePrimitive)
            {
                if (sourcePrimitive.ObjectValue == null)
                {
                    throw new InvalidOperationException("Source primitive value is null.");
                }

                object targetValue = sourcePrimitive.ObjectValue; // Start with the raw value

                if (targetElements.Any())
                {
                    // Update each existing target element directly on the resource's object graph
                    foreach (var targetElement in targetElements)
                    {
                        // Get the FHIRPath location of the target element (e.g., "Location.type[0].coding[0].code")
                        var targetPath = targetElement.Location;
                        if (string.IsNullOrEmpty(targetPath))
                        {
                            throw new InvalidOperationException($"Target element at {targetFhirPath} does not have a valid location.");
                        }

                        // Navigate the resourceCopy object graph directly, skipping the resource type prefix
                        var pathParts = targetPath.Split('.').Skip(1).ToArray(); // Skip the resource type (e.g., "Location")
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

                            if (i == pathParts.Length - 2) // One step before the last part (the property to set)
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

                        // Convert the source value to the appropriate FHIR type
                        var propertyToSet = parentPoco.GetType().GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
                        if (propertyToSet == null)
                        {
                            throw new InvalidOperationException($"Property {propertyName} not found on parent type {parentPoco.GetType().Name}.");
                        }

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
                        else if (targetValue is not Base && propertyToSet.PropertyType.IsAssignableFrom(typeof(Base)))
                        {
                            throw new InvalidOperationException($"Cannot assign raw value of type {targetValue.GetType().Name} to FHIR property {propertyName} of type {propertyToSet.PropertyType.Name}.");
                        }

                        // Set the value on the parent object directly
                        propertyToSet.SetValue(parentPoco, targetValue);
                    }
                }
                else
                {
                    // No target elements exist; create the structure and set the value
                    SetTargetValue(resource, targetFhirPath, sourcePrimitive.ObjectValue);
                }
            }
            else if (sourcePoco is Base sourceComplex)
            {
                var copiedObject = sourceComplex.DeepCopy() as Base;

                var pathParts = targetFhirPath.Split('.');
                if (pathParts.Length >= 2)
                {
                    var parentPath = string.Join(".", pathParts.Take(pathParts.Length - 1));
                    var propertyName = pathParts.Last().Split('[')[0];

                    var parentNode = resource.ToTypedElement().Select(parentPath).FirstOrDefault();
                    if (parentNode != null)
                    {
                        var parentPoco = parentNode.ToPoco() as Base;
                        var property = parentPoco?
                            .GetType()
                            .GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);

                        if (property != null
                            && !property.PropertyType.IsAssignableFrom(copiedObject.GetType())
                            && !(typeof(IList).IsAssignableFrom(property.PropertyType)
                                 && property.PropertyType.GenericTypeArguments.Length > 0
                                 && property.PropertyType.GenericTypeArguments[0]
                                       .IsAssignableFrom(copiedObject.GetType())))
                        {
                            throw new InvalidOperationException(
                                $"Target property {propertyName} of type {property.PropertyType.Name} " +
                                $"cannot accept source object of type {copiedObject.GetType().Name}.");
                        }
                    }
                }

                if (targetElements.Any())
                {
                    foreach (var targetElement in targetElements)
                    {
                        var targetParentPath = targetElement.Location;
                        var targetParentParts = targetParentPath.Split('.');
                        var targetPropertyName = targetParentParts.Last().Split('[')[0];
                        var targetParentPathWithoutProperty = string.Join(".", targetParentParts.Take(targetParentParts.Length - 1));
                        var targetParentElements = scopedNode.Select(targetParentPathWithoutProperty).ToList();

                        if (targetParentElements.Any())
                        {
                            var targetParentPoco = targetParentElements.First().ToPoco() as Base;
                            if (targetParentPoco != null)
                            {
                                var targetProperty = targetParentPoco.GetType().GetProperty(targetPropertyName, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
                                if (targetProperty != null)
                                {
                                    if (typeof(IList).IsAssignableFrom(targetProperty.PropertyType))
                                    {
                                        var list = targetProperty.GetValue(targetParentPoco) as IList;
                                        if (list != null)
                                        {
                                            var index = int.Parse(targetParentParts.Last().Split('[')[1].TrimEnd(']'));
                                            list[index] = copiedObject;
                                        }
                                    }
                                    else
                                    {
                                        targetProperty.SetValue(targetParentPoco, copiedObject);
                                    }
                                }
                            }
                        }
                    }
                }
                else
                {
                    SetTargetValue(resource, targetFhirPath, copiedObject);
                }
            }
            else
            {
                throw new InvalidOperationException("Source type is not supported.");
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

            var scopedNode = resource.ToTypedElement();
            var parentElements = string.IsNullOrEmpty(parentPath) ? [scopedNode] : scopedNode.Select(parentPath).ToList();

            Base parentPoco;
            if (!parentElements.Any())
            {
                parentPoco = CreateParentStructure(resource, parentPath);
                if (parentPoco == null)
                {
                    throw new InvalidOperationException($"Could not create parent structure for {parentPath}.");
                }
            }
            else
            {
                parentPoco = parentElements.First().ToPoco() as Base;
                if (parentPoco == null)
                {
                    throw new InvalidOperationException($"Could not resolve parent POCO for {parentPath}.");
                }
            }

            var property = parentPoco.GetType().GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
            if (property == null)
            {
                throw new InvalidOperationException($"Property {propertyName} not found on parent type {parentPoco.GetType().Name}.");
            }

            object targetValue = newValue;
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
            else if (newValue is not Base && property.PropertyType.IsAssignableFrom(typeof(Base)))
            {
                throw new InvalidOperationException($"Cannot assign raw value of type {newValue.GetType().Name} to FHIR property {propertyName} of type {property.PropertyType.Name}.");
            }

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

                    var existingItem = list[arrayIndex.Value] as Base;
                    if (existingItem != null)
                    {
                        var targetProperty = existingItem.GetType().GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
                        if (targetProperty != null && targetProperty.PropertyType.IsAssignableFrom(targetValue.GetType()))
                        {
                            targetProperty.SetValue(existingItem, targetValue);
                        }
                        else
                        {
                            list[arrayIndex.Value] = targetValue;
                        }
                    }
                    else
                    {
                        list[arrayIndex.Value] = targetValue;
                    }
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