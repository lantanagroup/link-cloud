namespace Link.UI.Services;

public static class MigrationGroupList
{
    public static List<string> Parse(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return [];

        return text
            .Split([',', ';', '\n', '\r'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(name => name.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToList();
    }

    public static string Canonical(string? text) => string.Join("\n", Parse(text));
}

public static class MigrationRequestGuard
{
    public const string NeedsDryRun = "Dry run the migration before requesting it.";
    public const string InputsDiffer = "Dry run again. The partitions, backup choice, or acknowledged groups no longer match the dry run.";

    public static string? Refusal(
        int partitions,
        bool backupSkip,
        bool backupSkipAcknowledged,
        string? groups,
        string? planHash,
        bool hasSnapshot,
        int dryPartitions,
        bool dryBackupSkip,
        bool dryBackupSkipAcknowledged,
        string? dryGroups)
    {
        if (!hasSnapshot || string.IsNullOrWhiteSpace(planHash))
            return NeedsDryRun;

        if (partitions != dryPartitions
            || backupSkip != dryBackupSkip
            || backupSkipAcknowledged != dryBackupSkipAcknowledged
            || !string.Equals(MigrationGroupList.Canonical(groups), MigrationGroupList.Canonical(dryGroups), StringComparison.Ordinal))
            return InputsDiffer;

        return null;
    }
}

public static class MigrationDryRunText
{
    public static bool ShowWindowLine(string? summary) =>
        string.IsNullOrWhiteSpace(summary) || !summary.Contains("Window", StringComparison.OrdinalIgnoreCase);
}

public static class KafkaIncreaseEligibility
{
    public static string? Line(string? catalogReason, PartitionPlan? plan, string topic)
    {
        if (plan is not null
            && !plan.FamilyCompletion
            && string.Equals(plan.Topic, topic, StringComparison.Ordinal))
        {
            if (plan.Accepted)
                return "Eligible for an in-place increase.";

            return "Not eligible for an in-place increase.";
        }

        return string.IsNullOrWhiteSpace(catalogReason) ? null : catalogReason;
    }

    public const string MigrationNotChecked = "Not checked yet. Dry run a migration to confirm it is eligible.";
    public const string MigrationEligible = "Eligible for an increase migration.";
    public const string MigrationRefused = "Not eligible for a migration.";

    public static string? MigrationLine(string? catalogReason, KafkaMigrationPlan? plan)
    {
        if (plan is not null)
            return plan.Accepted ? MigrationEligible : MigrationRefused;

        if (string.IsNullOrWhiteSpace(catalogReason))
            return null;

        if (catalogReason.Contains("Eligible for an increase migration", StringComparison.Ordinal))
            return MigrationNotChecked;

        return catalogReason;
    }
}

public readonly record struct KafkaControllerDisplay(string Value, string Caption, string? Title);

public static class KafkaControllerTile
{
    public const string NotReported = "Not reported";
    public const string SeparateNodeCaption = "KRaft controller (separate node, not exposed to clients)";
    public const string KnownCaption = "Controller";

    public static KafkaControllerDisplay Display(int? controllerId, string? unavailableReason)
    {
        if (controllerId is int id)
            return new KafkaControllerDisplay(id.ToString(), KnownCaption, null);

        if (!string.IsNullOrWhiteSpace(unavailableReason))
            return new KafkaControllerDisplay(NotReported, SeparateNodeCaption, unavailableReason.Trim());

        return new KafkaControllerDisplay("—", KnownCaption, null);
    }
}

public static class KafkaPlanBanner
{
    public static string? BesidePlan(string? pageError, string? planSummary, bool planReturned)
    {
        if (!planReturned || string.IsNullOrWhiteSpace(pageError) || string.IsNullOrWhiteSpace(planSummary))
            return string.IsNullOrWhiteSpace(pageError) ? null : pageError;

        return string.Equals(pageError.Trim(), planSummary.Trim(), StringComparison.Ordinal) ? null : pageError;
    }
}

public static class KafkaProduceRate
{
    public static string Text(bool known, double perSecond) =>
        known ? perSecond.ToString("0.###") : "measuring";
}

public static class KafkaLogDirs
{
    public static bool Show(ClusterSnapshot cluster) =>
        cluster.LogDirsAvailable || cluster.Brokers.Any(broker => broker.LogDirBytes >= 0);
}

public static class KafkaAttention
{
    public static bool Topic(KafkaTopicRow row) => TopicReasons(row).Count > 0;

    public static IReadOnlyList<string> TopicReasons(KafkaTopicRow row)
    {
        var reasons = new List<string>();
        if (row.HardBlocked)
            reasons.Add("Partition changes are blocked.");
        if (!row.FullIsr)
            reasons.Add("The ISR is short.");
        if (row.LeadersSkewed)
            reasons.Add("Leaders are skewed.");
        if (row.PartitionDrift)
            reasons.Add("The live partition count differs from topics.txt.");
        if (row.RetryBehind || row.ErrorBehind || row.ServiceRetryBehind || row.ServiceRedriveBehind || row.PinnedSiblingBehind)
            reasons.Add("A sibling topic is behind.");
        if (!row.LagKnown)
            reasons.Add("Lag is unknown.");
        else if (row.TotalLag > 0)
            reasons.Add("Lag is " + row.TotalLag + ".");
        if (!string.IsNullOrWhiteSpace(row.Error))
            reasons.Add(row.Error.Trim());
        return reasons;
    }

    public static bool Group(KafkaGroupRow row) => GroupReasons(row).Count > 0;

    public static IReadOnlyList<string> GroupReasons(KafkaGroupRow row)
    {
        var reasons = new List<string>();
        if (!row.Catalogued)
            reasons.Add("This group is not in the catalog.");
        if (!string.Equals(row.State?.Trim(), "Stable", StringComparison.OrdinalIgnoreCase))
            reasons.Add(string.IsNullOrWhiteSpace(row.State) ? "State is not reported." : "State is " + row.State.Trim() + ".");
        if (row.TotalLag > 0)
            reasons.Add("Lag is " + row.TotalLag + ".");
        if (row.UnownedPartitions.Count > 0)
            reasons.Add(row.UnownedPartitions.Count + " partitions are unowned.");
        if (row.Members.Count > 0 && row.Members.Any(member => !member.AdvertisesExpectedConfig))
            reasons.Add("A member does not advertise the expected config.");
        return reasons;
    }

    public static bool Broker(BrokerSnapshot row) =>
        !string.Equals(row.State?.Trim(), "up", StringComparison.OrdinalIgnoreCase);

    public static bool Partition(int leader, string? replicas, string? inSync, IEnumerable<long> lags)
    {
        if (leader < 0)
            return true;
        if (lags.Any(lag => lag > 0))
            return true;
        var replicaCount = CountList(replicas);
        return replicaCount > 0 && CountList(inSync) < replicaCount;
    }

    public static bool CommittedPartition(long lag, bool owned) => lag > 0 || !owned;

    public static string Join(IReadOnlyList<string> reasons) => string.Join(" ", reasons);

    private static int CountList(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? 0
            : value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Length;
}

public sealed record BrokerMoveTopicLine(string Topic, int Partitions, bool Internal);

public static class BrokerMovePreview
{
    public const int FoldAfter = 8;

    public static bool IsInternalTopic(string topic) =>
        !string.IsNullOrWhiteSpace(topic) && topic.StartsWith('_');

    public static string MoveSummary(bool accepted, bool alreadyEmpty, int moves)
    {
        if (alreadyEmpty)
            return "Already empty.";
        if (!accepted && moves == 0)
            return "No moves were planned because the preview was refused.";
        return moves + " move off or onto this broker.";
    }

    public static IReadOnlyList<BrokerMoveTopicLine> Lines(IEnumerable<ReplicaMove> moves) =>
        moves
            .GroupBy(move => move.Topic ?? "", StringComparer.Ordinal)
            .Select(group => new BrokerMoveTopicLine(group.Key, group.Count(), IsInternalTopic(group.Key)))
            .OrderBy(line => line.Internal)
            .ThenBy(line => line.Topic, StringComparer.Ordinal)
            .ToList();
}
