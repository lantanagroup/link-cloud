using Confluent.Kafka;
using Hl7.Fhir.Model;
using LantanaGroup.Link.DataAcquisition.Application.Interfaces;
using LantanaGroup.Link.DataAcquisition.Application.Models;
using LantanaGroup.Link.DataAcquisition.Application.Models.Factory.ReferenceQuery;
using LantanaGroup.Link.DataAcquisition.Application.Models.Kafka;
using LantanaGroup.Link.DataAcquisition.Application.Repositories;
using LantanaGroup.Link.DataAcquisition.Application.Serializers;
using LantanaGroup.Link.DataAcquisition.Application.Services.FhirApi;
using LantanaGroup.Link.DataAcquisition.Domain.Entities;
using LantanaGroup.Link.DataAcquisition.Domain.Models.QueryConfig;
using LantanaGroup.Link.DataAcquisition.Domain.Settings;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Models.Telemetry;
using LantanaGroup.Link.Shared.Application.Utilities;
using System.Text;
using Task = System.Threading.Tasks.Task;

namespace LantanaGroup.Link.DataAcquisition.Application.Services;

public interface IReferenceResourceService
{
    Task<List<Resource>> Execute_NoKafka(
        ReferenceQueryFactoryResult referenceQueryFactoryResult,
        GetPatientDataRequest request,
        FhirQueryConfiguration fhirQueryConfiguration,
        ReferenceQueryConfig referenceQueryConfig,
        string queryPlanType,
        CancellationToken cancellationToken = default);

    Task Execute(
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

    public ReferenceResourceService(
        ILogger<ReferenceResourceService> logger,
        IReferenceResourcesManager referenceResourcesManager,
        IFhirApiService fhirRepo,
        IProducer<string, ResourceAcquired> kafkaProducer,
        IDataAcquisitionServiceMetrics metrics)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _referenceResourcesManager = referenceResourcesManager ?? throw new ArgumentNullException(nameof(referenceResourcesManager));
        _fhirRepo = fhirRepo ?? throw new ArgumentNullException(nameof(fhirRepo));
        _kafkaProducer = kafkaProducer ?? throw new ArgumentNullException(nameof(kafkaProducer));
        _metrics = metrics ?? throw new ArgumentNullException(nameof(metrics));
    }

    public async Task<List<Resource>> Execute_NoKafka(ReferenceQueryFactoryResult referenceQueryFactoryResult, GetPatientDataRequest request, FhirQueryConfiguration fhirQueryConfiguration, ReferenceQueryConfig referenceQueryConfig, string queryPlanType, CancellationToken cancellationToken = default)
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

    public async Task Execute(
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
            await _fhirRepo.GetReferenceResourceAndGenerateMessage(
            fhirQueryConfiguration.FhirServerBaseUrl,
            referenceQueryFactoryResult.ResourceType,
            request,
            queryPlanType,
            x,
            referenceQueryConfig,
            fhirQueryConfiguration.Authentication);
        }
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
