using Confluent.Kafka;
using Hl7.Fhir.Model;
using Hl7.Fhir.Serialization;
using LantanaGroup.Link.DataAcquisition.Application.Models;
using LantanaGroup.Link.DataAcquisition.Application.Models.Factory.ReferenceQuery;
using LantanaGroup.Link.DataAcquisition.Application.Models.Kafka;
using LantanaGroup.Link.DataAcquisition.Application.Repositories;
using LantanaGroup.Link.DataAcquisition.Application.Services.FhirApi;
using LantanaGroup.Link.DataAcquisition.Domain.Entities;
using LantanaGroup.Link.DataAcquisition.Domain.Models.QueryConfig;
using LantanaGroup.Link.DataAcquisition.Domain.Settings;
using LantanaGroup.Link.Shared.Application.Interfaces;
using LantanaGroup.Link.Shared.Application.Models;
using System.Text;
using System.Text.Json;
using Task = System.Threading.Tasks.Task;

namespace LantanaGroup.Link.DataAcquisition.Application.Services;

public interface IReferenceResourceService
{
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
    private readonly IKafkaProducerFactory<string, ResourceAcquired> _kafkaProducerFactory;

    public ReferenceResourceService(
        ILogger<ReferenceResourceService> logger,
        IReferenceResourcesManager referenceResourcesManager,
        IFhirApiService fhirRepo,
        IKafkaProducerFactory<string, ResourceAcquired> kafkaProducerFactory)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _referenceResourcesManager = referenceResourcesManager ?? throw new ArgumentNullException(nameof(referenceResourcesManager));
        _fhirRepo = fhirRepo ?? throw new ArgumentNullException(nameof(fhirRepo));
        _kafkaProducerFactory = kafkaProducerFactory ?? throw new ArgumentNullException(nameof(kafkaProducerFactory));
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

        var validReferenceResources = referenceQueryFactoryResult.ReferenceIds.Where(x => x.TypeName == referenceQueryConfig.ResourceType).ToList();

        var existingReferenceResources =
            await _referenceResourcesManager.GetReferenceResourcesForListOfIds(
                validReferenceResources.Select(x => x.ElementId).ToList(),
                request.FacilityId);

        existingReferenceResources.ForEach(x => GenerateMessage(
            System.Text.Json.JsonSerializer.Deserialize<DomainResource>(x.ReferenceResource),
            request.ConsumeResult.Message.Value.PatientId,
            queryPlanType,
            request.CorrelationId,
            request.ConsumeResult.Message.Value.ScheduledReports));
            

        List<ResourceReference> missingReferences = validReferenceResources
            .Where(x => !existingReferenceResources.Any(y => y.ResourceId == x.ElementId)).ToList();

        var fullMissingResources = await _fhirRepo.GetReferenceResource(
            fhirQueryConfiguration.FhirServerBaseUrl,
            referenceQueryFactoryResult.ResourceType,
            request.ConsumeResult.Message.Value.PatientId,
            request.FacilityId,
            request.CorrelationId,
            queryPlanType,
            missingReferences,
            referenceQueryConfig,
            fhirQueryConfiguration.Authentication);

        var fullRefResourceList = new List<ReferenceResources>();
        fullRefResourceList.AddRange(existingReferenceResources);

        var jsonOptions = new JsonSerializerOptions().ForFhir(ModelInfo.ModelInspector);
        var currentDateTime = DateTime.UtcNow;
        var fhirDateTime = new FhirDateTime(
            currentDateTime.Year,
            currentDateTime.Month,
            currentDateTime.Day,
            currentDateTime.Hour,
            currentDateTime.Minute,
            currentDateTime.Second,
            TimeSpan.Zero);

        foreach (var resource in fullMissingResources)
        {
            if (resource.TypeName == nameof(OperationOutcome))
            {
                var opOutcome = (OperationOutcome)resource;
                var message = $"Operation Outcome encountered:\n {opOutcome.Text}";
                _logger.LogWarning(message);
                continue;
            }

            var refResource = new ReferenceResources
            {
                FacilityId = request.FacilityId,
                ResourceId = resource.Id,
                ReferenceResource = System.Text.Json.JsonSerializer.Serialize(resource, jsonOptions),
                ResourceType = referenceQueryFactoryResult.ResourceType,
                CreateDate = currentDateTime,
                ModifyDate = currentDateTime,
            };

            GenerateMessage(
            resource,
            request.ConsumeResult.Message.Value.PatientId,
            queryPlanType,
            request.CorrelationId,
            request.ConsumeResult.Message.Value.ScheduledReports);

            await _referenceResourcesManager.AddAsync(refResource);
        }
    }

    private void GenerateMessage(
            Resource resource,
            string patientId,
            string queryType,
            string correlationId,
            List<ScheduledReport> scheduledReports)
    {
        var producerConfig = new ProducerConfig();
        producerConfig.CompressionType = CompressionType.Zstd;

        _kafkaProducerFactory.CreateProducer(producerConfig).Produce(
            KafkaTopic.ResourceAcquired.ToString(),
            new Message<string, ResourceAcquired>
            {
                Key = patientId,
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
}
