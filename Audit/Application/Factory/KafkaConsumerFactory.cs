using Confluent.Kafka;
using LantanaGroup.Link.Audit.Application.Interfaces;
using LantanaGroup.Link.Audit.Application.Models;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using LantanaGroup.Link.Shared.Application.SerDes;
using Microsoft.Extensions.Options;

namespace LantanaGroup.Link.Audit.Application.Factory
{
    public class KafkaConsumerFactory : IKafkaConsumerFactory
    {
        private readonly IOptions<KafkaConnection> _kafkaConnection;

        public KafkaConsumerFactory(IOptions<KafkaConnection> kafkaConnection)
        {
            _kafkaConnection = kafkaConnection ?? throw new ArgumentNullException(nameof(kafkaConnection));
        }

        public IConsumer<string, AuditEventMessage> CreateAuditableEventConsumer(bool enableAutoCommit = false)
        {
            var config = _kafkaConnection.Value.CreateConsumerConfig();
            config.EnableAutoCommit = enableAutoCommit;            
            return new ConsumerBuilder<string, AuditEventMessage>(config).SetValueDeserializer(new JsonWithFhirMessageDeserializer<AuditEventMessage>()).Build();
        }
    }
}
