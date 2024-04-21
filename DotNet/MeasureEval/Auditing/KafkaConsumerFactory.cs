using Confluent.Kafka;
using LantanaGroup.Link.MeasureEval.Models;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using LantanaGroup.Link.Shared.Application.SerDes;
using Microsoft.Extensions.Options;

namespace LantanaGroup.Link.MeasureEval.Auditing
{
    public class KafkaConsumerFactory : IKafkaConsumerFactory
    {
        private readonly ILogger<KafkaConsumerFactory> _logger;
        private readonly IOptions<KafkaConnection> _kafkaConnection;

        public KafkaConsumerFactory(ILogger<KafkaConsumerFactory> logger, IOptions<KafkaConnection> kafkaConnection)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _kafkaConnection = kafkaConnection ?? throw new ArgumentNullException(nameof(kafkaConnection));
        }

        public IConsumer<string, NotificationMessage> CreateNotificationRequestedConsumer()
        {
            try
            {
                return new ConsumerBuilder<string, NotificationMessage>(_kafkaConnection.Value.CreateConsumerConfig()).SetValueDeserializer(new JsonWithFhirMessageDeserializer<NotificationMessage>()).Build();
            }
            catch (Exception ex)
            {
                _logger.LogError("Failed to create Notification Requested Kafka consumer.", ex);
                throw new Exception("Failed to create Notification Requested Kafka consumer.");
            }
        }
    }
}
