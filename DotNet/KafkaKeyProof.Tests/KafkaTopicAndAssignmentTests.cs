using Confluent.Kafka;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using LantanaGroup.Link.Shared.Application.Models.Kafka;

namespace LantanaGroup.Link.KafkaKeyProof.Tests;

public class KafkaTopicAndAssignmentTests
{
    [Fact]
    public void RetryAndRedriveNamesStayOffTheMainTopic()
    {
        Assert.Equal("PatientEvent-Retry-Report", KafkaTopicNames.Retry("PatientEvent", "Report"));
        Assert.Equal("PatientEvent-Redrive-Report", KafkaTopicNames.Redrive("PatientEvent", "Report"));
        Assert.Equal("PatientEvent-Error", KafkaTopicNames.Error("PatientEvent"));
        Assert.Equal(new[] { "PatientEvent", "PatientEvent-Redrive-Report" }, KafkaTopicNames.Subscription("PatientEvent", "Report"));
        Assert.True(KafkaTopicNames.TryMainFromRetry("PatientEvent-Retry-Report", out var main, out var service));
        Assert.Equal("PatientEvent", main);
        Assert.Equal("Report", service);
        Assert.Equal("PatientListsAcquired", KafkaTopicNames.Main("PatientListsAcquired"));
        Assert.Equal("PatientListsAcquired", KafkaTopicNames.Main("PatientListsAcquired-Redrive-Census"));
        Assert.Equal("PatientListsAcquired", KafkaTopicNames.Main("PatientListsAcquired-Retry-Census"));
    }

    [Fact]
    public void RevokeCommitsOnlyNamedProcessedPartitions()
    {
        var kept = new TopicPartition("PatientEvent", 0);
        var revoked = new TopicPartition("PatientEvent", 1);
        var untouched = new TopicPartition("PatientEvent", 2);
        var processed = new Dictionary<TopicPartition, Offset>
        {
            [kept] = new Offset(4),
            [revoked] = new Offset(9),
            [untouched] = new Offset(3)
        };

        var batch = KafkaAssignmentTracker.OffsetsForRevoked(
            [new TopicPartitionOffset(revoked, Offset.Unset)],
            processed);

        Assert.Single(batch);
        Assert.Equal(revoked, batch[0].TopicPartition);
        Assert.Equal(9, batch[0].Offset.Value);
    }

    [Fact]
    public void ProducerConfigUsesMurmur2AndConsumerUsesCooperativeSticky()
    {
        var connection = new KafkaConnection { ClientId = "Report", GroupId = "Report" };
        var producer = connection.CreateProducerConfig();
        Assert.Equal(Partitioner.Murmur2Random, producer.Partitioner);
        Assert.Equal(Acks.All, producer.Acks);
        Assert.True(producer.EnableIdempotence);

        var consumer = connection.CreateConsumerConfig();
        Assert.Equal(PartitionAssignmentStrategy.CooperativeSticky, consumer.PartitionAssignmentStrategy);
        Assert.Equal(AutoOffsetReset.Earliest, consumer.AutoOffsetReset);
    }

    [Fact]
    public void ApplyProducerOverwritesADifferentPartitioner()
    {
        var config = new ProducerConfig { Partitioner = Partitioner.ConsistentRandom };
        KafkaClientDefaults.ApplyProducer(config, "DataAcquisition");
        Assert.Equal(Partitioner.Murmur2Random, config.Partitioner);
    }

    [Fact]
    public void ApplyConsumerLeavesAnExplicitOffsetReset()
    {
        var config = new ConsumerConfig { AutoOffsetReset = AutoOffsetReset.Latest };
        KafkaClientDefaults.ApplyConsumer(config, "Report");
        Assert.Equal(AutoOffsetReset.Latest, config.AutoOffsetReset);
        Assert.Equal(PartitionAssignmentStrategy.CooperativeSticky, config.PartitionAssignmentStrategy);
    }
}
