using Confluent.Kafka;
using LantanaGroup.Link.Shared.Application.Interfaces;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using LantanaGroup.Link.Shared.Application.Models.Kafka;
using LantanaGroup.Link.Shared.Application.SerDes;
using Microsoft.Extensions.Logging;

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

    public IConsumer<TConsumerKey, TConsumerValue> CreateConsumer(ConsumerConfig config, IDeserializer<TConsumerKey>? keyDeserializer = null, IDeserializer<TConsumerValue>? valueDeserializer = null, KafkaAssignmentTracker? assignmentTracker = null)
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
            if (config.AutoOffsetReset is null)
            {
                config.AutoOffsetReset = AutoOffsetReset.Earliest;
            }

            config.PartitionAssignmentStrategy = PartitionAssignmentStrategy.CooperativeSticky;

            if (_kafkaConnection.SaslProtocolEnabled)
            {
                config.SecurityProtocol = _kafkaConnection.Protocol;
                config.SaslMechanism = _kafkaConnection.Mechanism;
                config.SaslUsername = _kafkaConnection.SaslUsername;
                config.SaslPassword = _kafkaConnection.SaslPassword;
            }

            KafkaClientDefaults.ApplyConsumer(config, _kafkaConnection.ClientId, _kafkaConnection.StaticMembership, typeof(TConsumerValue).Name);

            var consumerBuilder = new ConsumerBuilder<TConsumerKey, TConsumerValue>(config);

            if (typeof(TConsumerKey) != typeof(string))
            {
                consumerBuilder.SetKeyDeserializer(keyDeserializer ?? new JsonWithFhirMessageDeserializer<TConsumerKey>());
            }

            if (typeof(TConsumerValue) != typeof(string))
            {
                consumerBuilder.SetValueDeserializer(valueDeserializer ?? new JsonWithFhirMessageDeserializer<TConsumerValue>());
            }

            if (assignmentTracker != null)
            {
                consumerBuilder.SetPartitionsRevokedHandler((consumer, revoked) => assignmentTracker.OnRevoked(consumer, revoked, _logger));
                consumerBuilder.SetPartitionsLostHandler((consumer, lost) => assignmentTracker.OnRevoked(consumer, lost, _logger));
            }

            return consumerBuilder.Build();
        }
        catch (Exception ex)
        {
            string configOutput = $"\nBootstrap Server: {config.BootstrapServers}\nClient ID: {config.ClientId}\nGroup ID: {config.GroupId}\nSecurity Protocol: {config.SecurityProtocol.ToString()}";
            _logger.LogError(ex, "Failed to create Kafka consumer: {ErrorMessage}. Configuration: {Config}", ex.Message, configOutput);
            throw new Exception("Failed to create " + config.GroupId + " Kafka consumer.");
        }
    }
}
