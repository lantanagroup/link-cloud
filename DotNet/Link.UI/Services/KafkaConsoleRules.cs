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

public sealed record BrokerMoveTopicLine(string Topic, int Partitions, bool Internal);

public static class BrokerMovePreview
{
    public const int FoldAfter = 8;

    public static bool IsInternalTopic(string topic) =>
        !string.IsNullOrWhiteSpace(topic) && topic.StartsWith('_');

    public static IReadOnlyList<BrokerMoveTopicLine> Lines(IEnumerable<ReplicaMove> moves) =>
        moves
            .GroupBy(move => move.Topic ?? "", StringComparer.Ordinal)
            .Select(group => new BrokerMoveTopicLine(group.Key, group.Count(), IsInternalTopic(group.Key)))
            .OrderBy(line => line.Internal)
            .ThenBy(line => line.Topic, StringComparer.Ordinal)
            .ToList();
}
