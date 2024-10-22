using Confluent.Kafka;
using Hl7.Fhir.Model;
using LantanaGroup.Link.Normalization.Application.Models.Messages;
using LantanaGroup.Link.Shared.Application.Interfaces;
using LantanaGroup.Link.Shared.Application.Models;
using System.Text;

namespace LantanaGroup.Link.Normalization.Application.Services
{
    public class TriggerAuditEventCommand
    {
        public string Notes { get; set; }
        public string FacilityId { get; set; }
        public string CorrelationId { get; set; }

        public ResourceAcquiredMessage? resourceAcquiredMessage { get; set; }
        public List<Shared.Application.Models.Kafka.PropertyChangeModel> PropertyChanges { get; set; }
    }

    public interface IAuditService
    {
        System.Threading.Tasks.Task TriggerAuditEvent(TriggerAuditEventCommand request, CancellationToken cancellationToken);
    }

    public class AuditService : IAuditService
    {
        private readonly ILogger<AuditService> _logger;
        private readonly IProducer<string, Shared.Application.Models.Kafka.AuditEventMessage> _producer;

        public AuditService(ILogger<AuditService> logger,IProducer<string, Shared.Application.Models.Kafka.AuditEventMessage> producer)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _producer = producer ?? throw new ArgumentNullException(nameof(producer));
        }


        public async System.Threading.Tasks.Task TriggerAuditEvent(TriggerAuditEventCommand request, CancellationToken cancellationToken)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));

            var headers = new Headers
            {
                { "X-Correlation-Id", Encoding.ASCII.GetBytes(request.CorrelationId) }
            };

            try
            {
                await _producer.ProduceAsync(KafkaTopic.AuditableEventOccurred.ToString(),
                    new Message<string, Shared.Application.Models.Kafka.AuditEventMessage>
                    {
                        Key = request.FacilityId,
                        Headers = headers,
                        Value = new Shared.Application.Models.Kafka.AuditEventMessage
                        {
                            Notes = request.Notes ?? "",
                            Action = Shared.Application.Models.AuditEventType.Update,
                            EventDate = DateTime.UtcNow,
                            ServiceName = "NormalizationService",
                            PropertyChanges = request.PropertyChanges,
                            Resource = nameof(Bundle),
                        }
                    }, cancellationToken
                );
            }
            catch (ProduceException<string, Shared.Application.Models.Kafka.AuditEventMessage> ex)
            {
                _logger.LogError(ex, "Error producing audit event to Kafka.");
                throw;
            }
        }
    }
}
