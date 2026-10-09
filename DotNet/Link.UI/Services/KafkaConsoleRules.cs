using System.Net;
using System.Text;
using LantanaGroup.Link.Shared.Application.Models.Kafka;
using Link.UI.Models;

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

    public static string FamilyStat(IReadOnlyList<KafkaNamedTopic> members)
    {
        if (members.Count == 0)
            return "";
        var parts = members.Select(member =>
        {
            var name = MemberLabel(member);
            if (!member.Exists)
                return name + " is not on the broker";
            if (!member.LagKnown)
                return name + ", lag was not read";
            return name + ", " + member.Partitions.ToString() + " partitions, lag " + member.Lag.ToString();
        });
        return string.Join(". ", parts) + ".";
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

    public static string MessageType(string? topic)
    {
        var admission = KafkaBrowseAllowList.Admit(topic, null);
        if (!admission.Allowed || admission.Main.Length == 0)
            return string.IsNullOrWhiteSpace(topic) ? "Unknown" : topic.Trim();

        var role = admission.Kind switch
        {
            KafkaBrowseAllowList.KindError => "Error",
            KafkaBrowseAllowList.KindRetry => "Retry",
            KafkaBrowseAllowList.KindRedrive => "Redrive",
            KafkaBrowseAllowList.KindBackup => "Backup",
            _ => "Main"
        };
        return admission.Main + " " + role;
    }

    public static string KeyBadge(string? key)
    {
        var text = (key ?? "").Trim();
        if (text.Length == 0)
            return "No key";
        if (text.Length <= 36)
            return text;
        return text[..32] + "…";
    }

    public static string PayloadHtml(string? pretty, string? raw)
    {
        if (!string.IsNullOrEmpty(pretty))
            return Highlight(pretty);
        return WebUtility.HtmlEncode(raw ?? "");
    }

    public static string Highlight(string? text)
    {
        if (string.IsNullOrEmpty(text))
            return "";

        var html = new StringBuilder(text.Length + 32);
        var index = 0;
        while (index < text.Length)
        {
            var character = text[index];
            if (char.IsWhiteSpace(character))
            {
                html.Append(WebUtility.HtmlEncode(character.ToString()));
                index++;
                continue;
            }

            if (character is '{' or '}' or '[' or ']' or ':' or ',')
            {
                html.Append("<span class=\"lu-json-punct\">").Append(WebUtility.HtmlEncode(character.ToString())).Append("</span>");
                index++;
                continue;
            }

            if (character == '"')
            {
                var end = ReadJsonString(text, index);
                var token = text[index..end];
                var kind = IsJsonKey(text, end) ? "lu-json-key" : "lu-json-string";
                html.Append("<span class=\"").Append(kind).Append("\">").Append(WebUtility.HtmlEncode(token)).Append("</span>");
                index = end;
                continue;
            }

            if ((character == '-' && index + 1 < text.Length && char.IsDigit(text[index + 1])) || char.IsDigit(character))
            {
                var end = index + 1;
                while (end < text.Length && (char.IsDigit(text[end]) || text[end] is '.' or 'e' or 'E' or '+' or '-'))
                    end++;
                html.Append("<span class=\"lu-json-number\">").Append(WebUtility.HtmlEncode(text[index..end])).Append("</span>");
                index = end;
                continue;
            }

            if (MatchJsonWord(text, index, "true") || MatchJsonWord(text, index, "false"))
            {
                var word = character == 't' ? "true" : "false";
                html.Append("<span class=\"lu-json-bool\">").Append(word).Append("</span>");
                index += word.Length;
                continue;
            }

            if (MatchJsonWord(text, index, "null"))
            {
                html.Append("<span class=\"lu-json-null\">null</span>");
                index += 4;
                continue;
            }

            html.Append(WebUtility.HtmlEncode(character.ToString()));
            index++;
        }

        return html.ToString();
    }

    private static int ReadJsonString(string text, int start)
    {
        var index = start + 1;
        while (index < text.Length)
        {
            if (text[index] == '\\')
            {
                index += index + 1 < text.Length ? 2 : 1;
                continue;
            }

            if (text[index] == '"')
                return index + 1;
            index++;
        }

        return text.Length;
    }

    private static bool IsJsonKey(string text, int after)
    {
        var index = after;
        while (index < text.Length && char.IsWhiteSpace(text[index]))
            index++;
        return index < text.Length && text[index] == ':';
    }

    private static bool MatchJsonWord(string text, int start, string word)
    {
        if (start + word.Length > text.Length)
            return false;
        if (!text.AsSpan(start, word.Length).Equals(word, StringComparison.Ordinal))
            return false;
        if (start + word.Length < text.Length)
        {
            var next = text[start + word.Length];
            if (char.IsLetterOrDigit(next) || next == '_')
                return false;
        }

        return true;
    }

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

    public static KafkaBrowseSeekLinks SeekLinks(ThroughputKafkaPageQuery query, KafkaBrowsePage? page)
    {
        if (page is null || page.Records.Count == 0)
            return new KafkaBrowseSeekLinks(false, "", false, "", 0, 0);

        var applied = query.BrowseLimit < 1
            ? KafkaBrowseLimits.DefaultLimit
            : Math.Min(query.BrowseLimit, KafkaBrowseLimits.MaxLimit);
        var earliest = page.Records.Min(record => record.Offset);
        var latest = page.Records.Max(record => record.Offset);
        var earlierAt = Math.Max(0, earliest - applied);
        var laterAt = latest == long.MaxValue ? latest : latest + 1;
        var earlier = earliest > 0;
        var later = page.Records.Count >= applied;
        return new KafkaBrowseSeekLinks(
            earlier,
            earlier ? query.WithSeek("from-offset", earlierAt).Href() : "",
            later,
            later ? query.WithSeek("from-offset", laterAt).Href() : "",
            earliest,
            latest);
    }

    private static string ServiceLabel(string topic, string marker, string kind)
    {
        var index = topic.IndexOf(marker, StringComparison.Ordinal);
        if (index < 0 || index + marker.Length >= topic.Length)
            return kind;
        return kind + " " + topic[(index + marker.Length)..];
    }
}

public sealed record KafkaBrowseSeekLinks(bool Earlier, string EarlierHref, bool Later, string LaterHref, long Earliest, long Latest);

public sealed record KafkaSummaryStrip(int Total, string TotalLabel, int Attention, string HottestLabel, string Hottest, string Skew);

public sealed record KafkaWindowPager(int Page, int Pages, string Note, string Previous, string Next);

public sealed record KafkaWindow<T>
{
    public int Total { get; init; }
    public int Hot { get; init; }
    public long Hottest { get; init; }
    public string Skew { get; init; } = "";
    public int Page { get; init; } = 1;
    public int Pages { get; init; } = 1;
    public int PageSize { get; init; } = 25;
    public IReadOnlyList<T> Rows { get; init; } = [];
    public IReadOnlyList<T> HottestRows { get; init; } = [];

    public string CapNote =>
        Hot > HottestRows.Count
            ? HottestRows.Count + " of " + Hot + " shown. The rest are in the paged list, worst first."
            : "";
}

public static class KafkaWindows
{
    public const int HottestCap = 8;
    public const int AssignmentCap = 6;
    public const int UnownedCap = 8;

    public static int Size(int pageSize) => pageSize is 10 or 25 or 50 ? pageSize : 25;

    public static KafkaWindow<KafkaPartitionDetail> Topic(IReadOnlyList<KafkaPartitionDetail> rows, int page, int pageSize)
    {
        bool Hot(KafkaPartitionDetail row) =>
            KafkaAttention.Partition(row.Leader, row.Replicas, row.InSync, row.Lag.Select(item => item.Lag));
        long Heat(KafkaPartitionDetail row) => row.Lag.Count == 0 ? 0 : row.Lag.Max(item => item.Lag);
        var ordered = rows
            .OrderByDescending(Hot)
            .ThenByDescending(Heat)
            .ThenBy(row => row.Partition)
            .ToList();
        var hottest = ordered.Count == 0 ? 0 : ordered.Max(Heat);
        var skew = rows.Count == 0 ? "No partitions were reported." : LeaderSkew(rows.Select(row => row.Leader));
        return Slice(ordered, ordered.Count(Hot), hottest, skew, page, pageSize, Hot);
    }

    public static KafkaWindow<KafkaPartitionLagRow> Committed(IReadOnlyList<KafkaPartitionLagRow> rows, int page, int pageSize)
    {
        bool Hot(KafkaPartitionLagRow row) => KafkaAttention.CommittedPartition(row.Lag, row.Owned);
        var ordered = rows
            .OrderByDescending(Hot)
            .ThenByDescending(row => row.Lag)
            .ThenBy(row => row.Topic, StringComparer.Ordinal)
            .ThenBy(row => row.Partition)
            .ToList();
        var hottest = ordered.Count == 0 ? 0 : ordered.Max(row => row.Lag);
        var skew = rows.Count == 0 ? "No committed partitions." : LagSkew(rows);
        return Slice(ordered, ordered.Count(Hot), hottest, skew, page, pageSize, Hot);
    }

    public static KafkaWindow<KafkaPartitionFact> Facts(IReadOnlyList<KafkaPartitionFact> rows, int page, int pageSize)
    {
        bool Hot(KafkaPartitionFact row) => !row.PreferredLeader || row.Isr.Count < row.Replicas.Count || row.Leader < 0;
        long Span(KafkaPartitionFact row) =>
            row.HighWatermark < 0 || row.LogStart < 0 || row.HighWatermark < row.LogStart
                ? 0
                : row.HighWatermark - row.LogStart;
        var ordered = rows
            .OrderByDescending(Hot)
            .ThenByDescending(Span)
            .ThenBy(row => row.Partition)
            .ToList();
        var hottest = ordered.Count == 0 ? 0 : ordered.Max(Span);
        var skew = rows.Count == 0 ? "No partitions were reported." : LeaderSkew(rows.Select(row => row.Leader));
        return Slice(ordered, ordered.Count(Hot), hottest, skew, page, pageSize, Hot);
    }

    public static KafkaWindow<KafkaGroupRow> Groups(IReadOnlyList<KafkaGroupRow> rows, int page, int pageSize)
    {
        bool Hot(KafkaGroupRow row) => KafkaAttention.Group(row);
        var ordered = rows
            .OrderByDescending(Hot)
            .ThenByDescending(row => row.TotalLag)
            .ThenBy(row => row.GroupId, StringComparer.OrdinalIgnoreCase)
            .ToList();
        var hottest = ordered.Count == 0 ? 0 : ordered.Max(row => row.TotalLag);
        return Slice(ordered, ordered.Count(Hot), hottest, GroupSkew(rows), page, pageSize, Hot);
    }

    public static KafkaWindow<KafkaTopicCapability> DeniedRights(IReadOnlyList<KafkaTopicCapability> topics, int page, int pageSize)
    {
        var denied = topics
            .Where(right => right.CanAlterPartitions == false)
            .OrderBy(right => right.Topic, StringComparer.Ordinal)
            .ToList();
        return Slice(denied, denied.Count, 0, "", page, pageSize, static _ => false);
    }

    public static string LeaderSkew(IEnumerable<int> leaders)
    {
        var online = leaders.Where(id => id >= 0).ToList();
        if (online.Count == 0)
            return "No leader is online.";

        var counts = online
            .GroupBy(id => id)
            .Select(group => (Id: group.Key, Count: group.Count()))
            .OrderByDescending(row => row.Count)
            .ThenBy(row => row.Id)
            .ToList();
        if (counts.Count == 1)
            return "Broker " + counts[0].Id + " leads every partition.";

        var even = (int)Math.Ceiling(online.Count / (double)counts.Count);
        var top = counts[0];
        if (top.Count > even)
            return "Broker " + top.Id + " leads " + top.Count + " of " + online.Count + " partitions.";

        return "Leaders are spread across " + counts.Count + " brokers.";
    }

    public static string LagSkew(IEnumerable<KafkaPartitionLagRow> rows)
    {
        var list = rows.ToList();
        if (list.Count == 0)
            return "No committed partitions.";

        var byTopic = list
            .GroupBy(row => row.Topic ?? "", StringComparer.Ordinal)
            .Select(group => (Topic: group.Key, Lag: group.Sum(row => row.Lag)))
            .OrderByDescending(row => row.Lag)
            .ThenBy(row => row.Topic, StringComparer.Ordinal)
            .ToList();
        var total = byTopic.Sum(row => row.Lag);
        if (total <= 0)
            return "Lag is even. Every committed partition is caught up.";
        if (byTopic.Count == 1)
            return byTopic[0].Topic + " holds all " + total + " of the lag.";

        var even = (long)Math.Ceiling(total / (double)byTopic.Count);
        var top = byTopic[0];
        if (top.Lag > even)
            return top.Topic + " holds " + top.Lag + " of " + total + " lag.";

        return "Lag is spread across " + byTopic.Count + " topics.";
    }

    public static string GroupSkew(IReadOnlyList<KafkaGroupRow> rows)
    {
        if (rows.Count == 0)
            return "No groups were reported.";

        var total = rows.Sum(row => row.TotalLag);
        var lagging = rows
            .Where(row => row.TotalLag > 0)
            .OrderByDescending(row => row.TotalLag)
            .ThenBy(row => row.GroupId, StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (total <= 0 || lagging.Count == 0)
            return "Lag is even. No group is behind.";
        if (lagging.Count == 1)
            return lagging[0].GroupId + " holds all " + total + " of the lag.";

        var even = (long)Math.Ceiling(total / (double)rows.Count);
        var top = lagging[0];
        if (top.TotalLag > even)
            return top.GroupId + " holds " + top.TotalLag + " of " + total + " lag.";

        return "Lag is spread across " + lagging.Count + " groups.";
    }

    public static string AssignmentText(IReadOnlyList<string>? ids)
    {
        if (ids is null || ids.Count == 0)
            return "—";
        if (ids.Count > AssignmentCap)
            return ids.Count + " partitions";
        return string.Join(", ", ids);
    }

    public static string UnownedText(IReadOnlyList<string>? ids)
    {
        if (ids is null || ids.Count == 0)
            return "";
        var shown = string.Join(", ", ids.Take(UnownedCap));
        if (ids.Count > UnownedCap)
            return ids.Count + " unowned: " + shown + ", …";
        return ids.Count + " unowned: " + shown;
    }

    public static string LagByGroup(IReadOnlyList<KafkaPartitionLagRow>? lag)
    {
        if (lag is null || lag.Count == 0)
            return "";
        var ordered = lag
            .OrderByDescending(item => item.Lag)
            .ThenBy(item => item.GroupId, StringComparer.Ordinal)
            .ToList();
        var shown = string.Join("; ", ordered.Take(AssignmentCap).Select(item => item.GroupId + " " + item.Lag));
        if (ordered.Count > AssignmentCap)
            shown += "; " + (ordered.Count - AssignmentCap) + " more";
        return shown;
    }

    private static KafkaWindow<T> Slice<T>(List<T> worstFirst, int hot, long hottest, string skew, int page, int pageSize, Func<T, bool> isHot)
    {
        var size = Size(pageSize);
        var total = worstFirst.Count;
        var pages = Math.Max(1, (int)Math.Ceiling(total / (double)size));
        var current = page < 1 ? 1 : Math.Min(page, pages);
        var start = (current - 1) * size;
        var rows = total == 0 ? [] : worstFirst.Skip(start).Take(size).ToList();
        return new KafkaWindow<T>
        {
            Total = total,
            Hot = hot,
            Hottest = hottest,
            Skew = skew,
            Page = current,
            Pages = pages,
            PageSize = size,
            Rows = rows,
            HottestRows = worstFirst.Where(isHot).Take(HottestCap).ToList()
        };
    }
}
