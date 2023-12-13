using Confluent.Kafka;
using Confluent.Kafka.Extensions.Diagnostics;
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
    public class KafkaProducerFactory : IKafkaProducerFactory
    {
        private readonly ILogger<KafkaProducerFactory> _logger;
        private readonly IOptions<KafkaConnection> _kafkaConnection;

        public KafkaProducerFactory(ILogger<KafkaProducerFactory> logger, IOptions<KafkaConnection> kafkaConnection)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _kafkaConnection = kafkaConnection ?? throw new ArgumentNullException(nameof(kafkaConnection));
        }

        public IProducer<string, AuditEventMessage> CreateAuditEventProducer()
        {
            try 
            {
                return new ProducerBuilder<string, AuditEventMessage>(_kafkaConnection.Value.CreateProducerConfig()).SetValueSerializer(new JsonWithFhirMessageSerializer<AuditEventMessage>()).BuildWithInstrumentation();
            }
            catch (Exception ex)
            {
                ex.Data.Add("message", typeof(AuditEventMessage).Name);
                ex.Data.Add("topic", nameof(KafkaTopic.AuditableEventOccurred));
                _logger.LogError(new EventId(NotificationLoggingIds.KafkaProducer, "Notification Service - Create kafka producer factory"), ex, "Failed to create Audit Event Kafka producer.");
                var currentActivity = Activity.Current;
                currentActivity?.SetStatus(ActivityStatusCode.Error, "Failed to create Audit Event Kafka producer.");
                throw;
            }
            
        }
    }
}
