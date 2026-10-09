using System.Text.Encodings.Web;
using System.Text.Json;
using LantanaGroup.Link.Shared.Application.Models.Kafka;
using LantanaGroup.Link.Shared.Application.Services.Security;
using Link.UI.Services;

namespace Link.UI.Models;

public sealed record ThroughputKafkaPageQuery
{
    public const string Overview = "overview";
    public const string Topic = "topic";
    public const string Consumers = "consumers";
    public const string Brokers = "brokers";
    public const string Messages = "messages";
    public const string Migrate = "migrate";

    public string Q { get; init; } = "";
    public string Sort { get; init; } = "topic";
    public string Dir { get; init; } = "asc";
    public int Page { get; init; } = 1;
    public int PartPage { get; init; } = 1;
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
    public string BrowseMode { get; init; } = "newest";
    public IReadOnlyList<int> BrowsePartitions { get; init; } = [];
    public long? BrowseOffset { get; init; }
    public long? BrowseTimestamp { get; init; }
    public int BrowseLimit { get; init; } = 25;
    public string BrowseKey { get; init; } = "";
    public string BrowseHeaderName { get; init; } = "";
    public string BrowseHeaderValue { get; init; } = "";
    public string OpenRecord { get; init; } = "";
    public string StageRecord { get; init; } = "";
    public string MessageText { get; init; } = "";
    public string HeaderContains { get; init; } = "";
    public string ValueContains { get; init; } = "";
    public string MessageKind { get; init; } = "";
    public long? MessageFrom { get; init; }
    public long? MessageTo { get; init; }
    public int MsgPage { get; init; } = 1;
    public string Resume { get; init; } = "";
    public bool More { get; init; }
    public int Rf { get; init; }
    public int RfPage { get; init; } = 1;
    public int RfSize { get; init; } = 25;
    public string RfQ { get; init; } = "";
    public long RfThrottle { get; init; }

    public static ThroughputKafkaPageQuery From(IQueryCollection query, string? returnUrl)
    {
        var sort = One(query, "sort");
        var dir = One(query, "dir");
        var view = One(query, "view");
        var pageSize = int.TryParse(One(query, "pageSize"), out var size) ? size : 25;
        var page = int.TryParse(One(query, "page"), out var number) ? number : 1;
        var part = int.TryParse(One(query, "part"), out var partNumber) ? partNumber : 1;
        if (pageSize is not (10 or 25 or 50))
            pageSize = 25;
        if (page < 1)
            page = 1;
        if (part < 1)
            part = 1;
        if (sort is not ("topic" or "partitions" or "lag" or "rate" or "group" or "state" or "key" or "brokers" or "leaders"))
            sort = "topic";
        if (dir is not ("asc" or "desc"))
            dir = "asc";
        if (view is not (Overview or Topic or Consumers or Brokers or Messages or Migrate))
            view = Overview;

        var q = One(query, "q");
        if (q.Length > 200)
            q = q[..200];
        var tests = One(query, "tests");
        var advanced = One(query, "advanced");
        var mode = One(query, "mode").SanitizeAndRemove().ToLowerInvariant();
        if (mode.Length == 0)
            mode = "newest";
        var limit = 25;
        if (int.TryParse(One(query, "limit"), out var parsedLimit))
            limit = parsedLimit;
        var rf = int.TryParse(One(query, "rf"), out var rfValue) ? rfValue : 0;
        if (rf is < 0 or > 64)
            rf = 0;
        var rfPage = int.TryParse(One(query, "rfPage"), out var rfPageValue) ? rfPageValue : 1;
        if (rfPage < 1)
            rfPage = 1;
        var rfSize = int.TryParse(One(query, "rfSize"), out var rfSizeValue) ? rfSizeValue : 25;
        if (rfSize is not (10 or 25 or 50))
            rfSize = 25;
        var rfQuery = One(query, "rfQ").SanitizeAndRemove();
        if (rfQuery.Length > 40)
            rfQuery = rfQuery[..40];
        var rfThrottle = long.TryParse(One(query, "rfThrottle"), out var throttleValue) ? throttleValue : 0;
        if (rfThrottle < 0 || rfThrottle > ReplicationFactorRules.MaxThrottle)
            rfThrottle = 0;
        var msgPage = int.TryParse(One(query, "msgPage"), out var msgPageValue) ? msgPageValue : 1;
        if (msgPage < 1)
            msgPage = 1;
        var kind = One(query, "mtype").SanitizeAndRemove();
        if (kind.Length > 40)
            kind = kind[..40];

        return new ThroughputKafkaPageQuery
        {
            Q = q,
            Sort = sort,
            Dir = dir,
            Page = page,
            PartPage = part,
            PageSize = pageSize,
            Family = One(query, "family"),
            KeyClass = One(query, "keyClass"),
            View = view,
            Group = One(query, "group"),
            TopicName = One(query, "topic"),
            Broker = One(query, "broker"),
            Tests = tests is "1" or "true",
            Advanced = advanced is "1" or "true",
            ReturnUrl = returnUrl,
            BrowseMode = mode,
            BrowsePartitions = Partitions(query),
            BrowseOffset = LongOrNull(One(query, "offset")),
            BrowseTimestamp = LongOrNull(One(query, "timestamp")),
            BrowseLimit = limit,
            BrowseKey = Clip(One(query, "key").SanitizeAndRemove()),
            BrowseHeaderName = Clip(One(query, "headerName").SanitizeAndRemove()),
            BrowseHeaderValue = Clip(One(query, "headerValue").SanitizeAndRemove()),
            OpenRecord = Record(One(query, "record")),
            StageRecord = Record(One(query, "stage")),
            MessageText = Clip(One(query, "mq").SanitizeAndRemove()),
            HeaderContains = Clip(One(query, "hq").SanitizeAndRemove()),
            ValueContains = Clip(One(query, "vq").SanitizeAndRemove()),
            MessageKind = kind,
            MessageFrom = When(One(query, "mfrom")),
            MessageTo = When(One(query, "mto")),
            MsgPage = msgPage,
            Resume = KafkaBrowseResume.Format(KafkaBrowseResume.Parse(Clip(One(query, "resume"), 2000))),
            More = One(query, "more") is "1" or "true",
            Rf = rf,
            RfPage = rfPage,
            RfSize = rfSize,
            RfQ = rfQuery,
            RfThrottle = rfThrottle
        };
    }

    public ThroughputKafkaPageQuery WithReplication(int? page = null, int? size = null, string? search = null) =>
        this with
        {
            RfPage = page is > 0 ? page.Value : RfPage,
            RfSize = size is 10 or 25 or 50 ? size.Value : RfSize,
            RfQ = search ?? RfQ
        };

    public ThroughputKafkaPageQuery AtFrom(long from) =>
        this with { MessageFrom = from, MessageTo = null, Resume = "", MsgPage = 1, OpenRecord = "", StageRecord = "" };

    public string Href(string? view = null, string? sort = null, string? dir = null, int? page = null, string? family = null, string? group = null, string? q = null, string? topic = null, string? broker = null, string? keyClass = null, bool? tests = null, bool? advanced = null, string? record = null, bool closeRecord = false, int? part = null, string? stage = null, bool closeStage = false, int? msgPage = null, int? pageSize = null, string? resume = null, bool clearResume = false)
    {
        var chosenView = view ?? View;
        var partPage = part ?? PartPage;
        var changed = (view is not null && !string.Equals(view, View, StringComparison.Ordinal))
            || (group is not null && !string.Equals(group, Group, StringComparison.Ordinal))
            || (topic is not null && !string.Equals(topic, TopicName, StringComparison.Ordinal))
            || (broker is not null && !string.Equals(broker, Broker, StringComparison.Ordinal));
        if (part is null && changed)
            partPage = 1;
        var values = new Dictionary<string, string?>
        {
            ["q"] = q ?? Q,
            ["sort"] = sort ?? Sort,
            ["dir"] = dir ?? Dir,
            ["page"] = (page ?? Page).ToString(),
            ["pageSize"] = (pageSize is 10 or 25 or 50 ? pageSize.Value : PageSize).ToString(),
            ["family"] = family ?? Family,
            ["keyClass"] = keyClass ?? KeyClass,
            ["view"] = chosenView,
            ["group"] = group ?? Group,
            ["topic"] = topic ?? TopicName,
            ["broker"] = broker ?? Broker,
            ["tests"] = (tests ?? Tests) ? "1" : "",
            ["advanced"] = (advanced ?? Advanced) ? "1" : "",
            ["returnUrl"] = ReturnUrl
        };
        if (partPage > 1)
            values["part"] = partPage.ToString();
        var rf = changed ? 0 : Rf;
        if (rf > 0 && chosenView == Topic)
        {
            values["rf"] = rf.ToString();
            if (RfPage > 1)
                values["rfPage"] = RfPage.ToString();
            if (RfSize is 10 or 50)
                values["rfSize"] = RfSize.ToString();
            if (RfQ.Length > 0)
                values["rfQ"] = RfQ;
            if (RfThrottle > 0)
                values["rfThrottle"] = RfThrottle.ToString();
        }
        if (chosenView == Messages)
        {
            var messagePage = changed ? 1 : msgPage ?? MsgPage;
            if (messagePage > 1)
                values["msgPage"] = messagePage.ToString();
            if (MessageText.Length > 0)
                values["mq"] = MessageText;
            if (BrowseKey.Length > 0)
                values["key"] = BrowseKey;
            if (HeaderContains.Length > 0)
                values["hq"] = HeaderContains;
            if (ValueContains.Length > 0)
                values["vq"] = ValueContains;
            if (MessageKind.Length > 0)
                values["mtype"] = MessageKind;
            if (MessageFrom is long from)
                values["mfrom"] = from.ToString();
            if (MessageTo is long to)
                values["mto"] = to.ToString();
            if (More)
                values["more"] = "1";
            var resumeValue = changed || clearResume ? "" : resume ?? Resume;
            if (resumeValue.Length > 0)
                values["resume"] = resumeValue;
            var open = closeRecord ? "" : record ?? OpenRecord;
            if (open.Length > 0)
                values["record"] = open;
            var staged = closeStage || changed ? "" : stage ?? StageRecord;
            if (staged.Length > 0)
                values["stage"] = staged;
        }

        var pairs = values
            .Where(pair => !string.IsNullOrWhiteSpace(pair.Value))
            .Select(pair => Uri.EscapeDataString(pair.Key) + "=" + Uri.EscapeDataString(pair.Value!));
        var href = "/Operations/Kafka?" + string.Join("&", pairs);
        return href;
    }

    private static long? When(string raw)
    {
        if (raw.Length == 0)
            return null;
        if (long.TryParse(raw, out var unix))
            return unix < 0 ? null : unix;
        if (DateTimeOffset.TryParse(raw, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.AssumeUniversal, out var parsed))
            return parsed.ToUnixTimeMilliseconds();
        return null;
    }

    public ThroughputKafkaPageQuery WithSeek(string mode, long? offset) =>
        this with
        {
            View = Messages,
            BrowseMode = string.IsNullOrWhiteSpace(mode) ? "newest" : mode.Trim().ToLowerInvariant(),
            BrowseOffset = offset,
            OpenRecord = "",
            StageRecord = ""
        };

    public string ExportHref()
    {
        var href = Href(view: Messages, closeRecord: true);
        return "/Operations/Kafka/messages/export" + href["/Operations/Kafka".Length..];
    }

    private static string One(IQueryCollection query, string name) =>
        query.TryGetValue(name, out var values) ? values.ToString().Trim() : "";

    private static List<int> Partitions(IQueryCollection query)
    {
        var ids = new List<int>();
        if (!query.TryGetValue("partition", out var values))
            return ids;
        foreach (var value in values)
        {
            if (string.IsNullOrWhiteSpace(value))
                continue;
            foreach (var piece in value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (int.TryParse(piece, out var id) && id >= 0 && !ids.Contains(id))
                    ids.Add(id);
                if (ids.Count >= 80)
                    return ids;
            }
        }

        return ids;
    }

    private static long? LongOrNull(string value) =>
        long.TryParse(value, out var parsed) ? parsed : null;

    private static string Clip(string value) =>
        Clip(value, 200);

    private static string Clip(string value, int max) =>
        value.Length <= max ? value : value[..max];

    private static string Record(string value)
    {
        if (value.Length == 0 || value.Length > 40)
            return "";
        var split = value.Split(':');
        if (split.Length != 2)
            return "";
        if (!int.TryParse(split[0], out var partition) || partition < 0)
            return "";
        if (!long.TryParse(split[1], out var offset) || offset < 0)
            return "";
        return partition.ToString() + ":" + offset.ToString();
    }
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
    public ReplicationFactorPlan? ReplicationPlan { get; init; }
    public ChangeRequestRecord? Request { get; init; }
    public string? Message { get; init; }
    public string? Error { get; init; }
    public string DraftTopic { get; init; } = "";
    public int DraftPartitions { get; init; }
    public string DraftReason { get; init; } = "";
    public string DraftAddBrokerReason { get; init; } = "";
    public string DraftConfirmation { get; init; } = "";
    public bool DraftBackupSkip { get; init; }
    public bool DraftBackupSkipAcknowledged { get; init; }
    public string DraftAcknowledgedGroups { get; init; } = "";
    public string DraftPlanHash { get; init; } = "";
    public bool HasDryRunSnapshot { get; init; }
    public int DryRunPartitions { get; init; }
    public bool DryRunBackupSkip { get; init; }
    public bool DryRunBackupSkipAcknowledged { get; init; }
    public string DryRunAcknowledgedGroups { get; init; } = "";
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
    public KafkaBrowsePage? Messages { get; init; }
    public KafkaFamilyView? Family { get; init; }
    public string? BrowseError { get; init; }
    public KafkaProduceDraft? ProduceDraft { get; init; }

    public string ChartJson
    {
        get
        {
            var rates = ChartTopics
                .Where(row => row.ProduceRateKnown)
                .Select(row => new { label = row.Topic, value = row.ProduceRatePerSecond })
                .ToList();
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
    public string? AcknowledgedGroups { get; set; }
    public string? PlanHash { get; set; }
    public bool HasDryRunSnapshot { get; set; }
    public int DryRunPartitions { get; set; }
    public bool DryRunBackupSkip { get; set; }
    public bool DryRunBackupSkipAcknowledged { get; set; }
    public string? DryRunAcknowledgedGroups { get; set; }
    public string? Action { get; set; }
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

public sealed class KafkaMessageOpen
{
    public string Topic { get; init; } = "";
    public KafkaBrowseRecord Record { get; init; } = new();
    public string ReturnHref { get; init; } = "";
}
