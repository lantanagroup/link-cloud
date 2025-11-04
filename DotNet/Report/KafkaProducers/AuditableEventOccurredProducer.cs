using Confluent.Kafka;
using LantanaGroup.Link.Report.Settings;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Models.Kafka;
using LantanaGroup.Link.Shared.Settings;
using System.Text;

namespace LantanaGroup.Link.Report.KafkaProducers
{
    public class AuditableEventOccurredProducer(
        ILogger<AuditableEventOccurredProducer> _logger,
        IProducer<string, AuditEventMessage> _producer)
    {
        public async Task ProduceAsync(AuditEventMessage model, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(model);
            _logger.LogInformation("Producing audit event: {Notes}", model.Notes);
            try
            {
                Headers headers = [];
                if (!string.IsNullOrEmpty(model.CorrelationId))
                {
                    headers.Add(KafkaConstants.HeaderConstants.CorrelationId, Encoding.ASCII.GetBytes(model.CorrelationId));
                }
                model.ServiceName = ReportConstants.ServiceName;
                await _producer.ProduceAsync(nameof(KafkaTopic.AuditableEventOccurred), new Message<string, AuditEventMessage>
                {
                    Headers = headers,
                    Key = model.FacilityId ?? "",
                    Value = model
                }, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to produce audit event.");
            }
        }
    }
}
