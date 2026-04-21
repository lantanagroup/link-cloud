using Confluent.Kafka;
using DataAcquisition.Domain.Application.Models;
using Hl7.Fhir.Model;
using Hl7.Fhir.Rest;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Managers;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Api.Requests;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Factory.ReferenceQuery;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Queries;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Serializers;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models.QueryConfig;
using LantanaGroup.Link.DataAcquisition.Domain.Models;
using LantanaGroup.Link.Shared.Application.Utilities;
using Microsoft.Extensions.Logging;
using Task = System.Threading.Tasks.Task;

namespace LantanaGroup.Link.DataAcquisition.Domain.Application.Services;

public interface IReferenceResourceService
{
    Task<List<Resource>> FetchReferenceResources(
        ReferenceQueryFactoryResult referenceQueryFactoryResult,
        GetPatientDataRequest request,
        FhirQueryConfigurationModel fhirQueryConfiguration,
        ReferenceQueryConfig referenceQueryConfig,
        string queryPlanType,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Stages discovered reference resource ids from a primary-phase acquisition into the
    /// <c>PendingReferenceIds</c> table. No FHIR reads, cache lookups, junction writes,
    /// or Kafka publishes happen here — those are deferred until the correlation's
    /// Initial phase finishes, at which point the promoter drains the staging table into
    /// one referential <see cref="Infrastructure.Entities.DataAcquisitionLog"/> per
    /// resource type and the normal acquisition pipeline executes them (batched
    /// Search/SearchPost per the query plan).
    /// </summary>
    Task ProcessReferences(
        DataAcquisitionLogModel log,
        List<ResourceReference> refResources,
        FhirQueryConfigurationModel fhirQueryConfiguration,
        CancellationToken cancellationToken = default);
}

public class ReferenceResourceService : IReferenceResourceService
{
    private readonly ILogger<ReferenceResourceService> _logger;
    private readonly IReferenceResourcesQueries _referenceResourcesQueries;
    private readonly IReferenceResourcesManager _referenceResourcesManager;

    public ReferenceResourceService(
        ILogger<ReferenceResourceService> logger,
        IReferenceResourcesQueries referenceResourcesQueries,
        IReferenceResourcesManager referenceResourcesManager)
    {
        _logger = logger;
        _referenceResourcesQueries = referenceResourcesQueries;
        _referenceResourcesManager = referenceResourcesManager;
    }

    public async Task<List<Resource>> FetchReferenceResources(ReferenceQueryFactoryResult referenceQueryFactoryResult, GetPatientDataRequest request, FhirQueryConfigurationModel fhirQueryConfiguration, ReferenceQueryConfig referenceQueryConfig, string queryPlanType, CancellationToken cancellationToken = default)
    {
        var resources = new List<Resource>();
        if (referenceQueryFactoryResult.ReferenceIds?.Count == 0)
        {
            return resources;
        }

        var validReferenceResources =
            referenceQueryFactoryResult
            ?.ReferenceIds
            ?.Where(x => x.TypeName == referenceQueryConfig.ResourceType || x.Reference.StartsWith(referenceQueryConfig.ResourceType, StringComparison.InvariantCultureIgnoreCase))
            .ToList();

        var referenceIds = validReferenceResources.Select(x => x.Reference.SplitReference()).ToList();
        var existingReferenceResources = (await _referenceResourcesQueries.SearchAsync(new SearchReferenceResourcesModel
        {
            FacilityId = request.FacilityId,
            ResourceIds = referenceIds,
            PageSize = int.MaxValue
        })).Records;


        resources.AddRange(existingReferenceResources.Select(x => FhirResourceDeserializer.DeserializeFhirResource(x)));

        List<ResourceReference> missingReferences = validReferenceResources
            .Where(x => !existingReferenceResources.Any(y => y.ResourceId == x.Reference.SplitReference())).ToList();

        foreach (var x in missingReferences)
        {
            var fullMissingResources = new List<Resource>();
            resources.AddRange(fullMissingResources);
        }

        return resources;
    }

    public async Task ProcessReferences(
        DataAcquisitionLogModel log,
        List<ResourceReference> refResources,
        FhirQueryConfigurationModel fhirQueryConfiguration,
        CancellationToken cancellationToken = default)
    {
        if (refResources == null || refResources.Count == 0)
            return;

        if (log == null)
            throw new ArgumentNullException(nameof(log), "Data acquisition log cannot be null.");
        if (string.IsNullOrWhiteSpace(log.CorrelationId))
            throw new ArgumentException("Data acquisition log is missing a CorrelationId.", nameof(log));
        if (string.IsNullOrWhiteSpace(log.FacilityId))
            throw new ArgumentException("Data acquisition log is missing a FacilityId.", nameof(log));

        // Extract (ResourceType, ResourceId) tuples from the discovered references and
        // stage them. Invalid / unparseable references are silently skipped — the FHIR
        // bundle extractor is the gatekeeper for which reference types are eligible.
        var staged = new List<(string ResourceType, string ResourceId)>(refResources.Count);

        foreach (var rr in refResources)
        {
            if (string.IsNullOrWhiteSpace(rr?.Reference))
                continue;

            var identity = new ResourceIdentity(rr.Reference);
            if (string.IsNullOrWhiteSpace(identity.ResourceType) || string.IsNullOrWhiteSpace(identity.Id))
            {
                _logger.LogDebug(
                    "ProcessReferences: skipping unparseable reference '{Reference}' on log {LogId}.",
                    rr.Reference, log.Id);
                continue;
            }

            staged.Add((identity.ResourceType, identity.Id));
        }

        if (staged.Count == 0)
            return;

        _logger.LogInformation(
            "ProcessReferences: staging {Count} reference id(s) for log {LogId} (correlation {CorrelationId}).",
            staged.Count, log.Id, log.CorrelationId);

        await _referenceResourcesManager.StagePendingReferencesAsync(
            log.FacilityId,
            log.CorrelationId,
            staged,
            cancellationToken);
    }
}
