using Confluent.Kafka;
using LantanaGroup.Link.Audit.Application.Interfaces;
using LantanaGroup.Link.Audit.Application.Models;
using LantanaGroup.Link.Shared.Application.SerDes;
using Microsoft.Extensions.Options;

namespace LantanaGroup.Link.Audit.Application.Factory
{
    public class KafkaConsumerFactory : IKafkaConsumerFactory
    {
        private readonly IOptions<BrokerConnection> _brokerConnection;

        public KafkaConsumerFactory(IOptions<BrokerConnection> brokerConnection)
        {
            _brokerConnection = brokerConnection ?? throw new ArgumentNullException(nameof(brokerConnection));
        }

        public IConsumer<string, AuditEventMessage> CreateAuditableEventConsumer(bool enableAutoCommit = false)
        {
            var config = _brokerConnection.Value.CreateConsumerConfig();
            config.EnableAutoCommit = enableAutoCommit;            
            return new ConsumerBuilder<string, AuditEventMessage>(config).SetValueDeserializer(new JsonWithFhirMessageDeserializer<AuditEventMessage>()).Build();
        }
    }   

}
