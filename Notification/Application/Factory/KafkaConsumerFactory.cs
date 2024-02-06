using Confluent.Kafka;
using LantanaGroup.Link.Notification.Application.Interfaces;
using LantanaGroup.Link.Notification.Application.Models;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using LantanaGroup.Link.Shared.Application.SerDes;
using Microsoft.Extensions.Options;
using System.Diagnostics;
using static LantanaGroup.Link.Notification.Settings.NotificationConstants;

namespace LantanaGroup.Link.Notification.Application.Factory
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
                ex.Data.Add("message", typeof(NotificationMessage).Name);
                ex.Data.Add("topic", nameof(KafkaTopic.NotificationRequested));
                var currentActivity = Activity.Current;
                currentActivity?.SetStatus(ActivityStatusCode.Error, "Failed to create Notification Requested Kafka consumer.");
                _logger.LogError(new EventId(NotificationLoggingIds.EventConsumerException, "Notification Service - Create kafka consumer factory"), ex, "Failed to create Notification Requested Kafka consumer.");
                throw;
            }
        }
    }
}
