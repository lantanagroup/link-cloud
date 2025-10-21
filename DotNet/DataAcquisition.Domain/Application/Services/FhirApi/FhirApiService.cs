using Confluent.Kafka;
using Hl7.Fhir.Model;
using Hl7.Fhir.Rest;
using Hl7.Fhir.Serialization;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Interfaces;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Managers;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Factory;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Kafka;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Queries;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Services.FhirApi.Commands;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Entities;
using LantanaGroup.Link.DataAcquisition.Domain.Settings;
using LantanaGroup.Link.Shared.Application.Error.Exceptions;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Models.Telemetry;
using LantanaGroup.Link.Shared.Application.Services.Security;
using LantanaGroup.Link.Shared.Application.Utilities;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Text;
using System.Text.Json;
using DateTime = System.DateTime;
using RequestStatus = LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models.Enums.RequestStatus;
using StringComparison = System.StringComparison;
using Task = System.Threading.Tasks.Task;

namespace LantanaGroup.Link.DataAcquisition.Domain.Application.Services.FhirApi;

public interface IFhirApiService
{
    Task<List<string>> ExecuteRead(DataAcquisitionLog log, FhirQuery fhirQuery, ResourceType resourceType, FhirQueryConfiguration fhirQueryConfiguration, List<string> resourceIds, CancellationToken cancellationToken = default);
    Task<List<string>> ExecuteSearch(DataAcquisitionLog log, FhirQuery fhirQuery, FhirQueryConfiguration fhirQueryConfiguration, List<string> resourceIds, ResourceType resourceType, CancellationToken cancellationToken = default);
}

public class FhirApiService : IFhirApiService
{
    private static readonly JsonSerializerOptions _options = new JsonSerializerOptions().ForFhir();

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

    #region Interface Implementation
    public async Task<List<string>> ExecuteRead(DataAcquisitionLog log, FhirQuery fhirQuery, ResourceType resourceType, FhirQueryConfiguration fhirQueryConfiguration, List<string> resourceIds, CancellationToken cancellationToken = default)
    {
        List<string> resourceIdsToAcquire =
            fhirQuery.isReference.GetValueOrDefault()
            ? fhirQuery.IdQueryParameterValues.ToList()
            : [resourceType == ResourceType.Patient ? log.PatientId.SplitReference() : log.ResourceId];
        foreach (string resourceIdToAcquire in resourceIdsToAcquire)
        {
            await ExecuteRead(log, fhirQuery, resourceType, resourceIdToAcquire, fhirQueryConfiguration, resourceIds, cancellationToken);
        }
        return resourceIds;
    }

    private async Task<List<string>> ExecuteRead(DataAcquisitionLog log, FhirQuery fhirQuery, ResourceType resourceType, string resourceIdToAcquire, FhirQueryConfiguration fhirQueryConfiguration, List<string> resourceIds, CancellationToken cancellationToken = default)
    {
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

            if (fhirQuery.isReference.HasValue && fhirQuery.isReference.Value)
            {
                //if this is a reference resource, we need to handle it differently
                await HandleReferenceResource(log, resource, cancellationToken);
            }

            InsertDateExtension(resource);

            //get references
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
        catch (FhirOperationException ex)
        {
            if (fhirQuery.isReference.GetValueOrDefault() && (ex.Status == HttpStatusCode.NotFound || ex.Status == HttpStatusCode.Gone))
            {
                return resourceIds;
            }
            if (ex.Outcome != null)
            {
                string json = JsonSerializer.Serialize(ex.Outcome, _options);
                (log.Notes ?? []).Add($"OperationOutcome returned for HTTP {ex.Status}: {json}");
            }
            throw;
        }
    }

    public async Task<List<string>> ExecuteSearch(DataAcquisitionLog log, FhirQuery fhirQuery, FhirQueryConfiguration fhirQueryConfiguration, List<string> resourceIds, ResourceType resourceType, CancellationToken cancellationToken = default)
    {
        if (log == null) throw new ArgumentNullException(nameof(log));
        if (fhirQuery == null) throw new ArgumentNullException(nameof(fhirQuery));
        if (fhirQueryConfiguration == null) throw new ArgumentNullException(nameof(fhirQueryConfiguration));
        if (resourceIds == null) throw new ArgumentNullException(nameof(resourceIds));

        if (fhirQuery.isReference.GetValueOrDefault())
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
                await ExecutePagingSearch(log, fhirQuery, searchParams, fhirQueryConfiguration, resourceType, resourceIds, cancellationToken);
            }
            return resourceIds;
        }
        else
        {
            var searchParams = BuildSearchParams(fhirQuery.QueryParameters);
            return await ExecutePagingSearch(log, fhirQuery, searchParams, fhirQueryConfiguration, resourceType, resourceIds, cancellationToken);
        }
    }
    #endregion

    #region Private Methods
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
                    if(fhirQuery.isReference.HasValue && fhirQuery.isReference.Value)
                    {
                        //if this is a reference resource, we need to handle it differently
                        await HandleReferenceResource(log, resource, cancellationToken);
                    }

                    InsertDateExtension((DomainResource)resource);

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
        catch (FhirOperationException ex)
        {
            if (ex.Outcome != null)
            {
                string json = JsonSerializer.Serialize(ex.Outcome, _options);
                (log.Notes ?? []).Add($"OperationOutcome returned for HTTP {ex.Status}: {json}");
            }
            throw;
        }
    }

    private async Task HandleReferenceResource(DataAcquisitionLog log, Resource resource, CancellationToken cancellationToken)
    {
        if (resource == null) throw new ArgumentNullException(nameof(resource));

        InsertDateExtension((DomainResource)resource);

        //get existing reference resource record
        var existingReference = await _referenceResourceManager.GetByResourceIdAndFacilityId(resource.Id, log.FacilityId, cancellationToken);

        if (existingReference == null)
        {
            //if it doesn't exist, create a new one
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
        //existingReference.ReferenceResource.
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

    private void InsertDateExtension(DomainResource resource) 
    {
        if(resource == null)
            throw new ArgumentNullException(nameof(resource));

        if(resource.Meta == null)
        {
            resource.Meta = new Meta();
            resource.Meta.Extension = new List<Extension> { };
        }

        if(resource.Meta.Extension == null)
            resource.Meta.Extension = new List<Extension> { };

        if (!resource.Extension.Any(e => e.Url == DataAcquisitionConstants.Extension.DateReceivedExtensionUri))
            resource.Meta.Extension.Add(new Extension { Url = DataAcquisitionConstants.Extension.DateReceivedExtensionUri, Value =  new FhirDateTime(DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"))});
    }
    #endregion
}
