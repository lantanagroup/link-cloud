using Confluent.Kafka;
using HealthChecks.Kafka;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using LantanaGroup.Link.Shared.Application.Models.Kafka;

namespace LantanaGroup.Link.Shared.Application.Health
{
    public class KafkaHealthCheckConfiguration
    {
        private KafkaConnection _connection;
        private string _serviceName;

        public KafkaHealthCheckConfiguration(KafkaConnection connection, string serviceName)
        {
            _connection = connection;
            _serviceName = serviceName;
        }

        public KafkaHealthCheckOptions GetHealthCheckOptions()
        {
            var producerConfig = new ProducerConfig()
            {
                BootstrapServers = string.Join(", ", _connection.BootstrapServers),
                MessageTimeoutMs = 3000
            };

            if (_connection.SaslProtocolEnabled)
            {
                producerConfig.SaslMechanism = _connection.Mechanism;
                producerConfig.SecurityProtocol = _connection.Protocol;
                producerConfig.SaslUsername = _connection.SaslUsername;
                producerConfig.SaslPassword = _connection.SaslPassword;
            }

            KafkaClientDefaults.ApplyProducer(producerConfig, _serviceName);

            return new KafkaHealthCheckOptions()
            {
                Configuration = producerConfig,
                MessageBuilder = MessageBuilder,
                Topic = "Service-Healthcheck"
            };
        }

        private Message<string, string> MessageBuilder(KafkaHealthCheckOptions options)
        {
            var utcDate = DateTime.UtcNow;
            return new Message<string, string>()
            {
                Key = KafkaKeys.ForService(_serviceName),
                Value = $"Service health check on {utcDate} ({utcDate.Kind})"
            };
        }
    }
}
