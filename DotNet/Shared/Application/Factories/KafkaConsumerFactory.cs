using Confluent.Kafka;
using LantanaGroup.Link.Shared.Application.Interfaces;
using LantanaGroup.Link.Shared.Application.Models.Configs;
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

            // Say where the group starts reading. A group with no committed offset starts at
            // AutoOffsetReset, and at Latest it silently skips whatever is already on the topic.
            var groupId = config.GroupId;
            var offsetReset = config.AutoOffsetReset ?? AutoOffsetReset.Latest;
            consumerBuilder.SetPartitionsAssignedHandler((consumer, partitions) =>
                LogStartPositions(consumer, partitions, groupId, offsetReset));

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
            _logger.LogError(ex, "Failed to create Kafka consumer: {ErrorMessage}. Configuration: {Config}", ex.Message, configOutput);
            throw new Exception("Failed to create " + config.GroupId + " Kafka consumer.");
        }
    }

    private void LogStartPositions(IConsumer<TConsumerKey, TConsumerValue> consumer, List<TopicPartition> partitions,
        string groupId, AutoOffsetReset offsetReset)
    {
        if (partitions.Count == 0)
        {
            return;
        }

        try
        {
            var committed = consumer.Committed(partitions, TimeSpan.FromSeconds(5));
            foreach (var topic in committed.GroupBy(c => c.Topic))
            {
                var withOffset = topic.Where(c => c.Offset != Offset.Unset).ToList();
                var committedText = withOffset.Count == 0
                    ? "none"
                    : string.Join(", ", withOffset.Select(c => $"{c.Partition.Value}@{c.Offset.Value}"));

                // A partition with no committed offset only loses messages if it already holds some, so the
                // warning is kept for those; an empty partition starts the same wherever it starts.
                var holdingMessages = new List<int>();
                var empty = new List<int>();
                foreach (var partition in topic.Where(c => c.Offset == Offset.Unset))
                {
                    var watermarks = consumer.QueryWatermarkOffsets(partition.TopicPartition, TimeSpan.FromSeconds(5));
                    (watermarks.High.Value > watermarks.Low.Value ? holdingMessages : empty).Add(partition.Partition.Value);
                }

                if (holdingMessages.Count > 0 && offsetReset == AutoOffsetReset.Latest)
                {
                    _logger.LogWarning(
                        "Consumer group {GroupId:l} assigned {Topic:l}: partitions {Partitions:l} hold messages but have no committed offset, and start at {AutoOffsetReset}, skipping the messages already there; committed offsets {CommittedOffsets:l}",
                        groupId, topic.Key, string.Join(",", holdingMessages), offsetReset, committedText);
                }
                else if (holdingMessages.Count > 0 || empty.Count > 0)
                {
                    _logger.LogInformation(
                        "Consumer group {GroupId:l} assigned {Topic:l}: partitions {Partitions:l} have no committed offset and start at {AutoOffsetReset}{EmptyNote:l}; committed offsets {CommittedOffsets:l}",
                        groupId, topic.Key, string.Join(",", holdingMessages.Concat(empty).Order()), offsetReset,
                        holdingMessages.Count == 0 ? " (they are empty)" : string.Empty, committedText);
                }
                else
                {
                    _logger.LogInformation(
                        "Consumer group {GroupId:l} assigned {Topic:l}: resuming from committed offsets {CommittedOffsets:l}",
                        groupId, topic.Key, committedText);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not read committed offsets for consumer group {GroupId:l}", groupId);
        }
    }
}
