using System.Text;
using LantanaGroup.Link.LinkAdmin.BFF.Application.KafkaOps;
using LantanaGroup.Link.Shared.Application.Models.Kafka;

namespace UnitTests.Kafka;

[Trait("Category", "UnitTests")]
public class MigrationMachineTests
{
    [Fact]
    public void HappyPath_LeavesTheNewTopicEmpty_AndKeepsTheBackup()
    {
        var world = MigrationWorld.Create();
        var record = world.Record();
        var done = MigrationDriver.Drive(ref record, world);
        Assert.Equal(MigrationStep.Done, done.Step);
        Assert.False(done.BackupCleanupRequired);
        var topic = world.Topic(done.Topic);
        Assert.Equal(done.TargetPartitions, topic.Partitions);
        Assert.Empty(topic.Records);
        var backup = world.Topic(done.BackupTopic);
        Assert.Equal(world.SeedCount, backup.Records.Count);
        AssertKeyOrder(world.Seed, backup.Records);
        foreach (var group in done.Groups)
        {
            var committed = world.Committed(group);
            Assert.Equal(done.TargetPartitions, committed.Count);
            Assert.All(committed, offset => Assert.Equal(0, offset));
        }

        Assert.DoesNotContain(done.BackupTopic, world.Deleted);
    }

    [Theory]
    [InlineData(MigrationStep.A1)]
    [InlineData(MigrationStep.A2)]
    [InlineData(MigrationStep.A3)]
    [InlineData(MigrationStep.A4)]
    [InlineData(MigrationStep.B1)]
    [InlineData(MigrationStep.B2)]
    [InlineData(MigrationStep.B3)]
    [InlineData(MigrationStep.B4)]
    [InlineData(MigrationStep.B5)]
    [InlineData(MigrationStep.B6)]
    [InlineData(MigrationStep.B7)]
    [InlineData(MigrationStep.H1)]
    public void Abort_BeforeDelete_RollsBack_KeepsTopicAndBackup(MigrationStep step)
    {
        var world = MigrationWorld.Create();
        var record = world.Record();
        record = MigrationDriver.Until(record, world, step);
        Assert.Equal(step, record.Step);
        var before = world.Topic(record.Topic).Records.Count;
        var id = world.Topic(record.Topic).Id;
        record = MigrationDriver.Command(record, world, MigrationCommand.Abort, record.Executor, record.Topic);
        Assert.Equal(MigrationStep.RolledBack, record.Step);
        Assert.Equal(id, world.Topic(record.Topic).Id);
        Assert.Equal(MigrationWorld.OriginalPartitions, world.Topic(record.Topic).Partitions);
        Assert.Equal(before, world.Topic(record.Topic).Records.Count);
        Assert.DoesNotContain(record.BackupTopic, world.Deleted);
        if (world.Topics.ContainsKey(record.BackupTopic))
            Assert.True(record.BackupCleanupRequired);
    }

    [Fact]
    public void H1Timeout_RollsBack_AndKeepsTheBackup()
    {
        var world = MigrationWorld.Create();
        var record = MigrationDriver.Until(world.Record(), world, MigrationStep.H1);
        record.StepStartedUtc = world.Now.AddMinutes(-20);
        var tick = MigrationMachine.Describe(record, world.Observe(record), MigrationCommand.Tick, record.Executor, null, world.Now, world.Limits);
        Assert.Equal(MigrationStep.RollingBack, tick.Completed!.Step);
        record = MigrationDriver.Command(tick.Completed, world, MigrationCommand.Tick, record.Executor, null);
        Assert.Equal(MigrationStep.RolledBack, record.Step);
        Assert.DoesNotContain(record.BackupTopic, world.Deleted);
        Assert.True(record.BackupCleanupRequired);
    }

    [Theory]
    [InlineData(MigrationStep.A2)]
    [InlineData(MigrationStep.A4)]
    [InlineData(MigrationStep.B1)]
    [InlineData(MigrationStep.B6)]
    [InlineData(MigrationStep.C1)]
    [InlineData(MigrationStep.C3)]
    [InlineData(MigrationStep.C5)]
    [InlineData(MigrationStep.D1)]
    public void CrashBetweenActionAndCheckpoint_ResumesToTheSameStep(MigrationStep step)
    {
        var cleanWorld = MigrationWorld.Create();
        var clean = MigrationDriver.Until(cleanWorld.Record(), cleanWorld, step);
        var cleanTick = MigrationMachine.Describe(clean, cleanWorld.Observe(clean), MigrationCommand.Tick, clean.Executor, null, cleanWorld.Now, cleanWorld.Limits);
        cleanWorld.Apply(cleanTick.Effects, cleanTick.Persist ?? clean);
        var cleanResume = MigrationMachine.Describe(cleanTick.Persist ?? clean, cleanWorld.Observe(cleanTick.Persist ?? clean), MigrationCommand.Tick, clean.Executor, null, cleanWorld.Now, cleanWorld.Limits);
        var expected = cleanResume.Completed?.Step ?? clean.Step;

        var world = MigrationWorld.Create();
        var record = MigrationDriver.Until(world.Record(), world, step);
        var tick = MigrationMachine.Describe(record, world.Observe(record), MigrationCommand.Tick, record.Executor, null, world.Now, world.Limits);
        var before = world.Count(record.Topic) + world.Count(record.BackupTopic);
        world.Apply(tick.Effects, tick.Persist ?? record);
        var again = MigrationMachine.Describe(tick.Persist ?? record, world.Observe(tick.Persist ?? record), MigrationCommand.Tick, record.Executor, null, world.Now, world.Limits);
        if (again.Effects.Count > 0)
            world.Apply(again.Effects, again.Persist ?? tick.Persist ?? record);
        var resumed = again.Completed ?? MigrationMachine.Describe(again.Persist ?? tick.Persist ?? record, world.Observe(again.Persist ?? tick.Persist ?? record), MigrationCommand.Tick, record.Executor, null, world.Now, world.Limits).Completed;
        Assert.NotNull(resumed);
        Assert.Equal(expected, resumed!.Step);
        var after = world.Count(record.Topic) + world.Count(record.BackupTopic);
        if (step == MigrationStep.B6)
            Assert.Equal(before + world.SeedCount, after);
    }

    [Fact]
    public void BackupCopy_RunTwice_DoesNotDuplicate()
    {
        var world = MigrationWorld.Create();
        var record = MigrationDriver.Until(world.Record(), world, MigrationStep.B6);
        var tick = MigrationMachine.Describe(record, world.Observe(record), MigrationCommand.Tick, record.Executor, null, world.Now, world.Limits);
        world.Apply(tick.Effects, tick.Persist ?? record);
        var count = world.Count(record.BackupTopic);
        world.Apply(tick.Effects, tick.Persist ?? record);
        Assert.Equal(count, world.Count(record.BackupTopic));
        Assert.Equal(world.SeedCount, count);
        Assert.DoesNotContain(record.BackupTopic, world.Deleted);
    }

    [Fact]
    public void ForeignTopic_AtC1AndC3_StopsWithoutDeleting()
    {
        var world = MigrationWorld.Create();
        var record = MigrationDriver.Until(world.Record(), world, MigrationStep.C1);
        world.Topic(record.Topic).Id = "foreign";
        var tick = MigrationMachine.Describe(record, world.Observe(record), MigrationCommand.Tick, record.Executor, null, world.Now, world.Limits);
        Assert.Equal(MigrationStep.NeedsAttention, tick.Completed!.Step);
        Assert.Empty(tick.Effects);
        Assert.Equal("foreign", world.Topic(record.Topic).Id);

        var later = MigrationWorld.Create();
        var at = MigrationDriver.Until(later.Record(), later, MigrationStep.C3);
        later.Topics[at.Topic] = new MigrationWorld.TopicState
        {
            Id = "stray",
            Partitions = 3,
            Rf = 3,
            Records = [new CopiedRecord { Partition = 0, Offset = 0, Value = [1], TimestampMs = 5 }]
        };
        var blocked = MigrationMachine.Describe(at, later.Observe(at), MigrationCommand.Tick, at.Executor, null, later.Now, later.Limits);
        Assert.Equal(MigrationStep.NeedsAttention, blocked.Completed!.Step);
        Assert.DoesNotContain(blocked.Effects, effect => effect is MigrationEffect.DeleteTopic);
        Assert.Equal("stray", later.Topic(at.Topic).Id);
    }

    [Theory]
    [InlineData(MigrationStep.C2)]
    [InlineData(MigrationStep.C3)]
    [InlineData(MigrationStep.C4)]
    public void RecoverOriginal_RecreatesEmptyAtN_AndSetsOffsetsToZero(MigrationStep step)
    {
        var world = MigrationWorld.Create();
        var record = MigrationDriver.Until(world.Record(), world, step);
        if (step == MigrationStep.C4)
        {
            Assert.All(world.Watermarks(record.Topic), mark => Assert.Equal(0, mark));
        }

        record = MigrationDriver.Command(record, world, MigrationCommand.RecoverOriginal, "dana", record.Topic);
        Assert.Equal(MigrationStep.Done, record.Step);
        Assert.Equal("original", record.RecoveryChoice);
        Assert.Equal(MigrationWorld.OriginalPartitions, world.Topic(record.Topic).Partitions);
        Assert.Empty(world.Topic(record.Topic).Records);
        Assert.Equal(Enumerable.Repeat(0L, MigrationWorld.OriginalPartitions).ToList(), world.Committed(record.Groups[0]));
        Assert.Equal(world.SeedCount, world.Count(record.BackupTopic));
        Assert.DoesNotContain(record.BackupTopic, world.Deleted);
    }

    [Fact]
    public void RecoverOriginal_RefusesToDeleteWhenTheNewTopicHasData()
    {
        var world = MigrationWorld.Create();
        var record = MigrationDriver.Until(world.Record(), world, MigrationStep.C4);
        world.Topic(record.Topic).Records.Add(new CopiedRecord { Partition = 0, Offset = 0, Value = [1], TimestampMs = 1 });
        record.Step = MigrationStep.NeedsAttention;
        var refused = MigrationMachine.Describe(record, world.Observe(record), MigrationCommand.RecoverOriginal, record.Executor, record.Topic, world.Now, world.Limits);
        Assert.NotNull(refused.Error);
        Assert.DoesNotContain(record.Topic, world.Deleted);
    }

    [Fact]
    public void RecoverOriginal_RefusesTheExecutorWhenTheNewTopicIsEmpty()
    {
        var world = MigrationWorld.Create();
        var record = MigrationDriver.Until(world.Record(), world, MigrationStep.C4);
        var refused = MigrationMachine.Describe(record, world.Observe(record), MigrationCommand.RecoverOriginal, record.Executor, record.Topic, world.Now, world.Limits);
        Assert.Equal("A second person must delete the empty topic before it is recreated.", refused.Error);
        Assert.Empty(refused.Effects);
    }

    [Fact]
    public void DeleteForeign_RemovesAnEmptyTopic_AndDoesNotFinish()
    {
        var world = MigrationWorld.Create();
        var record = MigrationDriver.Until(world.Record(), world, MigrationStep.C3);
        world.Topics[record.Topic] = new MigrationWorld.TopicState { Id = "foreign", Partitions = record.TargetPartitions, Rf = 3 };
        record.Step = MigrationStep.NeedsAttention;
        record = MigrationDriver.Command(record, world, MigrationCommand.DeleteForeign, "dana", record.Topic);
        Assert.Equal(MigrationStep.NeedsAttention, record.Step);
        Assert.Contains(record.Topic, world.Deleted);
    }

    [Fact]
    public void Forward_RejectsAForeignTopicThatHasRecords()
    {
        var world = MigrationWorld.Create();
        var record = MigrationDriver.Until(world.Record(), world, MigrationStep.C2);
        world.Topics[record.Topic] = new MigrationWorld.TopicState
        {
            Id = "stray",
            Partitions = 3,
            Rf = 3,
            Records = [new CopiedRecord { Partition = 0, Offset = 0, Value = [1], TimestampMs = 1 }]
        };
        record.Step = MigrationStep.NeedsAttention;
        var refused = MigrationMachine.Describe(record, world.Observe(record), MigrationCommand.RecoverForward, "dana", record.Topic, world.Now, world.Limits);
        Assert.Contains("kafka-topics.sh", refused.Error, StringComparison.Ordinal);
        Assert.Empty(refused.Effects);
    }

    [Fact]
    public void Abort_OnAFinishedMigration_IsRejected()
    {
        var record = MigrationWorld.Create().Record();
        record.Step = MigrationStep.Done;
        var refused = MigrationMachine.Describe(record, new MigrationObservation(), MigrationCommand.Abort, "dana", null, DateTimeOffset.UtcNow, new MigrationLimits());
        Assert.Contains("already", refused.Error, StringComparison.Ordinal);
    }

    [Fact]
    public void HoldPoint_WaitsWithoutANote_AndTimesOutIntoRollback()
    {
        var world = MigrationWorld.Create();
        var record = MigrationDriver.Until(world.Record(), world, MigrationStep.H1);
        var notes = record.Timeline.Count;
        var waiting = MigrationMachine.Describe(record, world.Observe(record), MigrationCommand.Tick, record.Executor, null, world.Now, world.Limits);
        Assert.True(waiting.Wait);
        Assert.Equal(notes, (waiting.Persist ?? record).Timeline.Count);
        record.StepStartedUtc = world.Now.AddMinutes(-16);
        var timed = MigrationMachine.Describe(record, world.Observe(record), MigrationCommand.Tick, record.Executor, null, world.Now, world.Limits);
        Assert.Equal(MigrationStep.RollingBack, timed.Completed!.Step);
        var aborted = MigrationMachine.Describe(record, world.Observe(record), MigrationCommand.Abort, record.Executor, null, world.Now.AddMinutes(-20), world.Limits);
        Assert.Equal(MigrationStep.RollingBack, aborted.Completed!.Step);
    }

    [Fact]
    public void GroupWithNoCommit_HasNoLag_AndDeadIsNotRequiredStable()
    {
        Assert.Equal(0, MigrationGroups.Lag([10, 10], [-1, -1]));
        Assert.Equal(0, MigrationGroups.Lag([10], []));
        Assert.Equal(4, MigrationGroups.Lag([10], [6]));
        Assert.True(MigrationGroups.Ready("Empty", 0, [-1]));
        Assert.True(MigrationGroups.Ready("Stable", 1, [6]));
        Assert.False(MigrationGroups.Ready("Empty", 0, [6]));
        Assert.False(MigrationGroups.Ready("Dead", 0, [6]));
    }

    [Fact]
    public void Holds_AreReadFromTheStore()
    {
        var store = new InMemoryMigrationStore();
        var registry = new MigrationHoldRegistry(store);
        var record = MigrationWorld.Create().Record();
        record.Step = MigrationStep.H1;
        store.SaveAsync(record, 1, CancellationToken.None).GetAwaiter().GetResult();
        Assert.False(registry.IsHeld(record.Topic));
        Assert.True(registry.IsHeldAsync(record.Topic, CancellationToken.None).GetAwaiter().GetResult());
        Assert.Contains(record.Topic, registry.HeldTopicsAsync(CancellationToken.None).GetAwaiter().GetResult());
    }

    [Fact]
    public void PlanHash_MustMatchTheDryRun()
    {
        Assert.Equal("The plan hash from the dry run is required.", MigrationApprovals.ValidatePlanHash("", "abc"));
        Assert.Equal("The plan changed. Request it again.", MigrationApprovals.ValidatePlanHash("nope", "abc"));
        Assert.Null(MigrationApprovals.ValidatePlanHash("abc", "abc"));
    }

    [Fact]
    public void BackupMatch_RequiresCountAndOrder()
    {
        var source = new List<CopiedRecord>
        {
            new() { Partition = 0, Offset = 0, Key = [1], Value = [9], TimestampMs = 1 },
            new() { Partition = 0, Offset = 1, Key = [1], Value = [8], TimestampMs = 2 }
        };
        var copied = source.Select(record => new CopiedRecord
        {
            Partition = record.Offset == 0 ? 4 : 1,
            Offset = record.Offset == 0 ? 50 : 3,
            Key = record.Key,
            Value = record.Value,
            TimestampMs = record.TimestampMs,
            Headers = [new CopiedHeader { Name = KafkaTopicCatalog.MigrationHeader, Value = System.Text.Encoding.UTF8.GetBytes("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa;" + "ResourcesNormalized" + ";" + record.Partition + ";" + record.Offset) }]
        }).ToList();
        Assert.True(LogCopier.Matches(source, copied));
        Assert.Equal(LogCopier.Digest(source), LogCopier.Digest(copied));
        var changed = copied[0];
        copied[0] = new CopiedRecord
        {
            Partition = changed.Partition,
            Offset = changed.Offset,
            Key = changed.Key,
            Value = [7],
            TimestampMs = changed.TimestampMs,
            Headers = changed.Headers
        };
        Assert.False(LogCopier.Matches(source, copied));
    }

    [Fact]
    public void BrokerDefaultConfigs_AreRead_AndAreNotCreateOverrides()
    {
        var facts = new MigrationTopicFacts();
        KafkaMigrationAdmin.ApplyDescribedConfigs(facts,
        [
            new TopicConfigRow { Name = "cleanup.policy", Value = "delete", Source = "DefaultConfig" },
            new TopicConfigRow { Name = "min.insync.replicas", Value = "2", Source = "DefaultConfig" },
            new TopicConfigRow { Name = "retention.ms", Value = "1000", Source = "DynamicTopicConfig" },
            new TopicConfigRow { Name = "segment.bytes", Value = "1073741824", Source = "DefaultConfig" }
        ]);
        Assert.Equal("delete", facts.EffectiveCleanupPolicy);
        Assert.Equal(2, facts.EffectiveMinInSyncReplicas);
        Assert.Equal("1000", facts.Configs["retention.ms"]);
        Assert.False(facts.Configs.ContainsKey("cleanup.policy"));
        Assert.False(facts.Configs.ContainsKey("min.insync.replicas"));
        Assert.False(facts.Configs.ContainsKey("segment.bytes"));
    }

    [Fact]
    public void CatalogGroupWithNoCommit_IsNotAssignedOffsets()
    {
        var world = MigrationWorld.Create();
        world.Offsets.Remove("measureeval-events");
        var record = MigrationDriver.Until(world.Record(), world, MigrationStep.C5);
        Assert.Equal(["measureeval"], record.GroupsWithCommits);
        var seen = world.Observe(record);
        var width = seen.HighWatermarks.Count;
        foreach (var name in record.Groups)
        {
            seen.Groups[name] = new GroupObservation
            {
                State = "Empty",
                Members = 0,
                Lag = 0,
                Committed = Enumerable.Repeat(-1L, width).ToList()
            };
        }

        var tick = MigrationMachine.Describe(record, seen, MigrationCommand.Tick, record.Executor, null, world.Now, world.Limits);
        Assert.Null(tick.Completed);
        var noted = tick.Persist!;
        Assert.Contains(noted.Timeline, entry => entry.Text == "Started C5");
        var writes = tick.Effects.OfType<MigrationEffect.WriteOffsets>().ToList();
        Assert.Equal(["measureeval"], writes.Select(item => item.Group).ToList());
        Assert.All(writes[0].Offsets, offset => Assert.Equal(0, offset));
        Assert.Equal(width, writes[0].Offsets.Count);

        world.Apply(tick.Effects, noted);
        var after = world.Observe(noted);
        after.Groups["measureeval-events"] = new GroupObservation
        {
            State = "Empty",
            Members = 0,
            Committed = Enumerable.Repeat(-1L, after.HighWatermarks.Count).ToList()
        };
        var advanced = MigrationMachine.Describe(noted, after, MigrationCommand.Tick, record.Executor, null, world.Now, world.Limits);
        Assert.Equal(MigrationStep.D1, advanced.Completed!.Step);
        Assert.True(MigrationGroups.Ready("Empty", 0, [-1, -1]));
    }

    [Fact]
    public void StartedB6_IsNotedOnce()
    {
        var world = MigrationWorld.Create();
        var record = MigrationDriver.Until(world.Record(), world, MigrationStep.B6);
        var seen = world.Observe(record);
        seen.BackupVerified = false;
        seen.HeaderConsistent = false;
        var first = MigrationMachine.Describe(record, seen, MigrationCommand.Tick, record.Executor, null, world.Now, world.Limits);
        var noted = first.Persist!;
        Assert.Equal(1, noted.Timeline.Count(entry => entry.Text == "Started B6"));
        var second = MigrationMachine.Describe(noted, seen, MigrationCommand.Tick, record.Executor, null, world.Now, world.Limits);
        Assert.Equal(1, (second.Persist ?? noted).Timeline.Count(entry => entry.Text == "Started B6"));
    }

    [Fact]
    public void SkippedBackup_DoesNotSayTheTempTopicWasKept()
    {
        var world = MigrationWorld.Create();
        var record = world.Record();
        record.BackupSkipped = true;
        record = MigrationDriver.Until(record, world, MigrationStep.H1);
        Assert.False(world.Topics.ContainsKey(record.BackupTopic));
        record = MigrationDriver.Command(record, world, MigrationCommand.Abort, record.Executor, null);
        Assert.Equal(MigrationStep.RolledBack, record.Step);
        Assert.Contains(record.Timeline, entry => entry.Text.Contains("No temp topic was created", StringComparison.Ordinal));
        Assert.False(record.BackupCleanupRequired);
    }

    [Fact]
    public void Offsets_AreZeroOnTheEmptyTopic_AndNotPastTheEnd()
    {
        var world = MigrationWorld.Create();
        var record = MigrationDriver.Until(world.Record(), world, MigrationStep.C5);
        var tick = MigrationMachine.Describe(record, world.Observe(record), MigrationCommand.Tick, record.Executor, null, world.Now, world.Limits);
        var write = tick.Effects.OfType<MigrationEffect.WriteOffsets>().First();
        Assert.Equal(record.TargetPartitions, write.Offsets.Count);
        Assert.All(write.Offsets, offset => Assert.Equal(0, offset));
        Assert.All(world.Watermarks(record.Topic), mark => Assert.Equal(0, mark));
        world.Apply(tick.Effects, record);
        Assert.Equal(write.Offsets, world.Committed(record.Groups[0]));
    }

    [Fact]
    public void StaleFence_CannotWrite_AndLeaseRejectsASecondHolder()
    {
        var store = new InMemoryMigrationStore();
        var record = MigrationWorld.Create().Record();
        store.SaveAsync(record, 2, CancellationToken.None).GetAwaiter().GetResult();
        Assert.Throws<StaleFenceException>(() => store.SaveAsync(record, 1, CancellationToken.None).GetAwaiter().GetResult());
        var kept = store.GetAsync(record.Id, CancellationToken.None).GetAwaiter().GetResult();
        Assert.Equal(2, kept!.Fence);

        var lease = new InMemoryKafkaOpsLease();
        var first = lease.AcquireAsync(CancellationToken.None).GetAwaiter().GetResult();
        Assert.Throws<LeaseHeldException>(() => lease.AcquireAsync(CancellationToken.None).GetAwaiter().GetResult());
        first.DisposeAsync().AsTask().GetAwaiter().GetResult();
        var second = lease.AcquireAsync(CancellationToken.None).GetAwaiter().GetResult();
        Assert.True(second.Fence > first.Fence);
    }

    [Fact]
    public void Approvals_BindTheHash_AndRejectTheRequester()
    {
        var record = MigrationWorld.Create().Record();
        record.Step = MigrationStep.Pending;
        record.Requester = "alice";
        record.PlanHash = "abc";
        Assert.Equal("Type the topic name to request the migration.", MigrationApprovals.ValidateRequest("alice", "nope", record.Topic, "because"));
        Assert.Null(MigrationApprovals.ValidateRequest("alice", record.Topic, record.Topic, "because"));
        Assert.Equal("A different person must approve the migration.", MigrationApprovals.ValidateApprove(record, "alice", "abc"));
        Assert.Equal("The plan changed. Request it again.", MigrationApprovals.ValidateApprove(record, "bob", "other"));
        Assert.Null(MigrationApprovals.ValidateApprove(record, "bob", "abc"));
        record.Step = MigrationStep.Approved;
        record.Approver = "bob";
        record.ApprovedHash = "abc";
        Assert.Equal("The requester cannot execute the migration.", MigrationApprovals.ValidateExecute(record, "alice"));
        record.ApprovedHash = "nope";
        Assert.Equal("The approval is not bound to the current plan.", MigrationApprovals.ValidateExecute(record, "carol"));
        record.ApprovedHash = "abc";
        Assert.Null(MigrationApprovals.ValidateExecute(record, "carol"));
    }

    [Fact]
    public void Preflight_RefusesTheSlice1Gates_AndHashesMaterialFacts()
    {
        var facts = MigrationWorld.Facts();
        var ok = MigrationPreflight.Evaluate(facts);
        Assert.True(ok.Accepted, string.Join(" ", ok.Errors));
        Assert.True(ok.WindowMinutes > facts.EstimatedBackupMinutes);

        facts.Provider = "Disabled";
        Assert.Contains(MigrationPreflight.Evaluate(facts).Errors, error => error.Contains("Disabled", StringComparison.Ordinal));
        facts.Provider = "Strimzi";
        facts.StoreDurable = false;
        Assert.Contains(MigrationPreflight.Evaluate(facts).Errors, error => error.Contains("in-memory", StringComparison.OrdinalIgnoreCase));
        facts.StoreDurable = true;
        facts.DedicatedConnection = false;
        Assert.Contains(MigrationPreflight.Evaluate(facts).Errors, error => error.Contains("dedicated", StringComparison.OrdinalIgnoreCase));
        facts.DedicatedConnection = true;
        facts.Provider = "LocalCompose";
        facts.StoreDurable = false;
        Assert.True(MigrationPreflight.Evaluate(facts).Accepted, string.Join(" ", MigrationPreflight.Evaluate(facts).Errors));

        var changed = MigrationWorld.Facts();
        changed.RequestedPartitions = 8;
        Assert.NotEqual(ok.PlanHash, MigrationPreflight.Evaluate(changed).PlanHash);
        var lagging = MigrationWorld.Facts();
        lagging.Groups[0].Active = true;
        lagging.Groups[0].Mapped = false;
        Assert.Contains(MigrationPreflight.Evaluate(lagging).Errors, error => error.Contains("not in the stop set", StringComparison.Ordinal));
        var inactive = MigrationWorld.Facts();
        inactive.Groups[0].Active = false;
        inactive.Groups[0].Lag = 4;
        Assert.Contains(MigrationPreflight.Evaluate(inactive).Errors, error => error.Contains("Type the group name", StringComparison.Ordinal));
        inactive.Groups[0].Acknowledged = true;
        Assert.True(MigrationPreflight.Evaluate(inactive).Accepted);
        var compact = MigrationWorld.Facts();
        compact.CleanupPolicy = "compact";
        Assert.Contains(MigrationPreflight.Evaluate(compact).Errors, error => error.Contains("Compacted", StringComparison.Ordinal));
        var brokerDefault = MigrationWorld.Facts();
        brokerDefault.CleanupPolicy = "";
        Assert.True(MigrationPreflight.Evaluate(brokerDefault).Accepted, string.Join(" ", MigrationPreflight.Evaluate(brokerDefault).Errors));
        var missingInSync = MigrationWorld.Facts();
        missingInSync.MinInSyncReplicas = 0;
        Assert.Contains(MigrationPreflight.Evaluate(missingInSync).Errors, error => error.Contains("could not be read", StringComparison.Ordinal));
    }

    [Fact]
    public void Slice1_IsExactlyTheSixteenEligibleTopics()
    {
        Assert.Equal(16, KafkaTopicCatalog.Slice1Topics.Count);
        foreach (var topic in KafkaTopicCatalog.Slice1Topics)
            Assert.True(KafkaTopicCatalog.IsSlice1Eligible(topic), topic);
        Assert.False(KafkaTopicCatalog.IsSlice1Eligible("ReportScheduled"));
        Assert.False(KafkaTopicCatalog.IsSlice1Eligible("AuditableEventOccurred"));
        Assert.False(KafkaTopicCatalog.IsHardBlocked("DataAcquisitionRequested"));
        Assert.False(KafkaTopicCatalog.Find("ReadyToAcquire")!.OrderSensitive);
        Assert.Equal(KafkaTopicCatalog.PatientJsonShape, KafkaTopicCatalog.KeyShapeOf("ResourcesAcquired"));
    }

    [Fact]
    public void Copier_KeepsOrderHeadersTimestampsAndNullKeys()
    {
        var id = Guid.NewGuid();
        var source = new List<CopiedRecord>
        {
            Record("k", 0, 0, 10, "a"),
            Record("k", 0, 1, 11, "b"),
            new CopiedRecord { Key = null, Value = [4], Partition = 1, Offset = 0, TimestampMs = 12, Headers = [new CopiedHeader { Name = "h", Value = [8] }] }
        };
        var first = LogCopier.Copy(new CopyRequest { MigrationId = id, SourceTopic = "T", TargetPartitions = 4, Source = source });
        Assert.True(first.Ok, first.Failure);
        var second = LogCopier.Copy(new CopyRequest { MigrationId = id, SourceTopic = "T", TargetPartitions = 4, Source = source, Destination = first.Written });
        Assert.True(second.Ok, second.Failure);
        Assert.Empty(second.Written);
        var keyRecords = first.Written.Where(record => record.Key is not null).OrderBy(record => record.Offset).Select(record => Encoding.UTF8.GetString(record.Value!)).ToList();
        Assert.Equal(["a", "b"], keyRecords);
        Assert.All(first.Written, record => Assert.Contains(record.Headers, header => header.Name == KafkaTopicCatalog.MigrationHeader));
        Assert.Contains(first.Written, record => record.TimestampMs == 12 && (record.Key is null || record.Key.Length == 0));
        var nullRecord = source.Single(record => record.Key is null);
        Assert.Equal(KafkaMurmur.NullKeyPartition(1, 4), LogCopier.TargetPartition(nullRecord, 4));
        Assert.Equal(first.Digests[first.Written[0].Partition], LogCopier.Digest(first.Written.Where(record => record.Partition == first.Written[0].Partition)));
    }

    private static CopiedRecord Record(string key, int partition, long offset, long timestamp, string value) => new()
    {
        Key = Encoding.UTF8.GetBytes(key),
        Value = Encoding.UTF8.GetBytes(value),
        Partition = partition,
        Offset = offset,
        TimestampMs = timestamp,
        Headers = [new CopiedHeader { Name = "h", Value = [1] }]
    };

    private static void AssertKeyOrder(IReadOnlyList<CopiedRecord> source, IReadOnlyList<CopiedRecord> restored)
    {
        var expected = source.Where(record => record.Key is { Length: > 0 }).GroupBy(record => Convert.ToHexString(record.Key!))
            .ToDictionary(group => group.Key, group => group.OrderBy(record => record.Offset).Select(record => Convert.ToHexString(record.Value!)).ToList());
        var actual = restored.Where(record => record.Key is { Length: > 0 }).GroupBy(record => Convert.ToHexString(record.Key!))
            .ToDictionary(group => group.Key, group => group.OrderBy(record => record.Offset).Select(record => Convert.ToHexString(record.Value!)).ToList());
        Assert.Equal(expected, actual);
    }
}

internal static class MigrationDriver
{
    public static MigrationRecord Drive(ref MigrationRecord record, MigrationWorld world)
    {
        for (var i = 0; i < 120 && record.Step is not (MigrationStep.Done or MigrationStep.RolledBack or MigrationStep.Rejected or MigrationStep.NeedsAttention); i++)
        {
            var command = record.Step == MigrationStep.H1 ? MigrationCommand.Go : MigrationCommand.Tick;
            var typed = command == MigrationCommand.Go ? record.Topic : null;
            if (!Step(ref record, world, command, record.Executor, typed))
                break;
        }

        return record;
    }

    public static MigrationRecord Until(MigrationRecord record, MigrationWorld world, MigrationStep step)
    {
        for (var i = 0; i < 120 && record.Step != step; i++)
        {
            var command = record.Step == MigrationStep.H1 ? MigrationCommand.Go : MigrationCommand.Tick;
            var typed = command == MigrationCommand.Go ? record.Topic : null;
            if (!Step(ref record, world, command, record.Executor, typed))
                break;
        }

        return record;
    }

    public static MigrationRecord Command(MigrationRecord record, MigrationWorld world, MigrationCommand command, string? actor, string? typed)
    {
        Step(ref record, world, command, actor, typed);
        for (var i = 0; i < 40 && record.Step is not (MigrationStep.Done or MigrationStep.RolledBack or MigrationStep.Rejected or MigrationStep.NeedsAttention); i++)
        {
            if (!Step(ref record, world, MigrationCommand.Tick, actor, null))
                break;
        }

        return record;
    }

    private static bool Step(ref MigrationRecord record, MigrationWorld world, MigrationCommand command, string? actor, string? typed)
    {
        if (record.Step is MigrationStep.Done or MigrationStep.RolledBack or MigrationStep.Rejected)
            return false;
        var tick = MigrationMachine.Describe(record, world.Observe(record), command, actor, typed, world.Now, world.Limits);
        if (tick.Error is not null)
            throw new InvalidOperationException(tick.Error + " at " + record.Step);
        if (tick.Effects.Count > 0)
        {
            var intent = tick.Persist ?? record;
            world.Apply(tick.Effects, intent);
            var resume = MigrationMachine.Describe(intent, world.Observe(intent), MigrationCommand.Tick, actor, null, world.Now, world.Limits);
            record = resume.Completed ?? resume.Persist ?? intent;
            return true;
        }

        if (tick.Completed is not null)
        {
            record = tick.Completed;
            return true;
        }

        if (tick.Persist is not null)
        {
            record = tick.Persist;
            if (record.Step == MigrationStep.B2)
                world.Now = world.Now.AddMilliseconds(world.Limits.MetadataRefreshIntervalMs * 2 + 50);
            else if (record.Step == MigrationStep.B3)
                world.Now = world.Now.AddSeconds(11);
            else
                world.Now = world.Now.AddSeconds(1);
            return true;
        }

        return false;
    }
}

internal sealed class MigrationWorld
{
    public sealed class TopicState
    {
        public string Id { get; set; } = "";
        public int Partitions { get; set; }
        public int Rf { get; set; } = 3;
        public Dictionary<string, string> Configs { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        public List<CopiedRecord> Records { get; set; } = [];
        public bool FullIsr { get; set; } = true;
    }

    public const int OriginalPartitions = 3;
    public Dictionary<string, TopicState> Topics { get; } = new(StringComparer.Ordinal);
    public Dictionary<string, int> Workloads { get; } = new(StringComparer.Ordinal);
    public Dictionary<string, List<long>> Offsets { get; } = new(StringComparer.Ordinal);
    public Dictionary<string, string> GroupState { get; } = new(StringComparer.Ordinal);
    public Dictionary<string, int> GroupMembers { get; } = new(StringComparer.Ordinal);
    public List<string> Deleted { get; } = [];
    public bool Holds { get; set; }
    public DateTimeOffset Now { get; set; } = DateTimeOffset.Parse("2026-10-08T12:00:00Z");
    public MigrationLimits Limits { get; } = new() { MetadataRefreshIntervalMs = 1000, HoldMinutes = 15, MaxBackupMinutes = 15 };
    public List<CopiedRecord> Seed { get; private set; } = [];
    public int SeedCount => Seed.Count;
    public string SeedDigest { get; private set; } = "";
    private string? _backupSeal;

    public static MigrationWorld Create()
    {
        var world = new MigrationWorld();
        world.Seed = SeedRecords();
        world.SeedDigest = LogCopier.Digest(world.Seed);
        world.Topics["ResourcesNormalized"] = new TopicState
        {
            Id = "topic-original",
            Partitions = OriginalPartitions,
            Configs = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["cleanup.policy"] = "delete", ["retention.ms"] = "86400000" },
            Records = world.Seed.Select(Clone).ToList()
        };
        world.Topics["ResourcesNormalized-Retry"] = new TopicState { Id = "retry", Partitions = OriginalPartitions };
        world.Topics["ResourcesNormalized-Error"] = new TopicState { Id = "error", Partitions = OriginalPartitions };
        world.Workloads["Normalization"] = 2;
        world.Workloads["measureeval"] = 2;
        foreach (var group in new[] { "measureeval", "measureeval-events" })
        {
            world.Offsets[group] = world.Watermarks("ResourcesNormalized");
            world.GroupState[group] = "Stable";
            world.GroupMembers[group] = 2;
        }

        return world;
    }

    public MigrationRecord Record()
    {
        var id = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");
        return new MigrationRecord
        {
            Id = id,
            Topic = "ResourcesNormalized",
            OriginalPartitions = OriginalPartitions,
            TargetPartitions = 6,
            Step = MigrationStep.A1,
            StepStartedUtc = Now,
            Requester = "alice",
            Approver = "bob",
            Executor = "carol",
            ReplicationFactor = 3,
            BackupTopic = KafkaTopicCatalog.BackupTopicName("ResourcesNormalized", id),
            StopProducers = ["Normalization"],
            StopConsumers = ["measureeval"],
            Groups = ["measureeval", "measureeval-events"],
            SiblingsToGrow = ["ResourcesNormalized-Retry", "ResourcesNormalized-Error"]
        };
    }

    public static MigrationFacts Facts() => new()
    {
        Topic = "ResourcesNormalized",
        CurrentPartitions = 3,
        RequestedPartitions = 6,
        AllowTopicMigration = true,
        Provider = "LocalCompose",
        StoreDurable = true,
        DedicatedConnection = true,
        ReassignmentKnown = true,
        ReassignmentEmpty = true,
        ClusterHealthy = true,
        FullIsr = true,
        ReplicationFactor = 3,
        MinInSyncReplicas = 2,
        CleanupPolicy = "delete",
        EstimatedBackupMinutes = 2,
        EstimatedBytes = 1000,
        DiskKnown = true,
        FreeDiskBytes = 50_000_000,
        WorkloadsMapped = true,
        DrainMinutes = 1,
        SizeClass = "small",
        Groups = [new GroupFact { GroupId = "measureeval", Active = true, Mapped = true, Lag = 0 }],
        StopSet = ["Normalization", "measureeval"]
    };

    public TopicState Topic(string name) => Topics[name];
    public int Count(string name) => Topics.TryGetValue(name, out var topic) ? topic.Records.Count : 0;

    public List<long> Watermarks(string name)
    {
        var topic = Topics[name];
        var marks = new long[topic.Partitions];
        foreach (var record in topic.Records)
        {
            if (record.Partition >= 0 && record.Partition < marks.Length)
                marks[record.Partition] = Math.Max(marks[record.Partition], record.Offset + 1);
        }

        return marks.ToList();
    }

    public List<long> Committed(string group) => Offsets[group];

    public MigrationObservation Observe(MigrationRecord record)
    {
        Topics.TryGetValue(record.Topic, out var topic);
        Topics.TryGetValue(record.BackupTopic, out var backup);
        var seen = new MigrationObservation
        {
            PreflightOk = true,
            LivePlanHash = record.PlanHash,
            TopicPresent = topic is not null,
            TopicId = topic?.Id ?? "",
            Partitions = topic?.Partitions ?? 0,
            ReplicationFactor = topic?.Rf ?? record.ReplicationFactor,
            FullIsr = topic?.FullIsr ?? true,
            HighWatermarks = topic is null ? [] : Watermarks(record.Topic),
            Configs = topic is null ? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) : new Dictionary<string, string>(topic.Configs, StringComparer.OrdinalIgnoreCase),
            BackupPresent = backup is not null,
            BackupId = backup?.Id ?? "",
            BackupPartitions = backup?.Partitions ?? 0,
            BackupReplicationFactor = backup?.Rf ?? 0,
            BackupEmpty = backup is null || backup.Records.Count == 0,
            HoldsActive = Holds,
            OldestCreateTimeMs = topic?.Records.Count > 0 ? topic.Records.Min(item => item.TimestampMs) : backup?.Records.Count > 0 ? backup.Records.Min(item => item.TimestampMs) : 0,
            ConsumersStable = record.StopConsumers.All(name => Workloads.GetValueOrDefault(name) > 0) && GroupState.Values.All(state => state == "Stable"),
            VerificationOk = true,
            ClusterHealthy = true
        };
        foreach (var sibling in record.SiblingsToGrow)
            seen.SiblingPartitions[sibling] = Topics.TryGetValue(sibling, out var state) ? state.Partitions : 0;
        foreach (var pair in Workloads)
            seen.WorkloadReplicas[pair.Key] = pair.Value;
        foreach (var group in record.Groups)
        {
            var committed = Offsets.TryGetValue(group, out var offsets) ? offsets : [];
            seen.Groups[group] = new GroupObservation
            {
                State = GroupState.GetValueOrDefault(group, "Empty"),
                Members = GroupMembers.GetValueOrDefault(group),
                Lag = MigrationGroups.Lag(topic is null ? [] : seen.HighWatermarks, committed),
                Committed = [.. committed]
            };
        }

        var expectedRf = Math.Max(record.ReplicationFactor, Math.Min(3, topic?.Rf ?? record.ReplicationFactor));
        seen.BackupSpecMatches = backup is not null && backup.Partitions == record.TargetPartitions && backup.Rf == expectedRf;
        if (backup is not null && topic is not null && topic.Records.Count > 0)
        {
            var probe = LogCopier.Copy(new CopyRequest
            {
                MigrationId = record.Id,
                SourceTopic = record.Topic,
                TargetPartitions = record.TargetPartitions,
                Source = topic.Records,
                Destination = backup.Records
            });
            var sealedNow = probe.Ok && probe.Written.Count == 0 && backup.Records.Count == topic.Records.Count;
            seen.BackupVerified = sealedNow;
            seen.CountsMatch = sealedNow;
            seen.DigestMatch = sealedNow;
            seen.HeaderConsistent = probe.Ok && backup.Records.All(HasHeader);
            if (!probe.Ok)
            {
                seen.CopyFailed = true;
                seen.CopyFailure = probe.Failure;
            }
            if (sealedNow)
                _backupSeal = string.Join(",", backup.Records.GroupBy(item => item.Partition).OrderBy(group => group.Key).Select(group => group.Key + ":" + LogCopier.Digest(group)));
        }
        else if (backup is not null && _backupSeal is not null)
        {
            var digest = string.Join(",", backup.Records.GroupBy(item => item.Partition).OrderBy(group => group.Key).Select(group => group.Key + ":" + LogCopier.Digest(group)));
            seen.BackupVerified = digest == _backupSeal;
            seen.CountsMatch = seen.BackupVerified;
            seen.DigestMatch = seen.BackupVerified;
            seen.HeaderConsistent = backup.Records.All(HasHeader);
        }

        return seen;
    }

    public void Apply(IReadOnlyList<MigrationEffect> effects, MigrationRecord record)
    {
        foreach (var effect in effects)
        {
            switch (effect)
            {
                case MigrationEffect.CreateTopic create:
                    Create(create);
                    break;
                case MigrationEffect.DeleteTopic delete:
                    if (Topics.TryGetValue(delete.Name, out var existing))
                    {
                        if (delete.ExpectedId.Length > 0 && !string.Equals(existing.Id, delete.ExpectedId, StringComparison.Ordinal))
                            break;
                        if (delete.RequireEmpty && existing.Records.Count > 0)
                            break;
                        Topics.Remove(delete.Name);
                        Deleted.Add(delete.Name);
                    }
                    break;
                case MigrationEffect.GrowPartitions grow:
                    if (Topics.TryGetValue(grow.Name, out var sibling) && sibling.Partitions < grow.To)
                        sibling.Partitions = grow.To;
                    break;
                case MigrationEffect.ScaleWorkload scale:
                    Workloads[scale.Workload] = scale.Replicas;
                    if (record.StopConsumers.Contains(scale.Workload, StringComparer.Ordinal))
                    {
                        var up = record.StopConsumers.Any(name => Workloads.GetValueOrDefault(name) > 0);
                        foreach (var group in record.Groups)
                        {
                            GroupState[group] = up ? "Stable" : "Empty";
                            GroupMembers[group] = up ? 2 : 0;
                        }
                    }
                    break;
                case MigrationEffect.SetHold hold:
                    Holds = hold.Active;
                    break;
                case MigrationEffect.CopyLog copy:
                    Copy(copy, record);
                    break;
                case MigrationEffect.SetRetention retention:
                    if (Topics.TryGetValue(retention.Topic, out var retained))
                        retained.Configs["retention.ms"] = retention.RetentionMs.ToString();
                    break;
                case MigrationEffect.WriteOffsets write:
                    Offsets[write.Group] = [.. write.Offsets];
                    break;
                case MigrationEffect.ElectLeaders:
                    break;
            }
        }
    }

    private void Create(MigrationEffect.CreateTopic create)
    {
        if (Topics.TryGetValue(create.Name, out var existing))
        {
            var empty = existing.Records.Count == 0;
            var same = existing.Partitions == create.Partitions && existing.Rf == create.ReplicationFactor;
            if (same || !create.ReplaceIfEmpty || !empty)
            {
                Deleted.Remove(create.Name);
                return;
            }
            Topics.Remove(create.Name);
        }

        Topics[create.Name] = new TopicState
        {
            Id = create.Name.StartsWith("_linkmig-", StringComparison.Ordinal) ? "backup-id" : "topic-new",
            Partitions = create.Partitions,
            Rf = create.ReplicationFactor,
            Configs = new Dictionary<string, string>(create.Configs, StringComparer.OrdinalIgnoreCase)
        };
        Deleted.Remove(create.Name);
    }

    private void Copy(MigrationEffect.CopyLog copy, MigrationRecord record)
    {
        if (!Topics.TryGetValue(copy.Source, out var source) || !Topics.TryGetValue(copy.Destination, out var destination))
            return;
        var outcome = LogCopier.Copy(new CopyRequest
        {
            MigrationId = record.Id,
            SourceTopic = copy.Source,
            TargetPartitions = copy.TargetPartitions,
            RequireComputedTargetEqualsPartition = copy.RequireOneToOne,
            Source = source.Records,
            Destination = destination.Records
        });
        if (!outcome.Ok)
            return;
        destination.Records.AddRange(outcome.Written);
    }

    private static bool HasHeader(CopiedRecord record) =>
        record.Headers.Any(header => string.Equals(header.Name, KafkaTopicCatalog.MigrationHeader, StringComparison.OrdinalIgnoreCase));

    private static CopiedRecord Clone(CopiedRecord record) => new()
    {
        Key = record.Key,
        Value = record.Value,
        TimestampMs = record.TimestampMs,
        Partition = record.Partition,
        Offset = record.Offset,
        Headers = record.Headers.Select(header => new CopiedHeader { Name = header.Name, Value = header.Value }).ToList()
    };

    private static List<CopiedRecord> SeedRecords()
    {
        var records = new List<CopiedRecord>();
        var cursors = new int[OriginalPartitions];
        foreach (var key in new[] { "p1", "p2", "p3" })
        {
            var json = "{\"facilityId\":\"f\",\"patientId\":\"" + key + "\"}";
            var partition = KafkaMurmur.Partition(json, OriginalPartitions);
            for (var copy = 0; copy < 2; copy++)
            {
                records.Add(new CopiedRecord
                {
                    Key = Encoding.UTF8.GetBytes(json),
                    Value = Encoding.UTF8.GetBytes(key + "-" + copy),
                    Partition = partition,
                    Offset = cursors[partition]++,
                    TimestampMs = 1_700_000_000_000 + records.Count,
                    Headers = [new CopiedHeader { Name = "trace", Value = [1, 2] }]
                });
            }
        }

        records.Add(new CopiedRecord
        {
            Key = null,
            Value = [9],
            Partition = 1,
            Offset = cursors[1]++,
            TimestampMs = 1_700_000_000_100,
            Headers = [new CopiedHeader { Name = "trace", Value = [3] }]
        });
        return records;
    }
}
