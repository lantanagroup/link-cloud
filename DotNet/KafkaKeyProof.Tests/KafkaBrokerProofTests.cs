using System.Text;
using Confluent.Kafka;
using Confluent.Kafka.Admin;
using LantanaGroup.Link.Shared.Application.Models.Kafka;

namespace LantanaGroup.Link.KafkaKeyProof.Tests;

/// <summary>
/// Broker checks. Each test returns immediately when KAFKA_BOOTSTRAP is unset.
/// </summary>
public class KafkaBrokerProofTests
{
    [Fact]
    public async Task RetryTopicIsNotVisibleToTheMainTopicConsumer()
    {
        var bootstrap = Environment.GetEnvironmentVariable("KAFKA_BOOTSTRAP");
        if (string.IsNullOrWhiteSpace(bootstrap))
        {
            return;
        }

        var main = "proof-retry-main-" + Guid.NewGuid().ToString("N");
        var retry = KafkaTopicNames.Retry(main, "Report");
        var key = KafkaKeys.ForPatient("facility-proof", "patient-proof");
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        await CreateTopic(bootstrap, main, 3, timeout.Token);
        await CreateTopic(bootstrap, retry, 3, timeout.Token);

        using var producer = BuildProducer(bootstrap);
        await producer.ProduceAsync(retry, new Message<string, string> { Key = key, Value = "retry-value" }, timeout.Token);

        using var mainConsumer = BuildConsumer(bootstrap, "proof-main-" + main);
        mainConsumer.Subscribe(main);
        var mainRecord = mainConsumer.Consume(TimeSpan.FromSeconds(5));
        Assert.Null(mainRecord);

        using var retryConsumer = BuildConsumer(bootstrap, "proof-retry-" + main);
        retryConsumer.Subscribe(retry);
        var retryRecord = ConsumeUntil(retryConsumer, TimeSpan.FromSeconds(15), timeout.Token);
        Assert.NotNull(retryRecord);
        Assert.Equal(retry, retryRecord!.Topic);
        Assert.Equal(key, retryRecord.Message.Key);
        Assert.Equal("retry-value", retryRecord.Message.Value);
    }

    [Fact]
    public async Task TwoConsumersRebalanceWithoutLossOrDuplicate()
    {
        var bootstrap = Environment.GetEnvironmentVariable("KAFKA_BOOTSTRAP");
        if (string.IsNullOrWhiteSpace(bootstrap))
        {
            return;
        }

        var topic = "proof-rebalance-" + Guid.NewGuid().ToString("N");
        var group = "proof-rebalance-group-" + Guid.NewGuid().ToString("N");
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(40));
        await CreateTopic(bootstrap, topic, 2, timeout.Token);

        const int count = 20;
        var produced = new HashSet<string>();
        using (var producer = BuildProducer(bootstrap))
        {
            for (var i = 0; i < count; i++)
            {
                var key = KafkaKeys.ForPatient("facility-proof", "patient-" + i);
                produced.Add(key);
                await producer.ProduceAsync(topic, new Message<string, string> { Key = key, Value = i.ToString() }, timeout.Token);
            }
        }

        using var first = BuildConsumer(bootstrap, group + "-c1", group);
        using var second = BuildConsumer(bootstrap, group + "-c2", group);
        first.Subscribe(topic);
        second.Subscribe(topic);

        var seen = new Dictionary<string, int>();
        var deadline = DateTime.UtcNow.AddSeconds(25);
        while (seen.Count < count && DateTime.UtcNow < deadline)
        {
            timeout.Token.ThrowIfCancellationRequested();
            ReadOne(first, seen);
            ReadOne(second, seen);
        }

        Assert.Equal(count, seen.Count);
        Assert.All(produced, key => Assert.Equal(1, seen[key]));

        second.Close();
        var afterClose = DateTime.UtcNow.AddSeconds(8);
        while (DateTime.UtcNow < afterClose)
        {
            ReadOne(first, seen);
        }

        Assert.Equal(count, seen.Count);
        Assert.All(produced, key => Assert.Equal(1, seen[key]));
    }

    [Fact]
    public async Task GrowingPartitionsKeepsAnExistingKeyOnItsPartition()
    {
        var bootstrap = Environment.GetEnvironmentVariable("KAFKA_BOOTSTRAP");
        if (string.IsNullOrWhiteSpace(bootstrap))
        {
            return;
        }

        var topic = "proof-grow-" + Guid.NewGuid().ToString("N");
        var key = KafkaKeys.ForPatient("facility-proof", "patient-proof");
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        await CreateTopic(bootstrap, topic, 2, timeout.Token);

        using var producer = BuildProducer(bootstrap);
        var before = await producer.ProduceAsync(topic, new Message<string, string> { Key = key, Value = "before" }, timeout.Token);
        Assert.Equal(KafkaMurmur2.Partition(key, 2), before.Partition.Value);

        using var admin = new AdminClientBuilder(new AdminClientConfig { BootstrapServers = bootstrap }).Build();
        await admin.CreatePartitionsAsync([new PartitionsSpecification { Topic = topic, IncreaseTo = 4 }], new CreatePartitionsOptions());

        var after = await producer.ProduceAsync(topic, new Message<string, string> { Key = key, Value = "after" }, timeout.Token);
        var again = await producer.ProduceAsync(topic, new Message<string, string> { Key = key, Value = "again" }, timeout.Token);
        Assert.Equal(KafkaMurmur2.Partition(key, 4), after.Partition.Value);
        Assert.Equal(after.Partition.Value, again.Partition.Value);

        var description = admin.GetMetadata(topic, TimeSpan.FromSeconds(10));
        var topicMeta = Assert.Single(description.Topics, t => t.Topic == topic);
        Assert.Equal(4, topicMeta.Partitions.Count);
    }

    private static void ReadOne(IConsumer<string, string> consumer, Dictionary<string, int> seen)
    {
        var record = consumer.Consume(TimeSpan.FromMilliseconds(200));
        if (record == null)
        {
            return;
        }

        var key = record.Message.Key;
        seen[key] = seen.TryGetValue(key, out var count) ? count + 1 : 1;
        consumer.Commit(record);
    }

    private static ConsumeResult<string, string>? ConsumeUntil(IConsumer<string, string> consumer, TimeSpan budget, CancellationToken cancellationToken)
    {
        var deadline = DateTime.UtcNow.Add(budget);
        while (DateTime.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var record = consumer.Consume(TimeSpan.FromMilliseconds(500));
            if (record != null)
            {
                return record;
            }
        }

        return null;
    }

    private static async Task CreateTopic(string bootstrap, string topic, int partitions, CancellationToken cancellationToken)
    {
        using var admin = new AdminClientBuilder(new AdminClientConfig { BootstrapServers = bootstrap }).Build();
        try
        {
            await admin.CreateTopicsAsync(
                [new TopicSpecification { Name = topic, NumPartitions = partitions, ReplicationFactor = 1 }],
                new CreateTopicsOptions { RequestTimeout = TimeSpan.FromSeconds(15) });
        }
        catch (CreateTopicsException ex) when (ex.Results.All(r => r.Error.Code == ErrorCode.TopicAlreadyExists))
        {
        }

        cancellationToken.ThrowIfCancellationRequested();
    }

    private static IProducer<string, string> BuildProducer(string bootstrap)
    {
        return new ProducerBuilder<string, string>(new ProducerConfig
        {
            BootstrapServers = bootstrap,
            Partitioner = Partitioner.Murmur2Random,
            Acks = Acks.All,
            EnableIdempotence = true,
            ClientId = "proof-p"
        }).Build();
    }

    private static IConsumer<string, string> BuildConsumer(string bootstrap, string clientId, string? groupId = null)
    {
        return new ConsumerBuilder<string, string>(new ConsumerConfig
        {
            BootstrapServers = bootstrap,
            GroupId = groupId ?? clientId,
            ClientId = clientId,
            AutoOffsetReset = AutoOffsetReset.Earliest,
            PartitionAssignmentStrategy = PartitionAssignmentStrategy.CooperativeSticky,
            EnableAutoCommit = false
        }).Build();
    }
}
