using Confluent.Kafka;
using Hl7.Fhir.Model;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Interfaces;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Managers;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Factory.ReferenceQuery;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Kafka;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Serializers;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Services.FhirApi;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Entities;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models.QueryConfig;
using LantanaGroup.Link.DataAcquisition.Domain.Settings;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Models.Telemetry;
using LantanaGroup.Link.Shared.Application.Utilities;
using Microsoft.Extensions.Logging;
using System.Text;
using Task = System.Threading.Tasks.Task;
using RequestStatus = LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models.Enums.RequestStatus;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models.Enums;
using ResourceType = Hl7.Fhir.Model.ResourceType;

namespace LantanaGroup.Link.DataAcquisition.Domain.Application.Services;

public interface IReferenceResourceService
{
    Task<List<Resource>> FetchReferenceResources(
        ReferenceQueryFactoryResult referenceQueryFactoryResult,
        GetPatientDataRequest request,
        FhirQueryConfiguration fhirQueryConfiguration,
        ReferenceQueryConfig referenceQueryConfig,
        string queryPlanType,
        CancellationToken cancellationToken = default);

    Task CreateDataAcquisitionLogEntries(
        ReferenceQueryFactoryResult referenceQueryFactoryResult,
        GetPatientDataRequest request,
        FhirQueryConfiguration fhirQueryConfiguration,
        ReferenceQueryConfig referenceQueryConfig,
        string queryPlanType,
        CancellationToken cancellationToken = default);
}

public class ReferenceResourceService : IReferenceResourceService
{
    private readonly ILogger<ReferenceResourceService> _logger;
    private readonly IReferenceResourcesManager _referenceResourcesManager;
    private readonly IFhirApiService _fhirRepo;
    private readonly IProducer<string, ResourceAcquired> _kafkaProducer;
    private readonly IDataAcquisitionServiceMetrics _metrics;
    private readonly IDataAcquisitionLogManager _dataAcquisitionLogManager;

    public ReferenceResourceService(
        ILogger<ReferenceResourceService> logger,
        IReferenceResourcesManager referenceResourcesManager,
        IFhirApiService fhirRepo,
        IProducer<string, ResourceAcquired> kafkaProducer,
        IDataAcquisitionServiceMetrics metrics,
        IDataAcquisitionLogManager dataAcquisitionLogManager)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _referenceResourcesManager = referenceResourcesManager ?? throw new ArgumentNullException(nameof(referenceResourcesManager));
        _fhirRepo = fhirRepo ?? throw new ArgumentNullException(nameof(fhirRepo));
        _kafkaProducer = kafkaProducer ?? throw new ArgumentNullException(nameof(kafkaProducer));
        _metrics = metrics ?? throw new ArgumentNullException(nameof(metrics));
        _dataAcquisitionLogManager = dataAcquisitionLogManager ?? throw new ArgumentNullException(nameof(dataAcquisitionLogManager));
    }

    public async Task<List<Resource>> FetchReferenceResources(ReferenceQueryFactoryResult referenceQueryFactoryResult, GetPatientDataRequest request, FhirQueryConfiguration fhirQueryConfiguration, ReferenceQueryConfig referenceQueryConfig, string queryPlanType, CancellationToken cancellationToken = default)
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

        var existingReferenceResources =
            await _referenceResourcesManager.GetReferenceResourcesForListOfIds(
                validReferenceResources.Select(x => x.Reference.SplitReference()).ToList(),
                request.FacilityId);

        resources.AddRange(existingReferenceResources.Select(x => FhirResourceDeserializer.DeserializeFhirResource(x)));

        List<ResourceReference> missingReferences = validReferenceResources
            .Where(x => !existingReferenceResources.Any(y => y.ResourceId == x.Reference.SplitReference())).ToList();

        foreach(var x in missingReferences)
        {
            var fullMissingResources = await _fhirRepo.GetReferenceResource(
                fhirQueryConfiguration.FhirServerBaseUrl,
                referenceQueryFactoryResult.ResourceType,
                request.ConsumeResult.Message.Value.PatientId,
                request.FacilityId,
                request.CorrelationId,
                queryPlanType,
                x,
                referenceQueryConfig,
                fhirQueryConfiguration.Authentication);

            resources.AddRange(fullMissingResources);
        }

        return resources;
    }

    public async Task CreateDataAcquisitionLogEntries(
        ReferenceQueryFactoryResult referenceQueryFactoryResult, 
        GetPatientDataRequest request,
        FhirQueryConfiguration fhirQueryConfiguration,
        ReferenceQueryConfig referenceQueryConfig,
        string queryPlanType,
        CancellationToken cancellationToken = default)
    {
        if (referenceQueryFactoryResult.ReferenceIds?.Count == 0)
        {
            return;
        }

        var validReferenceResources = 
            referenceQueryFactoryResult
            ?.ReferenceIds
            ?.Where(x => x.TypeName == referenceQueryConfig.ResourceType || x.Reference.StartsWith(referenceQueryConfig.ResourceType, StringComparison.InvariantCultureIgnoreCase))
            .DistinctBy(x => x.Url.ToString())
            .ToList();

        var existingReferenceResources =
            await _referenceResourcesManager.GetReferenceResourcesForListOfIds(
                validReferenceResources.Select(x => x.Reference.SplitReference()).ToList(),
                request.FacilityId);

        foreach(var existingReference in existingReferenceResources)
        {          
            await GenerateMessage(
            FhirResourceDeserializer.DeserializeFhirResource(existingReference),
            request.FacilityId,
            request.ConsumeResult.Message.Value.PatientId,
            queryPlanType,
            request.CorrelationId,
            request.ConsumeResult.Message.Value.ScheduledReports);

            // Increment metric for resource acquired
            IncrementResourceAcquiredMetric(request.CorrelationId, request.ConsumeResult.Message.Value.PatientId.SplitReference(), request.FacilityId,
                queryPlanType, referenceQueryFactoryResult.ResourceType, existingReference.ResourceId);
        }            

        List<ResourceReference> missingReferences = validReferenceResources
            .Where(x => !existingReferenceResources.Any(y => y.ResourceId.Equals(x.Reference.SplitReference(), StringComparison.InvariantCultureIgnoreCase))).ToList();

        foreach(var x in missingReferences)
        {
            foreach(var s in request.ConsumeResult.Message.Value.ScheduledReports)
            {
                foreach (var report in s.ReportTypes)
                {
                    if(referenceQueryConfig.OperationType == null)
                    {
                        throw new ArgumentNullException(nameof(referenceQueryConfig.OperationType), "OperationType cannot be null");
                    }

                    List<List<string>> queryParameters = referenceQueryConfig.OperationType switch
                    {
                        OperationType.Search => BuildReferenceQueryParameters(referenceQueryFactoryResult),
                        OperationType.Read => null,
                        _ => throw new NotImplementedException()
                    };

                    foreach(var queryParameter in queryParameters)
                    {
                        await _dataAcquisitionLogManager.CreateAsync(new DataAcquisitionLog
                        {
                            CorrelationId = request.CorrelationId,
                            FacilityId = request.FacilityId,
                            PatientId = request.ConsumeResult.Message.Value.PatientId,
                            ResourceId = x.Reference.SplitReference(),
                            ScheduledReport = s,
                            QueryPhase = Infrastructure.Models.Enums.QueryPhase.Referential,
                            Status = RequestStatus.Pending,
                            ExecutionDate = DateTime.UtcNow,
                            FhirQuery = new List<FhirQuery> { 
                                new FhirQuery
                                {
                                    FacilityId = request.FacilityId,
                                    isReference = true,
                                    MeasureId = report,
                                    QueryParameters = queryParameter,
                                    Paged = referenceQueryConfig.Paged,
                                    ResourceTypes = new List<ResourceType> { Enum.Parse<ResourceType>(referenceQueryFactoryResult.ResourceType) }
                                }
                            },
                            Priority = Infrastructure.Models.Enums.AcquisitionPriority.Normal,
                            QueryType = FhirQueryTypeUtilities.ToDomain(queryPlanType),
                            TimeZone = fhirQueryConfiguration.TimeZone,
                            ReportableEvent = request.ConsumeResult.Message.Value.ReportableEvent,
                        }, cancellationToken);
                    }

                    
                }
            }
        }
    }

    private List<List<string>> BuildReferenceQueryParameters(ReferenceQueryFactoryResult referenceQueryFactoryResult)
    {
        var counter = 1;
        var maxSearchIdCount = 20; //probably should be a config. This represents the max number of ids that can be included in a single query.
        var retList = new List<List<string>>();

        while(counter <=  referenceQueryFactoryResult.ReferenceIds.Count/20)
        {
            var chunk = referenceQueryFactoryResult.ReferenceIds
                .Skip((counter - 1) * maxSearchIdCount)
                .Take(maxSearchIdCount)
                .Select(x => x.Identifier.ElementId )
                .ToList();

            retList.Add(new List<string>{ $"_id={string.Join(",", chunk)}" });
        }

        return retList;
    }

    private async Task GenerateMessage(
            Resource resource,
            string facilityId,
            string patientId,
            string queryType,
            string correlationId,
            List<ScheduledReport> scheduledReports)
    {
        var producerConfig = new ProducerConfig();
        producerConfig.CompressionType = CompressionType.Zstd;

        await _kafkaProducer.ProduceAsync(
            KafkaTopic.ResourceAcquired.ToString(),
            new Message<string, ResourceAcquired>
            {
                Key = facilityId,
                Headers = new Headers
                {
                    new Header(DataAcquisitionConstants.HeaderNames.CorrelationId, Encoding.UTF8.GetBytes(correlationId))
                },
                Value = new ResourceAcquired
                {
                    Resource = resource,
                    ScheduledReports = scheduledReports,
                    PatientId = string.Empty,
                    QueryType = queryType
                }
            });

    }

    private void IncrementResourceAcquiredMetric(string? correlationId, string? patientIdReference, string? facilityId, string? queryType, string resourceType, string resourceId)
    {
        _metrics.IncrementResourceAcquiredCounter([
            new KeyValuePair<string, object?>(DiagnosticNames.CorrelationId, correlationId),
            new KeyValuePair<string, object?>(DiagnosticNames.FacilityId, facilityId),
            new KeyValuePair<string, object?>(DiagnosticNames.PatientId, patientIdReference), //TODO: Can we keep this?
            new KeyValuePair<string, object?>(DiagnosticNames.QueryType, queryType),
            new KeyValuePair<string, object?>(DiagnosticNames.Resource, resourceType),
            new KeyValuePair<string, object?>(DiagnosticNames.ResourceId, resourceId)
        ]);
    }
}
