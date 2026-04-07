using System.Diagnostics;
using System.Net;
using System.Text;
using System.Text.Json;
using Confluent.Kafka;
using Confluent.Kafka;
using DataAcquisition.Domain.Application.Models;
using Hl7.Fhir.Model;
using Hl7.Fhir.Rest;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Managers;
using Microsoft.Extensions.Logging;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Exceptions;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Factory;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Kafka;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Queries;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Services.FhirApi.Commands;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models.Enums;
using LantanaGroup.Link.DataAcquisition.Domain.Models;
using LantanaGroup.Link.DataAcquisition.Domain.Settings;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Models.Kafka;
using LantanaGroup.Link.Shared.Application.Models.Telemetry;
using LantanaGroup.Link.Shared.Application.SerDes;
using LantanaGroup.Link.Shared.Application.Utilities;
using DateTime = System.DateTime;
using ResourceType = Hl7.Fhir.Model.ResourceType;
using Task = System.Threading.Tasks.Task;

namespace LantanaGroup.Link.DataAcquisition.Domain.Application.Services.FhirApi;

public interface IFhirApiService
{
    Task<IReadOnlyCollection<string>> ExecuteRead(DataAcquisitionLogModel log, FhirQueryModel fhirQuery, ResourceType resourceType, FhirQueryConfigurationModel fhirQueryConfiguration, CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<string>> ExecuteSearch(DataAcquisitionLogModel log, FhirQueryModel fhirQuery, FhirQueryConfigurationModel fhirQueryConfiguration, ResourceType resourceType, CancellationToken cancellationToken = default);
}

public class FhirApiService : IFhirApiService
{
    private static readonly JsonSerializerOptions _options = LinkFhirSerializerOptions.ForFhirLenientSerialization;

    private readonly IReferenceResourcesManager _referenceResourceManager;
    private readonly IReferenceResourcesQueries _referenceResourcesQueries;
    private readonly IReferenceResourceService _referenceResourceService;
    private readonly IReadFhirCommand _readFhirCommand;
    private readonly ISearchFhirCommand _searchFhirCommand;
    private readonly IProducer<ResourceKey, ResourceAcquired> _kafkaProducer;
    private readonly ILogger<FhirApiService> _logger;

    public FhirApiService(
        IReferenceResourcesManager referenceResourceManager,
        IReferenceResourcesQueries referenceResourcesQueries,
        IReferenceResourceService referenceResourceService,
        ISearchFhirCommand searchFhirCommand,
        IReadFhirCommand readFhirCommand,
        IProducer<ResourceKey, ResourceAcquired> kafkaProducer,
        ILogger<FhirApiService> logger)
    {
        _referenceResourceManager = referenceResourceManager;
        _referenceResourcesQueries = referenceResourcesQueries;
        _referenceResourceService = referenceResourceService;
        _searchFhirCommand = searchFhirCommand;
        _readFhirCommand = readFhirCommand;
        _kafkaProducer = kafkaProducer;
        _logger = logger;
    }

    #region Interface Implementation
    public async Task<IReadOnlyCollection<string>> ExecuteRead(DataAcquisitionLogModel log, FhirQueryModel fhirQuery, ResourceType resourceType, FhirQueryConfigurationModel fhirQueryConfiguration, CancellationToken cancellationToken = default)
    {
        using var activity = ServiceActivitySource.Instance.StartActivity("FhirApiService.ExecuteRead");
        activity?.SetTag(DiagnosticNames.FacilityId, log.FacilityId);
        activity?.SetTag(DiagnosticNames.CorrelationId, log.CorrelationId);
        activity?.SetTag(DiagnosticNames.ReportId, log.ReportTrackingId);
        activity?.SetTag(DiagnosticNames.ResourceType, resourceType.ToString());

        var resourceIds = new List<string>();
        List<string> resourceIdsToAcquire =
            fhirQuery.IsReference.GetValueOrDefault()
            ? fhirQuery.IdQueryParameterValues.ToList()
            : [resourceType == ResourceType.Patient ? log.PatientId.SplitReference() : log.ResourceId];
        foreach (string resourceIdToAcquire in resourceIdsToAcquire)
        {
            var ids = await ExecuteRead(log, fhirQuery, resourceType, resourceIdToAcquire, fhirQueryConfiguration, cancellationToken);
            resourceIds.AddRange(ids);
        }
        return resourceIds;
    }

    private async Task<IReadOnlyCollection<string>> ExecuteRead(DataAcquisitionLogModel log, FhirQueryModel fhirQuery, ResourceType resourceType, string resourceIdToAcquire, FhirQueryConfigurationModel fhirQueryConfiguration, CancellationToken cancellationToken = default)
    {
        using var activity = ServiceActivitySource.Instance.StartActivity("FhirApiService.ExecuteReadInternal");
        activity?.SetTag(DiagnosticNames.FacilityId, log.FacilityId);
        activity?.SetTag(DiagnosticNames.CorrelationId, log.CorrelationId);
        activity?.SetTag(DiagnosticNames.ReportId, log.Id);
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
                                                fhirQueryConfiguration),
                                            cancellationToken);

            resourceIds.Add($"{resourceType}/{resource.Id}");

            if (fhirQuery.IsReference.HasValue && fhirQuery.IsReference.Value)
            {
                await HandleReferenceResourceBatch(log, [resource], cancellationToken);
            }

            InsertDateExtension(resource);

            //get references
            var refResources = ReferenceResourceBundleExtractor.Extract(resource, fhirQuery.ResourceReferenceTypes.Select(x => x.ResourceType).ToList());
            await _referenceResourceService.ProcessReferences(log, refResources, cancellationToken);

            await GenerateResourceAcquiredMessage(new ResourceAcquired
            {
                Resource = resource,
                ScheduledReports = new List<ScheduledReport> { log.ScheduledReport },
                PatientId = !fhirQuery.IsReference ?? false ? log.PatientId : null,
                QueryType = log.QueryPhase.ToString(),
                ReportableEvent = log.ReportableEvent ?? throw new ArgumentNullException(nameof(log.ReportableEvent)),
            }, log.FacilityId, log.CorrelationId, cancellationToken);

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

    public async Task<IReadOnlyCollection<string>> ExecuteSearch(DataAcquisitionLogModel log, FhirQueryModel fhirQuery, FhirQueryConfigurationModel fhirQueryConfiguration, ResourceType resourceType, CancellationToken cancellationToken = default)
    {
        using var activity = ServiceActivitySource.Instance.StartActivity("FhirApiService.ExecuteSearch");
        activity?.SetTag(DiagnosticNames.FacilityId, log.FacilityId);
        activity?.SetTag(DiagnosticNames.CorrelationId, log.CorrelationId);
        activity?.SetTag(DiagnosticNames.ReportId, log.Id);
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
                var ids = await ExecutePagingSearch(log, fhirQuery, searchParams, fhirQueryConfiguration, resourceType, cancellationToken);
                resourceIds.AddRange(ids);
            }
            return resourceIds;
        }
        else
        {
            var searchParams = BuildSearchParams(fhirQuery.QueryParameters);
            return await ExecutePagingSearch(log, fhirQuery, searchParams, fhirQueryConfiguration, resourceType, cancellationToken);
        }
    }
    #endregion

    #region Private Methods
    private async Task<List<string>> ExecutePagingSearch(DataAcquisitionLogModel log, FhirQueryModel fhirQuery, SearchParams searchParams, FhirQueryConfigurationModel fhirQueryConfiguration, ResourceType resourceType, CancellationToken cancellationToken = default)
    {
        using var activity = ServiceActivitySource.Instance.StartActivity("FhirApiService.ExecutePagingSearch");
        activity?.SetTag(DiagnosticNames.FacilityId, log.FacilityId);
        activity?.SetTag(DiagnosticNames.CorrelationId, log.CorrelationId);
        activity?.SetTag(DiagnosticNames.ReportId, log.Id);
        activity?.SetTag(DiagnosticNames.ResourceType, resourceType.ToString());

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
                            fhirQuery.QueryType),
                            cancellationToken))
            {
                var refResources = ReferenceResourceBundleExtractor.Extract(bundle, fhirQuery.ResourceReferenceTypes.Select(x => x.ResourceType).ToList());

                await _referenceResourceService.ProcessReferences(log, refResources, cancellationToken);

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

                resourceIds.AddRange(resources.Select(r => $"{r.TypeName}/{r.Id}"));

                // Batch reference resource handling: collect all reference resources in this page,
                // do a single bulk lookup, then bulk create/update — instead of N individual round-trips.
                if (fhirQuery.IsReference.HasValue && fhirQuery.IsReference.Value && resources.Count > 0)
                {
                    await HandleReferenceResourceBatch(log, resources, cancellationToken);
                }

                foreach (var resource in resources)
                {
                    InsertDateExtension((DomainResource)resource);

                    await GenerateResourceAcquiredMessage(new ResourceAcquired
                    {
                        Resource = resource,
                        ScheduledReports = new List<ScheduledReport> { log.ScheduledReport },
                        PatientId = !fhirQuery.IsReference ?? false ? log.PatientId : null,
                        QueryType = log.QueryPhase.ToString(),
                        ReportableEvent = log.ReportableEvent ?? throw new ArgumentNullException(nameof(log.ReportableEvent)),
                    }, log.FacilityId, log.CorrelationId, cancellationToken);
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

    /// <summary>
    /// Batch-processes reference resources: one bulk lookup for the entire page,
    /// then bulk create/update — replaces the previous per-resource round-trip pattern.
    /// </summary>
    private async Task HandleReferenceResourceBatch(DataAcquisitionLogModel log, List<Resource> resources, CancellationToken cancellationToken)
    {
        static string BuildReferenceKey(string resourceType, string resourceId) => $"{resourceType}/{resourceId}";

        // Pre-fetch all existing reference resources for this facility + resource IDs/types in a single query
        var resourceIds = resources.Select(r => r.Id).Distinct().ToList();
        var resourceTypes = resources.Select(r => r.TypeName).Distinct().ToList();
        var existingRecords = (await _referenceResourcesQueries.SearchAsync(new SearchReferenceResourcesModel
        {
            FacilityId = log.FacilityId,
            ResourceIds = resourceIds,
            ResourceTypes = resourceTypes,
            PageSize = int.MaxValue
        })).Records;

        // Group by resource type + id and keep the most recent record per key, since the same
        // resource can appear multiple times (different query phases, prior runs, etc.)
        var existingByResourceKey = existingRecords
            .GroupBy(r => BuildReferenceKey(r.ResourceType, r.ResourceId))
            .ToDictionary(g => g.Key, g => g.OrderByDescending(r => r.ModifyDate ?? r.CreateDate).First());

        // Deduplicate incoming resources — a bundle page can contain the same resource twice
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var resource in resources)
        {
            var resourceKey = BuildReferenceKey(resource.TypeName, resource.Id);
            if (!seen.Add(resourceKey))
                continue;

            InsertDateExtension((DomainResource)resource);
            var serialized = JsonSerializer.Serialize(resource, LinkFhirSerializerOptions.ForFhirLenientSerialization);

            if (existingByResourceKey.TryGetValue(resourceKey, out var existing))
            {
                await _referenceResourceManager.UpdateAsync(new UpdateReferenceResourcesModel
                {
                    Id = existing.Id,
                    QueryPhase = existing.QueryPhase,
                    ResourceType = resource.TypeName,
                    ReferenceResource = serialized
                }, cancellationToken);
            }
            else
            {
                await _referenceResourceManager.CreateAsync(new CreateReferenceResourcesModel
                {
                    DataAcquisitionLogId = log.Id,
                    QueryPhase = QueryPhase.Referential,
                    FacilityId = log.FacilityId,
                    ResourceId = resource.Id,
                    ResourceType = resource.TypeName,
                    ReferenceResource = serialized
                }, cancellationToken);
            }
        }
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

    private async Task GenerateResourceAcquiredMessage(ResourceAcquired resourceAcquired, string facilityId, string correlationId, CancellationToken cancellationToken = default)
    {
        // No manual context manipulation needed!
        Activity.Current?.SetTag("link.resource_type", resourceAcquired.Resource?.TypeName);
        Activity.Current?.SetTag("messaging.destination", KafkaTopic.ResourceAcquired.ToString());

        await _kafkaProducer.ProduceAsync(
            KafkaTopic.ResourceAcquired.ToString(),
            new Message<ResourceKey, ResourceAcquired>
            {
                Key = new ResourceKey
                {
                    FacilityId = facilityId,
                    CorrelationId = correlationId
                },
                Headers = new Headers
                {
                new Header(DataAcquisitionConstants.HeaderNames.CorrelationId,
                    Encoding.UTF8.GetBytes(correlationId))
                },
                Value = resourceAcquired
            },
            cancellationToken);

        _kafkaProducer.Flush(cancellationToken);
    }

    private void InsertDateExtension(DomainResource resource)
    {
        if (resource == null)
            throw new ArgumentNullException(nameof(resource));

        if (resource.Meta == null)
        {
            resource.Meta = new Meta();
            resource.Meta.Extension = new List<Extension> { };
        }

        if (resource.Meta.Extension == null)
            resource.Meta.Extension = new List<Extension> { };

        if (!resource.Extension.Any(e => e.Url == DataAcquisitionConstants.Extension.DateReceivedExtensionUri))
            resource.Meta.Extension.Add(new Extension { Url = DataAcquisitionConstants.Extension.DateReceivedExtensionUri, Value = new FhirDateTime(DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ")) });
    }
    #endregion
}