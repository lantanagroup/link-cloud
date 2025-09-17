using Hl7.Fhir.ElementModel;
using Hl7.Fhir.Model;
using Hl7.FhirPath;
using LantanaGroup.Link.Normalization.Application.Models.Operations;
using LantanaGroup.Link.Normalization.Application.Operations;
using LantanaGroup.Link.Normalization.Application.Services.FhirPathValidation;
using System.Collections;

namespace LantanaGroup.Link.Normalization.Application.Services.Operations
{
    public class CopyLocationOperationService : BaseOperationService<CopyLocationOperation>
    {
        public CopyLocationOperationService(ILogger<CopyLocationOperationService> logger, TimeSpan? operationTimeout = null)
            : base(logger, operationTimeout)
        {
        }

        protected override async Task<OperationResult> ExecuteOperation(CopyLocationOperation operation, DomainResource resource)
        {
            if (resource is not Location) {
                return OperationResult.Failure($"Resource must be a Location");
            }

            if (((Location)resource).Type == null)
            {
                ((Location)resource).Type = new List<CodeableConcept>();
            }

            foreach (var identifier in ((Location)resource).Identifier) {
                CodeableConcept codeableConcept = new(identifier.System, identifier.Value);
                ((Location)resource).Type.Add(codeableConcept);
            }

            return OperationResult.Success(resource);
        }
    }
}