using Hl7.Fhir.Model;
using Hl7.Fhir.FhirPath;
using LantanaGroup.Link.Normalization.Application.Models.Operations;
using LantanaGroup.Link.Normalization.Application.Operations;
using LantanaGroup.Link.Normalization.Application.Services.FhirPathValidation;

namespace LantanaGroup.Link.Normalization.Application.Services.Operations
{
    public class CodeMapOperationService : BaseOperationService<CodeMapOperation>
    {
        public CodeMapOperationService(ILogger<CodeMapOperationService> logger, TimeSpan? operationTimeout = null)
            : base(logger, operationTimeout)
        {
        }

        protected override async Task<OperationResult> ExecuteOperation(CodeMapOperation operation, DomainResource resource)
        {
            var result = await FhirPathValidator.IsFhirPathValidForResourceType(operation.FhirPath, resource.TypeName);

            if (!result.IsValid)
                return OperationResult.Failure($"Invalid target FHIRPath expression: {operation.FhirPath}. {result.ErrorMessage}", resource);

            var sources = resource.Select(operation.FhirPath);

            if(sources == null || !sources.Any())
            {
                return OperationResult.NoAction($"Nothing found at {operation.FhirPath}", resource);
            }

            var anyUpdated = false;

            foreach (var source in sources)
            {
                if (source is Coding coding)
                {
                    if (UpdateCoding(coding, operation.CodeSystemMaps))
                    {
                        anyUpdated = true;
                    }
                }
                else if (source is CodeableConcept codeableConcept)
                {
                    foreach (var cdng in codeableConcept.Coding)
                    {
                        if (UpdateCoding(cdng, operation.CodeSystemMaps))
                        {
                            anyUpdated = true;
                        }
                    }
                }
                else
                {
                    Logger.LogWarning("Unsupported source type {SourceType} for FHIRPath {FhirPath} in operation {OperationName}.", source.GetType().Name, operation.FhirPath, operation.Name);
                }
            }

            if(anyUpdated)
                return OperationResult.Success(resource);
            else
                return OperationResult.NoAction("No code maps applied.", resource);
        }

        private bool UpdateCoding(Coding coding, List<CodeSystemMap> codeSystemMaps)
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
            }

            return updated;
        }
    }
}