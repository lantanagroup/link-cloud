using Hl7.Fhir.Model;
using Hl7.Fhir.Specification.Snapshot;
using Hl7.Fhir.Specification.Source;
using System.Collections.Concurrent;
using System.Text.RegularExpressions;

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
                    return (false, "StructureDefinition or snapshot not found for " + resourceTypeName);

                // Parse the FHIR path
                var segments = ParseFhirPath(fhirPath);

                var currentStructure = structureDefinition;
                var currentBasePath = resourceTypeName; // Path for Element.Path matching (e.g., Encounter.type)
                var displayPath = resourceTypeName; // Path for error messages (e.g., Encounter.type[0])

                foreach (var segment in segments)
                {
                    // Check if the segment is an index (e.g., [0])
                    if (Regex.IsMatch(segment, @"^\[\d+\]$"))
                    {
                        if (!int.TryParse(segment.Trim('[', ']'), out int index) || index < 0)
                            return (false, $"Invalid index '{segment}' at '{displayPath}'");

                        // Ensure the previous element supports indexing
                        var prevElement = currentStructure.Snapshot.Element.FirstOrDefault(e => e.Path == currentBasePath);
                        if (prevElement == null)
                            return (false, $"Path '{currentBasePath}' not found for index '{segment}'");

                        if (prevElement.Max != "*" && (prevElement.Max == null || int.Parse(prevElement.Max) <= 1))
                            return (false, $"Element '{currentBasePath}' does not support indexing (max cardinality: {prevElement.Max})");

                        // Update display path but not base path
                        displayPath += segment;
                        continue;
                    }

                    currentBasePath = $"{currentBasePath}.{segment}";
                    displayPath = $"{displayPath}.{segment}";

                    var element = currentStructure.Snapshot.Element.FirstOrDefault(e => e.Path == currentBasePath);
                    if (element == null)
                        return (false, $"Path segment '{segment}' not found at '{currentBasePath}'");

                    // If this is the last segment, we're done
                    if (segment == segments.Last())
                    {
                        return (true, null);
                    }

                    // Resolve the next StructureDefinition for complex types
                    var typeCode = element.Type?.FirstOrDefault()?.Code;
                    if (string.IsNullOrEmpty(typeCode))
                        return (false, $"Element '{segment}' has no type information at '{currentBasePath}'");

                    if (IsPrimitive(typeCode))
                        return (false, $"Element '{segment}' is a primitive type and cannot have child elements at '{currentBasePath}'");

                    var nextStructure = await GetStructureDefinitionAsync(typeCode);
                    if (nextStructure == null || nextStructure.Snapshot == null)
                        return (false, $"StructureDefinition for type '{typeCode}' not found");

                    currentStructure = nextStructure;
                    currentBasePath = typeCode;
                    displayPath = typeCode;
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
            if (_structureDefinitionCache.TryGetValue(resourceTypeName, out var definition))
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

        private static string[] ParseFhirPath(string fhirPath)
        {
            // Split path into segments, preserving indices (e.g., type[0].coding -> type, [0], coding)
            var segments = new List<string>();
            var regex = new Regex(@"(\w+|\[\d+\])");
            var matches = regex.Matches(fhirPath);

            foreach (Match match in matches)
            {
                segments.Add(match.Value);
            }

            return segments.ToArray();
        }
    }
}