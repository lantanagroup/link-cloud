using Google.Protobuf.Collections;
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
            if (resource is not Location) 
            {
                return OperationResult.Failure($"Resource must be a Location");
            }

            Location location = (Location)resource;

            if (location.Type == null)
            {
                location.Type = new List<CodeableConcept>();
            }

            foreach (var identifier in location.Identifier) 
            {
                if (string.IsNullOrWhiteSpace(identifier.System) && string.IsNullOrWhiteSpace(identifier.Value)) 
                {
                    continue;
                }

                // de-dupe on (system, code)
                var exists = location.Type.Any(cc =>
                cc.Coding.Any(cd =>
                string.Equals(cd.System, identifier.System, StringComparison.Ordinal) &&
                string.Equals(cd.Code, identifier.Value, StringComparison.Ordinal)));
                
                if (exists) 
                    continue;

                CodeableConcept codeableConcept = new(identifier.System, identifier.Value);
                location.Type.Add(codeableConcept);
            }

            return OperationResult.Success(location);
        }
    }
}