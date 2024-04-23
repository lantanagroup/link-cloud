using Confluent.Kafka;
using LantanaGroup.Link.MeasureEval.Models;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using LantanaGroup.Link.Shared.Application.SerDes;
using Microsoft.Extensions.Options;

namespace LantanaGroup.Link.MeasureEval.Auditing
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
                return new ProducerBuilder<string, AuditEventMessage>(_kafkaConnection.Value.CreateProducerConfig()).SetValueSerializer(new JsonWithFhirMessageSerializer<AuditEventMessage>()).Build();
            }
            catch (Exception ex)
            {
                _logger.LogError("Failed to create Audit Event Kafka producer.", ex);
                throw new Exception("Failed to create Audit Event Kafka producer.");
            }

        }
    }
}
