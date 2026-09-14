using Hl7.Fhir.Model;
using Hl7.Fhir.FhirPath;
using LantanaGroup.Link.Normalization.Application.Models.Operations;
using LantanaGroup.Link.Normalization.Application.Operations;
using LantanaGroup.Link.Shared.Application.Services.Security;
using LantanaGroup.Link.Shared.Application.Utilities;
using Task = System.Threading.Tasks.Task;

namespace LantanaGroup.Link.Normalization.Application.Services.Operations
{
    public class HSLOCMapOperationService : BaseOperationService<HSLOCMapOperation>
    {
        private readonly CodeMapOperationService _codeMapOperationService;
        private readonly ILogger<HSLOCMapOperationService> _logger;

        public static string LocationAliasCodeSystem = "https://nhsnlink.org/location-alias";
        public const int MAX_ITERATIONS = 20; //prevent infinite loops in case of circular references in the partOf hierarchy

        public HSLOCMapOperationService(ILogger<HSLOCMapOperationService> logger,
                                        CodeMapOperationService codeMapOperationService,
                                        TimeSpan? operationTimeout = null)
            : base(logger, operationTimeout)
        {
            _codeMapOperationService = codeMapOperationService;
            _logger = logger;
        }

        protected override async Task<OperationResult> ExecuteOperation(HSLOCMapOperation operation, DomainResource resource, List<DomainResource>? supportingResources = null, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var copyResult = copyIdentifierAndAliasToType(resource, supportingResources, cancellationToken);
            if (copyResult.SuccessCode == OperationStatus.Failure)
            {
                return copyResult;
            }

            var configuredSourceSystems = operation.CodeSystemMaps.Select(map => map.SourceSystem).ToHashSet();
            var unconfiguredOutcomes = resource.Select(operation.FhirPath)
                .SelectMany(source => source switch
                {
                    Coding coding => new[] { coding },
                    CodeableConcept concept => concept.Coding.AsEnumerable(),
                    _ => Enumerable.Empty<Coding>()
                })
                .Where(coding => !string.IsNullOrWhiteSpace(coding.Code) && !configuredSourceSystems.Contains(coding.System))
                .GroupBy(coding => coding.System ?? string.Empty)
                .Select(group => new CodeMappingOutcome(
                    group.Key, string.Empty, 0, group.Count(),
                    group.Select(coding => coding.Code).Distinct(StringComparer.OrdinalIgnoreCase).ToList()))
                .ToList();

            //Now that location.type is normalized, execute the code map.
            var codeMapOperation = new CodeMapOperation(
            operation.Name,
            operation.FhirPath,
            operation.CodeSystemMaps,
            operation.Description);

            var codeMapOperationResult = await _codeMapOperationService.ProcessOperationAsync(
                codeMapOperation,
                resource,
                supportingResources,
                cancellationToken);

            if (codeMapOperationResult.SuccessCode == OperationStatus.Failure)
            {
                return codeMapOperationResult;
            }

            var codeMapping = (codeMapOperationResult.CodeMapping ?? []).Concat(unconfiguredOutcomes).ToList();
            if (copyResult.SuccessCode == OperationStatus.Success && codeMapOperationResult.SuccessCode == OperationStatus.NoAction)
            {
                return OperationResult.Success(resource, codeMapping);
            }

            return new OperationResult(codeMapOperationResult.SuccessCode, codeMapOperationResult.ErrorMessage, resource, codeMapping);
        }

        /// <summary>
        /// Copies the Location.identifier and Location.alias values to Location.type, iteratively moving up the Location.partOf hierarchy. 
        /// This is done to ensure that all relevant codes are available in Location.type for the subsequent code mapping operation.
        /// </summary>
        protected OperationResult copyIdentifierAndAliasToType(DomainResource resource, List<DomainResource>? supportingResources, CancellationToken cancellationToken)
        {
            if (resource is not Location)
            {
                return OperationResult.Failure($"Resource must be a Location");
            }

            Location? location = (Location)resource;
            var originalLocation = location;

            if (location.Type == null)
            {
                location.Type = new List<CodeableConcept>();
            }

            int iterationCount = 0;
            int changes = 0;
            do
            {
                cancellationToken.ThrowIfCancellationRequested();

                //1. copy alias to type
                foreach(var alias in location.Alias)
                {
                    if (string.IsNullOrWhiteSpace(alias))
                    {
                        continue;
                    }

                    changes += AddAliasToType(originalLocation, alias);
                }

                //2. copy identifier to type
                foreach (var identifier in location.Identifier)
                {
                    if (string.IsNullOrWhiteSpace(identifier.System) || string.IsNullOrWhiteSpace(identifier.Value))
                    {
                        continue;
                    }

                    // de-dupe on (system, code)
                    var exists = originalLocation.Type.Any(cc =>
                    cc.Coding.Any(cd =>
                    string.Equals(cd.System, identifier.System, StringComparison.Ordinal) &&
                    string.Equals(cd.Code, identifier.Value, StringComparison.Ordinal)));

                    if (exists)
                        continue;

                    CodeableConcept codeableConcept = new(identifier.System, identifier.Value);
                    originalLocation.Type.Add(codeableConcept);
                    changes++;
                }

                //3. move up the partOf hierarchy
                var parentReference = location.PartOf?.Reference?.SplitReference();
                var parentLocation = string.IsNullOrWhiteSpace(parentReference)
                    ? null
                    : supportingResources?.FirstOrDefault(r => r is Location && r.Id == parentReference);
                if (parentLocation is Location parentLoc)
                {
                    location = parentLoc;
                }
                else
                {
                    if(!string.IsNullOrWhiteSpace(parentReference))
                    {
                        _logger.LogWarning("Parent location with reference {Reference} not found in supporting resources for Location {ResourceId}.", location.PartOf?.Reference.SanitizeForLog(), location.Id.SanitizeForLog());
                    }
                    location = null;
                }
                iterationCount++;
                if(iterationCount >= MAX_ITERATIONS && location != null)
                {
                    _logger.LogWarning("Maximum iteration count of {MaxIterations} reached while processing HSLOCMapOperation for Location {ResourceId}.", MAX_ITERATIONS, resource.Id.SanitizeForLog());
                }
            } while(location != null && iterationCount < MAX_ITERATIONS);

            if(changes > 0)
            {
                return OperationResult.Success(resource);
            }
            else
            {
                return OperationResult.NoAction("No alias or identifier values were copied to Location.Type.", resource);
            }
        }

        protected int AddAliasToType(Location location, string alias)
        {
            var trimmedAlias = alias.Trim();

            // de-dupe on (system, code)
            var exists = location.Type.Any(cc =>
                cc.Coding.Any(cd =>
                    string.Equals(cd.System, LocationAliasCodeSystem, StringComparison.Ordinal) &&
                    string.Equals(cd.Code, trimmedAlias, StringComparison.Ordinal)));

            if (exists)
                return 0;

            CodeableConcept codeableConcept = new(LocationAliasCodeSystem, trimmedAlias);
            location.Type.Add(codeableConcept);
            return 1;
        }
    }
}