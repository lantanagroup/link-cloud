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

            var why = plan.Errors.FirstOrDefault(error => !string.IsNullOrWhiteSpace(error));
            if (string.IsNullOrWhiteSpace(why))
                why = plan.Summary;
            return string.IsNullOrWhiteSpace(why)
                ? "Not eligible for an in-place increase."
                : "Not eligible for an in-place increase. " + why;
        }

        return string.IsNullOrWhiteSpace(catalogReason) ? null : catalogReason;
    }

    public static string? MigrationLine(string? catalogReason, KafkaMigrationPlan? plan)
    {
        if (plan is null)
            return string.IsNullOrWhiteSpace(catalogReason) ? null : catalogReason;
        if (plan.Accepted)
            return string.IsNullOrWhiteSpace(catalogReason) ? "Eligible for an increase migration." : catalogReason;

        var why = plan.Errors.FirstOrDefault(error => !string.IsNullOrWhiteSpace(error));
        if (string.IsNullOrWhiteSpace(why))
            why = plan.Summary;
        return string.IsNullOrWhiteSpace(why)
            ? "Not eligible for a migration."
            : "Not eligible for a migration. " + why;
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
