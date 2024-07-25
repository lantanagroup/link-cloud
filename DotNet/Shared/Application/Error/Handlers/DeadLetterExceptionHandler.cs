using Confluent.Kafka;
using LantanaGroup.Link.Shared.Application.Error.Exceptions;
using LantanaGroup.Link.Shared.Application.Error.Interfaces;
using LantanaGroup.Link.Shared.Application.Interfaces;
using LantanaGroup.Link.Shared.Settings;
using Microsoft.Extensions.Logging;
using System.Text;

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
                message = message ?? "";
                if (consumeResult == null)
                {
                    Logger.LogError(message: $"{GetType().Name}|{ServiceName}|{Topic}: consumeResult is null" + message);
                    HandleException(message, facilityId);
                    return;
                }

                Logger.LogError(message: $"{GetType().Name}: Failed to process {ServiceName} Event.", exception: new Exception(message));

                ProduceDeadLetter(consumeResult.Message.Key, consumeResult.Message.Value, consumeResult.Message.Headers, message);
            }
            catch (Exception e)
            {
                Logger.LogError(e, $"Error in {GetType().Name}.HandleException: " + e.Message);
                HandleException(e, facilityId ?? string.Empty);
            }
        }

        public virtual void HandleException(ConsumeResult<K, V> consumeResult, Exception ex, string facilityId)
        {
            var dlEx = new DeadLetterException(ex.Message, ex.InnerException);
            HandleException(consumeResult, dlEx, facilityId);
        }

        public virtual void HandleException(ConsumeResult<K, V> consumeResult, DeadLetterException ex, string facilityId)
        {
            try
            {
                if (consumeResult == null)
                {
                    Logger.LogError(message: $"{GetType().Name}|{ServiceName}|{Topic}: consumeResult is null", exception: ex);
                    HandleException(ex, facilityId);
                    return;
                }

                Logger.LogError(message: $"{GetType().Name}: Failed to process {ServiceName} Event.", exception: ex);

                ProduceDeadLetter(consumeResult.Message.Key, consumeResult.Message.Value, consumeResult.Message.Headers, ex.Message);
            }
            catch (Exception e)
            {
                Logger.LogError(e, $"Error in {GetType().Name}.HandleException: " + e.Message);
                HandleException(e, facilityId ?? string.Empty);
            }
        }

        public virtual void HandleException(Headers headers, string key, string value, DeadLetterException ex, string facilityId)
        {
            try
            {
                var consumeResult = new ConsumeResult<string, string>();

                consumeResult.Message = new Message<string, string>();

                consumeResult.Message.Key = key;
                consumeResult.Message.Value = value;
                consumeResult.Message.Headers = headers;

                Logger.LogError(message: $"{GetType().Name}: Failed to process {ServiceName} Event.", exception: ex);

                ProduceNullConsumeResultDeadLetter(consumeResult.Message.Key, consumeResult.Message.Value, consumeResult.Message.Headers, ex.Message);
            }
            catch (Exception e)
            {
                Logger.LogError(e, $"Error in {GetType().Name}.HandleException: " + e.Message);
                HandleException(e, facilityId ?? string.Empty);
            }
        }

        public virtual void HandleException(DeadLetterException ex, string facilityId)
        {
            try
            {
                var consumeResult = new ConsumeResult<string, string>();

                consumeResult.Message = new Message<string, string>();

                consumeResult.Message.Key = ex.Message;
                consumeResult.Message.Value = ex.StackTrace;
                consumeResult.Message.Headers = new Headers();

                Logger.LogError(message: $"{GetType().Name}: Failed to process {ServiceName} Event.", exception: ex);

                ProduceNullConsumeResultDeadLetter(consumeResult.Message.Key, consumeResult.Message.Value, consumeResult.Message.Headers, ex.Message);
            }
            catch (Exception e)
            {
                Logger.LogError(e, $"Error in {GetType().Name}.HandleException: " + e.Message);
                HandleException(e, facilityId ?? string.Empty);
            }
        }

        public virtual void HandleException(Exception ex, string facilityId)
        {
            try
            {
                var consumeResult = new ConsumeResult<string, string>();

                consumeResult.Message = new Message<string, string>();
                
                consumeResult.Message.Key = ex.Message;
                consumeResult.Message.Value = ex.StackTrace;
                consumeResult.Message.Headers = new Headers();

                Logger.LogError(message: $"{GetType().Name}: Failed to process {ServiceName} Event.", exception: ex);

                ProduceNullConsumeResultDeadLetter(consumeResult.Message.Key, consumeResult.Message.Value, consumeResult.Message.Headers, ex.Message);
            }
            catch (Exception e)
            {
                Logger.LogError(e, $"Error in {GetType().Name}.HandleException: " + e.Message);
                throw;
            }
        }

        public virtual void HandleException(string message, string facilityId)
        {
            try
            {
                var consumeResult = new ConsumeResult<string, string>();

                consumeResult.Message = new Message<string, string>();

                consumeResult.Message.Key = message;
                consumeResult.Message.Value = message;
                consumeResult.Message.Headers = new Headers();

                Logger.LogError(message: $"{GetType().Name}: Failed to process {ServiceName} Event.", exception: new Exception(message));

                ProduceNullConsumeResultDeadLetter(consumeResult.Message.Key, consumeResult.Message.Value, consumeResult.Message.Headers, message);
            }
            catch (Exception e)
            {
                Logger.LogError(e, $"Error in {GetType().Name}.HandleException: " + e.Message);
                throw;
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

        public virtual void ProduceNullConsumeResultDeadLetter(string key, string value, Headers headers, string exceptionMessage)
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