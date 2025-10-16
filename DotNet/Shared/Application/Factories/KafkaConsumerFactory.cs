using Confluent.Kafka;
using Confluent.Kafka.Extensions.Diagnostics;
using LantanaGroup.Link.Shared.Application.Interfaces;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using LantanaGroup.Link.Shared.Application.SerDes;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ZstdSharp.Unsafe;

namespace LantanaGroup.Link.Shared.Application.Factories;
public class KafkaConsumerFactory<TConsumerKey, TConsumerValue> : IKafkaConsumerFactory<TConsumerKey, TConsumerValue>
{
    private readonly ILogger<KafkaConsumerFactory<TConsumerKey, TConsumerValue>> _logger;
    private readonly KafkaConnection _kafkaConnection;

    public KafkaConsumerFactory(ILogger<KafkaConsumerFactory<TConsumerKey, TConsumerValue>> logger, KafkaConnection kafkaConnection)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _kafkaConnection = kafkaConnection ?? throw new ArgumentNullException(nameof(kafkaConnection));
    }

    public IConsumer<TConsumerKey, TConsumerValue> CreateConsumer(ConsumerConfig config, IDeserializer<TConsumerKey>? keyDeserializer = null, IDeserializer<TConsumerValue>? valueDeserializer = null) 
    {
        try
        {
            if (string.IsNullOrWhiteSpace(config.GroupId))
            {
                throw new ArgumentException("No Kafka Group Id set in consumer configuration");
            }

            config.BootstrapServers = string.Join(", ", _kafkaConnection.BootstrapServers);
            config.ReceiveMessageMaxBytes = _kafkaConnection.ReceiveMessageMaxBytes;
            config.ClientId = _kafkaConnection.ClientId;

            if (_kafkaConnection.SaslProtocolEnabled)
            {
                config.SecurityProtocol = _kafkaConnection.Protocol;
                config.SaslMechanism = _kafkaConnection.Mechanism;
                config.SaslUsername = _kafkaConnection.SaslUsername;
                config.SaslPassword = _kafkaConnection.SaslPassword;
            }

            var consumerBuilder = new ConsumerBuilder<TConsumerKey, TConsumerValue>(config);

            if (typeof(TConsumerKey) != typeof(string))
            {
                consumerBuilder.SetKeyDeserializer(keyDeserializer ?? new JsonWithFhirMessageDeserializer<TConsumerKey>());
            }

            if (typeof(TConsumerValue) != typeof(string))
            {
                consumerBuilder.SetValueDeserializer(valueDeserializer ?? new JsonWithFhirMessageDeserializer<TConsumerValue>());
            }

            return consumerBuilder.Build();
        }
        catch (Exception ex)
        {
            string configOutput = $"\nBootstrap Server: {config.BootstrapServers}\nClient ID: {config.ClientId}\nGroup ID: {config.GroupId}\nSecurity Protocol: {config.SecurityProtocol.ToString()}";
            _logger.LogError($"Failed to create Kafka consumer: {ex.Message}{configOutput}", ex);
            throw new Exception("Failed to create " + config.GroupId + " Kafka consumer.");
        }
    }
}
