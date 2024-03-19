using Confluent.Kafka;
using LantanaGroup.Link.Report.Application.Error.Interfaces;
using LantanaGroup.Link.Report.Application.Models;
using LantanaGroup.Link.Shared.Application.Interfaces;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Models.Kafka;

namespace LantanaGroup.Link.Report.Application.Error.Handlers
{
    public class ReportTransientExceptionHandler<K, V> : IReportTransientExceptionHandler<K, V>
    {
        private readonly ILogger<ReportTransientExceptionHandler<K, V>> _logger;
        private readonly IKafkaProducerFactory<string, AuditEventMessage> _auditProducerFactory;
        private readonly IKafkaProducerFactory<K, V> _producerFactory;

        public string Topic { get; set; }

        public ReportTransientExceptionHandler(ILogger<ReportTransientExceptionHandler<K, V>> logger,
            IKafkaProducerFactory<string, AuditEventMessage> auditProducerFactory,
            IKafkaProducerFactory<K, V> producerFactory)
        {
            _logger = logger;
            _auditProducerFactory = auditProducerFactory;
            _producerFactory = producerFactory;

            if (typeof(K) == typeof(MeasureReportScheduledKey))
            {
                Topic = "ReportScheduled-Retry";
            }
            else if (typeof(K) == typeof(ReportSubmittedKey))
            {
                Topic = "ReportSubmitted-Retry";
            }
            else if (typeof(K) == typeof(MeasureEvaluatedKey))
            {
                Topic = "MeasureEvaluated-Retry";
            }
            else if (typeof(V) == typeof(PatientsToQueryValue))
            {
                Topic = "PatientsToQuery-Retry";
            }
            else
            {
                throw new NotImplementedException("ReportTransientExceptionHandler<K, V>: Type K is not configured for Retry.");
            }
        }

        public void HandleException(ConsumeResult<K, V> consumeResult, Exception ex)
        {
            _logger.LogError(message: "Failed to process Report Event.", exception: ex);

            var auditValue = new AuditEventMessage
            {
                FacilityId = consumeResult.Message.Key as string,
                Action = AuditEventType.Query,
                ServiceName = "Report",
                EventDate = DateTime.UtcNow,
                Notes = $"Report processing failure \nException Message: {ex.Message}",
            };

            ProduceAuditEvent(_auditProducerFactory, auditValue, consumeResult.Message.Headers);
            ProduceRetryReportScheduledEvent(consumeResult.Message.Key, consumeResult.Message.Value, consumeResult.Message.Headers);
        }

        private void ProduceAuditEvent(IKafkaProducerFactory<string, AuditEventMessage> auditProducerFactory, AuditEventMessage auditValue, Headers headers)
        {
            using (var producer = auditProducerFactory.CreateAuditEventProducer())
            {
                producer.Produce(nameof(KafkaTopic.AuditableEventOccurred), new Message<string, AuditEventMessage>
                {
                    Value = auditValue,
                    Headers = headers
                });
                producer.Flush();
            }
        }

        private void ProduceRetryReportScheduledEvent(K key, V value, Headers headers)
        {
            using var producer = _producerFactory.CreateProducer(new ProducerConfig());
            producer.Produce(Topic, new Message<K, V>
            {
                Key = key,
                Value = value,
                Headers = headers
            });
            producer.Flush();
        }
    }
}