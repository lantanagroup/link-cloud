using Confluent.Kafka;
using LantanaGroup.Link.Report.Application.Error.Exceptions;
using LantanaGroup.Link.Report.Application.Error.Interfaces;
using LantanaGroup.Link.Shared.Application.Interfaces;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Models.Kafka;

namespace LantanaGroup.Link.QueryDispatch.Application.Errors.Handlers
{
    public class ReportExceptionHandler<K, V> : IReportExceptionHandler<K, V>
    {
        private readonly ILogger<ReportExceptionHandler<K, V>> _logger;
        private readonly IKafkaProducerFactory<string, AuditEventMessage> _auditProducerFactory;

        public ReportExceptionHandler(ILogger<ReportExceptionHandler<K, V>> logger, IKafkaProducerFactory<string, AuditEventMessage> auditProducerFactory)
        {
            _logger = logger;
            _auditProducerFactory = auditProducerFactory;
        }

        public void HandleException(ConsumeResult<K, V> consumeResult, Exception ex)
        {
            _logger.LogError(message:"Failed to process Report Scheduled Event.", exception: ex);

            var auditValue = new AuditEventMessage
            {
                FacilityId = consumeResult.Message.Key as string,
                Action = AuditEventType.Query,
                ServiceName = "Report",
                EventDate = DateTime.UtcNow,
                Notes = $"Report Scheduled processing failure \nException Message: {ex.Message}",
            };

            ProduceAuditEvent(_auditProducerFactory, auditValue, consumeResult.Message.Headers);
        }

        private void ProduceAuditEvent(IKafkaProducerFactory<string, AuditEventMessage> auditProducerFactory, AuditEventMessage auditValue, Headers headers)
        {
            using var producer = auditProducerFactory.CreateAuditEventProducer();
            producer.Produce(nameof(KafkaTopic.AuditableEventOccurred), new Message<string, AuditEventMessage>
            {
                Value = auditValue,
                Headers = headers
            });
            producer.Flush();
        }
    }
}