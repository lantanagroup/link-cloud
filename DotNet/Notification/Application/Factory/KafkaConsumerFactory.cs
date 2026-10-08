using Confluent.Kafka;
using LantanaGroup.Link.Notification.Application.Interfaces;
using LantanaGroup.Link.Notification.Application.Models;
using LantanaGroup.Link.Notification.Settings;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using LantanaGroup.Link.Shared.Application.Models.Kafka;
using LantanaGroup.Link.Shared.Application.SerDes;
using Microsoft.Extensions.Options;
using System.Diagnostics;
using static LantanaGroup.Link.Notification.Settings.NotificationConstants;

namespace LantanaGroup.Link.Notification.Application.Factory
{
    public class KafkaConsumerFactory : IKafkaConsumerFactory
    {
        private readonly ILogger<KafkaConsumerFactory> _logger;
        private readonly IOptions<KafkaConnection> _brokerConnection;

        public KafkaConsumerFactory(ILogger<KafkaConsumerFactory> logger, IOptions<KafkaConnection> brokerConnection)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _brokerConnection = brokerConnection ?? throw new ArgumentNullException(nameof(brokerConnection));
        }

        public IConsumer<string, NotificationMessage> CreateNotificationRequestedConsumer(bool enableAutoCommit = false, KafkaAssignmentTracker? assignmentTracker = null)
        {
            try
            {
                var config = _brokerConnection.Value.CreateConsumerConfig();
                config.GroupId = ServiceName;
                config.EnableAutoCommit = enableAutoCommit;
                var builder = new ConsumerBuilder<string, NotificationMessage>(config)
                    .SetValueDeserializer(new JsonWithFhirMessageDeserializer<NotificationMessage>());
                if (assignmentTracker != null)
                {
                    builder.SetPartitionsRevokedHandler((consumer, revoked) => assignmentTracker.OnRevoked(consumer, revoked, _logger));
                    builder.SetPartitionsLostHandler((consumer, lost) => assignmentTracker.OnRevoked(consumer, lost, _logger));
                }

                return builder.Build();
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
