using Confluent.Kafka;
using LantanaGroup.Link.Shared.Application.Extensions;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using LantanaGroup.Link.Shared.Application.Models.Kafka;
using Moq;

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
    public void DeadLetterCommitIsWhatRevokeCommits()
    {
        var tracker = new KafkaAssignmentTracker();
        var partition = new TopicPartition("PatientEvent", new Partition(0));
        var consumer = new Mock<IConsumer<string, string>>();
        var commits = new List<IReadOnlyList<TopicPartitionOffset>>();
        consumer
            .Setup(c => c.Commit(It.IsAny<IEnumerable<TopicPartitionOffset>>()))
            .Callback<IEnumerable<TopicPartitionOffset>>(offsets => commits.Add(offsets.ToList()));

        KafkaAssignmentRegistry.Register(consumer.Object, tracker);
        consumer.Object.SafeCommit(new[] { new TopicPartitionOffset(partition, new Offset(2)) });

        tracker.OnRevoked(consumer.Object, new[] { new TopicPartitionOffset(partition, Offset.Unset) });

        Assert.Equal(2, commits.Count);
        Assert.Equal(partition, commits[1].Single().TopicPartition);
        Assert.Equal(2, commits[1].Single().Offset.Value);
    }

    [Fact]
    public void RevokeAfterReassignDoesNotCommitTheStaleOffset()
    {
        var tracker = new KafkaAssignmentTracker();
        var partition = new TopicPartition("PatientEvent", new Partition(0));
        var consumer = new Mock<IConsumer<string, string>>();
        var commits = new List<IReadOnlyList<TopicPartitionOffset>>();
        consumer
            .Setup(c => c.Commit(It.IsAny<IEnumerable<TopicPartitionOffset>>()))
            .Callback<IEnumerable<TopicPartitionOffset>>(offsets => commits.Add(offsets.ToList()));

        KafkaAssignmentRegistry.Register(consumer.Object, tracker);
        consumer.Object.SafeCommit(new[] { new TopicPartitionOffset(partition, new Offset(100)) });

        var revoked = new[] { new TopicPartitionOffset(partition, Offset.Unset) };
        tracker.OnRevoked(consumer.Object, revoked);
        var commitsAfterFirstRevoke = commits.Count;

        // The partition is assigned again. No message on this pod has been processed.
        tracker.OnRevoked(consumer.Object, revoked);

        Assert.Equal(commitsAfterFirstRevoke, commits.Count);
        Assert.Equal(100, commits[^1].Single().Offset.Value);
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

    [Fact]
    public void ApplyConsumerStaticMembershipUsesAStableMemberName()
    {
        var off = new ConsumerConfig();
        KafkaClientDefaults.ApplyConsumer(off, "DataAcquisition", staticMembership: false, staticMemberName: "DataAcquisitionRequested");
        Assert.Null(off.GroupInstanceId);

        var first = new ConsumerConfig();
        KafkaClientDefaults.ApplyConsumer(first, "DataAcquisition", staticMembership: true, staticMemberName: "DataAcquisitionRequested");
        var again = new ConsumerConfig();
        KafkaClientDefaults.ApplyConsumer(again, "DataAcquisition", staticMembership: true, staticMemberName: "DataAcquisitionRequested");
        Assert.False(string.IsNullOrWhiteSpace(first.GroupInstanceId));
        Assert.Equal(first.GroupInstanceId, again.GroupInstanceId);
        Assert.Contains("DataAcquisitionRequested", first.GroupInstanceId);

        var other = new ConsumerConfig();
        KafkaClientDefaults.ApplyConsumer(other, "DataAcquisition", staticMembership: true, staticMemberName: "PatientCensusScheduled");
        Assert.NotEqual(first.GroupInstanceId, other.GroupInstanceId);

        var preset = new ConsumerConfig { GroupInstanceId = "already-set" };
        KafkaClientDefaults.ApplyConsumer(preset, "DataAcquisition", staticMembership: true, staticMemberName: "DataAcquisitionRequested");
        Assert.Equal("already-set", preset.GroupInstanceId);

        var longName = new string('a', 300);
        var longFirst = new ConsumerConfig();
        KafkaClientDefaults.ApplyConsumer(longFirst, longName, staticMembership: true, staticMemberName: "DataAcquisitionRequested");
        var longOther = new ConsumerConfig();
        KafkaClientDefaults.ApplyConsumer(longOther, longName, staticMembership: true, staticMemberName: "PatientCensusScheduled");
        Assert.True(longFirst.GroupInstanceId!.Length <= 249);
        Assert.EndsWith("DataAcquisitionRequested", longFirst.GroupInstanceId);
        Assert.NotEqual(longFirst.GroupInstanceId, longOther.GroupInstanceId);
    }
}
