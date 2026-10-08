namespace LantanaGroup.Link.LinkAdmin.BFF.Application.KafkaOps;

public static class MigrationMachine
{
    public static readonly MigrationStep[] ActionSteps =
    [
        MigrationStep.A1, MigrationStep.A2, MigrationStep.A3, MigrationStep.A4,
        MigrationStep.B1, MigrationStep.B2, MigrationStep.B3, MigrationStep.B4, MigrationStep.B5, MigrationStep.B6, MigrationStep.B7,
        MigrationStep.H1,
        MigrationStep.C1, MigrationStep.C2, MigrationStep.C3, MigrationStep.C4, MigrationStep.C5,
        MigrationStep.D1, MigrationStep.D2, MigrationStep.D3
    ];

    public static MigrationTick Describe(
        MigrationRecord record,
        MigrationObservation seen,
        MigrationCommand command,
        string? actor,
        string? typedName,
        DateTimeOffset now,
        MigrationLimits? limits = null)
    {
        limits ??= new MigrationLimits();
        if (record.Step is MigrationStep.Done or MigrationStep.RolledBack or MigrationStep.Rejected)
        {
            if (command is MigrationCommand.Abort or MigrationCommand.Go or MigrationCommand.RecoverForward or MigrationCommand.RecoverOriginal or MigrationCommand.DeleteForeign)
                return MigrationTick.Reject("The migration is already " + record.Step + ".");
            return MigrationTick.Done(record);
        }

        if (record.Step == MigrationStep.NeedsAttention)
            return Recover(record, seen, command, actor, typedName, now);

        // Recover-original is valid while the new topic is absent or still empty (C2-C4).
        // After C5 the offsets are written and recovery stays on the NeedsAttention path.
        if (command is MigrationCommand.RecoverForward or MigrationCommand.RecoverOriginal or MigrationCommand.DeleteForeign
            && record.Step is MigrationStep.C2 or MigrationStep.C3 or MigrationStep.C4)
            return Recover(record, seen, command, actor, typedName, now);

        if (record.Step == MigrationStep.RollingBack)
            return Rollback(record, seen, now);

        if (command == MigrationCommand.Abort)
        {
            if (!record.BeforeDelete)
                return Attention(record, "Abort does not delete anything after the original topic is removed.", now);
            return BeginRollback(record, "Aborted.", now);
        }

        if (command == MigrationCommand.Go)
            return Go(record, actor, typedName, now);

        if (command is MigrationCommand.RecoverForward or MigrationCommand.RecoverOriginal or MigrationCommand.DeleteForeign)
            return MigrationTick.Reject("Recovery is only available from NeedsAttention.");

        if (!seen.ClusterHealthy && record.Step is MigrationStep.C3 or MigrationStep.C4)
        {
            if (TimedOut(record, now, limits))
                return Attention(record, "The cluster was not healthy in time to recreate the topic.", now);
            return MigrationTick.Waiting(Note(record, now, "Waiting for a healthy cluster."));
        }

        if (TimedOut(record, now, limits) && !Postcondition(record, seen, now, limits))
            return Fail(record, "The step timed out.", now);

        if (Postcondition(record, seen, now, limits))
            return MigrationTick.Done(Advance(record, seen, now));

        return Act(record, seen, now, limits);
    }

    private static MigrationTick Go(MigrationRecord record, string? actor, string? typedName, DateTimeOffset now)
    {
        if (record.Step != MigrationStep.H1)
            return MigrationTick.Reject("The go confirmation is only accepted at the hold point.");
        if (!string.Equals(typedName, record.Topic, StringComparison.Ordinal))
            return MigrationTick.Reject("Type the topic name to delete it and continue.");
        if (string.IsNullOrWhiteSpace(actor) || string.Equals(actor, record.Requester, StringComparison.OrdinalIgnoreCase))
            return MigrationTick.Reject("The requester cannot execute the hold point.");
        if (!string.Equals(actor, record.Executor, StringComparison.OrdinalIgnoreCase))
            return MigrationTick.Reject("The person who started the migration must confirm the hold point.");
        return MigrationTick.Done(Advance(record, new MigrationObservation(), now, MigrationStep.C1));
    }

    private static bool Postcondition(MigrationRecord record, MigrationObservation seen, DateTimeOffset now, MigrationLimits limits)
    {
        return record.Step switch
        {
            MigrationStep.A1 => seen.PreflightOk && HashMatches(record, seen),
            MigrationStep.A2 => record.BackupSkipped || (seen.BackupPresent && seen.BackupSpecMatches && seen.BackupEmpty && seen.BackupId.Length > 0),
            MigrationStep.A3 => record.SiblingsToGrow.All(name => seen.SiblingPartitions.GetValueOrDefault(name) >= record.TargetPartitions),
            MigrationStep.A4 => seen.HoldsActive,
            MigrationStep.B1 => Scaled(record, record.StopProducers, record.ProducerReplicas, seen, 0),
            MigrationStep.B2 => Quiet(record, seen, now, limits),
            MigrationStep.B3 => record.DrainMatchedUtc is { } since && now - since >= TimeSpan.FromSeconds(10) && NoLag(seen),
            MigrationStep.B4 => Scaled(record, record.StopConsumers, record.ConsumerReplicas, seen, 0) && GroupsEmpty(seen, record),
            MigrationStep.B5 => Frozen(record, seen),
            MigrationStep.B6 => record.BackupSkipped || (seen.BackupVerified && seen.HeaderConsistent),
            MigrationStep.B7 => record.BackupSkipped || (seen.BackupVerified && seen.CountsMatch && seen.DigestMatch && seen.HeaderConsistent),
            MigrationStep.H1 => false,
            MigrationStep.C1 => !seen.TopicPresent,
            MigrationStep.C2 => !seen.TopicPresent,
            MigrationStep.C3 => Adopted(record, seen),
            MigrationStep.C4 => VerifiedEmpty(record, seen),
            MigrationStep.C5 => OffsetsAtZero(record, seen),
            MigrationStep.D1 => Scaled(record, record.StopConsumers, record.ConsumerReplicas, seen, -1) && seen.ConsumersStable,
            MigrationStep.D2 => !seen.HoldsActive && Scaled(record, record.StopProducers, record.ProducerReplicas, seen, -1),
            MigrationStep.D3 => seen.VerificationOk,
            _ => false
        };
    }

    private static MigrationTick Act(MigrationRecord record, MigrationObservation seen, DateTimeOffset now, MigrationLimits limits)
    {
        if (seen.CopyFailed && record.Step is MigrationStep.B6 or MigrationStep.B7)
            return Fail(record, seen.CopyFailure.Length > 0 ? seen.CopyFailure : "The backup copy failed.", now);

        var effects = new List<MigrationEffect>();
        var persist = record.Copy();
        switch (record.Step)
        {
            case MigrationStep.A1:
                return Fail(record, seen.PreflightError.Length > 0 ? seen.PreflightError : "Preflight failed.", now);
            case MigrationStep.A2:
                if (seen.BackupPresent && !seen.BackupEmpty)
                    return Fail(record, "The temp topic already has records and was left in place.", now);
                effects.Add(new MigrationEffect.CreateTopic(record.BackupTopic, record.TargetPartitions, BackupRf(record, seen), true, RetentionConfigs(record, seen, now, limits)));
                break;
            case MigrationStep.A3:
                foreach (var name in record.SiblingsToGrow)
                {
                    if (seen.SiblingPartitions.GetValueOrDefault(name) < record.TargetPartitions)
                        effects.Add(new MigrationEffect.GrowPartitions(name, record.TargetPartitions));
                }
                break;
            case MigrationStep.A4:
                effects.Add(new MigrationEffect.SetHold(true));
                break;
            case MigrationStep.B1:
                if (persist.ProducerReplicas.Count == 0)
                {
                    Capture(persist, record.StopProducers, seen);
                    return MigrationTick.Done(Note(persist, now, "Recorded producer replicas."));
                }
                ScaleTo(effects, record.StopProducers, 0);
                break;
            case MigrationStep.B2:
                if (record.QuietSinceUtc is null || !Same(record.SampleHighWatermarks, seen.HighWatermarks))
                {
                    persist.SampleHighWatermarks = [.. seen.HighWatermarks];
                    persist.QuietSinceUtc = now;
                    return MigrationTick.Waiting(Note(persist, now, "Quiet window restarted."));
                }

                return MigrationTick.Waiting(record);
            case MigrationStep.B3:
                if (!NoLag(seen))
                {
                    persist.DrainMatchedUtc = null;
                    return MigrationTick.Waiting(Note(persist, now, "Waiting for lag to reach zero."));
                }
                persist.DrainMatchedUtc ??= now;
                return MigrationTick.Waiting(Note(persist, now, "Lag is zero. Waiting for a second read."));
            case MigrationStep.B4:
                if (persist.ConsumerReplicas.Count == 0)
                {
                    Capture(persist, record.StopConsumers, seen);
                    return MigrationTick.Done(Note(persist, now, "Recorded consumer replicas."));
                }
                ScaleTo(effects, record.StopConsumers, 0);
                break;
            case MigrationStep.B5:
                return Fail(record, "The frozen facts no longer match the broker.", now);
            case MigrationStep.B6:
                effects.Add(new MigrationEffect.SetRetention(record.BackupTopic, Floor(record, seen, now, limits)));
                effects.Add(new MigrationEffect.CopyLog(record.Topic, record.BackupTopic, record.TargetPartitions, false));
                break;
            case MigrationStep.B7:
                return Fail(record, "The backup digest did not match.", now);
            case MigrationStep.H1:
                return MigrationTick.Waiting(record);
            case MigrationStep.C1:
                if (seen.TopicPresent && record.OriginalTopicId.Length > 0 && !string.Equals(seen.TopicId, record.OriginalTopicId, StringComparison.Ordinal))
                    return Attention(record, "T has a foreign topic id. It was not deleted.", now);
                effects.Add(new MigrationEffect.DeleteTopic(record.Topic, record.OriginalTopicId, false));
                break;
            case MigrationStep.C2:
                return MigrationTick.Waiting(Note(persist, now, "Waiting until T is absent."));
            case MigrationStep.C3:
                if (seen.TopicPresent && !Adopted(record, seen))
                    return Attention(record, "T exists with an unexpected id, partition count, or data. It was not deleted.", now);
                effects.Add(new MigrationEffect.CreateTopic(record.Topic, record.ActiveTarget, record.ReplicationFactor, false, record.FrozenConfigs));
                break;
            case MigrationStep.C4:
                if (seen.TopicPresent && seen.HighWatermarks.Any(mark => mark > 0))
                    return Attention(record, "The new topic is not empty. It was not deleted.", now);
                if (seen.LeadersSkewed)
                    effects.Add(new MigrationEffect.ElectLeaders(record.Topic));
                else if (!seen.FullIsr)
                    return MigrationTick.Waiting(Note(persist, now, "Waiting for a full ISR."));
                else
                    return Attention(record, "The new topic did not match the frozen configuration.", now);
                break;
            case MigrationStep.C5:
                if (!GroupsEmpty(seen, record))
                    return Attention(record, "A consumer group is not empty, so offsets were not written.", now);
                if (seen.HighWatermarks.Any(mark => mark > 0))
                    return Attention(record, "The new topic is not empty, so offsets were not written.", now);
                var zeros = ZeroOffsets(record, seen);
                foreach (var group in record.Groups)
                    effects.Add(new MigrationEffect.WriteOffsets(group, zeros));
                break;
            case MigrationStep.D1:
                ScaleTo(effects, record.StopConsumers, record.ConsumerReplicas);
                break;
            case MigrationStep.D2:
                effects.Add(new MigrationEffect.SetHold(false));
                ScaleTo(effects, record.StopProducers, record.ProducerReplicas);
                break;
            case MigrationStep.D3:
                return MigrationTick.Waiting(Note(persist, now, "Verification is still running."));
            default:
                return MigrationTick.Reject("This step cannot run.");
        }

        return MigrationTick.Acting(Note(persist, now, "Started " + record.Step), effects);
    }

    private static MigrationRecord Advance(MigrationRecord record, MigrationObservation seen, DateTimeOffset now, MigrationStep? forced = null)
    {
        var next = record.Copy();
        var step = forced ?? Next(record.Step);
        if (record.Step == MigrationStep.A2 && seen.BackupId.Length > 0)
            next.BackupTopicId = seen.BackupId;
        if (record.Step == MigrationStep.C3 && seen.TopicId.Length > 0)
            next.NewTopicId = seen.TopicId;
        if (record.Step == MigrationStep.B5)
        {
            next.OriginalTopicId = seen.TopicId;
            next.FrozenHighWatermarks = [.. seen.HighWatermarks];
            next.FrozenConfigs = new Dictionary<string, string>(seen.Configs, StringComparer.OrdinalIgnoreCase);
            next.ReplicationFactor = seen.ReplicationFactor > 0 ? seen.ReplicationFactor : record.ReplicationFactor;
        }

        if (record.Step == MigrationStep.B7 && record.BackupSkipped)
            next.Evidence = "The backup was skipped. The recreated topic starts empty.";

        next.Step = step;
        next.StepStartedUtc = now;
        next.StepSequence++;
        next.Failure = "";
        next.Timeline.Add(new MigrationTimelineEntry { Sequence = next.StepSequence, Step = step, Text = "Entered " + step, At = now });
        return next;
    }

    private static MigrationTick Fail(MigrationRecord record, string reason, DateTimeOffset now)
    {
        if (record.BeforeDelete)
            return BeginRollback(record, reason, now);
        return Attention(record, reason, now);
    }

    private static MigrationTick BeginRollback(MigrationRecord record, string reason, DateTimeOffset now)
    {
        var next = record.Copy();
        next.Step = MigrationStep.RollingBack;
        next.RollbackStage = 0;
        next.Failure = reason;
        next.StepStartedUtc = now;
        next.StepSequence++;
        next.Timeline.Add(new MigrationTimelineEntry { Sequence = next.StepSequence, Step = MigrationStep.RollingBack, Text = reason, At = now });
        return MigrationTick.Done(next);
    }

    private static MigrationTick Attention(MigrationRecord record, string reason, DateTimeOffset now)
    {
        var next = record.Copy();
        next.Step = MigrationStep.NeedsAttention;
        next.Failure = reason;
        next.StepStartedUtc = now;
        next.StepSequence++;
        if (record.BackupTopic.Length > 0)
            next.BackupCleanupRequired = true;
        next.Timeline.Add(new MigrationTimelineEntry { Sequence = next.StepSequence, Step = MigrationStep.NeedsAttention, Text = reason, At = now });
        return MigrationTick.Done(next);
    }

    private static MigrationTick Rollback(MigrationRecord record, MigrationObservation seen, DateTimeOffset now)
    {
        var effects = new List<MigrationEffect>();
        var next = record.Copy();
        switch (record.RollbackStage)
        {
            case 0:
                if (!Restored(record, record.StopConsumers, record.ConsumerReplicas, seen))
                {
                    ScaleTo(effects, record.StopConsumers, record.ConsumerReplicas);
                    return MigrationTick.Acting(next, effects);
                }
                next.RollbackStage = 1;
                break;
            case 1:
                if (seen.HoldsActive)
                    return MigrationTick.Acting(next, [new MigrationEffect.SetHold(false)]);
                next.RollbackStage = 2;
                break;
            case 2:
                if (!Restored(record, record.StopProducers, record.ProducerReplicas, seen))
                {
                    ScaleTo(effects, record.StopProducers, record.ProducerReplicas);
                    return MigrationTick.Acting(next, effects);
                }
                next.RollbackStage = 3;
                break;
            case 3:
                if (seen.TopicPresent && record.OriginalTopicId.Length > 0 && !string.Equals(seen.TopicId, record.OriginalTopicId, StringComparison.Ordinal))
                    return Attention(record, "Rollback stopped because T's topic id changed.", now);
                if (seen.TopicPresent && record.OriginalPartitions > 0 && seen.Partitions != record.OriginalPartitions)
                    return Attention(record, "Rollback stopped because T's partition count changed.", now);
                next.RollbackStage = 4;
                break;
            default:
                if (seen.BackupPresent)
                    next.BackupCleanupRequired = true;
                next.Step = MigrationStep.RolledBack;
                next.StepSequence++;
                next.Timeline.Add(new MigrationTimelineEntry { Sequence = next.StepSequence, Step = MigrationStep.RolledBack, Text = "Rolled back. The temp topic was kept.", At = now });
                return MigrationTick.Done(next);
        }

        next.StepSequence++;
        return MigrationTick.Done(next);
    }

    private static MigrationTick Recover(MigrationRecord record, MigrationObservation seen, MigrationCommand command, string? actor, string? typedName, DateTimeOffset now)
    {
        if (command == MigrationCommand.Tick)
            return MigrationTick.Waiting(record);
        if (!string.Equals(typedName, record.Topic, StringComparison.Ordinal))
            return MigrationTick.Reject("Type the topic name to recover.");
        if (string.IsNullOrWhiteSpace(actor) || string.Equals(actor, record.Requester, StringComparison.OrdinalIgnoreCase))
            return MigrationTick.Reject("The requester cannot recover the migration.");

        if (command == MigrationCommand.DeleteForeign)
        {
            if (string.Equals(actor, record.Executor, StringComparison.OrdinalIgnoreCase))
                return MigrationTick.Reject("A second person must delete a foreign topic.");
            if (!seen.TopicPresent || seen.HighWatermarks.Any(mark => mark > 0))
                return MigrationTick.Reject("A foreign topic is deleted only when it is empty.");
            // The delete stops here. Forward is a separate command and is what finishes the migration.
            var next = record.Copy();
            next.NewTopicId = "";
            next.Failure = "";
            return MigrationTick.Acting(Note(next, now, "Deleting the empty foreign topic."), [new MigrationEffect.DeleteTopic(record.Topic, seen.TopicId, true)]);
        }

        if (command == MigrationCommand.RecoverOriginal)
        {
            if (seen.TopicPresent && string.Equals(seen.TopicId, record.OriginalTopicId, StringComparison.Ordinal))
                return BeginRollback(record, "The original topic is still present.", now);
            if (seen.TopicPresent)
            {
                if (seen.HighWatermarks.Any(mark => mark > 0))
                    return MigrationTick.Reject("The new topic has records, so it was not deleted.");
                if (record.NewTopicId.Length > 0 && !string.Equals(seen.TopicId, record.NewTopicId, StringComparison.Ordinal))
                    return MigrationTick.Reject("The topic id does not match the recorded id, so it was not deleted.");
                if (string.Equals(actor, record.Executor, StringComparison.OrdinalIgnoreCase))
                    return MigrationTick.Reject("A second person must delete the empty topic before it is recreated.");
                var deleting = record.Copy();
                deleting.RecoveryChoice = "original";
                deleting.Step = MigrationStep.C2;
                deleting.NewTopicId = "";
                deleting.Failure = "";
                return MigrationTick.Acting(Note(deleting, now, "Deleting the empty new topic before recreating the original partition count."), [new MigrationEffect.DeleteTopic(record.Topic, seen.TopicId, true)]);
            }

            var next = record.Copy();
            next.RecoveryChoice = "original";
            next.Step = MigrationStep.C3;
            next.Failure = "";
            next.StepStartedUtc = now;
            return MigrationTick.Done(Note(next, now, "Recreating T with the original partition count."));
        }

        if (command == MigrationCommand.RecoverForward)
        {
            if (seen.TopicPresent && !Adopted(record, seen))
            {
                if (seen.HighWatermarks.Any(mark => mark > 0))
                    return MigrationTick.Reject("The topic has records. Delete it with kafka-topics.sh --delete --topic " + record.Topic + ", then continue. The migration will not delete it.");
                return MigrationTick.Reject("The topic id does not match this migration. A second person can delete it when it is empty, or delete it with kafka-topics.sh --delete --topic " + record.Topic + ".");
            }

            var next = record.Copy();
            next.RecoveryChoice = "";
            next.Step = seen.TopicPresent && Adopted(next, seen) ? MigrationStep.C4 : MigrationStep.C3;
            next.Failure = "";
            next.StepStartedUtc = now;
            return MigrationTick.Done(Note(next, now, "Continuing forward at the target partition count."));
        }

        return MigrationTick.Reject("That recovery command is not recognized.");
    }

    private static MigrationStep Next(MigrationStep step) => step switch
    {
        MigrationStep.A1 => MigrationStep.A2,
        MigrationStep.A2 => MigrationStep.A3,
        MigrationStep.A3 => MigrationStep.A4,
        MigrationStep.A4 => MigrationStep.B1,
        MigrationStep.B1 => MigrationStep.B2,
        MigrationStep.B2 => MigrationStep.B3,
        MigrationStep.B3 => MigrationStep.B4,
        MigrationStep.B4 => MigrationStep.B5,
        MigrationStep.B5 => MigrationStep.B6,
        MigrationStep.B6 => MigrationStep.B7,
        MigrationStep.B7 => MigrationStep.H1,
        MigrationStep.H1 => MigrationStep.C1,
        MigrationStep.C1 => MigrationStep.C2,
        MigrationStep.C2 => MigrationStep.C3,
        MigrationStep.C3 => MigrationStep.C4,
        MigrationStep.C4 => MigrationStep.C5,
        MigrationStep.C5 => MigrationStep.D1,
        MigrationStep.D1 => MigrationStep.D2,
        MigrationStep.D2 => MigrationStep.D3,
        MigrationStep.D3 => MigrationStep.Done,
        _ => step
    };

    private static bool HashMatches(MigrationRecord record, MigrationObservation seen) =>
        record.PlanHash.Length == 0 || seen.LivePlanHash.Length == 0 || string.Equals(record.PlanHash, seen.LivePlanHash, StringComparison.Ordinal);

    private static bool Quiet(MigrationRecord record, MigrationObservation seen, DateTimeOffset now, MigrationLimits limits) =>
        record.QuietSinceUtc is { } since
        && now - since >= TimeSpan.FromMilliseconds(Math.Max(1, limits.MetadataRefreshIntervalMs) * 2L)
        && Same(record.SampleHighWatermarks, seen.HighWatermarks);

    private static bool Frozen(MigrationRecord record, MigrationObservation seen) =>
        Same(record.SampleHighWatermarks, seen.HighWatermarks)
        && GroupsEmpty(seen, record)
        && NoLag(seen)
        && seen.TopicPresent;

    private static bool Adopted(MigrationRecord record, MigrationObservation seen)
    {
        if (!seen.TopicPresent || seen.Partitions != record.ActiveTarget)
            return false;
        if (record.NewTopicId.Length > 0)
            return string.Equals(seen.TopicId, record.NewTopicId, StringComparison.Ordinal);
        if (record.FrozenConfigs.Count > 0 && !ConfigsMatch(record, seen))
            return false;
        return seen.HighWatermarks.Count == 0 || seen.HighWatermarks.All(mark => mark == 0);
    }

    private static bool VerifiedEmpty(MigrationRecord record, MigrationObservation seen) =>
        Adopted(record, seen)
        && seen.FullIsr
        && seen.HighWatermarks.All(mark => mark == 0)
        && ConfigsMatch(record, seen);

    private static bool OffsetsAtZero(MigrationRecord record, MigrationObservation seen)
    {
        var expected = ZeroOffsets(record, seen);
        if (seen.HighWatermarks.Count != expected.Count || seen.HighWatermarks.Any(mark => mark != 0))
            return false;
        foreach (var name in record.Groups)
        {
            if (!seen.Groups.TryGetValue(name, out var group))
                return false;
            if (!string.Equals(group.State, "Empty", StringComparison.OrdinalIgnoreCase) || group.Members != 0)
                return false;
            if (!Same(group.Committed, expected))
                return false;
        }

        return true;
    }

    private static List<long> ZeroOffsets(MigrationRecord record, MigrationObservation seen)
    {
        var count = seen.HighWatermarks.Count > 0 ? seen.HighWatermarks.Count : record.ActiveTarget;
        return Enumerable.Repeat(0L, count).ToList();
    }

    private static bool ConfigsMatch(MigrationRecord record, MigrationObservation seen)
    {
        foreach (var pair in record.FrozenConfigs)
        {
            if (string.Equals(pair.Key, "retention.ms", StringComparison.OrdinalIgnoreCase))
                continue;
            if (!seen.Configs.TryGetValue(pair.Key, out var value) || !string.Equals(value, pair.Value, StringComparison.Ordinal))
                return false;
        }

        return true;
    }

    private static bool GroupsEmpty(MigrationObservation seen, MigrationRecord record) =>
        record.Groups.All(name => seen.Groups.TryGetValue(name, out var group)
            && string.Equals(group.State, "Empty", StringComparison.OrdinalIgnoreCase)
            && group.Members == 0);

    private static bool NoLag(MigrationObservation seen) => seen.Groups.Values.All(group => group.Lag == 0);

    private static bool Restored(MigrationRecord record, IReadOnlyList<string> names, Dictionary<string, int> recorded, MigrationObservation seen)
    {
        if (recorded.Count == 0)
            return true;
        return Scaled(record, names, recorded, seen, -1);
    }

    private static bool Scaled(MigrationRecord record, IReadOnlyList<string> names, Dictionary<string, int> recorded, MigrationObservation seen, int forced)
    {
        foreach (var name in names)
        {
            if (record.ManualChecklist && record.ManualConfirmed.Contains(name, StringComparer.Ordinal))
                continue;
            var expected = forced >= 0 ? forced : recorded.GetValueOrDefault(name);
            if (!seen.WorkloadReplicas.TryGetValue(name, out var actual) || actual != expected)
                return false;
        }

        return true;
    }

    private static void Capture(MigrationRecord record, IReadOnlyList<string> names, MigrationObservation seen)
    {
        var target = record.Step == MigrationStep.B4 ? record.ConsumerReplicas : record.ProducerReplicas;
        foreach (var name in names)
            target[name] = seen.WorkloadReplicas.GetValueOrDefault(name);
    }

    private static void ScaleTo(List<MigrationEffect> effects, IReadOnlyList<string> names, int replicas)
    {
        foreach (var name in names)
            effects.Add(new MigrationEffect.ScaleWorkload(name, replicas));
    }

    private static void ScaleTo(List<MigrationEffect> effects, IReadOnlyList<string> names, Dictionary<string, int> recorded)
    {
        foreach (var name in names)
            effects.Add(new MigrationEffect.ScaleWorkload(name, recorded.GetValueOrDefault(name)));
    }

    private static int BackupRf(MigrationRecord record, MigrationObservation seen) =>
        Math.Max(record.ReplicationFactor, Math.Min(3, Math.Max(1, seen.ReplicationFactor)));

    private static long Floor(MigrationRecord record, MigrationObservation seen, DateTimeOffset now, MigrationLimits limits)
    {
        var frozen = record.FrozenConfigs.TryGetValue("retention.ms", out var text) && long.TryParse(text, out var parsed) ? parsed : 0;
        return LogCopier.RetentionFloor(frozen, seen.OldestCreateTimeMs, now, limits.BackupRetentionHours);
    }

    private static Dictionary<string, string> RetentionConfigs(MigrationRecord record, MigrationObservation seen, DateTimeOffset now, MigrationLimits limits)
    {
        var configs = new Dictionary<string, string>(record.FrozenConfigs, StringComparer.OrdinalIgnoreCase);
        configs["retention.ms"] = Floor(record, seen, now, limits).ToString();
        configs["min.insync.replicas"] = Math.Min(2, Math.Max(1, BackupRf(record, seen))).ToString();
        return configs;
    }

    private static bool TimedOut(MigrationRecord record, DateTimeOffset now, MigrationLimits limits)
    {
        if (record.StepStartedUtc is null)
            return false;
        return now - record.StepStartedUtc.Value > MigrationLimits.For(record.Step, limits);
    }

    private static bool Same(IReadOnlyList<long> left, IReadOnlyList<long> right)
    {
        if (left.Count != right.Count)
            return false;
        for (var i = 0; i < left.Count; i++)
        {
            if (left[i] != right[i])
                return false;
        }

        return true;
    }

    private static MigrationRecord Note(MigrationRecord record, DateTimeOffset now, string text)
    {
        record.StepSequence++;
        record.StepStartedUtc ??= now;
        record.Timeline.Add(new MigrationTimelineEntry { Sequence = record.StepSequence, Step = record.Step, Text = text, At = now });
        return record;
    }
}

public static class MigrationApprovals
{
    public static string? ValidatePlanHash(string? clientHash, string liveHash)
    {
        if (string.IsNullOrWhiteSpace(clientHash))
            return "The plan hash from the dry run is required.";
        if (!string.Equals(clientHash.Trim(), liveHash, StringComparison.Ordinal))
            return "The plan changed. Request it again.";
        return null;
    }

    public static string? ValidateRequest(string? requester, string? typedName, string topic, string? reason)
    {
        if (string.IsNullOrWhiteSpace(requester))
            return "A requester is required.";
        if (!string.Equals(typedName, topic, StringComparison.Ordinal))
            return "Type the topic name to request the migration.";
        if (string.IsNullOrWhiteSpace(reason))
            return "A reason is required.";
        return null;
    }

    public static string? ValidateApprove(MigrationRecord record, string? approver, string liveHash)
    {
        if (record.Step != MigrationStep.Pending)
            return "Only a pending migration can be approved.";
        if (string.IsNullOrWhiteSpace(approver) || string.Equals(approver, record.Requester, StringComparison.OrdinalIgnoreCase))
            return "A different person must approve the migration.";
        if (!string.Equals(record.PlanHash, liveHash, StringComparison.Ordinal))
            return "The plan changed. Request it again.";
        return null;
    }

    public static string? ValidateExecute(MigrationRecord record, string? executor)
    {
        if (record.Step != MigrationStep.Approved)
            return "Approve the migration before it runs.";
        if (string.IsNullOrWhiteSpace(executor) || string.Equals(executor, record.Requester, StringComparison.OrdinalIgnoreCase))
            return "The requester cannot execute the migration.";
        if (!string.Equals(record.ApprovedHash, record.PlanHash, StringComparison.Ordinal))
            return "The approval is not bound to the current plan.";
        return null;
    }
}

public static class MigrationGroups
{
    public static bool HasCommit(IReadOnlyList<long>? committed) =>
        committed is { Count: > 0 } && committed.Any(offset => offset >= 0);

    public static long Lag(IReadOnlyList<long> watermarks, IReadOnlyList<long>? committed)
    {
        if (!HasCommit(committed))
            return 0;
        long lag = 0;
        for (var i = 0; i < watermarks.Count; i++)
        {
            var commit = i < committed!.Count ? committed[i] : -1L;
            lag += Math.Max(0, watermarks[i] - (commit >= 0 ? commit : 0));
        }

        return lag;
    }

    public static bool Ready(string? state, int members, IReadOnlyList<long>? committed) =>
        string.Equals(state, "Stable", StringComparison.OrdinalIgnoreCase)
        || (string.Equals(state, "Empty", StringComparison.OrdinalIgnoreCase) && members == 0 && !HasCommit(committed));
}
