using Confluent.Kafka;
using Hl7.Fhir.Model;
using Hl7.Fhir.Rest;
using Hl7.Fhir.Serialization;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Interfaces;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Managers;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Exceptions;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Factory;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Kafka;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Queries;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Services.FhirApi.Commands;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Entities;
using LantanaGroup.Link.DataAcquisition.Domain.Settings;
using LantanaGroup.Link.Shared.Application.Error.Exceptions;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Models.Telemetry;
using LantanaGroup.Link.Shared.Application.Utilities;
using Microsoft.Extensions.Logging;
using System.Text;
using DateTime = System.DateTime;
using RequestStatus = LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models.Enums.RequestStatus;
using Task = System.Threading.Tasks.Task;

namespace LantanaGroup.Link.DataAcquisition.Domain.Application.Services.FhirApi;

public interface IFhirApiService
{
    Task<List<string>> ExecuteRead(DataAcquisitionLog log, FhirQuery fhirQuery, ResourceType resourceType, FhirQueryConfiguration fhirQueryConfiguration, List<string> resourceIds, CancellationToken cancellationToken = default);
    Task<List<string>> ExecuteSearch(DataAcquisitionLog log, FhirQuery fhirQuery, FhirQueryConfiguration fhirQueryConfiguration, List<string> resourceIds, ResourceType resourceType, CancellationToken cancellationToken = default);
}

public class FhirApiService : IFhirApiService
{
    private readonly ILogger<FhirApiService> _logger;
    private readonly IDataAcquisitionLogManager _dataAcquisitionLogManager;
    private readonly IDataAcquisitionServiceMetrics _metrics;
    private readonly IBundleEventService<string, ResourceAcquired, ResourceAcquiredMessageGenerationRequest> _bundleResourceAcquiredEventService;
    private readonly IReferenceResourcesManager _referenceResourceManager;
    private readonly IReferenceResourceService _referenceResourceService;
    private readonly IReadFhirCommand _readFhirCommand;
    private readonly ISearchFhirCommand _searchFhirCommand;
    private readonly IProducer<string, ResourceAcquired> _kafkaProducer;
    private readonly IFhirQueryManager _fhirQueryManager;
    private readonly IDataAcquisitionLogQueries _dataAcquisitionLogQueries;

    public FhirApiService(
        ILogger<FhirApiService> logger,
        IDataAcquisitionServiceMetrics metrics,
        IBundleEventService<string, ResourceAcquired, ResourceAcquiredMessageGenerationRequest> bundleResourceAcquiredEventService,
        IReferenceResourcesManager referenceResourceManager,
        IDataAcquisitionLogManager dataAcquisitionLogManager,
        IReferenceResourceService referenceResourceService,
        ISearchFhirCommand searchFhirCommand,
        IReadFhirCommand readFhirCommand,
        IDataAcquisitionLogQueries dataAcquisitionLogQueries,
        IProducer<string, ResourceAcquired> kafkaProducer,
        IFhirQueryManager fhirQueryManager)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _metrics = metrics ?? throw new ArgumentNullException(nameof(metrics));
        _bundleResourceAcquiredEventService = bundleResourceAcquiredEventService ?? throw new ArgumentNullException(nameof(bundleResourceAcquiredEventService));
        _referenceResourceManager = referenceResourceManager ?? throw new ArgumentNullException(nameof(referenceResourceManager));
        _dataAcquisitionLogManager = dataAcquisitionLogManager ?? throw new ArgumentNullException(nameof(dataAcquisitionLogManager));
        _referenceResourceService = referenceResourceService ?? throw new ArgumentNullException(nameof(referenceResourceService));
        _searchFhirCommand = searchFhirCommand ?? throw new ArgumentNullException(nameof(searchFhirCommand));
        _readFhirCommand = readFhirCommand ?? throw new ArgumentNullException(nameof(readFhirCommand));
        _dataAcquisitionLogQueries = dataAcquisitionLogQueries ?? throw new ArgumentNullException(nameof(dataAcquisitionLogQueries));
        _kafkaProducer = kafkaProducer ?? throw new ArgumentNullException(nameof(kafkaProducer));
        _fhirQueryManager = fhirQueryManager ?? throw new ArgumentNullException(nameof(fhirQueryManager));
    }

    public async Task<List<string>> ExecuteRead(DataAcquisitionLog log, FhirQuery fhirQuery, ResourceType resourceType, FhirQueryConfiguration fhirQueryConfiguration, List<string> resourceIds, CancellationToken cancellationToken = default)
    {
        var resource = await _readFhirCommand.ExecuteAsync(
            new ReadFhirCommandRequest(
                log.FacilityId,
                resourceType,
                resourceType == ResourceType.Patient ? log.PatientId.SplitReference() : log.ResourceId,
                fhirQueryConfiguration.FhirServerBaseUrl,
                fhirQueryConfiguration),
            cancellationToken);

        resourceIds.Add($"{resourceType}/{resource.Id}");

        var refResources = ReferenceResourceBundleExtractor.Extract(resource, fhirQuery.ResourceReferenceTypes.Select(x => x.ResourceType).ToList());
        await _referenceResourceService.ProcessReferences(log, refResources, cancellationToken);

        await GenerateResourceAcquiredMessage(new ResourceAcquired
        {
            Resource = resource,
            ScheduledReports = new List<ScheduledReport> { log.ScheduledReport },
            PatientId = log.PatientId,
            QueryType = log.QueryPhase.ToString(),
            ReportableEvent = log.ReportableEvent ?? throw new ArgumentNullException(nameof(log.ReportableEvent)),
        }, log.FacilityId, log.CorrelationId, cancellationToken);

        return resourceIds;
    }

    public async Task<List<string>> ExecuteSearch(DataAcquisitionLog log, FhirQuery fhirQuery, FhirQueryConfiguration fhirQueryConfiguration, List<string> resourceIds, ResourceType resourceType, CancellationToken cancellationToken = default)
    {
        if (log == null) throw new ArgumentNullException(nameof(log));
        if (fhirQuery == null) throw new ArgumentNullException(nameof(fhirQuery));
        if (fhirQueryConfiguration == null) throw new ArgumentNullException(nameof(fhirQueryConfiguration));
        if (resourceIds == null) throw new ArgumentNullException(nameof(resourceIds));

        var searchParams = BuildSearchParams(fhirQuery.QueryParameters);
        return await ExecutePagingSearch(log, fhirQuery, searchParams, fhirQueryConfiguration, resourceType, resourceIds, cancellationToken);
    }

    private async Task<List<string>> ExecutePagingSearch(DataAcquisitionLog log, FhirQuery fhirQuery, SearchParams searchParams, FhirQueryConfiguration fhirQueryConfiguration, ResourceType resourceType, List<string> resourceIds, CancellationToken cancellationToken = default)
    {
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
                    log.QueryPhase),
                cancellationToken))
            {
                var refResources = ReferenceResourceBundleExtractor.Extract(bundle, fhirQuery.ResourceReferenceTypes.Select(x => x.ResourceType).ToList());

                await _referenceResourceService.ProcessReferences(log, refResources, cancellationToken);

                var resources = bundle.Entry.Select(e => e.Resource).ToList();
                resourceIds.AddRange(resources.Select(r => $"{r.TypeName}/{r.Id}"));

                foreach (var resource in resources)
                {
                    if (fhirQuery.isReference.HasValue && fhirQuery.isReference.Value)
                    {
                        await HandleReferenceResource(log, resource, cancellationToken);
                    }

                    await GenerateResourceAcquiredMessage(new ResourceAcquired
                    {
                        Resource = resource,
                        ScheduledReports = new List<ScheduledReport> { log.ScheduledReport },
                        PatientId = log.PatientId,
                        QueryType = log.QueryPhase.ToString(),
                        ReportableEvent = log.ReportableEvent ?? throw new ArgumentNullException(nameof(log.ReportableEvent)),
                    }, log.FacilityId, log.CorrelationId, cancellationToken);
                }
            }

            return resourceIds;
        }
        catch (ProduceException<string, ResourceAcquired> ex)
        {
            _logger.LogError(ex, "Error producing ResourceAcquired message for facility: {FacilityId}", log.FacilityId);
            log.Status = RequestStatus.Failed;
            log.Notes.Add($"[{DateTime.UtcNow}] Error producing ResourceAcquired message for facility: {log.FacilityId}\n{ex.Message}\n{ex.InnerException}");
            await _dataAcquisitionLogManager.UpdateAsync(log, cancellationToken);
            throw new TransientException("Error producing ResourceAcquired message", ex);
        }
        catch (FhirApiFetchFailureException ex)
        {
            _logger.LogError(ex, "Permanent error in paging search for {ResourceType} for facility {FacilityId}", resourceType, log.FacilityId);
            log.Status = RequestStatus.Failed;
            log.Notes.Add($"[{DateTime.UtcNow}] Permanent error in paging search for {resourceType}: {ex.Message}");
            await _dataAcquisitionLogManager.UpdateAsync(log, cancellationToken);
            throw new DeadLetterException($"Permanent error in paging search for {resourceType} for facility {log.FacilityId}", ex);
        }
        catch (TransientException ex)
        {
            _logger.LogWarning(ex, "Transient error in paging search for {ResourceType} for facility {FacilityId}", resourceType, log.FacilityId);
            throw;
        }
    }

    private async Task HandleReferenceResource(DataAcquisitionLog log, Resource resource, CancellationToken cancellationToken)
    {
        if (resource == null) throw new ArgumentNullException(nameof(resource));

        var existingReference = await _referenceResourceManager.GetByResourceIdAndFacilityId(resource.Id, log.FacilityId, cancellationToken);
        if (existingReference == null)
        {
            var newReference = new ReferenceResources
            {
                Id = Guid.NewGuid().ToString(),
                FacilityId = log.FacilityId,
                ResourceId = resource.Id,
                ResourceType = resource.TypeName,
                CreateDate = DateTime.UtcNow,
                ModifyDate = DateTime.UtcNow
            };
            await _referenceResourceManager.AddAsync(newReference, cancellationToken);
            existingReference = newReference;
        }

        existingReference.ReferenceResource = System.Text.Json.JsonSerializer.Serialize(resource, new System.Text.Json.JsonSerializerOptions().ForFhir());
        await _referenceResourceManager.UpdateAsync(existingReference, cancellationToken);
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
        await _kafkaProducer.ProduceAsync(
            KafkaTopic.ResourceAcquired.ToString(),
            new Message<string, ResourceAcquired>
            {
                Key = facilityId,
                Headers = new Headers
                {
                    new Header(DataAcquisitionConstants.HeaderNames.CorrelationId, Encoding.UTF8.GetBytes(correlationId))
                },
                Value = resourceAcquired
            }, cancellationToken);
        _kafkaProducer.Flush(cancellationToken);
    }

    private void RecordResourceAcquiredMetric(string? correlationId, string? patientIdReference, string? facilityId, string? queryType, string resourceType, string resourceId)
    {
        _metrics.IncrementResourceAcquiredCounter([
            new KeyValuePair<string, object?>(DiagnosticNames.CorrelationId, correlationId),
            new KeyValuePair<string, object?>(DiagnosticNames.FacilityId, facilityId),
            new KeyValuePair<string, object?>(DiagnosticNames.PatientId, patientIdReference),
            new KeyValuePair<string, object?>(DiagnosticNames.QueryType, queryType),
            new KeyValuePair<string, object?>(DiagnosticNames.Resource, resourceType),
            new KeyValuePair<string, object?>(DiagnosticNames.ResourceId, resourceId)
        ]);
    }
}