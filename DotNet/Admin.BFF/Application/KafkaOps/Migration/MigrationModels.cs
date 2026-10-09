namespace LantanaGroup.Link.LinkAdmin.BFF.Application.KafkaOps;

public enum MigrationStep
{
    Planned,
    Pending,
    Approved,
    A1,
    A2,
    A3,
    A4,
    B1,
    B2,
    B3,
    B4,
    B5,
    B6,
    B7,
    H1,
    C1,
    C2,
    C3,
    C4,
    C5,
    D1,
    D2,
    D3,
    Done,
    RollingBack,
    RolledBack,
    NeedsAttention,
    Rejected
}

public enum MigrationCommand
{
    Tick,
    Abort,
    Go,
    RecoverForward,
    RecoverOriginal,
    DeleteForeign
}

public sealed class MigrationLimits
{
    public int HoldMinutes { get; set; } = 15;
    public int MaxBackupMinutes { get; set; } = 15;
    public int BackupRetentionHours { get; set; } = 168;
    public int MetadataRefreshIntervalMs { get; set; } = 60_000;
    public bool GrowDotNetError { get; set; } = true;

    public static TimeSpan For(MigrationStep step, MigrationLimits limits)
    {
        var backup = TimeSpan.FromMinutes(Math.Max(1, limits.MaxBackupMinutes));
        return step switch
        {
            MigrationStep.A1 => TimeSpan.FromMinutes(2),
            MigrationStep.A2 or MigrationStep.A3 => TimeSpan.FromMinutes(1),
            MigrationStep.B1 or MigrationStep.B4 => TimeSpan.FromMinutes(5),
            MigrationStep.B2 => TimeSpan.FromMinutes(5),
            MigrationStep.B3 => TimeSpan.FromMinutes(10),
            MigrationStep.B6 or MigrationStep.B7 => backup,
            MigrationStep.H1 => TimeSpan.FromMinutes(Math.Max(1, limits.HoldMinutes)),
            MigrationStep.C1 or MigrationStep.C2 => TimeSpan.FromMinutes(2),
            MigrationStep.C3 or MigrationStep.C4 or MigrationStep.C5 => TimeSpan.FromMinutes(2),
            MigrationStep.D1 or MigrationStep.D2 => TimeSpan.FromMinutes(10),
            MigrationStep.D3 => TimeSpan.FromMinutes(15),
            _ => TimeSpan.FromMinutes(2)
        };
    }
}

public sealed class MigrationTimelineEntry
{
    public int Sequence { get; set; }
    public MigrationStep Step { get; set; }
    public string Text { get; set; } = "";
    public DateTimeOffset At { get; set; }

    public MigrationTimelineEntry Copy() => new() { Sequence = Sequence, Step = Step, Text = Text, At = At };
}

public sealed class MigrationRecord
{
    public Guid Id { get; set; }
    public string Topic { get; set; } = "";
    public int OriginalPartitions { get; set; }
    public int TargetPartitions { get; set; }
    public MigrationStep Step { get; set; }
    public int StepSequence { get; set; }
    public int RollbackStage { get; set; }
    public string PlanHash { get; set; } = "";
    public string ApprovedHash { get; set; } = "";
    public string Requester { get; set; } = "";
    public string Approver { get; set; } = "";
    public string Executor { get; set; } = "";
    public string Reason { get; set; } = "";
    public bool BackupSkipped { get; set; }
    public bool BackupCleanupRequired { get; set; }
    public string BackupTopic { get; set; } = "";
    public string BackupTopicId { get; set; } = "";
    public string OriginalTopicId { get; set; } = "";
    public string NewTopicId { get; set; } = "";
    public int ReplicationFactor { get; set; } = 3;
    public Dictionary<string, string> FrozenConfigs { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public string CleanupPolicy { get; set; } = "delete";
    public List<string> StopProducers { get; set; } = [];
    public List<string> StopConsumers { get; set; } = [];
    public Dictionary<string, int> ProducerReplicas { get; set; } = new(StringComparer.Ordinal);
    public Dictionary<string, int> ConsumerReplicas { get; set; } = new(StringComparer.Ordinal);
    public List<string> Groups { get; set; } = [];
    public List<string> GroupsWithCommits { get; set; } = [];
    public List<string> SiblingsToGrow { get; set; } = [];
    public List<long> SampleHighWatermarks { get; set; } = [];
    public List<long> FrozenHighWatermarks { get; set; } = [];
    public DateTimeOffset? StepStartedUtc { get; set; }
    public DateTimeOffset? QuietSinceUtc { get; set; }
    public DateTimeOffset? DrainMatchedUtc { get; set; }
    public string Failure { get; set; } = "";
    public string Evidence { get; set; } = "";
    public long RetentionOverrideMs { get; set; }
    public bool ManualChecklist { get; set; }
    public List<string> ManualConfirmed { get; set; } = [];
    public string RecoveryChoice { get; set; } = "";
    public List<MigrationTimelineEntry> Timeline { get; set; } = [];
    public long Fence { get; set; }

    public int ActiveTarget =>
        string.Equals(RecoveryChoice, "original", StringComparison.Ordinal) ? OriginalPartitions : TargetPartitions;

    public bool BeforeDelete => Step is MigrationStep.Planned or MigrationStep.Pending or MigrationStep.Approved
        or MigrationStep.A1 or MigrationStep.A2 or MigrationStep.A3 or MigrationStep.A4
        or MigrationStep.B1 or MigrationStep.B2 or MigrationStep.B3 or MigrationStep.B4
        or MigrationStep.B5 or MigrationStep.B6 or MigrationStep.B7 or MigrationStep.H1
        or MigrationStep.RollingBack;

    public MigrationRecord Copy()
    {
        var clone = (MigrationRecord)MemberwiseClone();
        clone.FrozenConfigs = new Dictionary<string, string>(FrozenConfigs, StringComparer.OrdinalIgnoreCase);
        clone.StopProducers = [.. StopProducers];
        clone.StopConsumers = [.. StopConsumers];
        clone.ProducerReplicas = new Dictionary<string, int>(ProducerReplicas, StringComparer.Ordinal);
        clone.ConsumerReplicas = new Dictionary<string, int>(ConsumerReplicas, StringComparer.Ordinal);
        clone.Groups = [.. Groups];
        clone.GroupsWithCommits = [.. GroupsWithCommits];
        clone.SiblingsToGrow = [.. SiblingsToGrow];
        clone.SampleHighWatermarks = [.. SampleHighWatermarks];
        clone.FrozenHighWatermarks = [.. FrozenHighWatermarks];
        clone.ManualConfirmed = [.. ManualConfirmed];
        clone.Timeline = Timeline.Select(entry => entry.Copy()).ToList();
        return clone;
    }
}

public sealed class GroupObservation
{
    public string State { get; set; } = "Stable";
    public int Members { get; set; }
    public long Lag { get; set; }
    public List<long> Committed { get; set; } = [];
    public bool Active { get; set; }
    public bool Mapped { get; set; } = true;
}

public sealed class MigrationObservation
{
    public bool PreflightOk { get; set; } = true;
    public string PreflightError { get; set; } = "";
    public string LivePlanHash { get; set; } = "";
    public bool TopicPresent { get; set; } = true;
    public string TopicId { get; set; } = "";
    public int Partitions { get; set; }
    public int ReplicationFactor { get; set; } = 3;
    public bool FullIsr { get; set; } = true;
    public bool LeadersSkewed { get; set; }
    public List<long> HighWatermarks { get; set; } = [];
    public Dictionary<string, string> Configs { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public bool BackupPresent { get; set; }
    public string BackupId { get; set; } = "";
    public int BackupPartitions { get; set; }
    public int BackupReplicationFactor { get; set; }
    public bool BackupEmpty { get; set; } = true;
    public bool BackupSpecMatches { get; set; }
    public bool BackupVerified { get; set; }
    public bool HeaderConsistent { get; set; } = true;
    public bool CountsMatch { get; set; } = true;
    public bool DigestMatch { get; set; } = true;
    public bool CopyFailed { get; set; }
    public string CopyFailure { get; set; } = "";
    public Dictionary<string, int> SiblingPartitions { get; set; } = new(StringComparer.Ordinal);
    public Dictionary<string, int> WorkloadReplicas { get; set; } = new(StringComparer.Ordinal);
    public bool HoldsActive { get; set; }
    public Dictionary<string, GroupObservation> Groups { get; set; } = new(StringComparer.Ordinal);
    public bool ConsumersStable { get; set; }
    public bool VerificationOk { get; set; } = true;
    public long OldestCreateTimeMs { get; set; }
    public bool ClusterHealthy { get; set; } = true;
}

public abstract record MigrationEffect
{
    public sealed record CreateTopic(string Name, int Partitions, int ReplicationFactor, bool ReplaceIfEmpty, IReadOnlyDictionary<string, string> Configs) : MigrationEffect;
    public sealed record DeleteTopic(string Name, string ExpectedId, bool RequireEmpty) : MigrationEffect;
    public sealed record GrowPartitions(string Name, int To) : MigrationEffect;
    public sealed record ScaleWorkload(string Workload, int Replicas) : MigrationEffect;
    public sealed record SetHold(bool Active) : MigrationEffect;
    public sealed record CopyLog(string Source, string Destination, int TargetPartitions, bool RequireOneToOne) : MigrationEffect;
    public sealed record SetRetention(string Topic, long RetentionMs) : MigrationEffect;
    public sealed record WriteOffsets(string Group, IReadOnlyList<long> Offsets) : MigrationEffect;
    public sealed record ElectLeaders(string Topic) : MigrationEffect;
}

public sealed class MigrationTick
{
    public MigrationRecord? Persist { get; init; }
    public MigrationRecord? Completed { get; init; }
    public IReadOnlyList<MigrationEffect> Effects { get; init; } = [];
    public bool Wait { get; init; }
    public string? Error { get; init; }

    public static MigrationTick Done(MigrationRecord record) => new() { Completed = record };
    public static MigrationTick Acting(MigrationRecord persist, IReadOnlyList<MigrationEffect> effects) =>
        new() { Persist = persist, Effects = effects };
    public static MigrationTick Waiting(MigrationRecord record) => new() { Persist = record, Wait = true };
    public static MigrationTick Reject(string error) => new() { Error = error };
}
