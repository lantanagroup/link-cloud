using Hl7.Fhir.Model;
using Hl7.Fhir.FhirPath;
using LantanaGroup.Link.Normalization.Application.Models.Operations;
using LantanaGroup.Link.Normalization.Application.Operations;

namespace LantanaGroup.Link.Normalization.Application.Services.Operations
{
    public class CodeMapOperationService : BaseOperationService<CodeMapOperation>
    {
        public CodeMapOperationService(ILogger<CodeMapOperationService> logger, TimeSpan? operationTimeout = null)
            : base(logger, operationTimeout)
        {
        }

        protected override DomainResource ExecuteOperation(CodeMapOperation operation, DomainResource resource)
        {
            if (!OperationServiceHelper.ValidateFhirPath(operation.FhirPath, out var validationError, Logger))
            {
                Logger.LogWarning("Invalid FHIRPath {FhirPath} for operation {OperationName}: {ErrorMessage}", operation.FhirPath, operation.Name, validationError);
                return resource;
            }

            var source = resource.Select(operation.FhirPath).FirstOrDefault();
            if (source == null)
                return resource;

            if (source is Coding coding)
            {
                UpdateCoding(coding, operation.CodeSystemMaps);
            }
            else if (source is CodeableConcept codeableConcept)
            {
                foreach (var cdng in codeableConcept.Coding)
                    UpdateCoding(cdng, operation.CodeSystemMaps);
            }
            else
            {
                Logger.LogWarning("Unsupported source type {SourceType} for FHIRPath {FhirPath} in operation {OperationName}.", source.GetType().Name, operation.FhirPath, operation.Name);
            }

            return resource;
        }

        private void UpdateCoding(Coding coding, List<CodeSystemMap> codeSystemMaps)
        {
            var codeSystemMap = codeSystemMaps.FirstOrDefault(x => x.SourceSystem == coding.System);
            if (codeSystemMap == null)
                return;

            if (codeSystemMap.CodeMaps.TryGetValue(coding.Code, out var matchingCodeMap))
            {
                coding.System = codeSystemMap.TargetSystem;
                coding.Code = matchingCodeMap.Code;
                coding.Display = matchingCodeMap.Display;
            }
        }
    }
}