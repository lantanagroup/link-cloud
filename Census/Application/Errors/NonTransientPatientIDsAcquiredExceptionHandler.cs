using Census.Settings;
using Confluent.Kafka;
using LantanaGroup.Link.Census.Application.Interfaces;
using LantanaGroup.Link.Shared.Application.Interfaces;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Models.Kafka;
using System.Text;
using System.Text.Json;

namespace LantanaGroup.Link.Census.Application.Errors;

public class NonTransientPatientIDsAcquiredExceptionHandler<K, V> : INonTransientExceptionHandler<K, V>
{
    private readonly ILogger<NonTransientPatientIDsAcquiredExceptionHandler<K, V>> _logger;
    private readonly IKafkaProducerFactory<string,AuditEventMessage> _auditProducerFactory;
    private readonly IKafkaProducerFactory<string,string> _deadLetterProducerFactory;

    public NonTransientPatientIDsAcquiredExceptionHandler(ILogger<NonTransientPatientIDsAcquiredExceptionHandler<K, V>> logger, IKafkaProducerFactory<string, AuditEventMessage> auditProducerFactory, IKafkaProducerFactory<string, string> deadLetterProducerFactory)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _auditProducerFactory = auditProducerFactory ?? throw new ArgumentNullException(nameof(auditProducerFactory));
        _deadLetterProducerFactory = deadLetterProducerFactory ?? throw new ArgumentNullException(nameof(deadLetterProducerFactory));
    }

    public void HandleException(ConsumeException consumeException)
    {
        _logger.LogError($"Consume failure, potentially schema related: {consumeException.Error.Reason}");
        var keyString = consumeException.ConsumerRecord.Message.Key != null ? Encoding.UTF8.GetString(consumeException.ConsumerRecord.Message.Key) : "";
        var valueString = consumeException.ConsumerRecord.Message.Value != null ? Encoding.UTF8.GetString(consumeException.ConsumerRecord.Message.Value) : "";

        var auditValue = new AuditEventMessage
        {
            FacilityId = keyString,
            Action = AuditEventType.Query,
            ServiceName = CensusConstants.ServiceName,
            EventDate = DateTime.UtcNow,
            Notes = $"Kafka {CensusConstants.MessageNames.PatientIDsAcquired} consume failure, potentially schema related \nException Message: {consumeException.Error}",
        };

        try
        {
            ProduceAuditEvent(auditValue, consumeException.ConsumerRecord.Message.Headers);
            ProduceDeadLetter(keyString, valueString, consumeException.ConsumerRecord.Message.Headers, consumeException.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred producing audit or dead letter event.");
        }
    }

    public void HandleException(ConsumeResult<K, V> consumeResult, Exception ex)
    {
        _logger.LogError($"Failed to process Patient Event.", ex);

        var auditValue = new AuditEventMessage
        {
            FacilityId = consumeResult.Message.Key as string,
            Action = AuditEventType.Query,
            ServiceName = CensusConstants.ServiceName,
            EventDate = DateTime.UtcNow,
            Notes = $"Patient IDs Acquired processing failure \nException Message: {ex}",
        };

        try
        {
            ProduceAuditEvent(auditValue, consumeResult.Message.Headers);
        }
        catch (Exception producerException)
        {
            _logger.LogError(producerException, "An error occurred producing an audit event.");
            return;
        }
        
        var key = consumeResult.Message.Key as string;
        string? value = null;
        try
        {
            value = JsonSerializer.Serialize<string>(consumeResult.Message.Value as string);
        }
        catch (Exception serializationException)
        {
            _logger.LogError(serializationException, "An error occurred serializing the consume result value.");
            return;
        }


        try
        {
            ProduceDeadLetter(key, value, consumeResult.Message.Headers, ex.Message);
        }
        catch (Exception producerException)
        {
            _logger.LogError(producerException, "An error occurred serializing the consume result value.");
            return;
        }
    }

    private void ProduceAuditEvent(AuditEventMessage auditValue, Headers headers)
    {
        using (var producer = _auditProducerFactory.CreateAuditEventProducer())
        {
            producer.Produce(nameof(KafkaTopic.AuditableEventOccurred), new Message<string, AuditEventMessage>
            {
                Value = auditValue,
                Headers = headers
            });

            producer.Flush();
        }
    }

    private void ProduceDeadLetter(string key, string value, Headers headers, string exceptionMessage)
    {
        headers.Add("X-Exception-Message", Encoding.UTF8.GetBytes(exceptionMessage));

        using (var producer = _deadLetterProducerFactory.CreateProducer(new ProducerConfig()))
        {
            producer.Produce(CensusConstants.MessageNames.PatientIDsAcquiredError, new Message<string, string>
            {
                Key = key,
                Value = value,
                Headers = headers
            });

            producer.Flush();
        }
    }
}
