using Google.Protobuf.Collections;
using Hl7.Fhir.ElementModel;
using Hl7.Fhir.Model;
using Hl7.FhirPath;
using LantanaGroup.Link.Normalization.Application.Models.Operations;
using LantanaGroup.Link.Normalization.Application.Operations;
using LantanaGroup.Link.Normalization.Application.Services.FhirPathValidation;
using LantanaGroup.Link.Shared.Application.Services.Security;
using System.Collections;

namespace LantanaGroup.Link.Normalization.Application.Services.Operations
{
    public class CopyLocationOperationService : BaseOperationService<CopyLocationOperation>
    {
        ILogger<CopyLocationOperationService> _logger;

        public CopyLocationOperationService(ILogger<CopyLocationOperationService> logger, TimeSpan? operationTimeout = null)
            : base(logger, operationTimeout)
        {
            _logger = logger;
        }

        protected override async Task<OperationResult> ExecuteOperation(CopyLocationOperation operation, DomainResource resource, CancellationToken cancellationToken = default)
        {
            if (resource is not Location)
            {
                return OperationResult.Failure($"Resource must be a Location");
            }

            _logger.LogDebug("Applying Copy Location Operation (ResourceType: {type}, ResourceId: {resourceId})", resource.TypeName.SanitizeForLog(), resource.Id.SanitizeForLog());

            Location location = (Location)resource;

            if (location.Type == null)
            {
                location.Type = new List<CodeableConcept>();
            }

            var addedCount = 0;

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
                addedCount++;
            }

            return addedCount > 0
                ? OperationResult.Success(location)
                : OperationResult.NoAction("No location identifiers required copying.", location);
        }
    }
}