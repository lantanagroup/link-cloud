using Hl7.Fhir.Model;
using Hl7.Fhir.Specification.Snapshot;
using Hl7.Fhir.Specification.Source;
using System.Collections.Concurrent;

namespace LantanaGroup.Link.Normalization.Application.Services.FhirPathValidation
{
    public static class FhirPathValidator
    {
        private static readonly ConcurrentDictionary<string, StructureDefinition> _structureDefinitionCache = new();
        private static readonly string _structureDefinitionsPath = Path.Combine(AppContext.BaseDirectory, "Application", "Services", "FhirValidation", "StructureDefinitions");
        private static readonly CachedResolver _resolver = new CachedResolver(new DirectorySource(_structureDefinitionsPath));

        /// <summary>
        /// Validates if a FHIR Path is valid for a given resource type using its structure definition.
        /// </summary>
        public static async Task<(bool IsValid, string? ErrorMessage)> IsFhirPathValidForResourceType(string fhirPath, string resourceTypeName)
        {
            if (string.IsNullOrWhiteSpace(fhirPath))
                throw new ArgumentException("FHIRPath expression cannot be null or empty.", nameof(fhirPath));
            if (string.IsNullOrWhiteSpace(resourceTypeName))
                throw new ArgumentException("Resource type cannot be null or empty.", nameof(resourceTypeName));

            try
            {
                var structureDefinition = await GetStructureDefinitionAsync(resourceTypeName);
                if (structureDefinition == null || structureDefinition.Snapshot == null)
                    return (false, "StructureDefinition or snapshot not found.");

                var segments = fhirPath.Split('.');
                var currentStructure = structureDefinition;
                var currentPath = resourceTypeName;

                for (int i = 0; i < segments.Length; i++)
                {
                    var segment = segments[i];
                    currentPath += "." + segment;

                    var element = currentStructure.Snapshot.Element.FirstOrDefault(e => e.Path == currentPath);
                    if (element == null)
                        return (false, $"Path segment '{segment}' not found at '{currentPath}'.");

                    // If this is the last segment, we're done
                    if (i == segments.Length - 1)
                        return (true, null);

                    // If the element has a complex type, resolve its StructureDefinition
                    var typeCode = element.Type?.FirstOrDefault()?.Code;
                    if (string.IsNullOrEmpty(typeCode))
                        return (false, $"Element '{segment}' has no type information.");

                    if (IsPrimitive(typeCode))
                        return (false, $"Element '{segment}' is a primitive type and cannot have child elements.");

                    var nextStructure = await GetStructureDefinitionAsync(typeCode);
                    if (nextStructure == null || nextStructure.Snapshot == null)
                        return (false, $"StructureDefinition for type '{typeCode}' not found.");

                    currentStructure = nextStructure;
                    currentPath = typeCode; // Reset path for the new structure
                }

                return (true, null);
            }
            catch (Exception ex)
            {
                return (false, $"Exception during validation: {ex.Message}");
            }
        }


        private static bool IsPrimitive(string typeCode)
        {
            var primitives = new HashSet<string>
            {
                "boolean", "integer", "decimal", "base64Binary", "instant", "string",
                "uri", "date", "dateTime", "time", "code", "oid", "id", "markdown",
                "unsignedInt", "positiveInt", "uuid", "xhtml"
            };
            return primitives.Contains(typeCode);
        }


        private static async Task<StructureDefinition?> GetStructureDefinitionAsync(string resourceTypeName)
        {
            var result = _structureDefinitionCache.TryGetValue(resourceTypeName, out var definition);

            if(result)
            {
                return definition;
            }

            definition = await _resolver.FindStructureDefinitionAsync($"http://hl7.org/fhir/StructureDefinition/{resourceTypeName}");
            if (definition != null && !definition.HasSnapshot)
            {
                var generator = new SnapshotGenerator(_resolver);
                await generator.UpdateAsync(definition);
                _structureDefinitionCache.TryAdd(resourceTypeName, definition);
            }
            else if (definition != null)
            {
                _structureDefinitionCache.TryAdd(resourceTypeName, definition);
            }

            return definition;
        }
    }
}
