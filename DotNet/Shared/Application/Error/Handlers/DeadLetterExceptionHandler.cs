using System.Diagnostics;
using Confluent.Kafka;
using LantanaGroup.Link.Shared.Application.Error.Exceptions;
using LantanaGroup.Link.Shared.Application.Error.Interfaces;
using LantanaGroup.Link.Shared.Application.Interfaces;
using LantanaGroup.Link.Shared.Settings;
using Microsoft.Extensions.Logging;
using System.Text;
using OpenTelemetry.Trace;

namespace LantanaGroup.Link.Shared.Application.Error.Handlers
{
    public class DeadLetterExceptionHandler<K, V> : IDeadLetterExceptionHandler<K, V>
    {
        protected readonly ILogger<DeadLetterExceptionHandler<K, V>> Logger;
        protected readonly IKafkaProducerFactory<K, V> ProducerFactory;
        protected readonly IKafkaProducerFactory<string, string> NullConsumeResultProducerFactory;

        public string Topic { get; set; } = string.Empty;

        public string ServiceName { get; set; } = string.Empty;

        public DeadLetterExceptionHandler(ILogger<DeadLetterExceptionHandler<K, V>> logger, 
            IKafkaProducerFactory<K, V> producerFactory,
            IKafkaProducerFactory<string, string> nullConsumeResultProducerFactory)
        {
            Logger = logger;
            ProducerFactory = producerFactory;
            NullConsumeResultProducerFactory = nullConsumeResultProducerFactory;
        }

        public void HandleException(ConsumeResult<K, V> consumeResult, string facilityId,string message = "")
        {
            try
            {
                var ex = new Exception(message);
                Logger.LogError(ex, "{Name}: Failed to process {S} Event.", GetType().Name, ServiceName);

                ProduceDeadLetter(consumeResult.Message.Key, consumeResult.Message.Value, consumeResult.Message.Headers, message);
            }
            catch (Exception e)
            {
                Logger.LogError(e, "Error in {name}.HandleException: {message}", GetType().Name, e.Message);
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
                Activity.Current?.RecordException(ex);
                
                Logger.LogError(ex, "{Name}: Failed to process {S} Event.", GetType().Name, ServiceName);

                ProduceDeadLetter(consumeResult.Message.Key, consumeResult.Message.Value, consumeResult.Message.Headers, ex.Message);
            }
            catch (Exception e)
            {
                Logger.LogError(e, "Error in {name}.HandleException: {message}", GetType().Name, e.Message);
            }
        }

        public virtual void ProduceDeadLetter(K key, V value, Headers headers, string exceptionMessage)
        {
            if (string.IsNullOrWhiteSpace(Topic))
            {
                throw new Exception(
                    $"{GetType().Name}.Topic has not been configured. Cannot Produce Dead Letter Event for {ServiceName}");
            }

            if (!headers.TryGetLastBytes(KafkaConstants.HeaderConstants.ExceptionService, out var headerValue))
            {
                headers.Add(KafkaConstants.HeaderConstants.ExceptionService, Encoding.UTF8.GetBytes(ServiceName));
            }

            headers.Add(KafkaConstants.HeaderConstants.ExceptionMessage, Encoding.UTF8.GetBytes(exceptionMessage));

            using var producer = ProducerFactory.CreateProducer(new ProducerConfig() { CompressionType = CompressionType.Zstd });
            producer.Produce(Topic, new Message<K, V>
            {
                Key = key,
                Value = value,
                Headers = headers
            });

            producer.Flush();
        }

        public virtual void HandleConsumeException(ConsumeException ex, string facilityId)
        {
            try
            {
                Activity.Current?.SetStatus(ActivityStatusCode.Error);
                Activity.Current?.RecordException(ex);
                
                if (string.IsNullOrWhiteSpace(facilityId))
                {
                    throw new ArgumentException("Error in HandleConsumeException: parameter facilityId is null or white space.");
                }

                if (ex?.ConsumerRecord?.Message == null)
                {
                    throw new Exception("Error in HandleConsumeException: ex.ConsumeRecord.Message contains null properties");
                }

                var message = new Message<string, string>()
                {
                    Headers = ex.ConsumerRecord.Message.Headers,
                    Key = Encoding.UTF8.GetString(ex.ConsumerRecord.Message.Key),
                    Value = Encoding.UTF8.GetString(ex.ConsumerRecord.Message.Value)
                };

                Logger.LogError(ex, "Error consuming message for topics: [{topic}] at {date}", Topic, DateTime.UtcNow);

                ProduceConsumeExceptionDeadLetter(message.Key, message.Value, message.Headers, ex.Message);
            }
            catch (Exception e)
            {
                Logger.LogError(e, "Error in {name}.HandleException: {message}", GetType().Name, e.Message);
            }
        }

        protected void ProduceConsumeExceptionDeadLetter(string key, string value, Headers headers, string exceptionMessage)
        {
            if (string.IsNullOrWhiteSpace(Topic))
            {
                throw new Exception(
                    $"{GetType().Name}.Topic has not been configured. Cannot Produce Dead Letter Event for {ServiceName}");
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
}