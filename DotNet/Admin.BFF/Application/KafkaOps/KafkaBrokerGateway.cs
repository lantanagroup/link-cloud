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

public sealed class ReassignmentListing
{
    public bool Known { get; set; }
    public List<string> Topics { get; set; } = [];
}

public static class KafkaLeaderElection
{
    public static readonly ElectionType Kind = ElectionType.Preferred;
}

public interface IKafkaBrokerGateway
{
    Task<IReadOnlyList<TopicWatermark>> DescribeTopicsAsync(IReadOnlyList<string> topics, bool probeAlter, CancellationToken cancellationToken);
    Task<IReadOnlyList<GroupView>> DescribeGroupsAsync(bool includeTestGroups, int expectedConfigVersion, CancellationToken cancellationToken);
    Task IncreasePartitionsAsync(string topic, int newCount, CancellationToken cancellationToken);
    Task<ClusterSnapshot> DescribeClusterAsync(CancellationToken cancellationToken);
    Task<ReassignmentListing> ListInFlightReassignmentsAsync(CancellationToken cancellationToken);
    Task ElectPreferredLeadersAsync(IReadOnlyList<TopicPartition> partitions, CancellationToken cancellationToken);
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

    private const int AdminClientFailureLimit = 3;

    private readonly KafkaConnection _connection;
    private readonly IKafkaAdminClientFactory _clients;
    private readonly SemaphoreSlim _adminGate = new(1, 1);
    private readonly Dictionary<string, int> _failuresByOperation = new(StringComparer.Ordinal);
    private IAdminClient? _admin;

    public KafkaBrokerGateway(KafkaConnection connection)
        : this(connection, new ConfluentAdminClientFactory())
    {
    }

    internal KafkaBrokerGateway(KafkaConnection connection, IKafkaAdminClientFactory clients)
    {
        _connection = connection;
        _clients = clients;
    }

    public Task<IReadOnlyList<TopicWatermark>> DescribeTopicsAsync(IReadOnlyList<string> topics, bool probeAlter, CancellationToken cancellationToken) =>
        UseClientAsync("topics", (admin, token) => DescribeTopicsCoreAsync(admin, topics, probeAlter, token), cancellationToken);

    private async Task<IReadOnlyList<TopicWatermark>> DescribeTopicsCoreAsync(IAdminClient admin, IReadOnlyList<string> topics, bool probeAlter, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (topics.Count == 0)
            return [];

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
                ThrowIfStale(topic.Error);
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

    public Task<IReadOnlyList<GroupView>> DescribeGroupsAsync(bool includeTestGroups, int expectedConfigVersion, CancellationToken cancellationToken) =>
        UseClientAsync("groups", (admin, token) => DescribeGroupsCoreAsync(admin, includeTestGroups, expectedConfigVersion, token), cancellationToken);

    private async Task<IReadOnlyList<GroupView>> DescribeGroupsCoreAsync(IAdminClient admin, bool includeTestGroups, int expectedConfigVersion, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var ids = CatalogGroupIds(includeTestGroups);
        if (ids.Count == 0)
            return [];

        var described = await DescribeCatalogGroupsAsync(admin, ids, cancellationToken);
        var present = described
            .Where(group => !string.IsNullOrWhiteSpace(group.GroupId) && !IsMissingGroup(group.Error))
            .Select(group => group.GroupId!)
            .Distinct(StringComparer.Ordinal)
            .ToList();
        var offsetResults = await ConsumerGroupOffsetQueries.ListPerGroupAsync<ListConsumerGroupOffsetsResult>(
            present,
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

        foreach (var row in committed.Values.SelectMany(rows => rows))
            ThrowIfStale(row.Error);

        var watermarkTopics = committed.Values
            .SelectMany(rows => rows)
            .Select(row => row.Topic)
            .Where(topic => !string.IsNullOrWhiteSpace(topic))
            .Distinct(StringComparer.Ordinal)
            .ToList();
        var watermarks = await WatermarksForCommittedAsync(admin, watermarkTopics, committed, cancellationToken);

        var views = new List<GroupView>();
        foreach (var groupId in ids)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var group = described.FirstOrDefault(candidate => string.Equals(candidate.GroupId, groupId, StringComparison.Ordinal));
            if (group is null || IsMissingGroup(group.Error))
            {
                views.Add(new GroupView { GroupId = groupId, State = "Empty" });
                continue;
            }

            ThrowIfGroupFailed(group.Error);
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
                    ThrowIfStale(row.Error);
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

    public Task<ReassignmentListing> ListInFlightReassignmentsAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        // Confluent.Kafka 2.16.0 still has no ListPartitionReassignments. The service asks the infrastructure provider.
        return Task.FromResult(new ReassignmentListing());
    }

    public Task ElectPreferredLeadersAsync(IReadOnlyList<TopicPartition> partitions, CancellationToken cancellationToken)
    {
        if (partitions.Count == 0)
            return Task.CompletedTask;
        return UseClientAsync("election", async (admin, token) =>
        {
            token.ThrowIfCancellationRequested();
            await admin.ElectLeadersAsync(
                KafkaLeaderElection.Kind,
                partitions,
                new ElectLeadersOptions { RequestTimeout = TimeSpan.FromSeconds(15), OperationTimeout = TimeSpan.FromSeconds(15) });
            return true;
        }, cancellationToken);
    }

    public Task IncreasePartitionsAsync(string topic, int newCount, CancellationToken cancellationToken) =>
        UseClientAsync("partitions", async (admin, token) =>
        {
            await IncreasePartitionsCoreAsync(admin, topic, newCount, token);
            return true;
        }, cancellationToken);

    private async Task IncreasePartitionsCoreAsync(IAdminClient admin, string topic, int newCount, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var metadata = admin.GetMetadata(topic, TimeSpan.FromSeconds(20));
        var current = metadata.Topics.FirstOrDefault(item => string.Equals(item.Topic, topic, StringComparison.Ordinal))?.Partitions.Count ?? 0;
        if (current >= newCount)
            return;

        await admin.CreatePartitionsAsync(
            [new PartitionsSpecification { Topic = topic, IncreaseTo = newCount }],
            new CreatePartitionsOptions { ValidateOnly = false, RequestTimeout = TimeSpan.FromSeconds(30) });
    }

    public Task<ClusterSnapshot> DescribeClusterAsync(CancellationToken cancellationToken) =>
        UseClientAsync("cluster", (admin, token) => DescribeClusterCoreAsync(admin, token), cancellationToken);

    private async Task<ClusterSnapshot> DescribeClusterCoreAsync(IAdminClient admin, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

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
            if (topic.Error.IsError)
                ThrowIfStale(topic.Error);
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
        _adminGate.Wait();
        try
        {
            _admin?.Dispose();
            _admin = null;
        }
        finally
        {
            _adminGate.Release();
        }
    }

    internal static bool IsHiddenTestGroup(string groupId) =>
        groupId.StartsWith("e2e-diag-", StringComparison.OrdinalIgnoreCase)
        || groupId.StartsWith("Dynamic:", StringComparison.Ordinal);

    private async Task<T> UseClientAsync<T>(string operation, Func<IAdminClient, CancellationToken, Task<T>> action, CancellationToken cancellationToken)
    {
        await _adminGate.WaitAsync(cancellationToken);
        try
        {
            _admin ??= _clients.Create(AdminConfig());
            try
            {
                var result = await action(_admin, cancellationToken);
                _failuresByOperation[operation] = 0;
                return result;
            }
            catch (Exception ex)
            {
                NoteFailure(operation, ex);
                throw;
            }
        }
        finally
        {
            _adminGate.Release();
        }
    }

    private void NoteFailure(string operation, Exception error)
    {
        if (error is OperationCanceledException)
            return;

        if (!IsStaleAdminError(error))
        {
            var count = _failuresByOperation.GetValueOrDefault(operation) + 1;
            _failuresByOperation[operation] = count;
            if (count < AdminClientFailureLimit)
                return;
        }

        SwapAdmin();
    }

    private void SwapAdmin()
    {
        var fresh = _clients.Create(AdminConfig());
        var old = _admin;
        _admin = fresh;
        _failuresByOperation.Clear();
        old?.Dispose();
    }

    private AdminClientConfig AdminConfig()
    {
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

        return config;
    }

    private static bool IsStaleAdminError(Exception error)
    {
        if (error is not KafkaException kafka)
            return false;
        return IsStaleAdminCode(kafka.Error.Code);
    }

    private static bool IsStaleAdminCode(ErrorCode code) =>
        code is ErrorCode.NotCoordinatorForGroup
            or ErrorCode.GroupCoordinatorNotAvailable
            or ErrorCode.LeaderNotAvailable
            or ErrorCode.NotLeaderForPartition
            or ErrorCode.Local_Transport
            or ErrorCode.Local_AllBrokersDown
            or ErrorCode.Local_TimedOut
            or ErrorCode.RequestTimedOut
            or ErrorCode.NetworkException;

    private static bool IsMissingGroup(Error? error) =>
        error is { Code: ErrorCode.GroupIdNotFound };

    private static void ThrowIfGroupFailed(Error? error)
    {
        if (error is null || error.Code == ErrorCode.NoError || IsMissingGroup(error))
            return;
        throw new KafkaException(error);
    }

    private static void ThrowIfStale(Error? error)
    {
        if (error is not null && IsStaleAdminCode(error.Code))
            throw new KafkaException(error);
    }

    private static void RejectUnsafeGroupErrors(IEnumerable<ConsumerGroupDescription> groups)
    {
        foreach (var group in groups)
        {
            if (IsMissingGroup(group.Error))
                continue;
            ThrowIfGroupFailed(group.Error);
        }
    }

    private async Task<List<ConsumerGroupDescription>> DescribeCatalogGroupsAsync(IAdminClient admin, IReadOnlyList<string> ids, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            var described = await admin.DescribeConsumerGroupsAsync(
                ids,
                new DescribeConsumerGroupsOptions { RequestTimeout = TimeSpan.FromSeconds(30) });
            var rows = described.ConsumerGroupDescriptions?.ToList() ?? [];
            RejectUnsafeGroupErrors(rows);
            return rows;
        }
        catch (DescribeConsumerGroupsException ex)
        {
            var rows = ex.Results?.ConsumerGroupDescriptions?.ToList() ?? [];
            RejectUnsafeGroupErrors(rows);
            return rows;
        }
    }

    private static List<string> CatalogGroupIds(bool includeTestGroups)
    {
        var ids = new List<string>();
        foreach (var topic in KafkaTopicCatalog.Topics)
        {
            foreach (var groupId in topic.Groups)
            {
                if (string.IsNullOrWhiteSpace(groupId))
                    continue;
                if (!includeTestGroups && IsHiddenTestGroup(groupId))
                    continue;
                if (!ids.Contains(groupId, StringComparer.Ordinal))
                    ids.Add(groupId);
            }
        }

        return ids;
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
            if (error is null)
                continue;
            ThrowIfStale(error.Error);
            if (error.Error.IsError)
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
            if (error is null)
                continue;
            ThrowIfStale(error.Error);
            if (error.Error.IsError)
                continue;
            map[error.Topic + "-" + error.Partition.Value] = Math.Max(0, error.Offset.Value);
        }

        return map;
    }

    internal static async Task<(List<int> Ids, bool Known)> ControllerEligibleIdsAsync(
        IAdminClient admin,
        IReadOnlyList<BrokerSnapshot> brokers,
        CancellationToken cancellationToken)
    {
        if (brokers.Count == 0)
            return ([], false);

        var eligible = new List<int>();
        var seen = new HashSet<int>();
        foreach (var broker in brokers)
        {
            if (!seen.Add(broker.Id))
                continue;

            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var described = await admin.DescribeConfigsAsync(
                    [new ConfigResource { Type = ResourceType.Broker, Name = broker.Id.ToString() }],
                    new DescribeConfigsOptions { RequestTimeout = TimeSpan.FromSeconds(15) });
                var sawBroker = false;
                string? roles = null;
                foreach (var result in described)
                {
                    if (!int.TryParse(result.ConfigResource.Name, out var id) || id != broker.Id)
                        continue;
                    sawBroker = true;
                    foreach (var entry in result.Entries)
                    {
                        if (string.Equals(entry.Key, "process.roles", StringComparison.OrdinalIgnoreCase))
                            roles = entry.Value.Value;
                    }
                }

                if (!sawBroker || string.IsNullOrWhiteSpace(roles))
                    return ([], false);
                if (roles.Contains("controller", StringComparison.OrdinalIgnoreCase))
                    eligible.Add(broker.Id);
            }
            catch (KafkaException)
            {
                return ([], false);
            }
        }

        return (eligible, true);
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

internal interface IKafkaAdminClientFactory
{
    IAdminClient Create(AdminClientConfig config);
}

internal sealed class ConfluentAdminClientFactory : IKafkaAdminClientFactory
{
    public IAdminClient Create(AdminClientConfig config) => new AdminClientBuilder(config).Build();
}
