using System.Diagnostics;
using System.Net;
using System.Text;
using System.Text.Json;
using Confluent.Kafka;
using DataAcquisition.Domain.Application.Models;
using Hl7.Fhir.Model;
using Hl7.Fhir.Rest;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Managers;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Exceptions;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Factory;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Kafka;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Queries;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Services.FhirApi.Commands;
using LantanaGroup.Link.DataAcquisition.Domain.Models;
using LantanaGroup.Link.DataAcquisition.Domain.Settings;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Models.Integration.DataAcquisition;
using LantanaGroup.Link.Shared.Application.Models.Kafka;
using LantanaGroup.Link.Shared.Application.Models.Telemetry;
using LantanaGroup.Link.Shared.Application.SerDes;
using LantanaGroup.Link.Shared.Application.Utilities;
using Microsoft.Extensions.Logging;
using DateTime = System.DateTime;
using QueryPhase = LantanaGroup.Link.Shared.Application.Models.Integration.DataAcquisition.QueryPhase;
using ResourceType = Hl7.Fhir.Model.ResourceType;
using Task = System.Threading.Tasks.Task;
using LantanaGroup.Link.Shared.Application.Interfaces;

namespace LantanaGroup.Link.DataAcquisition.Domain.Application.Services.FhirApi;

public interface IFhirApiService
{
    Task<IReadOnlyCollection<string>> ExecuteRead(DataAcquisitionLogModel log, FhirQueryModel fhirQuery, ResourceType resourceType, FhirQueryConfigurationModel fhirQueryConfiguration, DiscoveredReferenceAccumulator? referenceAccumulator = null, CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<string>> ExecuteSearch(DataAcquisitionLogModel log, FhirQueryModel fhirQuery, FhirQueryConfigurationModel fhirQueryConfiguration, ResourceType resourceType, DiscoveredReferenceAccumulator? referenceAccumulator = null, CancellationToken cancellationToken = default);
}

public class FhirApiService : IFhirApiService
{
    private static readonly JsonSerializerOptions _options = LinkFhirSerializerOptions.ForFhirLenientSerialization;

    private readonly IReferenceResourcesManager _referenceResourceManager;
    private readonly IReferenceResourcesQueries _referenceResourcesQueries;
    private readonly IReadFhirCommand _readFhirCommand;
    private readonly ISearchFhirCommand _searchFhirCommand;
    private readonly ILogger<FhirApiService> _logger;
    private readonly IResourceCache _resourceCache;
    private readonly IEncounterMappingQueries _encounterMappingQueries;
    private readonly IOrganizationLocationConfigurationQueries _organizationLocationConfigurationQueries;
    private readonly ILocationMappingService _locationMappingService;

    public FhirApiService(
        IReferenceResourcesManager referenceResourceManager,
        IReferenceResourcesQueries referenceResourcesQueries,
        ISearchFhirCommand searchFhirCommand,
        IReadFhirCommand readFhirCommand,
        ILogger<FhirApiService> logger,
        IResourceCache resourceCache,
        IEncounterMappingQueries encounterMappingQueries,
        IOrganizationLocationConfigurationQueries organizationLocationConfigurationQueries,
        ILocationMappingService locationMappingService)
    {
        _referenceResourceManager = referenceResourceManager;
        _referenceResourcesQueries = referenceResourcesQueries;
        _searchFhirCommand = searchFhirCommand;
        _readFhirCommand = readFhirCommand;
        _logger = logger;
        _resourceCache = resourceCache;
        _encounterMappingQueries = encounterMappingQueries;
        _organizationLocationConfigurationQueries = organizationLocationConfigurationQueries;
        _locationMappingService = locationMappingService;
    }

    #region Interface Implementation
    public async Task<IReadOnlyCollection<string>> ExecuteRead(DataAcquisitionLogModel log, FhirQueryModel fhirQuery, ResourceType resourceType, FhirQueryConfigurationModel fhirQueryConfiguration, DiscoveredReferenceAccumulator? referenceAccumulator = null, CancellationToken cancellationToken = default)
    {
        using var activity = ServiceActivitySource.Instance.StartActivity("FhirApiService.ExecuteRead");
        activity?.SetTag(DiagnosticNames.FacilityId, log.FacilityId);
        activity?.SetTag(DiagnosticNames.CorrelationId, log.CorrelationId);
        activity?.SetTag(DiagnosticNames.DataAcquisitionLogId, log.Id);
        activity?.SetTag(DiagnosticNames.ReportTrackingId, log.ReportTrackingId);
        activity?.SetTag(DiagnosticNames.ResourceType, resourceType.ToString());

        var resourceIds = new List<string>();
        List<string> resourceIdsToAcquire =
            fhirQuery.IsReference.GetValueOrDefault()
            ? fhirQuery.IdQueryParameterValues.ToList()
            : [resourceType == ResourceType.Patient ? log.PatientId.SplitReference() : log.ResourceId];
        foreach (string resourceIdToAcquire in resourceIdsToAcquire)
        {
            var ids = await ExecuteRead(log, fhirQuery, resourceType, resourceIdToAcquire, fhirQueryConfiguration, referenceAccumulator, cancellationToken);
            resourceIds.AddRange(ids);
        }
        return resourceIds;
    }

    private async Task<IReadOnlyCollection<string>> ExecuteRead(DataAcquisitionLogModel log, FhirQueryModel fhirQuery, ResourceType resourceType, string resourceIdToAcquire, FhirQueryConfigurationModel fhirQueryConfiguration, DiscoveredReferenceAccumulator? referenceAccumulator, CancellationToken cancellationToken = default)
    {
        using var activity = ServiceActivitySource.Instance.StartActivity("FhirApiService.ExecuteReadInternal");
        activity?.SetTag(DiagnosticNames.FacilityId, log.FacilityId);
        activity?.SetTag(DiagnosticNames.CorrelationId, log.CorrelationId);
        activity?.SetTag(DiagnosticNames.DataAcquisitionLogId, log.Id);
        activity?.SetTag(DiagnosticNames.ReportTrackingId, log.ReportTrackingId);
        activity?.SetTag(DiagnosticNames.ResourceType, resourceType.ToString());
        activity?.SetTag(DiagnosticNames.ResourceId, resourceIdToAcquire);

        var resourceIds = new List<string>();

        try
        {
            var resource = await _readFhirCommand.ExecuteAsync(
                                            new ReadFhirCommandRequest(
                                                log.FacilityId,
                                                resourceType,
                                                resourceIdToAcquire,
                                                fhirQueryConfiguration.FhirServerBaseUrl,
                                                fhirQueryConfiguration,
                                                log.ReportTrackingId),
                                            cancellationToken);

            var filteredResources = await FilterResourcesByEncounterMappingAsync(
                log,
                [resource],
                cancellationToken);

            if (filteredResources.Count == 0)
            {
                return resourceIds;
            }

            resourceIds.Add($"{resourceType}/{resource.Id}");

            InsertDateExtension(resource);

            // Reference discovery: accumulate discovered ref ids into the per-execution
            // accumulator. Drained once at the end of the primary log's execution by
            // ReferenceResourceService.FetchAndPersistAsync, which creates a single
            // batched reference-fetch DataAcquisitionLog per (correlation, type) for
            // cache misses (executed inline, retried by the AcquisitionProcessingJob on
            // failure) and a separate audit-only Completed log for cache hits.
            if (referenceAccumulator != null)
            {
                var validResourceTypes = fhirQuery.ResourceReferenceTypes
                    .Select(x => x.ResourceType)
                    .Where(resourceReferenceType => !string.IsNullOrWhiteSpace(resourceReferenceType))
                    .Select(resourceReferenceType => resourceReferenceType!)
                    .ToList();

                var refResources = ReferenceResourceBundleExtractor.Extract(resource, validResourceTypes);
                AccumulateDiscoveredReferences(refResources, referenceAccumulator);
            }

            AddResourceToCache(new ResourceAcquired
            {
                Resource = resource,
                ResourceType = resource.TypeName,
                ScheduledReports = new List<ScheduledReport> { log.ScheduledReport },
                PatientId = !fhirQuery.IsReference ?? false ? log.PatientId : null,
                QueryType = QueryPhaseUtilities.ToWireQueryType(log.QueryPhase),
                ReportableEvent = log.ReportableEvent ?? throw new ArgumentNullException(nameof(log.ReportableEvent)),
            }, log.CorrelationId);

            return resourceIds;
        }
        catch (TooManyRequestsException ex)
        {
            throw; // Propagate to higher level
        }
        catch (FhirOperationException ex)
        {
            if (fhirQuery.IsReference.GetValueOrDefault() && (ex.Status == HttpStatusCode.NotFound || ex.Status == HttpStatusCode.Gone))
            {
                return resourceIds;
            }

            if (ex.Status == HttpStatusCode.NotFound || ex.Status == HttpStatusCode.Gone || ex.Outcome != null)
            {
                string note = $"[{DateTime.UtcNow}] HTTP {ex.Status} returned for Read operation. See application logs for details.";

                log.Notes ??= new List<string>();
                log.Notes.Add(note);
                _logger.LogError(ex, "FhirOperationException for log {LogId} with facility {FacilityId}: {note}", log.Id, log.FacilityId, note);
                throw new OpOutcomeException(note, ex);
            }
            throw;
        }
    }

    public async Task<IReadOnlyCollection<string>> ExecuteSearch(DataAcquisitionLogModel log, FhirQueryModel fhirQuery, FhirQueryConfigurationModel fhirQueryConfiguration, ResourceType resourceType, DiscoveredReferenceAccumulator? referenceAccumulator = null, CancellationToken cancellationToken = default)
    {
        using var activity = ServiceActivitySource.Instance.StartActivity("FhirApiService.ExecuteSearch");
        activity?.SetTag(DiagnosticNames.FacilityId, log.FacilityId);
        activity?.SetTag(DiagnosticNames.CorrelationId, log.CorrelationId);
        activity?.SetTag(DiagnosticNames.DataAcquisitionLogId, log.Id);
        activity?.SetTag(DiagnosticNames.ReportTrackingId, log.ReportTrackingId);
        activity?.SetTag(DiagnosticNames.ResourceType, resourceType.ToString());

        if (log == null) throw new ArgumentNullException(nameof(log));
        if (fhirQuery == null) throw new ArgumentNullException(nameof(fhirQuery));
        if (fhirQueryConfiguration == null) throw new ArgumentNullException(nameof(fhirQueryConfiguration));

        var resourceIds = new List<string>();

        if (fhirQuery.IsReference.GetValueOrDefault())
        {
            int batchSize = fhirQuery.Paged.GetValueOrDefault();
            if (batchSize <= 0)
            {
                batchSize = int.MaxValue;
            }
            var resourceIdsToAcquire = fhirQuery.IdQueryParameterValues.ToList();
            for (int batchStart = 0; batchStart < resourceIdsToAcquire.Count; batchStart += batchSize)
            {
                var batchIds = resourceIdsToAcquire.Skip(batchStart).Take(batchSize);
                var searchParams = BuildSearchParams([$"_id={string.Join(',', batchIds)}"]);
                var ids = await ExecutePagingSearch(log, fhirQuery, searchParams, fhirQueryConfiguration, resourceType, referenceAccumulator, cancellationToken);
                resourceIds.AddRange(ids);
            }
            return resourceIds;
        }
        else
        {
            var searchParams = BuildSearchParams(fhirQuery.QueryParameters);
            return await ExecutePagingSearch(log, fhirQuery, searchParams, fhirQueryConfiguration, resourceType, referenceAccumulator, cancellationToken);
        }
    }
    #endregion

    #region Private Methods
    private async Task<List<string>> ExecutePagingSearch(DataAcquisitionLogModel log, FhirQueryModel fhirQuery, SearchParams searchParams, FhirQueryConfigurationModel fhirQueryConfiguration, ResourceType resourceType, DiscoveredReferenceAccumulator? referenceAccumulator, CancellationToken cancellationToken = default)
    {
        using var activity = ServiceActivitySource.Instance.StartActivity("FhirApiService.ExecutePagingSearch");
        activity?.SetTag(DiagnosticNames.FacilityId, log.FacilityId);
        activity?.SetTag(DiagnosticNames.CorrelationId, log.CorrelationId);
        activity?.SetTag(DiagnosticNames.DataAcquisitionLogId, log.Id);
        activity?.SetTag(DiagnosticNames.ReportTrackingId, log.ReportTrackingId);
        activity?.SetTag(DiagnosticNames.ResourceType, resourceType.ToString());

        var isReferenceLog = fhirQuery.IsReference.GetValueOrDefault();
        var resourceIds = new List<string>();
        try
        {
            await foreach (var bundle in _searchFhirCommand.ExecuteAsync(
                            new SearchFhirCommandRequest(
                                fhirQueryConfiguration,
                            resourceType,
                            searchParams,
                            log.FacilityId,
                            log.PatientId,
                            log.CorrelationId,
                            log.QueryPhase,
                            fhirQuery.QueryType,
                            log.ReportTrackingId),
                            cancellationToken))
            {
                var resources = bundle.Entry
                    .Where(e => e.Resource != null && e.Resource.TypeName != "OperationOutcome")
                    .Select(e => e.Resource)
                    .ToList();

                var outcomes = bundle.Entry
                    .Where(e => e.Resource is OperationOutcome)
                    .Select(e => (OperationOutcome)e.Resource)
                    .ToList();

                if (outcomes.Any())
                {
                    log.Notes ??= new List<string>();
                    string searchOutcomeNote = $"[{DateTime.UtcNow}] OperationOutcome(s) found in search bundle. See application logs for details.";
                    log.Notes.Add(searchOutcomeNote);
                    foreach (var outcome in outcomes)
                    {
                        string outcomeDetail = JsonSerializer.Serialize(outcome, _options);
                        _logger.LogInformation("OperationOutcome found in successful search bundle for log {LogId}: {outcomeDetail}", log.Id, outcomeDetail);
                    }
                }

                resources = await FilterResourcesByEncounterMappingAsync(
                    log,
                    resources,
                    cancellationToken);

                // Reference discovery: collect ref ids from filtered resources into the per-
                // execution accumulator. Drained at end of primary log execution by
                // ReferenceResourceService.FetchAndPersistAsync.
                if (referenceAccumulator != null)
                {
                    var validResourceTypes = fhirQuery.ResourceReferenceTypes
                        .Select(x => x.ResourceType)
                        .Where(resourceReferenceType => !string.IsNullOrWhiteSpace(resourceReferenceType))
                        .Select(resourceReferenceType => resourceReferenceType!)
                        .ToList();

                    var refResources = resources
                        .SelectMany(resource => ReferenceResourceBundleExtractor.Extract(resource, validResourceTypes))
                        .ToList();
                    AccumulateDiscoveredReferences(refResources, referenceAccumulator);
                }

                resourceIds.AddRange(resources.Select(r => $"{r.TypeName}/{r.Id}"));

                // When this is a reference-phase log, persist each fetched resource into
                // the canonical ReferenceResources cache (upsert) and junction it to the
                // log so subsequent correlations can cache-hit without a FHIR round trip.
                if (isReferenceLog && resources.Count > 0)
                {
                    await PersistAcquiredReferenceResourcesAsync(log, resources, cancellationToken);
                }

                foreach (var resource in resources)
                {
                    InsertDateExtension((DomainResource)resource);

                    if (resource is Location location && fhirQueryConfiguration.EnableLocationResolutionMapping)
                    {
                        await _locationMappingService.UpdateLocationMappingAsync(
                            log.FacilityId, location,
                            cancellationToken:cancellationToken);
                    }
                    
                    AddResourceToCache(new ResourceAcquired
                    {
                        Resource = resource,
                        ResourceType = resource.TypeName,
                        ScheduledReports = new List<ScheduledReport> { log.ScheduledReport },
                        PatientId = !fhirQuery.IsReference ?? false ? log.PatientId : null,
                        QueryType = QueryPhaseUtilities.ToWireQueryType(log.QueryPhase),
                        ReportableEvent = log.ReportableEvent ?? throw new ArgumentNullException(nameof(log.ReportableEvent)),
                    }, log.CorrelationId);
                }
            }

            return resourceIds;
        }
        catch (TooManyRequestsException ex)
        {
            throw; // Propagate to higher level
        }
        catch (FhirOperationException ex)
        {
            if (ex.Status == HttpStatusCode.NotFound || ex.Status == HttpStatusCode.Gone || ex.Outcome != null)
            {
                string note = $"[{DateTime.UtcNow}] HTTP {ex.Status} returned for Search operation. See application logs for details.";

                log.Notes ??= new List<string>();
                log.Notes.Add(note);
                _logger.LogWarning(ex, "Expected FHIR error encountered for search for log {LogId} with facility {FacilityId}: {note}", log.Id, log.FacilityId, note);
                throw new OpOutcomeException(note, ex);
            }
            throw;
        }
    }

    private async Task PersistAcquiredReferenceResourcesAsync(
        DataAcquisitionLogModel log,
        IReadOnlyList<Resource> resources,
        CancellationToken cancellationToken)
    {
        var toCreate = new List<CreateReferenceResourcesModel>(resources.Count);
        foreach (var resource in resources)
        {
            if (resource == null || string.IsNullOrWhiteSpace(resource.Id) || string.IsNullOrWhiteSpace(resource.TypeName))
                continue;

            string serialized;
            try
            {
                serialized = JsonSerializer.Serialize(resource, _options);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "PersistAcquiredReferenceResourcesAsync: failed to serialize {ResourceType}/{ResourceId} for log {LogId}; skipping cache persist.",
                    resource.TypeName, resource.Id, log.Id);
                continue;
            }

            toCreate.Add(new CreateReferenceResourcesModel
            {
                FacilityId = log.FacilityId,
                ResourceId = resource.Id,
                ResourceType = resource.TypeName,
                ReferenceResource = serialized,
                QueryPhase = log.QueryPhase ?? QueryPhase.Initial,
            });
        }

        if (toCreate.Count == 0)
            return;

        // Upsert canonical cache rows; do not link back into this reference log's
        // junction (the junction is reserved for primary logs that depend on them).
        await _referenceResourceManager.CreateBatchAsync(toCreate, cancellationToken);
    }

    private SearchParams BuildSearchParams(List<string> parameters)
    {
        var searchParams = new SearchParams();
        foreach (var param in parameters)
        {
            var splitParams = param.Split('=');
            if (splitParams.Length != 2)
            {
                throw new ArgumentException($"Invalid search parameter format: {param}");
            }
            searchParams.Add(splitParams[0], splitParams[1]);
        }
        return searchParams;
    }

    private async Task<List<Resource>> FilterResourcesByEncounterMappingAsync(
        DataAcquisitionLogModel log,
        IReadOnlyCollection<Resource> resources,
        CancellationToken cancellationToken)
    {
        if (resources.Count == 0)
        {
            return resources.ToList();
        }

        var resourceEncounterIds = resources
            .Select(resource => new
            {
                Resource = resource,
                EncounterIds = GetEncounterReferenceIds(resource)
            })
            .ToList();

        var encounterIds = resourceEncounterIds
            .SelectMany(x => x.EncounterIds)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (encounterIds.Count == 0)
        {
            return resources.ToList();
        }

        var organizationLocationMappingIsConfigured = await _organizationLocationConfigurationQueries
            .HasActiveByFacilityIdAsync(log.FacilityId, cancellationToken);

        if (!organizationLocationMappingIsConfigured)
        {
            return resources.ToList();
        }

        var encounterMappings = await _encounterMappingQueries.GetByFacilityIdAndEncounterIdsAsync(
            log.FacilityId,
            encounterIds,
            cancellationToken);

        var mappedEncounterIds = encounterMappings
            .Where(mapping => mapping.MappedToOrg)
            .Select(mapping => mapping.EncounterId)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var filteredResources = resourceEncounterIds
            .Where(x => x.EncounterIds.Count == 0 || x.EncounterIds.Any(mappedEncounterIds.Contains))
            .Select(x => x.Resource)
            .ToList();

        var removedCount = resources.Count - filteredResources.Count;
        if (removedCount > 0)
        {
            _logger.LogDebug(
                "Removed {RemovedCount} resource(s) without mapped encounter organization for facility {FacilityId}.",
                removedCount,
                log.FacilityId);
        }

        return filteredResources;
    }

    private static List<string> GetEncounterReferenceIds(Resource resource)
    {
        return ReferenceResourceBundleExtractor
            .Extract(resource, [ResourceType.Encounter.ToString()])
            .Select(GetEncounterReferenceId)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Select(id => id!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static string? GetEncounterReferenceId(ResourceReference reference)
    {
        if (string.IsNullOrWhiteSpace(reference.Reference))
        {
            return null;
        }

        try
        {
            var identity = new ResourceIdentity(reference.Reference);
            if (string.Equals(identity.ResourceType, ResourceType.Encounter.ToString(), StringComparison.OrdinalIgnoreCase)
                && !string.IsNullOrWhiteSpace(identity.Id))
            {
                return identity.Id;
            }
        }
        catch (Exception)
        {
        }

        if (string.Equals(reference.Type.SplitReference(), ResourceType.Encounter.ToString(), StringComparison.OrdinalIgnoreCase))
        {
            return reference.Reference.SplitReference();
        }

        return null;
    }

    private void AddResourceToCache(ResourceAcquired resourceAcquired, string correlationId)
    {
        if (resourceAcquired.Resource is DomainResource domainResource
            && !string.IsNullOrWhiteSpace(resourceAcquired.ResourceType)
            && Enum.TryParse<ResourceType>(resourceAcquired.ResourceType, out var resourceType))
        {
            _resourceCache.UpdateCorrelationCache($"{correlationId}:{resourceType}", new List<DomainResource> { domainResource }, resourceType);
        }
    }

    private void InsertDateExtension(DomainResource resource)
    {
        if (resource == null)
            throw new ArgumentNullException(nameof(resource));

        resource.Meta ??= new Meta();
        resource.Meta.Extension ??= new List<Extension>();

        if (!resource.Meta.Extension.Any(e => e.Url == DataAcquisitionConstants.Extension.DateReceivedExtensionUri))
        {
            resource.Meta.Extension.Add(new Extension
            {
                Url = DataAcquisitionConstants.Extension.DateReceivedExtensionUri,
                Value = new FhirDateTime(DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"))
            });
        }
    }

    /// <summary>
    /// Adds parseable <c>Type/Id</c> references from the discovered ResourceReferences into
    /// the per-execution accumulator. Invalid / unparseable references are silently skipped
    /// — the bundle extractor is the gatekeeper for which reference types are eligible.
    /// </summary>
    private static void AccumulateDiscoveredReferences(
        IReadOnlyList<ResourceReference> refResources,
        DiscoveredReferenceAccumulator accumulator)
    {
        if (refResources == null || refResources.Count == 0)
            return;

        foreach (var rr in refResources)
        {
            if (string.IsNullOrWhiteSpace(rr?.Reference))
                continue;

            var identity = new ResourceIdentity(rr.Reference);
            if (string.IsNullOrWhiteSpace(identity.ResourceType) || string.IsNullOrWhiteSpace(identity.Id))
                continue;

            accumulator.Add(identity.ResourceType, identity.Id);
        }
    }
    #endregion
}
