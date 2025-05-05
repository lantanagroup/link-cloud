using Hl7.Fhir.ElementModel;
using Hl7.Fhir.FhirPath;
using Hl7.Fhir.Model;
using Hl7.FhirPath;
using System.Collections;
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

        /// <summary>
        /// Copies a value from sourceFhirPath to targetFhirPath on a deep copy of the input resource.
        /// Returns the processed deep copy only if no exceptions occur; otherwise, throws the exception.
        /// The original resource remains unchanged.
        /// </summary>
        /// <param name="resource">The input DomainResource.</param>
        /// <returns>The processed deep copy of the resource.</returns>
        /// <exception cref="ArgumentException">Thrown if inputs are invalid.</exception>
        /// <exception cref="InvalidOperationException">Thrown if FHIRPath resolution or type issues occur.</exception>
        public DomainResource Execute(DomainResource resource)
        {
            if (resource == null || string.IsNullOrEmpty(SourceFhirPath) || string.IsNullOrEmpty(TargetFhirPath))
            {
                throw new ArgumentException("Resource, SourceFhirPath, and TargetFhirPath must not be null or empty.");
            }

            // Create a deep copy of the input resource
            var resourceCopy = resource.DeepCopy() as DomainResource;
            if (resourceCopy == null)
            {
                throw new InvalidOperationException("Failed to create a deep copy of the resource.");
            }

            // Perform the copy operation on the deep copy
            CopyFhirPathValue(resourceCopy, SourceFhirPath, TargetFhirPath);
            return resourceCopy;
        }

        private static void CopyFhirPathValue(Resource resource, string sourceFhirPath, string targetFhirPath)
        {
            // Convert the resource to a navigable element for FHIRPath evaluation
            var scopedNode = resource.ToTypedElement();

            // Evaluate the source FHIRPath to get the value(s)
            var sourceValues = scopedNode.Select(sourceFhirPath).ToList();

            if (!sourceValues.Any())
            {
                throw new InvalidOperationException($"No values found at source FHIRPath: {sourceFhirPath}");
            }

            // Take the first value (FHIRPath may return multiple values)
            var sourceValue = sourceValues.First();
            var sourcePoco = sourceValue?.ToPoco();

            if (sourcePoco == null)
            {
                throw new InvalidOperationException("Source value could not be resolved to a FHIR object.");
            }

            // Handle primitive and complex types
            if (sourcePoco is PrimitiveType sourcePrimitive)
            {
                // Handle primitive source (e.g., string, integer)
                if (sourcePrimitive.ObjectValue == null)
                {
                    throw new InvalidOperationException("Source primitive value is null.");
                }

                // Set the primitive value at the target path, creating structure if needed
                SetTargetValue(resource, targetFhirPath, sourcePrimitive.ObjectValue);
            }
            else if (sourcePoco is Base sourceComplex)
            {
                // Handle complex source by deep copying
                var copiedObject = sourceComplex.DeepCopy() as Base;

                // Verify target can accept this type of complex object by checking the property type
                var pathParts = targetFhirPath.Split('.');
                if (pathParts.Length >= 2)
                {
                    var parentPath = string.Join(".", pathParts.Take(pathParts.Length - 1));
                    var propertyName = pathParts.Last().Split('[')[0]; // Remove any array indexing
                    
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

                SetTargetValue(resource, targetFhirPath, copiedObject);
            }
            else
            {
                throw new InvalidOperationException("Source type is not supported.");
            }

        private static void SetTargetValue(Resource resource, string targetFhirPath, object newValue)
        {
            // Split the FHIRPath to identify the parent path and property
            var pathParts = targetFhirPath.Split('.');
            if (pathParts.Length < 2)
            {
                throw new InvalidOperationException($"Target FHIRPath {targetFhirPath} is too short to resolve parent and property.");
            }

            // Construct the parent FHIRPath (everything except the last part)
            var parentPath = string.Join(".", pathParts.Take(pathParts.Length - 1));
            var propertyName = pathParts.Last();

            // Handle array indexing (e.g., type[0].coding.code)
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

            // Evaluate the parent FHIRPath
            var scopedNode = resource.ToTypedElement();
            var parentElements = string.IsNullOrEmpty(parentPath) ? [ scopedNode ] : scopedNode.Select(parentPath).ToList();

            Base parentPoco;
            if (!parentElements.Any())
            {
                // Parent path does not exist; create the necessary structure
                parentPoco = CreateParentStructure(resource, parentPath);
                if (parentPoco == null)
                {
                    throw new InvalidOperationException($"Could not create parent structure for {parentPath}.");
                }
            }
            else
            {
                // Use the first parent
                parentPoco = parentElements.First().ToPoco() as Base;
                if (parentPoco == null)
                {
                    throw new InvalidOperationException($"Could not resolve parent POCO for {parentPath}.");
                }
            }

            // Get the property info
            var property = parentPoco.GetType().GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
            if (property == null)
            {
                throw new InvalidOperationException($"Property {propertyName} not found on parent type {parentPoco.GetType().Name}.");
            }

            // Convert newValue to the appropriate FHIR type if necessary
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

            // Handle single vs. list properties
            if (typeof(IList).IsAssignableFrom(property.PropertyType))
            {
                // Property is a list (e.g., Location.type)
                var list = property.GetValue(parentPoco) as IList;
                if (list == null)
                {
                    list = (IList)Activator.CreateInstance(property.PropertyType);
                    property.SetValue(parentPoco, list);
                }

                if (arrayIndex.HasValue)
                {
                    // Ensure the list is large enough
                    while (list.Count <= arrayIndex.Value)
                    {
                        // Create a new instance of the list item type
                        var itemType = property.PropertyType.GenericTypeArguments[0];
                        list.Add(Activator.CreateInstance(itemType));
                    }

                    // Set the value at the specified index
                    list[arrayIndex.Value] = targetValue;
                }
                else
                {
                    // If no index is specified, append or replace first
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
                // Single value property
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

                // Handle array indexing (e.g., type[0])
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

                // Get the property
                var property = current.GetType().GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
                if (property == null)
                {
                    throw new InvalidOperationException($"Property {propertyName} not found on type {current.GetType().Name}.");
                }

                if (typeof(IList).IsAssignableFrom(property.PropertyType))
                {
                    // Property is a list
                    var list = property.GetValue(current) as IList;
                    if (list == null)
                    {
                        list = (IList)Activator.CreateInstance(property.PropertyType);
                        property.SetValue(current, list);
                    }

                    // Ensure the list has enough elements
                    var itemType = property.PropertyType.GenericTypeArguments[0];
                    while (list.Count <= (arrayIndex ?? 0))
                    {
                        list.Add(Activator.CreateInstance(itemType));
                    }

                    // Move to the indexed element
                    current = list[arrayIndex ?? 0] as Base;
                }
                else
                {
                    // Single value property
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
