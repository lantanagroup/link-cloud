using Confluent.Kafka;

namespace LantanaGroup.Link.Notification.Application.Models
{
    public class BrokerConnection
    {
        public List<string> BootstrapServers { get; set; } = new List<string>();
        public string ClientId { get; set; } = string.Empty;
        public string GroupId { get; set; } = "default";
        public bool SaslProtocolEnabled { get; set; } = false;
        public string? SaslUsername { get; set; } = null;
        public string? SaslPassword { get; set; } = null;
        public bool? ApiVersionRequest { get; set; } = null;
        public int? ReceiveMessageMaxBytes { get; set; } = null;

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
    }
}

