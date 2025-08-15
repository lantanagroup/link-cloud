using Confluent.Kafka;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Models.Kafka;
using LantanaGroup.Link.Tenant.Services;
using System.Text;

namespace LantanaGroup.Link.Tenant.Commands
{
    public class CreateAuditEventCommand
    {
        private readonly ILogger<CreateAuditEventCommand> _logger;
        private readonly IProducer<string, AuditEventMessage> _producer;

        public CreateAuditEventCommand(ILogger<CreateAuditEventCommand> logger, IProducer<string, AuditEventMessage> producer)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _producer = producer ?? throw new ArgumentNullException(nameof(producer));
        }


        public async void Execute(string facilityId, AuditEventMessage auditEvent, CancellationToken cancellationToken = default)
        {

            using (ServiceActivitySource.Instance.StartActivity("Produce Audit Event"))
            {

                try
                {
                    // send the Audit Event
                    Headers headers = new Headers();
                    headers.Add("X-Correlation-Id", Encoding.ASCII.GetBytes(auditEvent.CorrelationId ?? Guid.NewGuid().ToString()));

                    await _producer.ProduceAsync(KafkaTopic.AuditableEventOccurred.ToString(), new Message<string, AuditEventMessage>
                    {
                        Key = facilityId,
                        Value = auditEvent,
                        Headers = headers
                    });

                }
                catch (Exception ex)
                {
                    _logger.LogError($"Failed to generate an audit event for create of facility configuration {facilityId}.", ex);
                }
            }
        }
    }
}