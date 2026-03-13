using Confluent.Kafka;
using LantanaGroup.Link.Shared.Application.Error.Exceptions;
using LantanaGroup.Link.Shared.Application.Error.Interfaces;
using LantanaGroup.Link.Shared.Application.Interfaces;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Settings;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Text;

namespace LantanaGroup.Link.Shared.Application.Error.Handlers;

public class DeadLetterExceptionHandler<T, K, V> : IDeadLetterExceptionHandler<T, K, V>
{
    protected readonly IKafkaProducerFactory<K, V> ProducerFactory;
    protected readonly IKafkaProducerFactory<string, string> NullConsumeResultProducerFactory;
    protected readonly ServiceInformation ServiceInformation;
    private readonly IExceptionLogger<T> _exceptionHandler;

    public string Topic { get; set; } = string.Empty;

    protected string ServiceName { get; set; } = string.Empty;

    public DeadLetterExceptionHandler(
        IKafkaProducerFactory<K, V> producerFactory,
        IKafkaProducerFactory<string, string> nullConsumeResultProducerFactory,
        ServiceInformation serviceInformation,
        IExceptionLogger<T> exceptionHandler)
    {
        ProducerFactory = producerFactory ?? throw new ArgumentNullException(nameof(producerFactory));
        NullConsumeResultProducerFactory = nullConsumeResultProducerFactory ?? throw new ArgumentNullException(nameof(nullConsumeResultProducerFactory));
        ServiceInformation = serviceInformation ?? throw new ArgumentNullException(nameof(serviceInformation));
        _exceptionHandler = exceptionHandler ?? throw new ArgumentNullException(nameof(exceptionHandler));

        ServiceName = ServiceInformation.ServiceConfigName ?? throw new ArgumentNullException("ServiceName must be populated");
    }

    public void HandleException(ConsumeResult<K, V> consumeResult, string facilityId, string message = "")
    {
        try
        {
            var ex = new Exception(message);
            _exceptionHandler.Handle(ex, "Failed to process event", LogLevel.Error, facilityId,
                new { Service = ServiceName, Topic = Topic, Partition = consumeResult.Partition.Value, Offset = consumeResult.Offset.Value });

            ProduceDeadLetter(consumeResult, message);
        }
        catch (Exception e)
        {
            _exceptionHandler.Handle(e, "Error in HandleException", LogLevel.Error);
        }
    }

    public virtual void HandleException(ConsumeResult<K, V> consumeResult, Exception ex, string facilityId)
    {
        var dlEx = new DeadLetterException(ex.Message, ex);
        HandleException(consumeResult, dlEx, facilityId);
    }

    public virtual void HandleException(ConsumeResult<K, V> consumeResult, DeadLetterException ex, string facilityId)
    {
        try
        {
            Activity.Current?.SetStatus(ActivityStatusCode.Error);
            Activity.Current?.AddException(ex);

            _exceptionHandler.Handle(ex, "Failed to process event", LogLevel.Error, facilityId,
                new { Service = ServiceName, Topic = Topic, Partition = consumeResult.Partition.Value, Offset = consumeResult.Offset.Value });

            ProduceDeadLetter(consumeResult, ex.Message);
        }
        catch (Exception e)
        {
            _exceptionHandler.Handle(e, "Error in HandleException", LogLevel.Error);
        }
    }

    public virtual void ProduceDeadLetter(ConsumeResult<K, V> consumeResult, string exceptionMessage)
    {
        if (string.IsNullOrWhiteSpace(Topic))
        {
            throw new Exception("Topic has not been configured. Cannot Produce Dead Letter Event.");
        }

        if (!consumeResult.Message.Headers.TryGetLastBytes(KafkaConstants.HeaderConstants.ExceptionService, out var headerValue))
        {
            consumeResult.Message.Headers.Add(KafkaConstants.HeaderConstants.ExceptionService, Encoding.UTF8.GetBytes(ServiceName));
        }

        consumeResult.Message.Headers.Add(KafkaConstants.HeaderConstants.ExceptionPartition, Encoding.UTF8.GetBytes(consumeResult.Partition.Value.ToString()));
        consumeResult.Message.Headers.Add(KafkaConstants.HeaderConstants.ExceptionOffset, Encoding.UTF8.GetBytes(consumeResult.Offset.Value.ToString()));
        consumeResult.Message.Headers.Add(KafkaConstants.HeaderConstants.ExceptionMessage, Encoding.UTF8.GetBytes(exceptionMessage));

        using var producer = ProducerFactory.CreateProducer(new ProducerConfig() { CompressionType = CompressionType.Zstd });
        producer.Produce(Topic, new Message<K, V>
        {
            Key = consumeResult.Message.Key,
            Value = consumeResult.Message.Value,
            Headers = consumeResult.Message.Headers
        });

        producer.Flush();
    }

    public virtual void HandleConsumeException(ConsumeException ex, string facilityId)
    {
        try
        {
            Activity.Current?.SetStatus(ActivityStatusCode.Error);
            Activity.Current?.AddException(ex);

            if (ex?.ConsumerRecord?.Message == null)
            {
                throw new Exception("Error in HandleConsumeException: ex.ConsumerRecord.Message contains null properties");
            }

            var message = new Message<string, string>()
            {
                Headers = ex.ConsumerRecord.Message.Headers,
                Key = Encoding.UTF8.GetString(ex.ConsumerRecord.Message.Key),
                Value = Encoding.UTF8.GetString(ex.ConsumerRecord.Message.Value)
            };

            _exceptionHandler.Handle(ex, "Error consuming message", LogLevel.Error, facilityId,
                new { Topic = Topic });

            ProduceConsumeExceptionDeadLetter(message.Key, message.Value, message.Headers, ex.Message);
        }
        catch (Exception e)
        {
            _exceptionHandler.Handle(e, "Error in HandleConsumeException", LogLevel.Error);
        }
    }

    protected void ProduceConsumeExceptionDeadLetter(string key, string value, Headers headers, string exceptionMessage)
    {
        if (string.IsNullOrWhiteSpace(Topic))
        {
            throw new Exception("Topic has not been configured. Cannot Produce Dead Letter Event.");
        }

        if (!headers.TryGetLastBytes(KafkaConstants.HeaderConstants.ExceptionService, out var headerValue))
        {
            headers.Add(KafkaConstants.HeaderConstants.ExceptionService, Encoding.UTF8.GetBytes(ServiceName));
        }

        headers.Add(KafkaConstants.HeaderConstants.ExceptionMessage, Encoding.UTF8.GetBytes(exceptionMessage));

        using var producer = NullConsumeResultProducerFactory.CreateProducer(new ProducerConfig());
        producer.Produce(Topic, new Message<string, string>
        {
            Key = key,
            Value = value,
            Headers = headers
        });

        producer.Flush();
    }
}