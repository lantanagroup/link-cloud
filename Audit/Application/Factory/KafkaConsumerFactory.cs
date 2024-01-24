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
        private readonly IOptions<TempKafkaConnection> _kafkaConnection;

        public KafkaConsumerFactory(IOptions<TempKafkaConnection> kafkaConnection)
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

    public class TempKafkaConnection
    {
        public ConsumerConfig CreateConsumerConfig()
        {
            var config = new ConsumerConfig
            {
                BootstrapServers = string.Join(", ", BootstrapServers),
                ClientId = ClientId,
                GroupId = GroupId,
                AutoOffsetReset = AutoOffsetReset.Earliest,
                AllowAutoCreateTopics = true
            };

            if (SaslProtocolEnabled)
            {
                config.SecurityProtocol = SecurityProtocol.SaslSsl;
                config.SaslUsername = SaslUsername;
                config.SaslPassword = SaslPassword;
                config.SaslMechanism = SaslMechanism.Plain;
                config.ApiVersionRequest = ApiVersionRequest;
                config.ReceiveMessageMaxBytes = ReceiveMessageMaxBytes;
            }

            return config;
        }

        public ProducerConfig CreateProducerConfig()
        {
            var config = new ProducerConfig
            {
                BootstrapServers = string.Join(", ", BootstrapServers),
                ClientId = ClientId
            };

            if (SaslProtocolEnabled)
            {
                config.SecurityProtocol = SecurityProtocol.SaslSsl;
                config.SaslUsername = SaslUsername;
                config.SaslPassword = SaslPassword;
                config.SaslMechanism = SaslMechanism.Plain;
                config.ApiVersionRequest = ApiVersionRequest;
                config.ReceiveMessageMaxBytes = ReceiveMessageMaxBytes;
            }

            return config;
        }

        public List<string> BootstrapServers { get; set; } = new List<string>();
        public string ClientId { get; set; } = string.Empty;
        public string GroupId { get; set; } = "default";
        public bool SaslProtocolEnabled { get; set; } = false;
        public string? SaslUsername { get; set; } = null;
        public string? SaslPassword { get; set; } = null;
        public bool? ApiVersionRequest { get; set; } = null;
        public int? ReceiveMessageMaxBytes { get; set; } = null;
    }

}
