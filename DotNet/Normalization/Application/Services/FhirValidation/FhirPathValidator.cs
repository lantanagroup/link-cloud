using Hl7.Fhir.ElementModel;
using Hl7.Fhir.Model;
using Hl7.Fhir.Specification.Snapshot;
using Hl7.Fhir.Specification.Source;
using Hl7.FhirPath;
using Hl7.FhirPath.Expressions;
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
                var sd = await GetStructureDefinitionAsync(resourceTypeName);
                if (sd == null) return (false, "No StructureDefinition Found to Validate FHirPath");

                var resourceType = Type.GetType($"Hl7.Fhir.Model.{resourceTypeName}, Hl7.Fhir.R4", throwOnError: true);
                var dummyResource = (DomainResource)Activator.CreateInstance(resourceType);
                var typed = dummyResource.ToTypedElement();

                var symbolTable = new SymbolTable();
                symbolTable.AddStandardFP();
                var compiler = new FhirPathCompiler(symbolTable);
                var expression = compiler.Compile(fhirPath);
                var result = expression(typed, EvaluationContext.CreateDefault());

                return (true, null);
            }
            catch (Exception ex)
            {
                return (false, ex.Message);
            }
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
