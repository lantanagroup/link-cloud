using System.Globalization;
using LantanaGroup.Link.Shared.Application.Models.Kafka;

namespace Link.UI.Services;

public sealed record KafkaPipelineStep(string Topic, long Lag, bool LagKnown, double Rate, string Tone, string Href);

public sealed record KafkaBrokerCard(int Id, string Host, bool Online, bool InSync, int Partitions, int Leaders, string Disk, string Href);

public sealed record KafkaDonutSlice(string Name, int Value, string Color);

/// <summary>
/// Everything the Kafka overview widgets show, built once from bounded calls: the topic and
/// group views, the cluster snapshot, and the top-N history. No per-partition reads happen here.
/// </summary>
public sealed class KafkaDashboard
{
    public const int TopN = 5;
    public const int MaxBrokerCards = 12;

    public IReadOnlyList<KafkaPipelineStep> Pipeline { get; init; } = [];
    public IReadOnlyList<KafkaBrokerCard> Brokers { get; init; } = [];
    public int BrokerTotal { get; init; }
    public IReadOnlyList<KafkaDonutSlice> Leaders { get; init; } = [];
    public string LeaderGradient { get; init; } = "";
    public int LeaderTotal { get; init; }
    public KafkaHistoryResponse History { get; init; } = new();
    public IReadOnlyList<KafkaAttentionItem> Attention { get; init; } = [];
    public string? Error { get; init; }

    public bool HasHistory => History.Points.Count >= 2;
}

public static class KafkaDashboardRules
{
    public const string ToneOk = "ok";
    public const string ToneWarn = "warn";
    public const string ToneBad = "bad";
    public const string ToneUnknown = "unknown";
    public const long WarnLag = 1;
    public const long BadLag = 1000;

    public static string LagTone(bool known, long lag) =>
        !known ? ToneUnknown : lag >= BadLag ? ToneBad : lag >= WarnLag ? ToneWarn : ToneOk;

    /// <summary>Main topics in catalog pipeline order. Topics outside the chain are left out.</summary>
    public static IReadOnlyList<KafkaPipelineStep> Pipeline(IEnumerable<KafkaTopicRow>? topics) =>
        (topics ?? [])
            .Select(row => (Row: row, Step: KafkaTopicCatalog.PipelineStep(row.Topic)))
            .Where(item => item.Step >= 0 && string.Equals(KafkaTopicCatalog.MainName(item.Row.Topic), item.Row.Topic, StringComparison.OrdinalIgnoreCase))
            .OrderBy(item => item.Step)
            .Select(item => new KafkaPipelineStep(
                item.Row.Topic,
                item.Row.TotalLag,
                item.Row.LagKnown,
                item.Row.ProduceRateKnown ? item.Row.ProduceRatePerSecond : -1,
                LagTone(item.Row.LagKnown, item.Row.TotalLag),
                KafkaConsoleRoutes.Topics(item.Row.Topic, KafkaConsoleRoutes.Summary)))
            .ToList();

    public static IReadOnlyList<KafkaBrokerCard> Brokers(ClusterSnapshot cluster)
    {
        var placements = cluster.Placements ?? [];
        return (cluster.Brokers ?? [])
            .OrderByDescending(KafkaAttention.Broker).ThenBy(row => row.Id)
            .Take(KafkaDashboard.MaxBrokerCards)
            .Select(row =>
            {
                var hosted = placements.Where(item => item.Replicas.Contains(row.Id)).ToList();
                var inSync = hosted.All(item => item.Isr.Contains(row.Id));
                return new KafkaBrokerCard(
                    row.Id,
                    string.IsNullOrWhiteSpace(row.Host) ? "broker " + row.Id : row.Host,
                    !string.Equals(row.State, "down", StringComparison.OrdinalIgnoreCase) && !string.Equals(row.State, "offline", StringComparison.OrdinalIgnoreCase),
                    inSync,
                    row.PartitionCount,
                    row.LeaderCount,
                    row.LogDirBytes >= 0 ? Bytes(row.LogDirBytes) : "",
                    KafkaConsoleRoutes.Broker(row.Id.ToString(CultureInfo.InvariantCulture)));
            })
            .ToList();
    }

    public static IReadOnlyList<KafkaDonutSlice> Leaders(ClusterSnapshot cluster)
    {
        var rows = (cluster.Brokers ?? []).OrderByDescending(row => row.LeaderCount).ThenBy(row => row.Id).ToList();
        var shown = rows.Take(ReportManifestRules.DonutColors.Length - 1).ToList();
        var slices = shown.Select((row, index) => new KafkaDonutSlice("Broker " + row.Id, row.LeaderCount, ReportManifestRules.DonutColors[index])).ToList();
        var other = rows.Skip(shown.Count).Sum(row => row.LeaderCount);
        if (other > 0)
            slices.Add(new KafkaDonutSlice("Other brokers", other, ReportManifestRules.DonutOtherColor));
        return slices;
    }

    public static string Bytes(long bytes)
    {
        string[] units = ["B", "KB", "MB", "GB", "TB", "PB"];
        double value = bytes;
        var unit = 0;
        while (value >= 1000 && unit < units.Length - 1)
        {
            value /= 1000;
            unit++;
        }
        return value.ToString(value >= 100 || unit == 0 ? "0" : "0.#", CultureInfo.InvariantCulture) + " " + units[unit];
    }

    public static string Rate(double perSecond) =>
        perSecond < 0 ? "—" : perSecond >= 100 ? perSecond.ToString("N0", CultureInfo.InvariantCulture) + "/s" : perSecond.ToString("0.#", CultureInfo.InvariantCulture) + "/s";

    /// <summary>An SVG polyline for a sparkline in a 100 by 30 box. Empty when there is no history.</summary>
    public static string Spark(IReadOnlyList<double> values)
    {
        if (values.Count < 2)
            return "";
        var max = values.Max();
        var min = Math.Min(0, values.Min());
        var span = max - min <= 0 ? 1 : max - min;
        return string.Join(" ", values.Select((value, index) =>
            (index * 100.0 / (values.Count - 1)).ToString("0.##", CultureInfo.InvariantCulture) + "," +
            (28 - (value - min) / span * 26).ToString("0.##", CultureInfo.InvariantCulture)));
    }

    public static KafkaDashboard Build(KafkaTopicsResponse topics, KafkaGroupsResponse groups, ClusterSnapshot cluster, KafkaHistoryResponse history)
    {
        var leaders = Leaders(cluster);
        return new KafkaDashboard
        {
            Pipeline = Pipeline(topics.Topics),
            Brokers = Brokers(cluster),
            BrokerTotal = cluster.Brokers?.Count ?? 0,
            Leaders = leaders,
            LeaderTotal = leaders.Sum(slice => slice.Value),
            LeaderGradient = ReportManifestRules.DonutGradient(leaders.Select(slice => slice.Value).ToList(), leaders.Select(slice => slice.Color).ToList()),
            History = history,
            Attention = KafkaNeedsAttention.Rank(topics.Topics, groups.Groups, cluster.Placements, cluster.OfflinePartitions, cluster.UnderReplicatedPartitions),
            Error = topics.Error ?? cluster.Error
        };
    }
}
