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
        /// Validates if a FHIRPath is valid for a given resource type using its StructureDefinition.
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
                if (structureDefinition?.Snapshot == null)
                    return (false, $"StructureDefinition or snapshot not found for {resourceTypeName}");

                var segments = ParseFhirPath(fhirPath);
                return await ValidatePath(segments, resourceTypeName, structureDefinition);
            }
            catch (Exception ex)
            {
                return (false, $"Exception during validation: {ex.Message}");
            }
        }

        private static async Task<(bool IsValid, string? ErrorMessage)> ValidatePath(string[] segments, string resourceTypeName, StructureDefinition structureDefinition)
        {
            var currentStructure = structureDefinition;
            var currentBasePath = resourceTypeName; // Tracks the current path in the StructureDefinition
            var displayPath = resourceTypeName; // Tracks the user-provided FHIRPath for error messages

            for (int i = 0; i < segments.Length; i++)
            {
                var segment = segments[i];

                // Handle index segments (e.g., [0])
                if (Regex.IsMatch(segment, @"^\[\d+\]$"))
                {
                    if (!TryValidateIndex(segment, currentStructure, currentBasePath, out var error))
                        return (false, error);

                    displayPath += segment;
                    continue;
                }

                // Find the element for the current segment
                var (element, elementPath, choiceType) = FindElement(segment, currentStructure, currentBasePath, i < segments.Length - 1 ? segments[i + 1] : null);
                if (element == null)
                    return (false, $"Path segment '{segment}' not found at '{displayPath}.{segment}'");

                // Update paths
                currentBasePath = elementPath;
                displayPath = displayPath == resourceTypeName ? $"{displayPath}.{segment}" : $"{displayPath}.{segment}";

                // If last segment, path is valid
                if (i == segments.Length - 1)
                    return (true, null);

                // Validate element types and prepare for next segment
                var typeCodes = element.Type?.Select(t => t.Code).ToList() ?? new List<string>();
                if (!typeCodes.Any())
                    return (false, $"Element '{segment}' has no type information at '{currentBasePath}'");

                // Handle primitive lists (e.g., HumanName.given)
                if (typeCodes.All(IsPrimitive) && element.Max == "*")
                {
                    if (segments.Skip(i + 1).Any(s => !Regex.IsMatch(s, @"^\[\d+\]$")))
                        return (false, $"Element '{segment}' is a primitive list and cannot have non-index child elements at '{currentBasePath}'");
                    continue;
                }

                // Handle primitive types
                if (typeCodes.All(IsPrimitive))
                    return (false, $"Element '{segment}' is a primitive type and cannot have child elements at '{currentBasePath}'");

                // Select type for next StructureDefinition
                string typeCode = choiceType ?? typeCodes.FirstOrDefault(t => !t.EndsWith("[x]", StringComparison.OrdinalIgnoreCase)) ?? typeCodes.First();

                // Prefer Quantity for dose[x] if next segment is 'value'
                if (elementPath.EndsWith("[x]") && i + 1 < segments.Length && segments[i + 1] == "value" && typeCodes.Contains("Quantity"))
                    typeCode = "Quantity";

                // Move to next StructureDefinition for non-BackboneElement/Element types
                if (typeCode != "BackboneElement" && typeCode != "Element")
                {
                    currentStructure = await GetStructureDefinitionAsync(typeCode);
                    if (currentStructure?.Snapshot == null)
                        return (false, $"StructureDefinition for type '{typeCode}' not found");

                    currentBasePath = typeCode;
                    displayPath = typeCode;
                }
            }

            return (true, null);
        }

        private static (ElementDefinition? Element, string? Path, string? ChoiceType) FindElement(string segment, StructureDefinition structure, string basePath, string? nextSegment)
        {
            // Try exact path first
            var elementPath = basePath == structure.Type ? $"{basePath}.{segment}" : $"{basePath}.{segment}";
            var element = structure.Snapshot.Element.FirstOrDefault(e => string.Equals(e.Path, elementPath, StringComparison.OrdinalIgnoreCase));
            if (element != null)
                return (element, elementPath, null);

            // Try choice path (e.g., value[x], dose[x])
            foreach (var elem in structure.Snapshot.Element.Where(e => e.Path.StartsWith(basePath) && e.Path.EndsWith("[x]")))
            {
                var baseName = elem.Path.Substring(basePath.Length + 1).Replace("[x]", "");
                var types = elem.Type?.Select(t => t.Code).ToList() ?? new List<string>();

                foreach (var type in types)
                {
                    // Match segment to baseName (e.g., 'dose') or baseName + type (e.g., 'doseQuantity')
                    if (segment.Equals(baseName, StringComparison.OrdinalIgnoreCase) ||
                        segment.Equals(baseName + type, StringComparison.OrdinalIgnoreCase))
                    {
                        return (elem, elem.Path, type);
                    }
                }
            }

            return (null, elementPath, null);
        }

        private static bool TryValidateIndex(string segment, StructureDefinition structure, string basePath, out string? error)
        {
            error = null;
            if (!int.TryParse(segment.Trim('[', ']'), out var index) || index < 0)
            {
                error = $"Invalid index '{segment}' at '{basePath}'";
                return false;
            }

            var element = structure.Snapshot.Element.FirstOrDefault(e => e.Path == basePath);
            if (element == null)
            {
                error = $"Path '{basePath}' not found for index '{segment}'";
                return false;
            }

            if (element.Max != "*" && (element.Max == null || int.Parse(element.Max) <= 1))
            {
                error = $"Element '{basePath}' does not support indexing (max cardinality: {element.Max})";
                return false;
            }

            return true;
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
                return definition;

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