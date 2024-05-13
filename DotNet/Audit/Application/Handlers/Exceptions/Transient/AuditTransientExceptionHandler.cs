using Confluent.Kafka;
using LantanaGroup.Link.Audit.Infrastructure;
using LantanaGroup.Link.Audit.Infrastructure.Logging;
using LantanaGroup.Link.Shared.Application.Error.Exceptions;
using LantanaGroup.Link.Shared.Application.Error.Interfaces;
using LantanaGroup.Link.Shared.Application.Extensions.Telemetry;
using LantanaGroup.Link.Shared.Application.Interfaces;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Models.Kafka;
using LantanaGroup.Link.Shared.Application.Models.Telemetry;
using LantanaGroup.Link.Shared.Settings;
using OpenTelemetry.Trace;
using System.Diagnostics;
using System.Text;

namespace LantanaGroup.Link.Audit.Application.Handlers.Exceptions.Transient
{
    public class AuditTransientExceptionHandler<K, V> : ITransientExceptionHandler<K, V>
    {
        protected readonly ILogger<AuditTransientExceptionHandler<K, V>> _logger;
        protected readonly IKafkaProducerFactory<K, V> _producerFactory;

        public string Topic { get; set; } = string.Empty;

        public string ServiceName { get; set; } = string.Empty;

        public AuditTransientExceptionHandler(ILogger<AuditTransientExceptionHandler<K, V>> logger, IKafkaProducerFactory<K, V> producerFactory)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _producerFactory = producerFactory ?? throw new ArgumentNullException(nameof(producerFactory));
        }

        public void HandleException(ConsumeResult<K, V> consumeResult, string facilityId, AuditEventType auditEventType, string message = "")
        {
            using Activity? activity = ServiceActivitySource.Instance.StartActivityWithTags("AuditTransientExceptionHandler.HandleException",
                [
                    new KeyValuePair<string, object?>(DiagnosticNames.Service, ServiceName),
                    new KeyValuePair<string, object?>(DiagnosticNames.FacilityId, facilityId),
                    new KeyValuePair<string, object?>(DiagnosticNames.AuditLogAction, nameof(auditEventType))
                ]);

            try
            {
                message ??= "";
                if (consumeResult == null)
                {
                    var consumeResultIsNullMessage = $"ConsumeResult is null, cannot produce Audit or Retry events for topic {Topic}: " + message;
                    _logger.LogTransientException(consumeResultIsNullMessage);
                    return;
                }

                var errorMessage = $"{GetType().Name}: Failed to process {ServiceName} Event: " + message;
                _logger.LogTransientException(errorMessage);

                ProduceRetryScheduledEvent(consumeResult.Message.Key, consumeResult.Message.Value,
                    consumeResult.Message.Headers, facilityId);
            }
            catch (Exception ex)
            {
                Activity.Current?.SetStatus(ActivityStatusCode.Error);
                Activity.Current?.RecordException(ex);
                var errorMessage = $"Error in {GetType().Name}.HandleException: " + ex.Message;
                _logger.LogTransientException(errorMessage);
                throw;
            }
        }

        public virtual void HandleException(ConsumeResult<K, V> consumeResult, Exception ex, AuditEventType auditEventType, string facilityId)
        {
            using Activity? activity = ServiceActivitySource.Instance.StartActivityWithTags("AuditTransientExceptionHandler.HandleException",
                [
                    new KeyValuePair<string, object?>(DiagnosticNames.Service, ServiceName),
                    new KeyValuePair<string, object?>(DiagnosticNames.FacilityId, facilityId),
                    new KeyValuePair<string, object?>(DiagnosticNames.AuditLogAction, nameof(auditEventType))
                ]);

            var tEx = new TransientException(ex.Message, auditEventType, ex.InnerException);
            HandleException(consumeResult, tEx, facilityId);
        }

        public virtual void HandleException(ConsumeResult<K, V>? consumeResult, TransientException rex, string facilityId)
        {
            using Activity? activity = ServiceActivitySource.Instance.StartActivityWithTags("AuditTransientExceptionHandler.HandleException",
                [
                    new KeyValuePair<string, object?>(DiagnosticNames.Service, ServiceName),
                    new KeyValuePair<string, object?>(DiagnosticNames.FacilityId, facilityId)
                ]);

            try
            {
                if (consumeResult == null)
                {
                    var consumeResultIsNullMessage = $"ConsumeResult is null, cannot produce Audit or Retry events for topic {Topic}: " + rex.Message;
                    _logger.LogDeadLetterException(consumeResultIsNullMessage);
                    return;
                }

                var errorMessage = $"{GetType().Name}: Failed to process {ServiceName} Event, {rex.Message}";
                _logger.LogTransientException(errorMessage);

                ProduceRetryScheduledEvent(consumeResult.Message.Key, consumeResult.Message.Value,
                    consumeResult.Message.Headers, facilityId);
            }
            catch (Exception ex)
            {
                Activity.Current?.SetStatus(ActivityStatusCode.Error);
                Activity.Current?.RecordException(ex);
                var errorMessage = $"Error in {GetType().Name}.HandleException: " + ex.Message;
                _logger.LogTransientException(errorMessage);
                throw;
            }
        }

        public virtual void ProduceAuditEvent(AuditEventMessage auditValue, Headers headers)
        {
            throw new NotImplementedException();
        }

        public virtual void ProduceRetryScheduledEvent(K key, V value, Headers headers, string facilityId)
        {
            using Activity? activity = ServiceActivitySource.Instance.StartActivityWithTags("AuditTransientExceptionHandler.ProduceRetryScheduledEvent",
               [
                   new KeyValuePair<string, object?>(DiagnosticNames.Service, ServiceName)
               ]);

            if (string.IsNullOrWhiteSpace(Topic))
            {
                throw new Exception(
                    $"{GetType().Name}.Topic has not been configured. Cannot Produce Retry Event for {ServiceName}");
            }

            if (!headers.TryGetLastBytes(KafkaConstants.HeaderConstants.ExceptionService, out var headerValue))
            {
                headers.Add(KafkaConstants.HeaderConstants.ExceptionService, Encoding.UTF8.GetBytes(ServiceName));
            }

            if (!string.IsNullOrEmpty(facilityId) && !headers.TryGetLastBytes(KafkaConstants.HeaderConstants.ExceptionFacilityId, out var topicValue))
            {
                headers.Add(KafkaConstants.HeaderConstants.ExceptionFacilityId, Encoding.UTF8.GetBytes(facilityId));
            }

            using var producer = _producerFactory.CreateProducer(new ProducerConfig(), useOpenTelemetry: false);
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
