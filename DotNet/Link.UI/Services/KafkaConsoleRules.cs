using LantanaGroup.Link.Shared.Application.Models.Kafka;

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

public static class KafkaBrowseText
{
    public static string When(long unixMs)
    {
        if (unixMs <= 0)
            return "—";
        try
        {
            return LinkUiTime.Display(DateTimeOffset.FromUnixTimeMilliseconds(unixMs));
        }
        catch (ArgumentOutOfRangeException)
        {
            return "—";
        }
    }

    public static string MemberLabel(KafkaNamedTopic member)
    {
        if (member.Kind == KafkaBrowseAllowList.KindMain)
            return "Main";
        if (member.Kind == KafkaBrowseAllowList.KindError)
            return "Error";
        if (member.Kind == KafkaBrowseAllowList.KindRetry)
            return ServiceLabel(member.Topic, "-Retry-", "Retry");
        if (member.Kind == KafkaBrowseAllowList.KindRedrive)
            return ServiceLabel(member.Topic, "-Redrive-", "Redrive");
        return member.Topic;
    }

    public static string FacilityHref(string? facilityId) =>
        string.IsNullOrWhiteSpace(facilityId) ? "" : "/Tenants/View/" + Uri.EscapeDataString(facilityId.Trim());

    public static string ReportHref(string? facilityId, string? reportId)
    {
        if (string.IsNullOrWhiteSpace(facilityId) || string.IsNullOrWhiteSpace(reportId))
            return "";
        return "/Tenants/Report/" + Uri.EscapeDataString(facilityId.Trim()) + "?reportId=" + Uri.EscapeDataString(reportId.Trim());
    }

    public static string RecordId(int partition, long offset) =>
        partition.ToString() + ":" + offset.ToString();

    public static string Verdict(KafkaBrowsePage? page, string? error)
    {
        if (!string.IsNullOrWhiteSpace(error))
            return "This read was refused.";
        if (page is null)
            return "";
        if (page.Metadata.CapHit)
            return "The read stopped at the safety cap.";
        if (page.Metadata.Returned == 1)
            return "Showing 1 record.";
        return "Showing " + page.Metadata.Returned + " records.";
    }

    public static string NextStep(KafkaBrowsePage? page, string? error)
    {
        if (!string.IsNullOrWhiteSpace(error))
            return "Change the seek and fetch again.";
        if (page is null)
            return "";
        if (page.Metadata.CapHit)
            return "Narrow the partitions, the key, or the time, then fetch again.";
        if (page.Metadata.Returned == 0)
            return "Nothing matched. Widen the seek or clear the key filter.";
        return "Open a row to read it, or export JSON. Export reads this seek again and writes an audit.";
    }

    public static string Tone(KafkaBrowsePage? page, string? error)
    {
        if (!string.IsNullOrWhiteSpace(error))
            return "alert-danger";
        if (page?.Metadata.CapHit == true)
            return "alert-warning";
        return "alert-success";
    }

    public static string BadgeClass(KafkaBrowsePage? page, string? error)
    {
        if (!string.IsNullOrWhiteSpace(error))
            return "au-badge-danger";
        if (page?.Metadata.CapHit == true)
            return "au-badge-warning";
        return "au-badge-success";
    }

    public static string Badge(KafkaBrowsePage? page, string? error)
    {
        if (!string.IsNullOrWhiteSpace(error))
            return "Refused";
        if (page?.Metadata.CapHit == true)
            return "Capped";
        return "Fetched";
    }

    private static string ServiceLabel(string topic, string marker, string kind)
    {
        var index = topic.IndexOf(marker, StringComparison.Ordinal);
        if (index < 0 || index + marker.Length >= topic.Length)
            return kind;
        return kind + " " + topic[(index + marker.Length)..];
    }
}
