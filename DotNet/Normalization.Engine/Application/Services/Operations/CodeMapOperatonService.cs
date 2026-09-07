using Hl7.Fhir.Model;
using Hl7.Fhir.FhirPath;
using LantanaGroup.Link.Normalization.Application.Models.Operations;
using LantanaGroup.Link.Normalization.Application.Operations;
using LantanaGroup.Link.Normalization.Application.Services.FhirPathValidation;
using LantanaGroup.Link.Shared.Application.Services.Security;

namespace LantanaGroup.Link.Normalization.Application.Services.Operations
{
    public class CodeMapOperationService : BaseOperationService<CodeMapOperation>
    {
        ILogger<CodeMapOperationService> _logger;

        public CodeMapOperationService(ILogger<CodeMapOperationService> logger, TimeSpan? operationTimeout = null)
            : base(logger, operationTimeout)
        {
            _logger = logger;
        }

        protected override async Task<OperationResult> ExecuteOperation(CodeMapOperation operation, DomainResource resource, List<DomainResource>? supportingResources = null, CancellationToken cancellationToken = default)
        {
            //Daniel - 4/2026: I don't think we need this per execution. We should probably move a check like this in the Rest API and validate that it's a valid path.             
            //var result = await FhirPathValidator.IsFhirPathValidForResourceType(operation.FhirPath, resource.TypeName);

            //if (!result.IsValid)
            //    return OperationResult.Failure($"Invalid target FHIRPath expression: {operation.FhirPath}. {result.ErrorMessage}", resource);

            var sources = resource.Select(operation.FhirPath);

            if (sources == null || !sources.Any())
            {
                return OperationResult.NoAction($"Nothing found at {operation.FhirPath}", resource);
            }

            var anyUpdated = false;

            // One tally per CodeSystemMap this resource actually exercised. Allocated once per operation
            // execution rather than per coding: this runs inside the per-resource operation-sequence loop,
            // which executes hundreds of thousands of times in a single report.
            var tallies = new Dictionary<CodeSystemMap, Tally>();

            foreach (var source in sources)
            {
                if (source is Coding coding)
                {
                    if (UpdateCoding(coding, operation.CodeSystemMaps, operation.Name, tallies))
                    {
                        anyUpdated = true;
                    }
                }
                else if (source is CodeableConcept codeableConcept)
                {
                    foreach (var cdng in codeableConcept.Coding)
                    {
                        if (UpdateCoding(cdng, operation.CodeSystemMaps, operation.Name, tallies))
                        {
                            anyUpdated = true;
                        }
                    }
                }
                else
                {
                    Logger.LogWarning("Unsupported source type {SourceType} for FHIRPath {FhirPath} in operation {OperationName}.", source.GetType().Name.SanitizeForLog(), operation.FhirPath.SanitizeForLog(), operation.Name.SanitizeForLog());
                }
            }

            var codeMapping = BuildOutcomes(tallies);

            if (anyUpdated)
            {
                return OperationResult.Success(resource, codeMapping);
            }
            else
                return OperationResult.NoAction("No code maps applied.", resource, codeMapping);
        }

        private bool UpdateCoding(Coding coding, List<CodeSystemMap> codeSystemMaps, string operationName, Dictionary<CodeSystemMap, Tally> tallies)
        {
            var updated = false;

            // The Where is evaluated lazily against coding.System, which the loop body rewrites on a match.
            // Left as-is to preserve existing behavior; the tallies are attributed by the map's own
            // configured systems rather than the coding's, so they do not inherit that dependency.
            foreach (var codeSystemMap in codeSystemMaps.Where(x => x.SourceSystem == coding.System))
            {
                if (codeSystemMap == null)
                    continue;

                var tally = GetOrCreateTally(tallies, codeSystemMap);

                if (codeSystemMap.CodeMaps.TryGetValue(coding.Code, out var matchingCodeMap))
                {
                    coding.System = codeSystemMap.TargetSystem;
                    coding.Code = matchingCodeMap.Code;
                    coding.Display = matchingCodeMap.Display;
                    updated = true;
                    tally.MappedCount++;
                }
                else
                {
                    tally.UnmappedCount++;

                    // Recorded before any rewrite could reach it, so this is the code as the EHR sent it.
                    tally.UnmappedCodes.Add(coding.Code);

                    _logger.LogWarning("No code map found for code {Code}|{System} in mapping {OperationName}", coding.Code.SanitizeForLog(), coding.System.SanitizeForLog(), operationName.SanitizeForLog());
                }
            }

            return updated;
        }

        private static Tally GetOrCreateTally(Dictionary<CodeSystemMap, Tally> tallies, CodeSystemMap codeSystemMap)
        {
            if (!tallies.TryGetValue(codeSystemMap, out var tally))
            {
                tally = new Tally();
                tallies[codeSystemMap] = tally;
            }

            return tally;
        }

        private static List<CodeMappingOutcome> BuildOutcomes(Dictionary<CodeSystemMap, Tally> tallies) =>
            tallies
                .Select(entry => new CodeMappingOutcome(
                    entry.Key.SourceSystem,
                    entry.Key.TargetSystem,
                    entry.Value.MappedCount,
                    entry.Value.UnmappedCount,
                    entry.Value.UnmappedCodes.ToList()))
                .ToList();

        /// <summary>
        /// Mutable counts for one code map while a resource is being processed, projected into a
        /// <see cref="CodeMappingOutcome"/> once the resource is done.
        /// </summary>
        private sealed class Tally
        {
            public int MappedCount { get; set; }
            public int UnmappedCount { get; set; }

            /// <summary>
            /// Distinct unmapped codes. A resource repeating the same missing code says nothing new about
            /// what the facility needs to configure, and <see cref="UnmappedCount"/> keeps the true total.
            /// </summary>
            public HashSet<string> UnmappedCodes { get; } = new(StringComparer.OrdinalIgnoreCase);
        }
    }
}