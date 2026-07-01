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

        protected override async Task<OperationResult> ExecuteOperation(CodeMapOperation operation, DomainResource resource, CancellationToken cancellationToken = default)
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

            foreach (var source in sources)
            {
                if (source is Coding coding)
                {
                    if (UpdateCoding(coding, operation.CodeSystemMaps, operation.Name))
                    {
                        anyUpdated = true;
                    }
                }
                else if (source is CodeableConcept codeableConcept)
                {
                    foreach (var cdng in codeableConcept.Coding)
                    {
                        if (UpdateCoding(cdng, operation.CodeSystemMaps, operation.Name))
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

            if (anyUpdated)
            {
                return OperationResult.Success(resource);
            }
            else
                return OperationResult.NoAction("No code maps applied.", resource);
        }

        private bool UpdateCoding(Coding coding, List<CodeSystemMap> codeSystemMaps, string operationName)
        {
            var updated = false;
            foreach (var codeSystemMap in codeSystemMaps.Where(x => x.SourceSystem == coding.System))
            {
                if (codeSystemMap == null)
                    continue;

                if (codeSystemMap.CodeMaps.TryGetValue(coding.Code, out var matchingCodeMap))
                {
                    coding.System = codeSystemMap.TargetSystem;
                    coding.Code = matchingCodeMap.Code;
                    coding.Display = matchingCodeMap.Display;
                    updated = true;
                }
                else
                {
                    _logger.LogWarning("No code map found for code {Code}|{System} in mapping {OperationName}", coding.Code.SanitizeForLog(), coding.System.SanitizeForLog(), operationName.SanitizeForLog());
                }
            }

            return updated;
        }
    }
}