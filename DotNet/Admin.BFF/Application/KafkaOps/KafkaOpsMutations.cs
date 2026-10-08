using System.Security.Claims;
using System.Text.Json;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using LantanaGroup.Link.Shared.Application.Models.Kafka;

namespace LantanaGroup.Link.LinkAdmin.BFF.Application.KafkaOps;

public sealed partial class KafkaOpsService
{
    public async Task<ClusterSnapshot> GetClusterAsync(CancellationToken cancellationToken)
    {
        var cacheKey = Key("view:cluster");
        var cached = await _cache.GetAsync<ClusterSnapshot>(cacheKey, cancellationToken);
        if (cached is not null && cached.Error is null && cached.BrokerCount > 0)
            return cached;

        ClusterSnapshot snapshot;
        try
        {
            snapshot = await _broker.DescribeClusterAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Kafka cluster view failed");
            return new ClusterSnapshot { Error = "Kafka could not be read. " + ex.Message };
        }

        if (snapshot.Error is null && snapshot.BrokerCount > 0)
            await _cache.SetAsync(cacheKey, snapshot, TimeSpan.FromSeconds(Math.Clamp(_options.Value.CacheSeconds, 5, 10)), ExpirationType.Absolute, cancellationToken);
        return snapshot;
    }

    public async Task<ReplicaScalePlan> PlanScaleAsync(string groupId, int desiredMembers, CancellationToken cancellationToken)
    {
        var topics = await GetTopicsAsync(cancellationToken);
        if (topics.Error is not null)
            throw new KafkaOpsRejectedException(topics.Error);
        var groups = await GetGroupsAsync(false, cancellationToken);
        if (groups.Error is not null)
            throw new KafkaOpsRejectedException(groups.Error);

        var subscribed = topics.Topics.Where(row => row.Groups.Contains(groupId, StringComparer.Ordinal)).ToList();
        var partitionCount = subscribed.Count == 0 ? 0 : subscribed.Min(row => row.Partitions);
        var linkMax = subscribed.Count == 0 ? partitionCount : subscribed.Min(row => row.MaxReplicas);
        var current = groups.Groups.FirstOrDefault(group => group.GroupId == groupId)?.Members.Count ?? 0;
        return ReplicaScalePlanner.Evaluate(groupId, current, partitionCount, desiredMembers, linkMax, _options.Value.RequireSecondApprover);
    }

    public async Task<ChangeRequestRecord> CreateScaleAsync(ClaimsPrincipal user, string groupId, int desiredMembers, string reason, string? correlationId, CancellationToken cancellationToken)
    {
        EnsureWritable();
        EnsureActor(user, KafkaChangeKind.ScaleReplicas);
        var plan = await PlanScaleAsync(groupId, desiredMembers, cancellationToken);
        if (!plan.Accepted)
            throw new KafkaOpsRejectedException(string.Join(" ", plan.Errors));
        if (!_infra.Enabled)
            throw new KafkaOpsRejectedException(_infra.Detail);

        return await SaveNewAsync(user, reason, correlationId, plan.SecondApproverRequired, plan.Summary, cancellationToken, record =>
        {
            record.Kind = KafkaChangeKind.ScaleReplicas;
            record.GroupId = groupId;
            record.Family = groupId;
            record.BeforeReplicas = plan.CurrentMembers;
            record.DesiredReplicas = plan.DesiredMembers;
            record.MaxReplicas = plan.Ceiling;
        });
    }

    public async Task<BrokerMovePlan> PlanDecommissionAsync(int brokerId, CancellationToken cancellationToken) =>
        BrokerMovePlanner.Decommission(brokerId, await BrokerIdsAsync(cancellationToken), await FactsAsync(cancellationToken));

    public async Task<ChangeRequestRecord> CreateDecommissionAsync(ClaimsPrincipal user, int brokerId, string reason, string? correlationId, CancellationToken cancellationToken)
    {
        EnsureWritable();
        EnsureActor(user, KafkaChangeKind.DecommissionBroker);
        var plan = await PlanDecommissionAsync(brokerId, cancellationToken);
        if (!plan.Accepted)
            throw new KafkaOpsRejectedException(string.Join(" ", plan.Errors));
        if (!_infra.Enabled)
            throw new KafkaOpsRejectedException(_infra.Detail);

        var json = ReassignmentJson.ForMoves(plan.Moves, brokerId, null);
        return await SaveNewAsync(user, reason, correlationId, true, plan.Summary, cancellationToken, record =>
        {
            record.Kind = KafkaChangeKind.DecommissionBroker;
            record.Family = "broker-" + brokerId;
            record.BrokerId = brokerId;
            record.ReassignmentJson = json;
            record.SecondApproverRequired = true;
        });
    }

    public async Task<BrokerMovePlan> PlanRebalanceAsync(int brokerId, CancellationToken cancellationToken) =>
        BrokerMovePlanner.SpreadOnto(brokerId, await BrokerIdsAsync(cancellationToken), await FactsAsync(cancellationToken));

    public async Task<ChangeRequestRecord> CreateRebalanceAsync(ClaimsPrincipal user, int brokerId, string reason, string? correlationId, CancellationToken cancellationToken)
    {
        EnsureWritable();
        EnsureActor(user, KafkaChangeKind.Rebalance);
        var plan = await PlanRebalanceAsync(brokerId, cancellationToken);
        if (!plan.Accepted)
            throw new KafkaOpsRejectedException(string.Join(" ", plan.Errors));
        if (plan.Moves.Count == 0)
            throw new KafkaOpsRejectedException(plan.Summary);
        if (!_infra.Enabled)
            throw new KafkaOpsRejectedException(_infra.Detail);

        var json = ReassignmentJson.ForMoves(plan.Moves, null, brokerId);
        return await SaveNewAsync(user, reason, correlationId, true, plan.Summary, cancellationToken, record =>
        {
            record.Kind = KafkaChangeKind.Rebalance;
            record.Family = "broker-" + brokerId;
            record.BrokerId = brokerId;
            record.ReassignmentJson = json;
            record.SecondApproverRequired = true;
        });
    }

    public async Task<ChangeRequestRecord> CreateAddBrokerAsync(ClaimsPrincipal user, string reason, string? correlationId, CancellationToken cancellationToken)
    {
        EnsureWritable();
        EnsureActor(user, KafkaChangeKind.AddBroker);
        if (!_infra.Enabled)
            throw new KafkaOpsRejectedException(_infra.Detail);
        var cluster = await GetClusterAsync(cancellationToken);
        if (cluster.Error is not null)
            throw new KafkaOpsRejectedException(cluster.Error);

        return await SaveNewAsync(user, reason, correlationId, _options.Value.RequireSecondApprover,
            "Add one broker. The provider starts it, then this request waits until the broker registers and is up. Rebalancing onto it is a separate action.",
            cancellationToken, record =>
            {
                record.Kind = KafkaChangeKind.AddBroker;
                record.Family = "brokers";
                record.BeforeReplicas = cluster.BrokerCount;
                record.DesiredReplicas = cluster.BrokerCount + 1;
            });
    }

    public async Task<ChangeRequestRecord> CancelAsync(ClaimsPrincipal user, Guid id, CancellationToken cancellationToken)
    {
        EnsureWritable();
        var record = await RequireAsync(id, cancellationToken);
        EnsureActor(user, record.Kind);
        if (record.Kind is not (KafkaChangeKind.DecommissionBroker or KafkaChangeKind.Rebalance))
            throw new KafkaOpsRejectedException("Only an in-flight reassignment can be cancelled.");
        if (record.Status is not (KafkaChangeStatus.Executing or KafkaChangeStatus.Converging))
            throw new KafkaOpsRejectedException("This request is not in flight.");

        await _infra.CancelReassignmentAsync(cancellationToken);
        record.Status = KafkaChangeStatus.Cancelled;
        record.Failure = "Cancelled by " + UserName(user);
        record.ClosedUtc = DateTimeOffset.UtcNow;
        record.Progress = "Reassignment cancelled.";
        await SaveAsync(record, cancellationToken);
        await AuditAsync(record, "cancelled", cancellationToken);
        return record;
    }

    private async Task<ChangeRequestRecord> ExecuteInfraAsync(ClaimsPrincipal user, ChangeRequestRecord record, CancellationToken cancellationToken)
    {
        EnsureActor(user, record.Kind);
        var error = ChangeRequestWorkflow.MarkExecuting(record, UserName(user), DateTimeOffset.UtcNow);
        if (error is not null)
            throw new KafkaOpsRejectedException(error);
        if (!_infra.Enabled)
            throw new KafkaOpsRejectedException(_infra.Detail);

        try
        {
            switch (record.Kind)
            {
                case KafkaChangeKind.ScaleReplicas:
                    var scale = await PlanScaleAsync(record.GroupId, record.DesiredReplicas, cancellationToken);
                    if (!scale.Accepted)
                        throw new KafkaOpsRejectedException(string.Join(" ", scale.Errors));
                    await _infra.ScaleGroupAsync(record.GroupId, record.DesiredReplicas, cancellationToken);
                    record.Progress = "Scale requested. Waiting for the group to become stable.";
                    break;
                case KafkaChangeKind.AddBroker:
                    await _infra.AddBrokerAsync(cancellationToken);
                    record.Progress = "Broker start requested. Waiting for it to register.";
                    break;
                case KafkaChangeKind.DecommissionBroker:
                    var leaving = await PlanDecommissionAsync(record.BrokerId, cancellationToken);
                    if (!leaving.Accepted)
                        throw new KafkaOpsRejectedException(string.Join(" ", leaving.Errors));
                    record.ReassignmentJson = ReassignmentJson.ForMoves(leaving.Moves, record.BrokerId, null);
                    if (!leaving.AlreadyEmpty)
                        await _infra.ApplyReassignmentAsync(record.ReassignmentJson, cancellationToken);
                    record.Progress = leaving.AlreadyEmpty
                        ? "Broker is already empty. Waiting for a green cluster before it is stopped."
                        : "Replicas are moving off the broker.";
                    break;
                case KafkaChangeKind.Rebalance:
                    var spread = await PlanRebalanceAsync(record.BrokerId, cancellationToken);
                    if (!spread.Accepted || spread.Moves.Count == 0)
                        throw new KafkaOpsRejectedException(spread.Summary);
                    record.ReassignmentJson = ReassignmentJson.ForMoves(spread.Moves, null, record.BrokerId);
                    await _infra.ApplyReassignmentAsync(record.ReassignmentJson, cancellationToken);
                    record.Progress = "Replicas are moving onto the broker.";
                    break;
                default:
                    throw new KafkaOpsRejectedException("This change has no infrastructure action.");
            }
        }
        catch (KafkaOpsRejectedException)
        {
            throw;
        }
        catch (Exception ex)
        {
            record.Status = KafkaChangeStatus.Failed;
            record.Failure = ex.Message;
            record.ClosedUtc = DateTimeOffset.UtcNow;
            await SaveAsync(record, cancellationToken);
            await AuditAsync(record, "execution-failed", cancellationToken);
            throw new KafkaOpsRejectedException(ex.Message);
        }

        record.Status = KafkaChangeStatus.Converging;
        await SaveAsync(record, cancellationToken);
        await AuditAsync(record, "executed", cancellationToken);
        return record;
    }

    private async Task TrackInfraAsync(ChangeRequestRecord record, CancellationToken cancellationToken)
    {
        var timedOut = record.ExecutedUtc is { } executed
            && DateTimeOffset.UtcNow - executed > TimeSpan.FromSeconds(Math.Max(30, _options.Value.ScaleTimeoutSeconds));
        try
        {
            var done = record.Kind switch
            {
                KafkaChangeKind.ScaleReplicas => await ScaleSettledAsync(record, cancellationToken),
                KafkaChangeKind.AddBroker => await BrokerAddedAsync(record, cancellationToken),
                KafkaChangeKind.DecommissionBroker => await DecommissionSettledAsync(record, cancellationToken),
                KafkaChangeKind.Rebalance => await RebalanceSettledAsync(record, cancellationToken),
                _ => false
            };
            if (done)
            {
                record.Status = KafkaChangeStatus.Done;
                record.ClosedUtc = DateTimeOffset.UtcNow;
                record.ConvergedUtc = DateTimeOffset.UtcNow;
                await SaveAsync(record, cancellationToken);
                await AuditAsync(record, "converged", cancellationToken);
                return;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Infrastructure poll failed for {Kind}", record.Kind);
            return;
        }

        if (!timedOut)
        {
            await SaveAsync(record, cancellationToken);
            return;
        }

        record.Status = KafkaChangeStatus.NeedsAttention;
        record.ClosedUtc = DateTimeOffset.UtcNow;
        record.Failure = "The change did not settle before the timeout. The broker was not removed.";
        await SaveAsync(record, cancellationToken);
        await AuditAsync(record, "needs-attention", cancellationToken);
    }

    private async Task<bool> ScaleSettledAsync(ChangeRequestRecord record, CancellationToken cancellationToken)
    {
        var groups = await _broker.DescribeGroupsAsync(false, _options.Value.ExpectedConfigVersion, cancellationToken);
        var group = groups.FirstOrDefault(candidate => candidate.GroupId == record.GroupId);
        var stable = group is not null
            && group.State.Equals("Stable", StringComparison.OrdinalIgnoreCase)
            && group.Members.Count == record.DesiredReplicas
            && group.UnownedPartitions.Count == 0;
        record.Progress = stable
            ? "Group is stable with the requested members."
            : "Waiting for Stable with " + record.DesiredReplicas + " members. Current state: " + (group?.State ?? "missing") + ", members " + (group?.Members.Count ?? 0) + ".";
        return stable;
    }

    private async Task<bool> BrokerAddedAsync(ChangeRequestRecord record, CancellationToken cancellationToken)
    {
        var cluster = await _broker.DescribeClusterAsync(cancellationToken);
        var up = cluster.Brokers.Count(broker => broker.State == "up");
        var ready = up >= record.DesiredReplicas && cluster.OfflinePartitions == 0;
        record.Progress = ready
            ? "The new broker is registered."
            : "Waiting for " + record.DesiredReplicas + " brokers. " + up + " are up.";
        return ready;
    }

    private async Task<bool> DecommissionSettledAsync(ChangeRequestRecord record, CancellationToken cancellationToken)
    {
        var cluster = await _broker.DescribeClusterAsync(cancellationToken);
        var held = cluster.Placements.Any(placement => placement.Replicas.Contains(record.BrokerId) || placement.Leader == record.BrokerId);
        var green = cluster.UnderReplicatedPartitions == 0 && cluster.OfflinePartitions == 0;
        if (held || !green)
        {
            record.Progress = held
                ? "Replicas are still on broker " + record.BrokerId + "."
                : "The cluster is not green yet.";
            return false;
        }

        await _infra.RemoveBrokerAsync(record.BrokerId, cancellationToken);
        record.Progress = "Broker " + record.BrokerId + " was empty and the cluster was green, so the provider stopped it.";
        return true;
    }

    private async Task<bool> RebalanceSettledAsync(ChangeRequestRecord record, CancellationToken cancellationToken)
    {
        var cluster = await _broker.DescribeClusterAsync(cancellationToken);
        var moves = ReadMoves(record.ReassignmentJson);
        var settled = moves.All(move => cluster.Placements.Any(placement =>
            placement.Topic == move.Topic
            && placement.Partition == move.Partition
            && move.Replicas.All(replica => placement.Replicas.Contains(replica))));
        record.Progress = settled ? "The reassignment matches the live replicas." : "Waiting for the reassignment to finish.";
        return settled && cluster.UnderReplicatedPartitions == 0 && cluster.OfflinePartitions == 0;
    }

    private async Task<List<int>> BrokerIdsAsync(CancellationToken cancellationToken)
    {
        var cluster = await GetClusterAsync(cancellationToken);
        if (cluster.Error is not null)
            throw new KafkaOpsRejectedException(cluster.Error);
        return cluster.Brokers.Select(broker => broker.Id).ToList();
    }

    private async Task<List<BrokerPartitionFact>> FactsAsync(CancellationToken cancellationToken)
    {
        var cluster = await GetClusterAsync(cancellationToken);
        var topics = await GetTopicsAsync(cancellationToken);
        return cluster.Placements.Select(placement =>
        {
            var row = topics.Topics.FirstOrDefault(topic => topic.Topic == placement.Topic);
            var min = 1;
            if (row?.Configs is not null && row.Configs.TryGetValue("min.insync.replicas", out var text) && int.TryParse(text, out var parsed) && parsed > 0)
                min = parsed;
            return new BrokerPartitionFact
            {
                Topic = placement.Topic,
                Partition = placement.Partition,
                Leader = placement.Leader,
                Replicas = placement.Replicas,
                MinInSyncReplicas = min
            };
        }).ToList();
    }

    private async Task<ChangeRequestRecord> SaveNewAsync(
        ClaimsPrincipal user,
        string reason,
        string? correlationId,
        bool secondApprover,
        string summary,
        CancellationToken cancellationToken,
        Action<ChangeRequestRecord> fill)
    {
        if (string.IsNullOrWhiteSpace(reason))
            throw new KafkaOpsRejectedException("A reason is required.");
        var open = await OpenRecordsAsync(cancellationToken);
        if (open.Any(Open))
            throw new KafkaOpsRejectedException("Another change is already in flight in this environment.");

        var now = DateTimeOffset.UtcNow;
        var record = new ChangeRequestRecord
        {
            Id = Guid.NewGuid(),
            Environment = _environment.EnvironmentName,
            Reason = reason.Trim(),
            Requester = UserName(user),
            DryRunSummary = summary,
            CorrelationId = string.IsNullOrWhiteSpace(correlationId) ? Guid.NewGuid().ToString("N") : correlationId.Trim(),
            SecondApproverRequired = secondApprover,
            Status = secondApprover ? KafkaChangeStatus.Pending : KafkaChangeStatus.Approved,
            CreatedUtc = now,
            ApprovedUtc = secondApprover ? null : now
        };
        fill(record);
        if (!record.SecondApproverRequired)
            record.Approver = record.Requester;
        await SaveAsync(record, cancellationToken);
        await AuditAsync(record, "requested", cancellationToken);
        return record;
    }

    private void EnsureActor(ClaimsPrincipal user, KafkaChangeKind kind)
    {
        var allowed = kind == KafkaChangeKind.PartitionIncrease ? CanManage(user) : CanScale(user);
        if (!allowed)
            throw new KafkaOpsRejectedException("You are not allowed to run this change.");
    }

    private static List<ReplicaMove> ReadMoves(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return [];
        using var document = JsonDocument.Parse(json);
        if (!document.RootElement.TryGetProperty("partitions", out var partitions))
            return [];
        var moves = new List<ReplicaMove>();
        foreach (var partition in partitions.EnumerateArray())
        {
            moves.Add(new ReplicaMove
            {
                Topic = partition.GetProperty("topic").GetString() ?? "",
                Partition = partition.GetProperty("partition").GetInt32(),
                Replicas = partition.GetProperty("replicas").EnumerateArray().Select(item => item.GetInt32()).ToList()
            });
        }

        return moves;
    }
}
