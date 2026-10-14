using System.Text.Json;
using Confluent.Kafka;
using Confluent.Kafka.Admin;
using LantanaGroup.Link.Shared.KafkaAdmin.Protocol;
using LantanaGroup.Link.Shared.Application.Models.Configs;

namespace LantanaGroup.Link.Operations.Application.KafkaOps;

/// <summary>
/// Applies exact partition reassignments on Kafka itself: no infrastructure provider is involved.
/// Moves are submitted in waves of <see cref="BatchPartitions"/> partitions; the caller submits the next wave once
/// the previous one has left Kafka's reassignment list. Replication throttles cover the whole plan and are cleared at the end.
/// </summary>
public interface IKafkaPartitionMover
{
    int BatchPartitions { get; }

    /// <summary>Submits the next wave starting at <paramref name="alreadySubmitted"/>. Returns the new submitted count.</summary>
    Task<int> SubmitAsync(string reassignmentJson, int alreadySubmitted, long throttleBytesPerSecond, CancellationToken cancellationToken);

    /// <summary>Cancels whatever part of the plan is still reassigning. Partitions that finished keep their new replicas.</summary>
    Task CancelAsync(string reassignmentJson, CancellationToken cancellationToken);

    /// <summary>Removes the replication throttles this plan set. Safe to call more than once.</summary>
    Task ClearThrottleAsync(string reassignmentJson, CancellationToken cancellationToken);
}

public sealed class PlannedMove
{
    public string Topic { get; init; } = "";
    public int Partition { get; init; }
    public List<int> Replicas { get; init; } = [];
}

/// <summary>
/// The four configs Kafka's own reassignment tool sets: a rate on every broker that sends or receives, and per topic the
/// leader side (current replicas) and follower side (replicas being added) as "partition:broker" lists.
/// </summary>
public sealed class ReplicationThrottlePlan
{
    public const string LeaderRate = "leader.replication.throttled.rate";
    public const string FollowerRate = "follower.replication.throttled.rate";
    public const string LeaderReplicas = "leader.replication.throttled.replicas";
    public const string FollowerReplicas = "follower.replication.throttled.replicas";

    public SortedSet<int> Brokers { get; } = [];
    public SortedDictionary<string, string> Leaders { get; } = new(StringComparer.Ordinal);
    public SortedDictionary<string, string> Followers { get; } = new(StringComparer.Ordinal);

    public static ReplicationThrottlePlan For(IEnumerable<PlannedMove> moves, IReadOnlyDictionary<(string Topic, int Partition), IReadOnlyList<int>> current)
    {
        var plan = new ReplicationThrottlePlan();
        var leaders = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        var followers = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        foreach (var move in moves)
        {
            current.TryGetValue((move.Topic, move.Partition), out var existing);
            existing ??= [];
            var adding = move.Replicas.Where(replica => !existing.Contains(replica)).ToList();
            if (adding.Count == 0)
                continue;
            foreach (var broker in existing.Concat(move.Replicas))
                plan.Brokers.Add(broker);
            Add(leaders, move.Topic, existing.Select(broker => move.Partition + ":" + broker));
            Add(followers, move.Topic, adding.Select(broker => move.Partition + ":" + broker));
        }

        foreach (var (topic, items) in leaders)
            plan.Leaders[topic] = string.Join(",", items);
        foreach (var (topic, items) in followers)
            plan.Followers[topic] = string.Join(",", items);
        return plan;
    }

    private static void Add(Dictionary<string, List<string>> into, string topic, IEnumerable<string> items)
    {
        if (!into.TryGetValue(topic, out var list))
            into[topic] = list = [];
        list.AddRange(items);
    }
}

public interface IKafkaThrottleConfig
{
    Task ApplyAsync(ReplicationThrottlePlan plan, long bytesPerSecond, CancellationToken cancellationToken);

    Task ClearAsync(IReadOnlyCollection<int> brokers, IReadOnlyCollection<string> topics, CancellationToken cancellationToken);
}

public sealed class KafkaNativePartitionMover : IKafkaPartitionMover
{
    private readonly IKafkaReassignmentAdmin _admin;
    private readonly IKafkaBrokerGateway _broker;
    private readonly IKafkaThrottleConfig _throttles;

    public KafkaNativePartitionMover(IKafkaReassignmentAdmin admin, IKafkaBrokerGateway broker, IKafkaThrottleConfig throttles, int batchPartitions)
    {
        _admin = admin;
        _broker = broker;
        _throttles = throttles;
        BatchPartitions = Math.Clamp(batchPartitions, 1, 5000);
    }

    public int BatchPartitions { get; }

    public static List<PlannedMove> Read(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return [];
        using var document = JsonDocument.Parse(json);
        if (!document.RootElement.TryGetProperty("partitions", out var partitions))
            return [];
        return partitions.EnumerateArray().Select(item => new PlannedMove
        {
            Topic = item.GetProperty("topic").GetString() ?? "",
            Partition = item.GetProperty("partition").GetInt32(),
            Replicas = item.GetProperty("replicas").EnumerateArray().Select(replica => replica.GetInt32()).ToList()
        }).Where(move => move.Topic.Length > 0 && move.Replicas.Count > 0).ToList();
    }

    public async Task<int> SubmitAsync(string reassignmentJson, int alreadySubmitted, long throttleBytesPerSecond, CancellationToken cancellationToken)
    {
        var moves = Read(reassignmentJson);
        if (alreadySubmitted >= moves.Count)
            return moves.Count;
        if (alreadySubmitted <= 0 && throttleBytesPerSecond > 0)
        {
            var cluster = await _broker.DescribeClusterAsync(cancellationToken);
            if (cluster.Error is not null)
                throw new KafkaOpsRejectedException(cluster.Error);
            var current = cluster.Placements.ToDictionary(item => (item.Topic, item.Partition), item => (IReadOnlyList<int>)item.Replicas.ToList());
            await _throttles.ApplyAsync(ReplicationThrottlePlan.For(moves, current), throttleBytesPerSecond, cancellationToken);
        }

        var start = Math.Max(0, alreadySubmitted);
        var wave = moves.Skip(start).Take(BatchPartitions).Select(move => new PartitionTarget(move.Topic, move.Partition, move.Replicas)).ToList();
        var outcome = await _admin.AlterAsync(wave, cancellationToken);
        var failed = outcome.Partitions.Where(item => item.ErrorCode != KafkaAdminMessages.ErrorNone).ToList();
        if (failed.Count > 0)
            throw new KafkaOpsRejectedException("Kafka refused " + failed.Count + " of " + wave.Count + " partition moves. First: " + failed[0].Topic + "-" + failed[0].Partition + " "
                + KafkaAdminMessages.ErrorName(failed[0].ErrorCode) + (failed[0].ErrorMessage.Length > 0 ? " " + failed[0].ErrorMessage : "") + ".");
        return start + wave.Count;
    }

    public async Task CancelAsync(string reassignmentJson, CancellationToken cancellationToken)
    {
        var planned = Read(reassignmentJson).Select(move => (move.Topic, move.Partition)).ToHashSet();
        var ongoing = await _admin.ListAsync(cancellationToken);
        var targets = ongoing.Where(item => planned.Contains((item.Topic, item.Partition)))
            .Select(item => new PartitionTarget(item.Topic, item.Partition, null))
            .ToList();
        foreach (var chunk in targets.Chunk(Math.Max(BatchPartitions, 500)))
        {
            var outcome = await _admin.AlterAsync(chunk, cancellationToken);
            var failed = outcome.Partitions.FirstOrDefault(item => item.ErrorCode is not (KafkaAdminMessages.ErrorNone or KafkaAdminMessages.ErrorNoReassignmentInProgress));
            if (failed is not null)
                throw new KafkaOpsRejectedException("Kafka did not cancel " + failed.Topic + "-" + failed.Partition + ". " + KafkaAdminMessages.ErrorName(failed.ErrorCode) + ".");
        }
    }

    public async Task ClearThrottleAsync(string reassignmentJson, CancellationToken cancellationToken)
    {
        var topics = Read(reassignmentJson).Select(move => move.Topic).Distinct(StringComparer.Ordinal).ToList();
        var cluster = await _broker.DescribeClusterAsync(cancellationToken);
        if (cluster.Error is not null)
            throw new KafkaOpsRejectedException(cluster.Error);
        await _throttles.ClearAsync(cluster.Brokers.Select(broker => broker.Id).ToList(), topics, cancellationToken);
    }
}

/// <summary>Sets and removes replication throttles with IncrementalAlterConfigs, one call per broker as librdkafka requires.</summary>
public sealed class ConfluentThrottleConfig : IKafkaThrottleConfig
{
    private static readonly string[] TopicKeys = [ReplicationThrottlePlan.LeaderReplicas, ReplicationThrottlePlan.FollowerReplicas];
    private static readonly string[] BrokerKeys = [ReplicationThrottlePlan.LeaderRate, ReplicationThrottlePlan.FollowerRate];
    private readonly KafkaConnection _connection;

    public ConfluentThrottleConfig(KafkaConnection connection) => _connection = connection;

    public async Task ApplyAsync(ReplicationThrottlePlan plan, long bytesPerSecond, CancellationToken cancellationToken)
    {
        using var admin = NewAdmin();
        var rate = bytesPerSecond.ToString(System.Globalization.CultureInfo.InvariantCulture);
        foreach (var broker in plan.Brokers)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await AlterAsync(admin, new Dictionary<ConfigResource, List<ConfigEntry>>
            {
                [new ConfigResource { Type = ResourceType.Broker, Name = broker.ToString(System.Globalization.CultureInfo.InvariantCulture) }] =
                    BrokerKeys.Select(key => Entry(key, rate, AlterConfigOpType.Set)).ToList()
            });
        }

        var topics = new Dictionary<ConfigResource, List<ConfigEntry>>();
        foreach (var topic in plan.Leaders.Keys.Union(plan.Followers.Keys, StringComparer.Ordinal))
        {
            var entries = new List<ConfigEntry>();
            if (plan.Leaders.TryGetValue(topic, out var leaders))
                entries.Add(Entry(ReplicationThrottlePlan.LeaderReplicas, leaders, AlterConfigOpType.Set));
            if (plan.Followers.TryGetValue(topic, out var followers))
                entries.Add(Entry(ReplicationThrottlePlan.FollowerReplicas, followers, AlterConfigOpType.Set));
            topics[new ConfigResource { Type = ResourceType.Topic, Name = topic }] = entries;
        }

        if (topics.Count > 0)
            await AlterAsync(admin, topics);
    }

    public async Task ClearAsync(IReadOnlyCollection<int> brokers, IReadOnlyCollection<string> topics, CancellationToken cancellationToken)
    {
        using var admin = NewAdmin();
        foreach (var broker in brokers)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await AlterAsync(admin, new Dictionary<ConfigResource, List<ConfigEntry>>
            {
                [new ConfigResource { Type = ResourceType.Broker, Name = broker.ToString(System.Globalization.CultureInfo.InvariantCulture) }] =
                    BrokerKeys.Select(key => Entry(key, "", AlterConfigOpType.Delete)).ToList()
            });
        }

        if (topics.Count > 0)
        {
            await AlterAsync(admin, topics.ToDictionary(
                topic => new ConfigResource { Type = ResourceType.Topic, Name = topic },
                _ => TopicKeys.Select(key => Entry(key, "", AlterConfigOpType.Delete)).ToList()));
        }
    }

    private static ConfigEntry Entry(string name, string value, AlterConfigOpType operation) =>
        new() { Name = name, Value = value, IncrementalOperation = operation };

    private static Task AlterAsync(IAdminClient admin, Dictionary<ConfigResource, List<ConfigEntry>> configs) =>
        admin.IncrementalAlterConfigsAsync(configs, new IncrementalAlterConfigsOptions { RequestTimeout = TimeSpan.FromSeconds(15) });

    private IAdminClient NewAdmin()
    {
        var config = new AdminClientConfig
        {
            BootstrapServers = string.Join(",", _connection.BootstrapServers ?? []),
            ClientId = "LinkAdminBFF-throttle"
        };
        if (_connection.SaslProtocolEnabled)
        {
            config.SecurityProtocol = _connection.Protocol;
            config.SaslMechanism = _connection.Mechanism;
            config.SaslUsername = _connection.SaslUsername;
            config.SaslPassword = _connection.SaslPassword;
        }

        return new AdminClientBuilder(config).Build();
    }
}
