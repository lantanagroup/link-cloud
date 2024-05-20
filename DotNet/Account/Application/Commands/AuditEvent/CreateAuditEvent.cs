using Confluent.Kafka;
using LantanaGroup.Link.Account.Infrastructure;
using LantanaGroup.Link.Account.Infrastructure.Logging;
using LantanaGroup.Link.Account.Settings;
using LantanaGroup.Link.Shared.Application.Interfaces;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Models.Kafka;
using OpenTelemetry.Trace;
using System.Diagnostics;
using System.Text;

namespace LantanaGroup.Link.Account.Application.Commands.AuditEvent
{
    public class CreateAuditEvent : ICreateAuditEvent
    {
        private readonly ILogger<CreateAuditEvent> _logger;
        private readonly IKafkaProducerFactory<string, object> _kafkaProducerFactory;

        public CreateAuditEvent(ILogger<CreateAuditEvent> logger, IKafkaProducerFactory<string, object> kafkaProducerFactory)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _kafkaProducerFactory = kafkaProducerFactory ?? throw new ArgumentNullException(nameof(kafkaProducerFactory));
        }

        public async Task<bool> Execute(AuditEventMessage model, CancellationToken cancellationToken = default)
        {
            using Activity? activity = ServiceActivitySource.Instance.StartActivity("CreateAuditEvent:Execute");

            using var producer = _kafkaProducerFactory.CreateAuditEventProducer();

            try
            {
                model.ServiceName = AccountConstants.ServiceName;

                // send the Audit Event
                Headers headers = [];

                if(!string.IsNullOrEmpty(model.CorrelationId))
                {
                    headers.Add("X-Correlation-Id", Encoding.ASCII.GetBytes(model.CorrelationId));
                }

                await producer.ProduceAsync(nameof(KafkaTopic.AuditableEventOccurred), new Message<string, AuditEventMessage>
                {
                    Key = model.FacilityId ?? string.Empty,
                    Value = model,
                    Headers = headers
                }, cancellationToken);

                _logger.LogAuditEventCreated(model);

                return true;
            }
            catch (Exception ex)
            {
                Activity.Current?.SetStatus(ActivityStatusCode.Error);
                Activity.Current?.RecordException(ex);
                _logger.LogAuditEventCreationException(ex.Message, model);
                throw;
            }              
        }
    }
}
