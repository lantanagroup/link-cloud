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
        var resolved = KafkaControllerId.Resolve(1, read, rolesKnown: true, controllerEligibleBrokerIds: []);
        Assert.Equal(100, resolved.Id);
        Assert.Equal("", resolved.UnavailableReason);
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

        var hidden = KafkaControllerId.Resolve(2, new MetadataLogLeader(false, -1), rolesKnown: true, controllerEligibleBrokerIds: []);
        Assert.Null(hidden.Id);
        Assert.Equal(KafkaControllerId.QuorumNotExposedReason, hidden.UnavailableReason);

        var zooKeeper = KafkaControllerId.Resolve(2, new MetadataLogLeader(false, -1), rolesKnown: false, controllerEligibleBrokerIds: []);
        Assert.Equal(2, zooKeeper.Id);
        Assert.Equal("", zooKeeper.UnavailableReason);

        var unreadableLeader = KafkaControllerId.Resolve(2, new MetadataLogLeader(true, -1), rolesKnown: false, controllerEligibleBrokerIds: [1]);
        Assert.Null(unreadableLeader.Id);
        Assert.Equal("", unreadableLeader.UnavailableReason);
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

    [Fact]
    public void Decommission_GroupsRefusalsByTopic_AndLabelsInternalTopics()
    {
        var partitions = Enumerable.Range(0, 12).Select(index => new BrokerPartitionFact
        {
            Topic = index < 10 ? "__consumer_offsets" : "ReadyToAcquire",
            Partition = index,
            Leader = 1,
            Replicas = [1],
            MinInSyncReplicas = 1
        }).ToList();

        var plan = BrokerMovePlanner.Decommission(1, [1], partitions);

        Assert.False(plan.Accepted);
        Assert.Empty(plan.Moves);
        Assert.Equal(2, plan.Errors.Count);
        Assert.StartsWith("ReadyToAcquire: 2 partitions", plan.Errors[0], StringComparison.Ordinal);
        Assert.Contains("(internal) __consumer_offsets: 10 partitions", plan.Errors[1], StringComparison.Ordinal);
        Assert.DoesNotContain(plan.Errors, error => error.Contains("partition 0", StringComparison.Ordinal));
    }

    [Fact]
    public void PartitionAdd_IsNotCalledEligibleBeforeADryRun()
    {
        Assert.Equal(
            "Not checked yet. Dry run an in-place increase to confirm it is eligible.",
            KafkaTopicEligibility.PartitionAddReason(false, "blocked"));
        Assert.Equal("blocked", KafkaTopicEligibility.PartitionAddReason(true, "blocked"));
    }

    [Fact]
    public void Migration_IsNotCalledEligibleBeforeADryRun()
    {
        Assert.Equal(
            "Not checked yet. Dry run a migration to confirm it is eligible.",
            KafkaTopicEligibility.MigrationCatalogLine(true, "unused"));
        Assert.Equal("This topic has no consumer.", KafkaTopicEligibility.MigrationCatalogLine(false, "This topic has no consumer."));
        Assert.Equal("", KafkaTopicEligibility.MigrationCatalogLine(false, null));
        Assert.DoesNotContain("Eligible for an increase migration.", KafkaTopicEligibility.MigrationNotChecked, StringComparison.Ordinal);
    }

    [Fact]
    public void PlanHash_IncludesAcknowledgedGroupNames_AndIgnoresOrder()
    {
        var facts = MigrationWorld.Facts();
        var baseline = MigrationPreflight.Evaluate(facts).PlanHash;
        facts.Groups[0].Acknowledged = true;
        Assert.NotEqual(baseline, MigrationPreflight.Evaluate(facts).PlanHash);

        var typed = MigrationWorld.Facts();
        typed.AcknowledgedGroupIds = ["zeta", "alpha"];
        var reordered = MigrationWorld.Facts();
        reordered.AcknowledgedGroupIds = ["alpha", "zeta", "alpha"];
        Assert.Equal(MigrationPreflight.Evaluate(typed).PlanHash, MigrationPreflight.Evaluate(reordered).PlanHash);
        Assert.NotEqual(baseline, MigrationPreflight.Evaluate(typed).PlanHash);
    }
}
