using System.Net;
using System.Text;
using System.Text.Json;
using Confluent.Kafka;
using DataAcquisition.Domain.Application.Models;
using Hl7.Fhir.Model;
using Hl7.Fhir.Rest;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Managers;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Api.Requests;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Factory.ReferenceQuery;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Kafka;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Queries;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Serializers;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Services.FhirApi.Commands;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Context;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models.QueryConfig;
using LantanaGroup.Link.DataAcquisition.Domain.Models;
using LantanaGroup.Link.DataAcquisition.Domain.Settings;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Models.Kafka;
using LantanaGroup.Link.Shared.Application.SerDes;
using LantanaGroup.Link.Shared.Application.Utilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using QueryPhase = LantanaGroup.Link.Shared.Application.Models.Integration.DataAcquisition.QueryPhase;
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
    /// Inline fetch-or-cache for discovered reference resource IDs.
    /// Checks the canonical <c>ReferenceResources</c> cache; fetches any missing
    /// resources from the FHIR server; stores them; and links them to the log.
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
    private readonly IReadFhirCommand _readFhirCommand;
    private readonly DataAcquisitionDbContext _dbContext;
    private readonly IProducer<ResourceKey, ResourceAcquired> _kafkaProducer;

    public ReferenceResourceService(
        ILogger<ReferenceResourceService> logger,
        IReferenceResourcesQueries referenceResourcesQueries,
        IReferenceResourcesManager referenceResourcesManager,
        IReadFhirCommand readFhirCommand,
        IProducer<ResourceKey, ResourceAcquired> kafkaProducer,
        DataAcquisitionDbContext dbContext)
    {
        _logger = logger;
        _referenceResourcesQueries = referenceResourcesQueries;
        _referenceResourcesManager = referenceResourcesManager;
        _readFhirCommand = readFhirCommand;
        _kafkaProducer = kafkaProducer;
        _dbContext = dbContext;
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

        // Group discovered references by type and deduplicate IDs
        var groupedIdentities = refResources.Select(rr => rr.Reference)
            .Where(r => !string.IsNullOrEmpty(r))
            .Select(r => new ResourceIdentity(r))
            .GroupBy(i => i.ResourceType)
            .ToList();

        _logger.LogInformation("Processing {Count} reference resources for log with ID: {LogId}", groupedIdentities.Sum(g => g.Count()), log.Id);

        var allCanonicalIdsToLink = new List<Guid>();
        var resourceByCanonicalId = new Dictionary<Guid, Resource>();

        foreach (var group in groupedIdentities)
        {
            var resourceType = group.Key;
            if (string.IsNullOrEmpty(resourceType))
            {
                _logger.LogWarning("Skipping reference resources with no type for log with ID: {LogId}", log.Id);
                continue;
            }

            var distinctIds = group.Select(i => i.Id).Distinct().ToList();

            // Check canonical cache: which ones already exist?
            var existing = (await _referenceResourcesQueries.SearchAsync(new SearchReferenceResourcesModel
            {
                FacilityId = log.FacilityId,
                ResourceType = resourceType,
                ResourceIds = distinctIds,
                PageSize = int.MaxValue
            })).Records;

            var existingIdSet = existing.Select(r => r.ResourceId).ToHashSet(StringComparer.Ordinal);
            allCanonicalIdsToLink.AddRange(existing.Select(r => r.Id));
            foreach (var existingRecord in existing)
            {
                try
                {
                    var deserialized = FhirResourceDeserializer.DeserializeFhirResource(existingRecord);
                    if (deserialized != null)
                        resourceByCanonicalId[existingRecord.Id] = deserialized;
                }
                catch
                {
                    // Non-fatal; skip event publish for malformed cached payload.
                }
            }

            // Determine which IDs need fetching
            var missingIds = distinctIds.Where(id => !existingIdSet.Contains(id)).ToList();

            if (missingIds.Count == 0)
                continue;

            // Fetch missing resources from FHIR server and cache them
            var fhirResourceType = Enum.Parse<ResourceType>(resourceType);
            var toCreate = new List<CreateReferenceResourcesModel>();

            foreach (var resourceId in missingIds)
            {
                try
                {
                    var resource = await _readFhirCommand.ExecuteAsync(
                        new ReadFhirCommandRequest(
                            log.FacilityId,
                            fhirResourceType,
                            resourceId,
                            fhirQueryConfiguration.FhirServerBaseUrl,
                            fhirQueryConfiguration,
                            log.ReportTrackingId),
                        cancellationToken);

                    var serialized = JsonSerializer.Serialize(resource, LinkFhirSerializerOptions.ForFhirLenientSerialization);

                    toCreate.Add(new CreateReferenceResourcesModel
                    {
                        FacilityId = log.FacilityId,
                        ResourceId = resource.Id,
                        ResourceType = resource.TypeName,
                        ReferenceResource = serialized,
                        QueryPhase = QueryPhase.Referential,
                    });
                }
                catch (FhirOperationException ex) when (ex.Status == HttpStatusCode.NotFound || ex.Status == HttpStatusCode.Gone)
                {
                    _logger.LogWarning("Reference resource {ResourceType}/{ResourceId} not found (HTTP {Status}) for log {LogId}; skipping.",
                        resourceType, resourceId, ex.Status, log.Id);
                }
            }

            if (toCreate.Count > 0)
            {
                await _referenceResourcesManager.CreateBatchAsync(toCreate, cancellationToken);

                // Look up newly created IDs for junction linking
                var newResourceIds = toCreate.Select(c => c.ResourceId).ToList();
                var newRecords = (await _referenceResourcesQueries.SearchAsync(new SearchReferenceResourcesModel
                {
                    FacilityId = log.FacilityId,
                    ResourceType = resourceType,
                    ResourceIds = newResourceIds,
                    PageSize = int.MaxValue
                })).Records;
                allCanonicalIdsToLink.AddRange(newRecords.Select(r => r.Id));
                foreach (var newRecord in newRecords)
                {
                    try
                    {
                        var deserialized = FhirResourceDeserializer.DeserializeFhirResource(newRecord);
                        if (deserialized != null)
                            resourceByCanonicalId[newRecord.Id] = deserialized;
                    }
                    catch
                    {
                        // Non-fatal; skip event publish for malformed payload.
                    }
                }
            }
        }

        // Link all canonical rows to this log and publish ResourceAcquired events for
        // newly linked references so downstream consumers (MeasureEval/Report) receive
        // reference resources in this correlation's stream.
        if (allCanonicalIdsToLink.Count > 0)
        {
            var requestedCanonicalIds = allCanonicalIdsToLink.Distinct().ToList();

            var alreadyLinkedIds = await _dbContext.DataAcquisitionLogs
                .Where(l => l.Id == log.Id)
                .SelectMany(l => l.ReferenceResources.Select(r => r.Id))
                .ToListAsync(cancellationToken);

            var newIdsToLink = requestedCanonicalIds
                .Except(alreadyLinkedIds)
                .ToList();

            if (newIdsToLink.Count > 0)
            {
                await _referenceResourcesManager.LinkToLogAsync(log.Id, newIdsToLink, cancellationToken);

                foreach (var referenceId in newIdsToLink)
                {
                    if (!resourceByCanonicalId.TryGetValue(referenceId, out var resource))
                        continue;
                    if (resource == null)
                        continue;

                    await _kafkaProducer.ProduceAsync(
                        KafkaTopic.ResourceAcquired.ToString(),
                        new Message<ResourceKey, ResourceAcquired>
                        {
                            Key = new ResourceKey
                            {
                                FacilityId = log.FacilityId,
                                CorrelationId = log.CorrelationId
                            },
                            Headers = new Headers
                            {
                                new Header(DataAcquisitionConstants.HeaderNames.CorrelationId,
                                    Encoding.UTF8.GetBytes(log.CorrelationId))
                            },
                            Value = new ResourceAcquired
                            {
                                Resource = resource,
                                ResourceType = resource.TypeName,
                                ScheduledReports = [log.ScheduledReport],
                                PatientId = null,
                                QueryType = log.QueryPhase.ToString(),
                                ReportableEvent = log.ReportableEvent ?? throw new ArgumentNullException(nameof(log.ReportableEvent))
                            }
                        },
                        cancellationToken);
                }
            }
        }
    }
}
