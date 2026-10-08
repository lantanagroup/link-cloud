using System.Text.Encodings.Web;
using System.Text.Json;
using Link.UI.Services;

namespace Link.UI.Models;

public sealed record ThroughputKafkaPageQuery
{
    public const string Overview = "overview";
    public const string Topic = "topic";
    public const string Consumers = "consumers";
    public const string Brokers = "brokers";
    public const string Migrate = "migrate";

    public string Q { get; init; } = "";
    public string Sort { get; init; } = "topic";
    public string Dir { get; init; } = "asc";
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 25;
    public string Family { get; init; } = "";
    public string KeyClass { get; init; } = "";
    public string View { get; init; } = Overview;
    public string Group { get; init; } = "";
    public string TopicName { get; init; } = "";
    public string Broker { get; init; } = "";
    public bool Tests { get; init; }
    public bool Advanced { get; init; }
    public string? ReturnUrl { get; init; }

    public static ThroughputKafkaPageQuery From(IQueryCollection query, string? returnUrl)
    {
        var sort = One(query, "sort");
        var dir = One(query, "dir");
        var view = One(query, "view");
        var pageSize = int.TryParse(One(query, "pageSize"), out var size) ? size : 25;
        var page = int.TryParse(One(query, "page"), out var number) ? number : 1;
        if (pageSize is not (10 or 25 or 50))
            pageSize = 25;
        if (page < 1)
            page = 1;
        if (sort is not ("topic" or "partitions" or "lag" or "rate" or "group" or "state" or "key" or "brokers" or "leaders"))
            sort = "topic";
        if (dir is not ("asc" or "desc"))
            dir = "asc";
        if (view is not (Overview or Topic or Consumers or Brokers or Migrate))
            view = Overview;

        var q = One(query, "q");
        if (q.Length > 200)
            q = q[..200];
        var tests = One(query, "tests");
        var advanced = One(query, "advanced");

        return new ThroughputKafkaPageQuery
        {
            Q = q,
            Sort = sort,
            Dir = dir,
            Page = page,
            PageSize = pageSize,
            Family = One(query, "family"),
            KeyClass = One(query, "keyClass"),
            View = view,
            Group = One(query, "group"),
            TopicName = One(query, "topic"),
            Broker = One(query, "broker"),
            Tests = tests is "1" or "true",
            Advanced = advanced is "1" or "true",
            ReturnUrl = returnUrl
        };
    }

    public string Href(string? view = null, string? sort = null, string? dir = null, int? page = null, string? family = null, string? group = null, string? q = null, string? topic = null, string? broker = null, string? keyClass = null, bool? tests = null, bool? advanced = null)
    {
        var values = new Dictionary<string, string?>
        {
            ["q"] = q ?? Q,
            ["sort"] = sort ?? Sort,
            ["dir"] = dir ?? Dir,
            ["page"] = (page ?? Page).ToString(),
            ["pageSize"] = PageSize.ToString(),
            ["family"] = family ?? Family,
            ["keyClass"] = keyClass ?? KeyClass,
            ["view"] = view ?? View,
            ["group"] = group ?? Group,
            ["topic"] = topic ?? TopicName,
            ["broker"] = broker ?? Broker,
            ["tests"] = (tests ?? Tests) ? "1" : "",
            ["advanced"] = (advanced ?? Advanced) ? "1" : "",
            ["returnUrl"] = ReturnUrl
        };
        var pairs = values
            .Where(pair => !string.IsNullOrWhiteSpace(pair.Value))
            .Select(pair => Uri.EscapeDataString(pair.Key) + "=" + Uri.EscapeDataString(pair.Value!));
        return "/Operations/Kafka?" + string.Join("&", pairs);
    }

    private static string One(IQueryCollection query, string name) =>
        query.TryGetValue(name, out var values) ? values.ToString().Trim() : "";
}

public sealed record ThroughputKafkaPage
{
    public ThroughputKafkaPageQuery Query { get; init; } = new();
    public KafkaTopicsResponse Topics { get; init; } = new();
    public KafkaGroupsResponse Groups { get; init; } = new();
    public KafkaCapabilitiesResponse Capabilities { get; init; } = new();
    public ClusterSnapshot Cluster { get; init; } = new();
    public InfraStatus Infra { get; init; } = new();
    public List<KafkaTopicRow> TopicRows { get; init; } = [];
    public List<KafkaGroupRow> GroupRows { get; init; } = [];
    public List<BrokerSnapshot> BrokerRows { get; init; } = [];
    public List<KafkaTopicRow> ChartTopics { get; init; } = [];
    public int TopicCount { get; init; }
    public int GroupCount { get; init; }
    public int BrokerCount { get; init; }
    public int TopicPages { get; init; } = 1;
    public int GroupPages { get; init; } = 1;
    public int BrokerPages { get; init; } = 1;
    public IReadOnlyList<string> Families { get; init; } = [];
    public IReadOnlyList<string> KeyClasses { get; init; } = [];
    public PartitionPlan? Plan { get; init; }
    public ReplicaScalePlan? ScalePlan { get; init; }
    public BrokerMovePlan? MovePlan { get; init; }
    public ChangeRequestRecord? Request { get; init; }
    public string? Message { get; init; }
    public string? Error { get; init; }
    public string DraftTopic { get; init; } = "";
    public int DraftPartitions { get; init; }
    public string DraftReason { get; init; } = "";
    public string DraftConfirmation { get; init; } = "";
    public bool DraftOverride { get; init; }
    public string DraftOverrideReason { get; init; } = "";
    public int DraftReplicas { get; init; }
    public int DraftDelta { get; init; } = 1;
    public int DraftBrokerId { get; init; } = -1;
    public KafkaGroupRow? SelectedGroup { get; init; }
    public KafkaTopicRow? SelectedTopic { get; init; }
    public BrokerSnapshot? SelectedBroker { get; init; }
    public List<KafkaPartitionDetail> Partitions { get; init; } = [];
    public int ReplicaCeiling { get; init; }
    public KafkaMigrationPlan? MigrationPlan { get; init; }
    public KafkaMigrationRecord? Migration { get; init; }
    public KafkaTopicDetail? Detail { get; init; }
    public KafkaTopicConfigs? Configs { get; init; }
    public string? Runbook { get; init; }

    public string ChartJson
    {
        get
        {
            var rates = ChartTopics.Select(row => new { label = row.Topic, value = row.ProduceRatePerSecond }).ToList();
            var lags = ChartTopics.Select(row => new { label = row.Topic, value = (double)row.TotalLag }).ToList();
            if (Query.View == ThroughputKafkaPageQuery.Topic && Partitions.Count > 0)
            {
                lags = Partitions
                    .Select(row => new { label = "p" + row.Partition, value = (double)row.Lag.Sum(item => item.Lag) })
                    .ToList();
            }
            else if (Query.View == ThroughputKafkaPageQuery.Consumers && SelectedGroup is not null)
            {
                lags = SelectedGroup.Partitions
                    .OrderBy(row => row.Topic, StringComparer.Ordinal)
                    .ThenBy(row => row.Partition)
                    .Select(row => new { label = row.Topic + " " + row.Partition, value = (double)row.Lag })
                    .ToList();
            }

            return JsonSerializer.Serialize(new { rates, lags }, new JsonSerializerOptions
            {
                Encoder = JavaScriptEncoder.Default
            });
        }
    }
}

public sealed class KafkaMigrationForm
{
    public string? Topic { get; set; }
    public int Partitions { get; set; }
    public string? Reason { get; set; }
    public string? Confirmation { get; set; }
    public bool BackupSkip { get; set; }
    public bool BackupSkipAcknowledged { get; set; }
    public Guid MigrationId { get; set; }
    public string? Workload { get; set; }
    public string? BackupName { get; set; }
}

public sealed class KafkaPartitionDetail
{
    public int Partition { get; init; }
    public int Leader { get; init; }
    public string Replicas { get; init; } = "";
    public string InSync { get; init; } = "";
    public long HighWatermark { get; init; } = -1;
    public List<KafkaPartitionLagRow> Lag { get; init; } = [];
}
