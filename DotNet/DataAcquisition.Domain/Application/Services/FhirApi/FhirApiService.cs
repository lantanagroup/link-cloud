using Confluent.Kafka;
using DataAcquisition.Domain.Application.Models;
using Hl7.Fhir.Model;
using Hl7.Fhir.Rest;
using Hl7.Fhir.Serialization;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Managers;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Factory;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Kafka;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Queries;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Services.FhirApi.Commands;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Entities;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models.Enums;
using LantanaGroup.Link.DataAcquisition.Domain.Models;
using LantanaGroup.Link.DataAcquisition.Domain.Settings;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Utilities;
using System.Net;
using System.Text;
using System.Text.Json;
using DateTime = System.DateTime;
using ResourceType = Hl7.Fhir.Model.ResourceType;
using Task = System.Threading.Tasks.Task;

namespace LantanaGroup.Link.DataAcquisition.Domain.Application.Services.FhirApi;

public interface IFhirApiService
{
    Task<List<string>> ExecuteRead(DataAcquisitionLogModel log, FhirQueryModel fhirQuery, ResourceType resourceType, FhirQueryConfigurationModel fhirQueryConfiguration, List<string> resourceIds, CancellationToken cancellationToken = default);
    Task<List<string>> ExecuteSearch(DataAcquisitionLogModel log, FhirQueryModel fhirQuery, FhirQueryConfigurationModel fhirQueryConfiguration, List<string> resourceIds, ResourceType resourceType, CancellationToken cancellationToken = default);
}

public class FhirApiService : IFhirApiService
{
    private static readonly JsonSerializerOptions _options = new JsonSerializerOptions().ForFhir();

    private readonly IReferenceResourcesManager _referenceResourceManager;
    private readonly IReferenceResourcesQueries _referenceResourcesQueries;
    private readonly IReferenceResourceService _referenceResourceService;
    private readonly IReadFhirCommand _readFhirCommand;
    private readonly ISearchFhirCommand _searchFhirCommand;
    private readonly IProducer<string, ResourceAcquired> _kafkaProducer;

    public FhirApiService(
        IReferenceResourcesManager referenceResourceManager,
        IReferenceResourcesQueries referenceResourcesQueries,
        IReferenceResourceService referenceResourceService,
        ISearchFhirCommand searchFhirCommand,
        IReadFhirCommand readFhirCommand,
        IProducer<string, ResourceAcquired> kafkaProducer)
    {
        _referenceResourceManager = referenceResourceManager;
        _referenceResourcesQueries = referenceResourcesQueries;
        _referenceResourceService = referenceResourceService;
        _searchFhirCommand = searchFhirCommand;
        _readFhirCommand = readFhirCommand;
        _kafkaProducer = kafkaProducer;
    }

    #region Interface Implementation
    public async Task<List<string>> ExecuteRead(DataAcquisitionLogModel log, FhirQueryModel fhirQuery, ResourceType resourceType, FhirQueryConfigurationModel fhirQueryConfiguration, List<string> resourceIds, CancellationToken cancellationToken = default)
    {
        List<string> resourceIdsToAcquire =
            fhirQuery.IsReference.GetValueOrDefault()
            ? fhirQuery.IdQueryParameterValues.ToList()
            : [resourceType == ResourceType.Patient ? log.PatientId.SplitReference() : log.ResourceId];
        foreach (string resourceIdToAcquire in resourceIdsToAcquire)
        {
            await ExecuteRead(log, fhirQuery, resourceType, resourceIdToAcquire, fhirQueryConfiguration, resourceIds, cancellationToken);
        }
        return resourceIds;
    }

    private async Task<List<string>> ExecuteRead(DataAcquisitionLogModel log, FhirQueryModel fhirQuery, ResourceType resourceType, string resourceIdToAcquire, FhirQueryConfigurationModel fhirQueryConfiguration, List<string> resourceIds, CancellationToken cancellationToken = default)
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

            if (fhirQuery.IsReference.HasValue && fhirQuery.IsReference.Value)
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
                ScheduledReports = new List<Shared.Application.Models.ScheduledReport> { log.ScheduledReport },
                PatientId = !fhirQuery.IsReference ?? false ? log.PatientId : null,
                QueryType = log.QueryPhase.ToString(),
                ReportableEvent = log.ReportableEvent ?? throw new ArgumentNullException(nameof(log.ReportableEvent)),
            }, log.FacilityId, log.CorrelationId, cancellationToken);

            return resourceIds;
        }
        catch (FhirOperationException ex)
        {
            if (fhirQuery.IsReference.GetValueOrDefault() && (ex.Status == HttpStatusCode.NotFound || ex.Status == HttpStatusCode.Gone))
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

    public async Task<List<string>> ExecuteSearch(DataAcquisitionLogModel log, FhirQueryModel fhirQuery, FhirQueryConfigurationModel fhirQueryConfiguration, List<string> resourceIds, ResourceType resourceType, CancellationToken cancellationToken = default)
    {
        if (log == null) throw new ArgumentNullException(nameof(log));
        if (fhirQuery == null) throw new ArgumentNullException(nameof(fhirQuery));
        if (fhirQueryConfiguration == null) throw new ArgumentNullException(nameof(fhirQueryConfiguration));
        if (resourceIds == null) throw new ArgumentNullException(nameof(resourceIds));

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
    private async Task<List<string>> ExecutePagingSearch(DataAcquisitionLogModel log, FhirQueryModel fhirQuery, SearchParams searchParams, FhirQueryConfigurationModel fhirQueryConfiguration, ResourceType resourceType, List<string> resourceIds, CancellationToken cancellationToken = default)
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
                    if(fhirQuery.IsReference.HasValue && fhirQuery.IsReference.Value)
                    {
                        //if this is a reference resource, we need to handle it differently
                        await HandleReferenceResource(log, resource, cancellationToken);
                    }

                    InsertDateExtension((DomainResource)resource);

                    await GenerateResourceAcquiredMessage(new ResourceAcquired
                    {
                        Resource = resource,
                        ScheduledReports = new List<Shared.Application.Models.ScheduledReport> { log.ScheduledReport },
                        PatientId = !fhirQuery.IsReference ?? false ? log.PatientId : null,
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

    private async Task HandleReferenceResource(DataAcquisitionLogModel log, Resource resource, CancellationToken cancellationToken)
    {
        if (resource == null) throw new ArgumentNullException(nameof(resource));

        InsertDateExtension((DomainResource)resource);

        //get existing reference resource record
        var existingReference = await _referenceResourcesQueries.GetAsync(resource.Id, log.FacilityId, cancellationToken);

        if (existingReference == null)
        {
            existingReference = await _referenceResourceManager.CreateAsync(new CreateReferenceResourcesModel
            {
                DataAcquisitionLogId = log.Id,
                QueryPhase = QueryPhase.Referential,
                FacilityId = log.FacilityId,
                ResourceId = resource.Id,
                ResourceType = resource.TypeName,
                ReferenceResource = System.Text.Json.JsonSerializer.Serialize(resource, new System.Text.Json.JsonSerializerOptions().ForFhir())
            }, cancellationToken);
        }
        else
        {
            existingReference = await _referenceResourceManager.UpdateAsync(new UpdateReferenceResourcesModel
            {
                Id = existingReference.Id,
                QueryPhase = existingReference.QueryPhase,
                ResourceType = resource.TypeName,
                ReferenceResource = JsonSerializer.Serialize(resource, new JsonSerializerOptions().ForFhir())
            }, cancellationToken);
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
