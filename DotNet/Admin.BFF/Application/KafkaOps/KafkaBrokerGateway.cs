using Confluent.Kafka;
using Confluent.Kafka.Admin;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using LantanaGroup.Link.Shared.Application.Models.Kafka;

namespace LantanaGroup.Link.LinkAdmin.BFF.Application.KafkaOps;

public sealed class TopicWatermark
{
    public string Topic { get; set; } = "";
    public int Partitions { get; set; }
    public int ReplicationFactor { get; set; }
    public Dictionary<string, string> Configs { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public List<long> HighWatermarks { get; set; } = [];
    public string? Error { get; set; }
    public List<string> AuthorizedOperations { get; set; } = [];
    public bool? CanAlterPartitions { get; set; }
}

public sealed class GroupView
{
    public string GroupId { get; set; } = "";
    public string State { get; set; } = "";
    public List<GroupMemberView> Members { get; set; } = [];
    public List<PartitionLagView> Partitions { get; set; } = [];
    public long TotalLag { get; set; }
    public List<string> UnownedPartitions { get; set; } = [];
    public int MembersOnExpectedConfig { get; set; }
}

public sealed class GroupMemberView
{
    public string ClientId { get; set; } = "";
    public string Host { get; set; } = "";
    public string ClientHost { get; set; } = "";
    public List<string> Assignment { get; set; } = [];
    public bool AdvertisesExpectedConfig { get; set; }
}

public sealed class PartitionLagView
{
    public string Topic { get; set; } = "";
    public int Partition { get; set; }
    public long HighWatermark { get; set; }
    public long Committed { get; set; }
    public long Lag { get; set; }
    public bool Owned { get; set; }
}

public interface IKafkaBrokerGateway
{
    Task<IReadOnlyList<TopicWatermark>> DescribeTopicsAsync(IReadOnlyList<string> topics, bool probeAlter, CancellationToken cancellationToken);
    Task<IReadOnlyList<GroupView>> DescribeGroupsAsync(bool includeTestGroups, int expectedConfigVersion, CancellationToken cancellationToken);
    Task IncreasePartitionsAsync(string topic, int newCount, CancellationToken cancellationToken);
    Task<ClusterSnapshot> DescribeClusterAsync(CancellationToken cancellationToken);
}

public sealed class KafkaBrokerGateway : IKafkaBrokerGateway, IDisposable
{
    private static readonly string[] ConfigNames =
    [
        "retention.ms",
        "cleanup.policy",
        "min.insync.replicas",
        "max.message.bytes"
    ];

    private readonly KafkaConnection _connection;
    private readonly object _gate = new();
    private IAdminClient? _admin;

    public KafkaBrokerGateway(KafkaConnection connection)
    {
        _connection = connection;
    }

    public async Task<IReadOnlyList<TopicWatermark>> DescribeTopicsAsync(IReadOnlyList<string> topics, bool probeAlter, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (topics.Count == 0)
            return [];

        var admin = Client();
        var known = new HashSet<string>(
            admin.GetMetadata(TimeSpan.FromSeconds(20)).Topics.Select(topic => topic.Topic),
            StringComparer.Ordinal);
        var present = topics.Where(known.Contains).ToList();
        var result = topics
            .Where(name => !known.Contains(name))
            .Select(name => new TopicWatermark { Topic = name, Error = "Unknown topic or partition" })
            .ToList();
        if (present.Count == 0)
            return result;

        var described = await admin.DescribeTopicsAsync(
            TopicCollection.OfTopicNames(present),
            new DescribeTopicsOptions { IncludeAuthorizedOperations = true, RequestTimeout = TimeSpan.FromSeconds(20) });

        foreach (var topic in described.TopicDescriptions)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var view = new TopicWatermark { Topic = topic.Name ?? "" };
            if (topic.Error.IsError)
            {
                view.Error = topic.Error.Reason;
                result.Add(view);
                continue;
            }

            view.Partitions = topic.Partitions?.Count ?? 0;
            view.ReplicationFactor = topic.Partitions?.FirstOrDefault()?.Replicas?.Count ?? 0;
            if (topic.AuthorizedOperations is not null)
            {
                view.AuthorizedOperations = topic.AuthorizedOperations
                    .Select(operation => operation.ToString())
                    .Distinct(StringComparer.Ordinal)
                    .OrderBy(name => name, StringComparer.Ordinal)
                    .ToList();
            }

            if (view.Partitions > 0)
                view.HighWatermarks = await HighWatermarksAsync(admin, view.Topic, view.Partitions, cancellationToken);

            view.Configs = await ConfigsAsync(admin, view.Topic, cancellationToken);

            if (probeAlter && view.Partitions > 0)
                view.CanAlterPartitions = await ProbeAlterAsync(admin, view.Topic, view.Partitions, cancellationToken);

            result.Add(view);
        }

        return result;
    }

    public async Task<IReadOnlyList<GroupView>> DescribeGroupsAsync(bool includeTestGroups, int expectedConfigVersion, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var admin = Client();
        var listed = await admin.ListConsumerGroupsAsync(new ListConsumerGroupsOptions { RequestTimeout = TimeSpan.FromSeconds(20) });
        var ids = listed.Valid
            .Select(group => group.GroupId)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Where(id => includeTestGroups || !IsHiddenTestGroup(id))
            .Distinct(StringComparer.Ordinal)
            .ToList();

        if (ids.Count == 0)
            return [];

        var described = await admin.DescribeConsumerGroupsAsync(ids, new DescribeConsumerGroupsOptions { RequestTimeout = TimeSpan.FromSeconds(30) });
        var offsetResults = await ConsumerGroupOffsetQueries.ListPerGroupAsync<ListConsumerGroupOffsetsResult>(
            ids,
            async (groupId, token) =>
            {
                var listed = await admin.ListConsumerGroupOffsetsAsync(
                    new ConsumerGroupTopicPartitions[] { new(groupId, null) },
                    new ListConsumerGroupOffsetsOptions { RequestTimeout = TimeSpan.FromSeconds(30) });
                token.ThrowIfCancellationRequested();
                return listed;
            },
            cancellationToken);

        var committed = new Dictionary<string, List<TopicPartitionOffsetError>>(StringComparer.Ordinal);
        foreach (var groupOffsets in offsetResults)
        {
            if (groupOffsets.Group is null)
                continue;
            committed[groupOffsets.Group] = groupOffsets.Partitions?.ToList() ?? [];
        }

        var watermarkTopics = committed.Values
            .SelectMany(rows => rows)
            .Select(row => row.Topic)
            .Where(topic => !string.IsNullOrWhiteSpace(topic))
            .Distinct(StringComparer.Ordinal)
            .ToList();
        var watermarks = await WatermarksForCommittedAsync(admin, watermarkTopics, committed, cancellationToken);

        var views = new List<GroupView>();
        foreach (var group in described.ConsumerGroupDescriptions)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var view = new GroupView
            {
                GroupId = group.GroupId ?? "",
                State = group.State.ToString()
            };

            var owned = new HashSet<string>(StringComparer.Ordinal);
            foreach (var member in group.Members ?? [])
            {
                var assignment = member.Assignment?.TopicPartitions?
                    .Select(tp => tp.Topic + "-" + tp.Partition.Value)
                    .ToList() ?? [];
                foreach (var item in assignment)
                    owned.Add(item);

                var memberView = new GroupMemberView
                {
                    ClientId = member.ClientId ?? "",
                    Host = member.Host ?? "",
                    ClientHost = member.Host ?? "",
                    Assignment = assignment,
                    AdvertisesExpectedConfig = KafkaConfigAdvertisement.ClientAdvertisesExpectedConfig(member.ClientId, expectedConfigVersion)
                };
                view.Members.Add(memberView);
            }

            view.MembersOnExpectedConfig = view.Members.Count(member => member.AdvertisesExpectedConfig);
            if (committed.TryGetValue(view.GroupId, out var rows))
            {
                foreach (var row in rows)
                {
                    var key = row.Topic + "-" + row.Partition.Value;
                    var high = watermarks.TryGetValue(key, out var watermark) ? watermark : 0;
                    var committedOffset = row.Offset.IsSpecial ? 0 : Math.Max(0, row.Offset.Value);
                    var lag = row.Offset.IsSpecial ? high : Math.Max(0, high - committedOffset);
                    view.Partitions.Add(new PartitionLagView
                    {
                        Topic = row.Topic,
                        Partition = row.Partition.Value,
                        HighWatermark = high,
                        Committed = row.Offset.IsSpecial ? -1 : committedOffset,
                        Lag = lag,
                        Owned = owned.Contains(key)
                    });
                    if (!owned.Contains(key))
                        view.UnownedPartitions.Add(key);
                    view.TotalLag += lag;
                }
            }

            views.Add(view);
        }

        return views;
    }

    public async Task IncreasePartitionsAsync(string topic, int newCount, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var admin = Client();
        var metadata = admin.GetMetadata(topic, TimeSpan.FromSeconds(20));
        var current = metadata.Topics.FirstOrDefault(item => string.Equals(item.Topic, topic, StringComparison.Ordinal))?.Partitions.Count ?? 0;
        if (current >= newCount)
            return;

        await admin.CreatePartitionsAsync(
            [new PartitionsSpecification { Topic = topic, IncreaseTo = newCount }],
            new CreatePartitionsOptions { ValidateOnly = false, RequestTimeout = TimeSpan.FromSeconds(30) });
    }

    public async Task<ClusterSnapshot> DescribeClusterAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var admin = Client();
        var described = await admin.DescribeClusterAsync(new DescribeClusterOptions
        {
            IncludeAuthorizedOperations = true,
            RequestTimeout = TimeSpan.FromSeconds(20)
        });
        var metadata = admin.GetMetadata(TimeSpan.FromSeconds(20));
        var brokers = described.Nodes
            .Select(node => new BrokerSnapshot
            {
                Id = node.Id,
                Host = node.Host ?? "",
                Port = node.Port,
                Rack = node.Rack ?? "",
                State = "up",
                LogDirBytes = -1
            })
            .ToList();
        var known = brokers.Select(broker => broker.Id).ToHashSet();
        var placements = new List<PartitionPlacement>();
        var underReplicated = 0;
        var offline = 0;
        var isrShrunk = 0;
        foreach (var topic in metadata.Topics)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (topic.Error.IsError || topic.Partitions is null)
                continue;
            foreach (var partition in topic.Partitions)
            {
                var replicas = partition.Replicas?.ToList() ?? [];
                var isr = partition.InSyncReplicas?.ToList() ?? [];
                var health = ClusterHealthCounts.ForPartition(partition.Leader, replicas, isr);
                offline += health.Offline;
                underReplicated += health.UnderReplicated;
                isrShrunk += health.IsrShrunk;
                placements.Add(new PartitionPlacement
                {
                    Topic = topic.Topic ?? "",
                    Partition = partition.PartitionId,
                    Leader = partition.Leader,
                    Replicas = replicas,
                    Isr = isr
                });
                foreach (var replica in replicas)
                {
                    if (!known.Contains(replica))
                    {
                        known.Add(replica);
                        brokers.Add(new BrokerSnapshot { Id = replica, State = "missing", LogDirBytes = -1 });
                    }
                }

                foreach (var broker in brokers)
                {
                    if (replicas.Contains(broker.Id))
                        broker.PartitionCount++;
                    if (partition.Leader == broker.Id)
                        broker.LeaderCount++;
                }
            }
        }

        var roles = await ControllerEligibleIdsAsync(admin, brokers, cancellationToken);
        return new ClusterSnapshot
        {
            BrokerCount = described.Nodes.Count,
            ControllerId = described.Controller?.Id,
            UnderReplicatedPartitions = underReplicated,
            OfflinePartitions = offline,
            IsrShrunkPartitions = isrShrunk,
            ControllerEligibleIds = roles.Ids,
            ControllerRolesKnown = roles.Known,
            LogDirsAvailable = false,
            LogDirDetail = "This client cannot describe log directories. A broker is empty when no partition lists it as a replica or a leader.",
            Brokers = brokers.OrderBy(broker => broker.Id).ToList(),
            Placements = placements
        };
    }

    public void Dispose()
    {
        lock (_gate)
        {
            _admin?.Dispose();
            _admin = null;
        }
    }

    internal static bool IsHiddenTestGroup(string groupId) =>
        groupId.StartsWith("e2e-diag-", StringComparison.OrdinalIgnoreCase)
        || groupId.StartsWith("Dynamic:", StringComparison.Ordinal);

    private IAdminClient Client()
    {
        lock (_gate)
        {
            if (_admin is not null)
                return _admin;

            var config = new AdminClientConfig
            {
                BootstrapServers = string.Join(",", _connection.BootstrapServers ?? []),
                ClientId = "LinkAdminBFF-ops"
            };
            if (_connection.SaslProtocolEnabled)
            {
                config.SecurityProtocol = _connection.Protocol;
                config.SaslMechanism = _connection.Mechanism;
                config.SaslUsername = _connection.SaslUsername;
                config.SaslPassword = _connection.SaslPassword;
            }

            _admin = new AdminClientBuilder(config).Build();
            return _admin;
        }
    }

    private static async Task<List<long>> HighWatermarksAsync(IAdminClient admin, string topic, int partitions, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var specs = Enumerable.Range(0, partitions)
            .Select(partition => new TopicPartitionOffsetSpec
            {
                TopicPartition = new TopicPartition(topic, partition),
                OffsetSpec = OffsetSpec.Latest()
            })
            .ToList();
        var listed = await admin.ListOffsetsAsync(specs, new ListOffsetsOptions { RequestTimeout = TimeSpan.FromSeconds(20) });
        var values = new long[partitions];
        foreach (var info in listed.ResultInfos)
        {
            var error = info.TopicPartitionOffsetError;
            if (error is null || error.Error.IsError)
                continue;
            if (error.Partition.Value >= 0 && error.Partition.Value < values.Length)
                values[error.Partition.Value] = error.Offset.Value;
        }

        return values.ToList();
    }

    private static async Task<Dictionary<string, long>> WatermarksForCommittedAsync(
        IAdminClient admin,
        IReadOnlyList<string> topics,
        Dictionary<string, List<TopicPartitionOffsetError>> committed,
        CancellationToken cancellationToken)
    {
        var specs = new List<TopicPartitionOffsetSpec>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var rows in committed.Values)
        {
            foreach (var row in rows)
            {
                var key = row.Topic + "-" + row.Partition.Value;
                if (!seen.Add(key))
                    continue;
                specs.Add(new TopicPartitionOffsetSpec
                {
                    TopicPartition = new TopicPartition(row.Topic, row.Partition),
                    OffsetSpec = OffsetSpec.Latest()
                });
            }
        }

        var map = new Dictionary<string, long>(StringComparer.Ordinal);
        if (specs.Count == 0)
            return map;

        cancellationToken.ThrowIfCancellationRequested();
        var listed = await admin.ListOffsetsAsync(specs, new ListOffsetsOptions { RequestTimeout = TimeSpan.FromSeconds(20) });
        foreach (var info in listed.ResultInfos)
        {
            var error = info.TopicPartitionOffsetError;
            if (error is null || error.Error.IsError)
                continue;
            map[error.Topic + "-" + error.Partition.Value] = Math.Max(0, error.Offset.Value);
        }

        return map;
    }

    private static async Task<(List<int> Ids, bool Known)> ControllerEligibleIdsAsync(
        IAdminClient admin,
        IReadOnlyList<BrokerSnapshot> brokers,
        CancellationToken cancellationToken)
    {
        if (brokers.Count == 0)
            return ([], false);

        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            var described = await admin.DescribeConfigsAsync(
                brokers.Select(broker => new ConfigResource { Type = ResourceType.Broker, Name = broker.Id.ToString() }).ToList(),
                new DescribeConfigsOptions { RequestTimeout = TimeSpan.FromSeconds(15) });
            var eligible = new List<int>();
            foreach (var result in described)
            {
                if (!int.TryParse(result.ConfigResource.Name, out var id) || eligible.Contains(id))
                    continue;
                string? roles = null;
                foreach (var entry in result.Entries)
                {
                    if (string.Equals(entry.Key, "process.roles", StringComparison.OrdinalIgnoreCase))
                        roles = entry.Value.Value;
                }

                if (roles is not null && roles.Contains("controller", StringComparison.OrdinalIgnoreCase))
                    eligible.Add(id);
            }

            return (eligible, true);
        }
        catch (KafkaException)
        {
            return ([], false);
        }
    }

    private static async Task<Dictionary<string, string>> ConfigsAsync(IAdminClient admin, string topic, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var configs = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            var described = await admin.DescribeConfigsAsync(
                [new ConfigResource { Type = ResourceType.Topic, Name = topic }],
                new DescribeConfigsOptions { RequestTimeout = TimeSpan.FromSeconds(15) });
            foreach (var result in described)
            {
                foreach (var entry in result.Entries)
                {
                    if (ConfigNames.Contains(entry.Key, StringComparer.OrdinalIgnoreCase))
                        configs[entry.Key] = entry.Value.Value ?? "";
                }
            }
        }
        catch (KafkaException)
        {
            // Config describe is optional for the page. The partition count still stands.
        }

        return configs;
    }

    private static async Task<bool?> ProbeAlterAsync(IAdminClient admin, string topic, int partitions, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            await admin.CreatePartitionsAsync(
                [new PartitionsSpecification { Topic = topic, IncreaseTo = partitions + 1 }],
                new CreatePartitionsOptions { ValidateOnly = true, RequestTimeout = TimeSpan.FromSeconds(15) });
            return true;
        }
        catch (KafkaException ex) when (ex.Error.Code is ErrorCode.TopicAuthorizationFailed or ErrorCode.ClusterAuthorizationFailed)
        {
            return false;
        }
        catch (KafkaException)
        {
            return null;
        }
    }
}
