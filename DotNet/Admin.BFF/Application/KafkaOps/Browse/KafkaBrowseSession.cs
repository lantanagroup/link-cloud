using Confluent.Kafka;
using Confluent.Kafka.Admin;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using LantanaGroup.Link.Shared.Application.Models.Kafka;

namespace LantanaGroup.Link.LinkAdmin.BFF.Application.KafkaOps;

public sealed class KafkaBrowseSeek
{
    public string Topic { get; init; } = "";
    public int Partition { get; init; }
    public long Offset { get; init; }
}

public sealed class KafkaBrowsePartition
{
    public int Id { get; init; }
    public long Low { get; init; }
    public long High { get; init; }
}

public sealed class KafkaBrowsePolled
{
    public int Partition { get; init; }
    public long Offset { get; init; }
    public long TimestampUnixMs { get; init; }
    public byte[]? Key { get; init; }
    public byte[]? Value { get; init; }
    public List<KafkaBrowseHeaderBytes> Headers { get; init; } = [];
    public bool EndOfPartition { get; init; }
}

public sealed class KafkaBrowseHeaderBytes
{
    public string Name { get; init; } = "";
    public byte[] Value { get; init; } = [];
}

/// <summary>
/// One assigned read. The session has no commit method. Callers close it when the page is done.
/// </summary>
public interface IKafkaBrowseSession : IDisposable
{
    IReadOnlyList<KafkaBrowsePartition> Describe(string topic);
    long? OffsetForTime(string topic, int partition, long unixMs);
    void AssignAndSeek(IReadOnlyList<KafkaBrowseSeek> seeks);
    KafkaBrowsePolled? Poll(TimeSpan wait);
}

public interface IKafkaBrowseSessionFactory
{
    IKafkaBrowseSession Open();
}

public sealed class ConfluentBrowseSessionFactory(KafkaConnection connection) : IKafkaBrowseSessionFactory
{
    public IKafkaBrowseSession Open() => new ConfluentBrowseSession(connection);
}

public sealed class ConfluentBrowseSession : IKafkaBrowseSession
{
    private readonly IConsumer<byte[], byte[]> _consumer;
    private readonly IAdminClient? _metadata;

    public ConfluentBrowseSession(KafkaConnection connection)
        : this(new ConsumerBuilder<byte[], byte[]>(BrowseConfig(connection)).Build(), MetadataClient(connection))
    {
    }

    internal ConfluentBrowseSession(IConsumer<byte[], byte[]> consumer)
        : this(consumer, null)
    {
    }

    private ConfluentBrowseSession(IConsumer<byte[], byte[]> consumer, IAdminClient? metadata)
    {
        _consumer = consumer;
        _metadata = metadata;
    }

    public static ConsumerConfig BrowseConfig(KafkaConnection connection)
    {
        var config = connection.CreateConsumerConfig();
        config.GroupId = KafkaBrowseLimits.GroupId;
        config.ClientId = "LinkAdminBFF-browse";
        config.EnableAutoCommit = false;
        config.EnableAutoOffsetStore = false;
        config.AutoOffsetReset = AutoOffsetReset.Error;
        config.EnablePartitionEof = true;
        config.AllowAutoCreateTopics = false;
        config.GroupInstanceId = null;
        return config;
    }

    public IReadOnlyList<KafkaBrowsePartition> Describe(string topic)
    {
        if (_metadata is null)
            throw new InvalidOperationException("Browse metadata is not available on this session.");

        var metadata = _metadata.GetMetadata(topic, TimeSpan.FromSeconds(10));
        var described = metadata.Topics.FirstOrDefault(item => string.Equals(item.Topic, topic, StringComparison.Ordinal));
        if (described is null || described.Error.IsError)
            throw new KafkaException(described?.Error ?? new Error(ErrorCode.UnknownTopicOrPart, "Unknown topic or partition"));

        var partitions = new List<KafkaBrowsePartition>();
        foreach (var partition in described.Partitions.OrderBy(item => item.PartitionId))
        {
            var marks = _consumer.QueryWatermarkOffsets(new TopicPartition(topic, partition.PartitionId), TimeSpan.FromSeconds(10));
            partitions.Add(new KafkaBrowsePartition
            {
                Id = partition.PartitionId,
                Low = marks.Low.Value,
                High = marks.High.Value
            });
        }

        return partitions;
    }

    public long? OffsetForTime(string topic, int partition, long unixMs)
    {
        var offsets = _consumer.OffsetsForTimes(
            [new TopicPartitionTimestamp(topic, partition, new Timestamp(unixMs, TimestampType.CreateTime))],
            TimeSpan.FromSeconds(10));
        var found = offsets.FirstOrDefault();
        if (found is null || found.Offset.IsSpecial)
            return null;
        return found.Offset.Value;
    }

    public void AssignAndSeek(IReadOnlyList<KafkaBrowseSeek> seeks)
    {
        // The offset has to travel with Assign. A later Seek runs before librdkafka
        // has applied the assignment and fails with Local: Erroneous state.
        _consumer.Assign(seeks.Select(seek => new TopicPartitionOffset(seek.Topic, seek.Partition, seek.Offset)).ToList());
    }

    public KafkaBrowsePolled? Poll(TimeSpan wait)
    {
        var result = _consumer.Consume(wait);
        if (result is null)
            return null;
        if (result.IsPartitionEOF)
            return new KafkaBrowsePolled { Partition = result.Partition.Value, EndOfPartition = true };

        var headers = new List<KafkaBrowseHeaderBytes>();
        if (result.Message?.Headers is not null)
        {
            foreach (var header in result.Message.Headers)
            {
                headers.Add(new KafkaBrowseHeaderBytes
                {
                    Name = header.Key ?? "",
                    Value = header.GetValueBytes() ?? []
                });
            }
        }

        return new KafkaBrowsePolled
        {
            Partition = result.Partition.Value,
            Offset = result.Offset.Value,
            TimestampUnixMs = result.Message?.Timestamp.UnixTimestampMs ?? 0,
            Key = result.Message?.Key,
            Value = result.Message?.Value,
            Headers = headers
        };
    }

    public void Dispose()
    {
        _metadata?.Dispose();
        _consumer.Dispose();
    }

    private static IAdminClient MetadataClient(KafkaConnection connection)
    {
        var config = new AdminClientConfig
        {
            BootstrapServers = string.Join(",", connection.BootstrapServers ?? []),
            ClientId = "LinkAdminBFF-browse-meta"
        };
        if (connection.SaslProtocolEnabled)
        {
            config.SecurityProtocol = connection.Protocol;
            config.SaslMechanism = connection.Mechanism;
            config.SaslUsername = connection.SaslUsername;
            config.SaslPassword = connection.SaslPassword;
        }

        return new AdminClientBuilder(config).Build();
    }
}
