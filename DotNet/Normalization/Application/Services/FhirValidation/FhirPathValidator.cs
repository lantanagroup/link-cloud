using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Collections.Concurrent;
using System.Collections.Generic;
using Hl7.Fhir.Model;
using Hl7.Fhir.Specification.Snapshot;
using Hl7.Fhir.Specification.Source;

namespace LantanaGroup.Link.Normalization.Application.Services.FhirPathValidation
{
    public static class FhirPathValidator
    {
        private static readonly ConcurrentDictionary<string, StructureDefinition> _structureDefinitionCache = new();
        private static readonly string _structureDefinitionsPath = Path.Combine(AppContext.BaseDirectory, "Application", "Services", "FhirValidation", "StructureDefinitions");
        private static readonly CachedResolver _resolver = new CachedResolver(new DirectorySource(_structureDefinitionsPath));
        private static readonly string _logFilePath = Path.Combine(AppContext.BaseDirectory, "FhirPathValidator.log");

        /// <summary>
        /// Logs a message to a file for debugging in environments like Test Explorer.
        /// </summary>
        private static void LogToFile(string message)
        {
            try
            {
                File.AppendAllText(_logFilePath, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} - {message}{Environment.NewLine}");
            }
            catch
            {
                // Ignore logging errors to avoid affecting validation
            }
        }

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

                // Log all snapshot element paths for debugging
                var elementPaths = structureDefinition.Snapshot.Element.Select(e => e.Path).ToList();
                LogToFile($"Snapshot element paths for '{resourceTypeName}': {string.Join(", ", elementPaths)}");

                // Parse the FHIR path
                var segments = ParseFhirPath(fhirPath);
                LogToFile($"Parsed segments: {string.Join(", ", segments)}");

                var currentStructure = structureDefinition;
                var currentBasePath = resourceTypeName; // Path for Element.Path matching (e.g., Observation.value[x])
                var displayPath = resourceTypeName; // Path for error messages (e.g., Observation.valueQuantity)

                for (int i = 0; i < segments.Length; i++)
                {
                    var segment = segments[i];
                    LogToFile($"Processing segment: '{segment}', basePath: '{currentBasePath}', displayPath: '{displayPath}'");

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
                        LogToFile($"Index validated, new displayPath: '{displayPath}'");
                        continue;
                    }

                    // Handle choice types first (e.g., valueQuantity -> value[x])
                    string elementPath = null;
                    ElementDefinition element = null;
                    string choiceType = null;

                    // Try choice path (e.g., Observation.value[x])
                    var validTypes = new List<string>();
                    string choicePath = null;
                    // Find potential choice paths by checking if segment contains any valid type
                    var snapshotElements = currentStructure.Snapshot.Element;
                    foreach (var elem in snapshotElements.Where(e => e.Path.StartsWith(currentBasePath) && e.Path.EndsWith("[x]")))
                    {
                        var baseName = elem.Path.Substring(0, elem.Path.Length - 3); // Remove [x]
                        var types = elem.Type?.Select(t => t.Code).ToList() ?? new List<string>();
                        foreach (var type in types)
                        {
                            if (segment.Equals(baseName.Replace(currentBasePath + ".", "") + type, StringComparison.OrdinalIgnoreCase))
                            {
                                choicePath = elem.Path;
                                validTypes = types;
                                element = elem;
                                choiceType = type;
                                break;
                            }
                        }
                        if (element != null) break;
                    }

                    if (element != null)
                    {
                        elementPath = choicePath;
                        LogToFile($"Checking choice types for '{choicePath}': {string.Join(", ", validTypes)}");
                        LogToFile($"Matched choice type: '{choiceType}' for segment '{segment}'");
                    }
                    else
                    {
                        LogToFile($"Choice path for segment '{segment}' not found in snapshot");
                    }

                    // Try exact path if choice path not matched
                    if (element == null)
                    {
                        elementPath = currentBasePath == resourceTypeName ? $"{currentBasePath}.{segment}" : $"{currentBasePath}.{segment}";
                        LogToFile($"Trying exact path: '{elementPath}'");
                        element = currentStructure.Snapshot.Element.FirstOrDefault(e => string.Equals(e.Path, elementPath, StringComparison.OrdinalIgnoreCase));
                    }

                    if (element == null)
                        return (false, $"Path segment '{segment}' not found at '{elementPath}'");

                    LogToFile($"Found element with path: '{elementPath}'");

                    // Update paths
                    currentBasePath = elementPath;
                    displayPath = displayPath == resourceTypeName ? $"{displayPath}.{segment}" : $"{displayPath}.{segment}";

                    // If this is the last segment, we're done
                    if (i == segments.Length - 1)
                    {
                        LogToFile("Reached last segment, path is valid");
                        return (true, null);
                    }

                    // Resolve the next StructureDefinition for complex types
                    var typeCodes = element.Type?.Select(t => t.Code).ToList() ?? new List<string>();
                    if (!typeCodes.Any())
                        return (false, $"Element '{segment}' has no type information at '{currentBasePath}'");

                    // Check if the element is a primitive list (e.g., HumanName.given)
                    if (typeCodes.All(IsPrimitive) && element.Max == "*")
                    {
                        // Allow indexing into primitive lists, no further children
                        if (segments.Skip(i + 1).Any(s => !Regex.IsMatch(s, @"^\[\d+\]$")))
                            return (false, $"Element '{segment}' is a primitive list and cannot have non-index child elements at '{currentBasePath}'");
                        LogToFile($"Primitive list detected: '{currentBasePath}'");
                        continue;
                    }

                    if (typeCodes.All(IsPrimitive))
                        return (false, $"Element '{segment}' is a primitive type and cannot have child elements at '{currentBasePath}'");

                    // Handle choice type for the next segment
                    string typeCode = choiceType ?? typeCodes.FirstOrDefault(t => !t.EndsWith("[x]", StringComparison.OrdinalIgnoreCase)) ?? typeCodes.First();

                    // Prefer Quantity for dose[x] if next segment is 'value'
                    if (elementPath.EndsWith("[x]") && i + 1 < segments.Length && segments[i + 1] == "value")
                    {
                        if (typeCodes.Contains("Quantity"))
                            typeCode = "Quantity"; // Prefer Quantity for .value
                        LogToFile($"Preferring 'Quantity' for choice type due to next segment 'value'");
                    }

                    if (typeCode != "BackboneElement" && typeCode != "Element")
                    {
                        var nextStructure = await GetStructureDefinitionAsync(typeCode);
                        if (nextStructure == null || nextStructure.Snapshot == null)
                            return (false, $"StructureDefinition for type '{typeCode}' not found");

                        currentStructure = nextStructure;
                        currentBasePath = typeCode;
                        displayPath = typeCode;
                        LogToFile($"Moving to new structure: '{typeCode}'");
                    }
                    else
                    {
                        LogToFile($"Staying with current structure for {typeCode}: '{currentBasePath}'");
                    }
                }

                return (true, null);
            }
            catch (Exception ex)
            {
                LogToFile($"Exception during validation: {ex.Message}");
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
                LogToFile($"Cache hit for '{resourceTypeName}'");
                return definition;
            }

            LogToFile($"Fetching StructureDefinition for '{resourceTypeName}'");
            definition = await _resolver.FindStructureDefinitionAsync($"http://hl7.org/fhir/StructureDefinition/{resourceTypeName}");
            if (definition != null && !definition.HasSnapshot)
            {
                LogToFile($"Generating snapshot for '{resourceTypeName}'");
                try
                {
                    var generator = new SnapshotGenerator(_resolver);
                    await generator.UpdateAsync(definition);
                }
                catch (Exception ex)
                {
                    LogToFile($"Snapshot generation failed for '{resourceTypeName}': {ex.Message}");
                    throw;
                }
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
            // Split path into segments, preserving indices (e.g., valueQuantity.value -> valueQuantity, value)
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