using Confluent.Kafka;
using LantanaGroup.Link.LinkAdmin.BFF.Application.KafkaOps;
using Xunit;

namespace UnitTests.Kafka;

[Trait("Category", "UnitTests")]
public class KafkaOpsConsoleFixTests
{
    [Fact]
    public void ControllerId_UsesTheMetadataLogLeader()
    {
        var partition = new PartitionMetadata(0, 100, [100], [100], new Error(ErrorCode.NoError));
        var topic = new TopicMetadata(KafkaControllerId.MetadataLogTopic, [partition], new Error(ErrorCode.NoError));
        var read = KafkaControllerId.Read(new Metadata([], [topic], 1, ""));

        Assert.True(read.Present);
        Assert.Equal(100, read.Leader);
        Assert.Equal(100, KafkaControllerId.Resolve(1, read, rolesKnown: true, controllerEligibleBrokerIds: []));
    }

    [Fact]
    public void ControllerId_DoesNotUseARandomBrokerWhenNoBrokerIsControllerEligible()
    {
        Assert.False(KafkaControllerId.Read(null).Present);
        var other = new TopicMetadata(
            "ReadyToAcquire",
            [new PartitionMetadata(0, 1, [1], [1], new Error(ErrorCode.NoError))],
            new Error(ErrorCode.NoError));
        Assert.False(KafkaControllerId.Read(new Metadata([], [other], 1, "")).Present);

        var missing = new TopicMetadata(
            KafkaControllerId.MetadataLogTopic,
            [],
            new Error(ErrorCode.UnknownTopicOrPart));
        Assert.False(KafkaControllerId.Read(new Metadata([], [missing], 1, "")).Present);

        Assert.Null(KafkaControllerId.Resolve(2, new MetadataLogLeader(false, -1), rolesKnown: true, controllerEligibleBrokerIds: []));
        Assert.Equal(2, KafkaControllerId.Resolve(2, new MetadataLogLeader(false, -1), rolesKnown: false, controllerEligibleBrokerIds: []));
        Assert.Null(KafkaControllerId.Resolve(2, new MetadataLogLeader(true, -1), rolesKnown: false, controllerEligibleBrokerIds: [1]));
    }

    [Fact]
    public void ProduceRate_IsUnknownUntilTwoSamples()
    {
        var at = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var one = ProduceRateSamples.Measure([new WatermarkSample { At = at, Sum = 10 }]);
        Assert.False(one.Known);
        Assert.Equal(0, one.PerSecond);

        var stalled = ProduceRateSamples.Measure(
        [
            new WatermarkSample { At = at, Sum = 10 },
            new WatermarkSample { At = at, Sum = 16 }
        ]);
        Assert.False(stalled.Known);

        var measured = ProduceRateSamples.Measure(
        [
            new WatermarkSample { At = at, Sum = 10 },
            new WatermarkSample { At = at.AddSeconds(2), Sum = 16 }
        ]);
        Assert.True(measured.Known);
        Assert.Equal(3, measured.PerSecond);
    }

    [Fact]
    public void Decommission_CountsLeadersOnceAndKeepsInternalMoves()
    {
        var partitions = Enumerable.Range(0, 4).Select(index => new BrokerPartitionFact
        {
            Topic = index < 3 ? "_linkmig-journal" : "ReadyToAcquire",
            Partition = index,
            Leader = 1,
            Replicas = [1, 2],
            MinInSyncReplicas = 1
        }).ToList();

        var plan = BrokerMovePlanner.Decommission(1, [1, 2, 3], partitions);

        Assert.Equal(4, plan.Moves.Count);
        Assert.Contains(plan.Moves, move => move.Topic == "_linkmig-journal");
        Assert.Contains(plan.Notes, note => note == "4 leader(s) move off broker 1.");
        Assert.DoesNotContain(plan.Notes, note => note.Contains("partition 0", StringComparison.Ordinal));
    }
}
