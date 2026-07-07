using Google.Protobuf.Collections;
using Grpc.Core;
using Hl7.Fhir.ElementModel;
using Hl7.Fhir.Model;
using Hl7.FhirPath;
using LantanaGroup.Link.Normalization.Application.Models.Operations;
using LantanaGroup.Link.Normalization.Application.Operations;
using LantanaGroup.Link.Normalization.Application.Services.FhirPathValidation;
using LantanaGroup.Link.Shared.Application.Services.Security;
using System.Collections;
using Task = System.Threading.Tasks.Task;

namespace LantanaGroup.Link.Normalization.Application.Services.Operations
{
    public class CopyLocationAliasToTypeIterativelyOperationService : BaseOperationService<CopyLocationAliasToTypeIterativelyOperation>
    {
        ILogger<CopyLocationAliasToTypeIterativelyOperationService> _logger;
        public static string LocationAliasCodeSystem = "https://nhsnlink.org/location-alias";

        public CopyLocationAliasToTypeIterativelyOperationService(ILogger<CopyLocationAliasToTypeIterativelyOperationService> logger, TimeSpan? operationTimeout = null)
            : base(logger, operationTimeout)
        {
            _logger = logger;
        }

        protected override async Task<OperationResult> ExecuteOperation(CopyLocationAliasToTypeIterativelyOperation operation, DomainResource resource, List<DomainResource>? supportingResources = null, CancellationToken cancellationToken = default)
        {
            if (resource is not Location)
            {
                return OperationResult.Failure($"Resource must be a Location");
            }

            _logger.LogDebug("Applying Copy Location Alias to Type Iteratively Operation (ResourceType: {type}, ResourceId: {resourceId})", resource.TypeName.SanitizeForLog(), resource.Id.SanitizeForLog());

            Location? location = (Location)resource;

            if (location.Type == null)
            {
                location.Type = new List<CodeableConcept>();
            }

            var maxIterations = operation.MaxIterations; //prevent infinite loops in case of circular references in the partOf hierarchy
            if (maxIterations <= 0)
            {
                return OperationResult.Failure("MaxIterations must be greater than zero.");
            }

            int iterationCount = 0;
            do
            {
                foreach(var alias in location.Alias)
                {
                    if (string.IsNullOrWhiteSpace(alias))
                    {
                        continue;
                    }

                    if (operation.SplitOnComma)
                    {
                        var aliases = alias.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                        foreach (var a in aliases)
                        {
                            AddAliasToType(location, a);
                        }
                    }
                    else
                    {
                        AddAliasToType(location, alias);
                    }
                }

                var parentLocation = supportingResources?.FirstOrDefault(r => r is Location && r.Id == location.PartOf?.Reference);
                if (parentLocation is Location parentLoc)
                {
                    location = parentLoc;
                }
                else
                {
                    if(location.PartOf != null)
                    {
                        _logger.LogWarning("Parent location with reference {Reference} not found in supporting resources for Location {ResourceId}.", location.PartOf.Reference.SanitizeForLog(), location.Id.SanitizeForLog());
                    }
                    location = null;
                }
                iterationCount++;
                if(iterationCount >= maxIterations && location != null)
                {
                    _logger.LogWarning("Maximum iteration count of {MaxIterations} reached while processing CopyLocationAliasToTypeIterativelyOperation for Location {ResourceId}.", maxIterations, resource.Id.SanitizeForLog());
                }
            } while(location != null && iterationCount < maxIterations);

            return OperationResult.Success(resource);
        }

        protected void AddAliasToType(Location location, string alias)
        {
            var trimmedAlias = alias.Trim();

            // de-dupe on (system, code)
            var exists = location.Type.Any(cc =>
                cc.Coding.Any(cd =>
                    string.Equals(cd.System, LocationAliasCodeSystem, StringComparison.Ordinal) &&
                    string.Equals(cd.Code, trimmedAlias, StringComparison.Ordinal)));

            if (exists)
                return;

            CodeableConcept codeableConcept = new(LocationAliasCodeSystem, trimmedAlias);
            location.Type.Add(codeableConcept);
        }
    }
}
