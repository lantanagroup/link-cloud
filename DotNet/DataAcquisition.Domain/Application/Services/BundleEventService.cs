using System.Text;
using Confluent.Kafka;
using Hl7.Fhir.Model;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Api.Configuration;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Kafka;
using LantanaGroup.Link.DataAcquisition.Domain.Settings;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Models.Kafka;
using Microsoft.Extensions.Logging;
using Task = System.Threading.Tasks.Task;

namespace LantanaGroup.Link.DataAcquisition.Domain.Application.Services;

public record ResourceAcquiredMessageGenerationRequest(string facilityId, string patientId, string queryType, string correlationId, ReportableEvent ReportableEvent, List<ScheduledReport> scheduledReports);

public interface IBundleEventService<EventKey, EventValue, EventRequest>
{
    Task GenerateEventAsync(Bundle bundle, EventRequest request, CancellationToken cancellationToken = default);
}

public class BundleResourceAcquiredEventService : IBundleEventService<ResourceKey, ResourceAcquired, ResourceAcquiredMessageGenerationRequest>
{
    private readonly ILogger<BundleResourceAcquiredEventService> _logger;
    private readonly IProducer<ResourceKey, ResourceAcquired> _producer;

    public BundleResourceAcquiredEventService(ILogger<BundleResourceAcquiredEventService> logger, IProducer<ResourceKey, ResourceAcquired> producer)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _producer = producer ?? throw new ArgumentNullException(nameof(producer));
    }

    public async Task GenerateEventAsync(Bundle bundle, ResourceAcquiredMessageGenerationRequest request, CancellationToken cancellationToken = default)
    {
        foreach (var e in bundle.Entry)
        {
            if (e.Resource is Resource resource)
            {
                await _producer.ProduceAsync(
                    KafkaTopic.ResourceAcquired.ToString(),
                    new Message<ResourceKey, ResourceAcquired>
                    {
                        Key = new ResourceKey
                        {
                            FacilityId = request.facilityId,
                            CorrelationId = request.correlationId
                        },
                        Headers = new Headers
                        {
                            new Header(DataAcquisitionConstants.HeaderNames.CorrelationId, Encoding.UTF8.GetBytes(request.correlationId))
                        },
                        Value = new ResourceAcquired
                        {
                            Resource = resource,
                            ResourceType = resource.TypeName,
                            ScheduledReports = request.scheduledReports,
                            PatientId = RemovePatientId(e.Resource) ? string.Empty : request.patientId,
                            QueryType = request.queryType,
                            ReportableEvent = request.ReportableEvent
                        }
                    });
            }
        }
    }

    private bool RemovePatientId(Resource resource)
    {
        return resource switch
        {
            Location => true,
            Medication => true,
            _ => false,
        };
    }
}
