using System.Security.Claims;
using System.Text.Json;
using Confluent.Kafka;
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

    public Task<BrokerMovePlan> PlanDecommissionAsync(int brokerId, CancellationToken cancellationToken) =>
        PlanDecommissionAsync(brokerId, null, cancellationToken);

    private async Task<BrokerMovePlan> PlanDecommissionAsync(int brokerId, Guid? except, CancellationToken cancellationToken)
    {
        ClusterSnapshot cluster;
        try
        {
            cluster = await _broker.DescribeClusterAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            throw new KafkaOpsRejectedException("Kafka could not be read. " + ex.Message);
        }

        if (cluster.Error is not null)
            throw new KafkaOpsRejectedException(cluster.Error);
        if (!cluster.ControllerRolesKnown)
            throw new KafkaOpsRejectedException("Broker roles could not be read, so decommission is refused.");
        await EnsureNoReassignmentAsync(except, cancellationToken);
        return BrokerMovePlanner.Decommission(
            brokerId,
            cluster.Brokers.Select(broker => broker.Id).ToList(),
            await FactsAsync(cancellationToken),
            cluster.ControllerEligibleIds);
    }

    public async Task<ChangeRequestRecord> CreateDecommissionAsync(ClaimsPrincipal user, int brokerId, string reason, string? correlationId, CancellationToken cancellationToken)
    {
        EnsureWritable();
        EnsureActor(user, KafkaChangeKind.DecommissionBroker);
        await EnsureNoReassignmentAsync(null, cancellationToken);
        var plan = await PlanDecommissionAsync(brokerId, cancellationToken);
        if (!plan.Accepted)
            throw new KafkaOpsRejectedException(string.Join(" ", plan.Errors));
        if (!_infra.Enabled)
            throw new KafkaOpsRejectedException(_infra.Detail);

        var json = ReassignmentJson.ForMoves(plan.Moves, brokerId, null);
        var original = OriginalAssignment(await FactsAsync(cancellationToken), plan.Moves);
        return await SaveNewAsync(user, reason, correlationId, true, plan.Summary, cancellationToken, record =>
        {
            record.Kind = KafkaChangeKind.DecommissionBroker;
            record.Family = "broker-" + brokerId;
            record.BrokerId = brokerId;
            record.ReassignmentJson = json;
            record.OriginalAssignmentJson = original;
            record.RebalanceName = KafkaRebalanceNames.For(record.Id);
            record.SecondApproverRequired = true;
        });
    }

    public Task<BrokerMovePlan> PlanRebalanceAsync(int brokerId, CancellationToken cancellationToken) =>
        PlanRebalanceAsync(brokerId, null, cancellationToken);

    private async Task<BrokerMovePlan> PlanRebalanceAsync(int brokerId, Guid? except, CancellationToken cancellationToken)
    {
        await EnsureNoReassignmentAsync(except, cancellationToken);
        return BrokerMovePlanner.SpreadOnto(brokerId, await BrokerIdsAsync(cancellationToken), await FactsAsync(cancellationToken));
    }

    public async Task<ChangeRequestRecord> CreateRebalanceAsync(ClaimsPrincipal user, int brokerId, string reason, string? correlationId, CancellationToken cancellationToken)
    {
        EnsureWritable();
        EnsureActor(user, KafkaChangeKind.Rebalance);
        await EnsureNoReassignmentAsync(null, cancellationToken);
        var plan = await PlanRebalanceAsync(brokerId, cancellationToken);
        if (!plan.Accepted)
            throw new KafkaOpsRejectedException(string.Join(" ", plan.Errors));
        if (plan.Moves.Count == 0)
            throw new KafkaOpsRejectedException(plan.Summary);
        if (!_infra.Enabled)
            throw new KafkaOpsRejectedException(_infra.Detail);

        var json = ReassignmentJson.ForMoves(plan.Moves, null, brokerId);
        var original = OriginalAssignment(await FactsAsync(cancellationToken), plan.Moves);
        return await SaveNewAsync(user, reason, correlationId, true, plan.Summary, cancellationToken, record =>
        {
            record.Kind = KafkaChangeKind.Rebalance;
            record.Family = "broker-" + brokerId;
            record.BrokerId = brokerId;
            record.ReassignmentJson = json;
            record.OriginalAssignmentJson = original;
            record.RebalanceName = KafkaRebalanceNames.For(record.Id);
            record.SecondApproverRequired = true;
        });
    }

    public async Task<ChangeRequestRecord> CreateAddBrokerAsync(ClaimsPrincipal user, string reason, string? correlationId, CancellationToken cancellationToken)
    {
        EnsureWritable();
        EnsureActor(user, KafkaChangeKind.AddBroker);
        await EnsureNoReassignmentAsync(null, cancellationToken);
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
        var target = await RequireAsync(id, cancellationToken);
        EnsureActor(user, target.Kind);
        if (target.Kind is not (KafkaChangeKind.DecommissionBroker or KafkaChangeKind.Rebalance))
            throw new KafkaOpsRejectedException("Only an in-flight reassignment can be cancelled.");
        if (target.Status is not (KafkaChangeStatus.Executing or KafkaChangeStatus.Converging or KafkaChangeStatus.TimedOut))
            throw new KafkaOpsRejectedException("This request is not in flight.");
        if (string.IsNullOrWhiteSpace(target.RebalanceName))
            throw new KafkaOpsRejectedException("This request has no reassignment to cancel.");

        return await SaveNewAsync(user, "Cancel the in-flight reassignment.", null, true,
            "Cancel the reassignment for " + target.RebalanceName + " and check that replicas return to the original assignment.",
            cancellationToken, record =>
            {
                record.Kind = KafkaChangeKind.CancelReassignment;
                record.Family = target.Family;
                record.Topic = target.Topic;
                record.BrokerId = target.BrokerId;
                record.TargetRequestId = target.Id;
                record.RebalanceName = target.RebalanceName;
                record.OriginalAssignmentJson = target.OriginalAssignmentJson;
                record.ReassignmentJson = target.ReassignmentJson;
                record.SecondApproverRequired = true;
            },
            alongsideReassignment: true);
    }

    private async Task<ChangeRequestRecord> ExecuteCancelAsync(ClaimsPrincipal user, ChangeRequestRecord record, CancellationToken cancellationToken)
    {
        EnsureActor(user, record.Kind);
        var error = ChangeRequestWorkflow.MarkExecuting(record, UserName(user), DateTimeOffset.UtcNow);
        if (error is not null)
            throw new KafkaOpsRejectedException(error);

        var target = await RequireAsync(record.TargetRequestId, cancellationToken);
        try
        {
            if (!_infra.Enabled)
                throw new KafkaOpsRejectedException(_infra.Detail);
            await _infra.CancelReassignmentAsync(record.RebalanceName, cancellationToken);
            var cluster = await _broker.DescribeClusterAsync(cancellationToken);
            if (!ReplicasMatch(record.OriginalAssignmentJson, cluster))
                throw new KafkaOpsRejectedException("The replicas did not return to the original assignment.");

            var now = DateTimeOffset.UtcNow;
            record.Status = KafkaChangeStatus.Cancelled;
            record.Progress = "Replicas returned to the original assignment.";
            record.ClosedUtc = now;
            target.Status = KafkaChangeStatus.Cancelled;
            target.Progress = "Reassignment cancelled. Replicas returned to the original assignment.";
            target.ClosedUtc = now;
            target.Failure = "";
            await SaveAsync(record, cancellationToken);
            await SaveAsync(target, cancellationToken);
            await ReleaseRebalanceAsync(target, cancellationToken);
            await AuditAsync(record, "cancelled", cancellationToken);
            return record;
        }
        catch (Exception ex)
        {
            var reason = ex is KafkaOpsRejectedException ? ex.Message : "The cancel failed. " + ex.Message;
            await PersistFailedAsync(record, reason, cancellationToken);
            throw new KafkaOpsRejectedException(reason);
        }
    }

    private async Task<ChangeRequestRecord> ExecuteInfraAsync(ClaimsPrincipal user, ChangeRequestRecord record, CancellationToken cancellationToken)
    {
        EnsureActor(user, record.Kind);
        if (record.Kind != KafkaChangeKind.ScaleReplicas)
            await EnsureNoReassignmentAsync(record.Id, cancellationToken);
        var error = ChangeRequestWorkflow.MarkExecuting(record, UserName(user), DateTimeOffset.UtcNow);
        if (error is not null)
            throw new KafkaOpsRejectedException(error);

        try
        {
            if (!_infra.Enabled)
                throw new KafkaOpsRejectedException(_infra.Detail);

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
                    var leaving = await PlanDecommissionAsync(record.BrokerId, record.Id, cancellationToken);
                    if (!leaving.Accepted)
                        throw new KafkaOpsRejectedException(string.Join(" ", leaving.Errors));
                    record.ReassignmentJson = ReassignmentJson.ForMoves(leaving.Moves, record.BrokerId, null);
                    if (!leaving.AlreadyEmpty)
                        await _infra.ApplyReassignmentAsync(record.ReassignmentJson, record.RebalanceName, refresh: false, cancellationToken);
                    record.Progress = leaving.AlreadyEmpty
                        ? "Broker is already empty. Waiting for a green cluster before it is stopped."
                        : "Replicas are moving off the broker.";
                    break;
                case KafkaChangeKind.Rebalance:
                    var spread = await PlanRebalanceAsync(record.BrokerId, record.Id, cancellationToken);
                    if (!spread.Accepted || spread.Moves.Count == 0)
                        throw new KafkaOpsRejectedException(spread.Summary);
                    record.ReassignmentJson = ReassignmentJson.ForMoves(spread.Moves, null, record.BrokerId);
                    await _infra.ApplyReassignmentAsync(record.ReassignmentJson, record.RebalanceName, refresh: false, cancellationToken);
                    record.Progress = "Replicas are moving onto the broker.";
                    break;
                default:
                    throw new KafkaOpsRejectedException("This change has no infrastructure action.");
            }
        }
        catch (Exception ex)
        {
            await PersistFailedAsync(record, ex.Message, cancellationToken);
            if (ex is KafkaOpsRejectedException)
                throw;
            throw new KafkaOpsRejectedException(ex.Message);
        }

        record.Status = KafkaChangeStatus.Converging;
        await SaveAsync(record, cancellationToken);
        await AuditAsync(record, "executed", cancellationToken);
        return record;
    }

    private async Task TrackInfraAsync(ChangeRequestRecord record, CancellationToken cancellationToken)
    {
        var timedOut = InfraTimedOut(record);
        var alreadyTimedOut = record.Status == KafkaChangeStatus.TimedOut;
        if (record.NextPollUtc is { } next && DateTimeOffset.UtcNow < next && !timedOut && !alreadyTimedOut)
            return;

        bool done;
        try
        {
            done = record.Kind switch
            {
                KafkaChangeKind.ScaleReplicas => await ScaleSettledAsync(record, cancellationToken),
                KafkaChangeKind.AddBroker => await BrokerAddedAsync(record, cancellationToken),
                KafkaChangeKind.DecommissionBroker => await DecommissionSettledAsync(record, cancellationToken),
                KafkaChangeKind.Rebalance => await RebalanceSettledAsync(record, cancellationToken),
                _ => false
            };
            if (!done)
            {
                record.PollFailures = 0;
                record.NextPollUtc = null;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Infrastructure poll failed for {Kind}", record.Kind);
            if (alreadyTimedOut)
                return;
            if (timedOut)
            {
                await PersistTimedOutAsync(record, "The change did not settle before the timeout. The last poll failed. " + ex.Message, cancellationToken);
                return;
            }

            record.PollFailures++;
            var delaySeconds = Math.Min(30, 1 << Math.Min(record.PollFailures, 5));
            record.NextPollUtc = DateTimeOffset.UtcNow.AddSeconds(delaySeconds);
            record.Progress = "The last poll failed and will be retried. " + ex.Message;
            await SaveAsync(record, cancellationToken);
            return;
        }

        if (done)
        {
            await FinishMoveAsync(record, cancellationToken);
            record.Status = KafkaChangeStatus.Done;
            record.ClosedUtc = DateTimeOffset.UtcNow;
            record.ConvergedUtc = DateTimeOffset.UtcNow;
            record.PollFailures = 0;
            record.NextPollUtc = null;
            record.Failure = "";
            await SaveAsync(record, cancellationToken);
            await ReleaseRebalanceAsync(record, cancellationToken);
            await AuditAsync(record, "converged", cancellationToken);
            return;
        }

        if (alreadyTimedOut)
        {
            // Unknown stays timed out. Do not ask the provider here, and do not stop the broker.
            var listing = await _broker.ListInFlightReassignmentsAsync(cancellationToken);
            if (listing.Known && listing.Topics.Count == 0)
            {
                await PersistFailedAsync(record, "The reassignment ended before the replicas matched.", cancellationToken);
                return;
            }

            return;
        }

        if (!timedOut)
        {
            await SaveAsync(record, cancellationToken);
            return;
        }

        await PersistTimedOutAsync(record, TimedOutReason(record.Kind), cancellationToken);
    }

    private static string TimedOutReason(KafkaChangeKind kind) => kind switch
    {
        KafkaChangeKind.ScaleReplicas => "The change did not settle before the timeout. The group did not reach the requested members.",
        KafkaChangeKind.AddBroker => "The change did not settle before the timeout. The broker did not register.",
        KafkaChangeKind.DecommissionBroker => "The change did not settle before the timeout. The broker was not removed.",
        KafkaChangeKind.Rebalance => "The change did not settle before the timeout. The reassignment did not finish.",
        _ => "The change did not settle before the timeout."
    };

    private bool InfraTimedOut(ChangeRequestRecord record) =>
        record.ExecutedUtc is { } executed
        && DateTimeOffset.UtcNow - executed > TimeSpan.FromSeconds(Math.Max(30, _options.Value.ScaleTimeoutSeconds));

    private async Task PersistTimedOutAsync(ChangeRequestRecord record, string reason, CancellationToken cancellationToken)
    {
        record.Status = KafkaChangeStatus.TimedOut;
        record.ClosedUtc = DateTimeOffset.UtcNow;
        record.Failure = reason;
        await SaveAsync(record, cancellationToken);
        await AuditAsync(record, "timed-out", cancellationToken);
    }

    private async Task<bool> ScaleSettledAsync(ChangeRequestRecord record, CancellationToken cancellationToken)
    {
        var groups = await _broker.DescribeGroupsAsync(false, _options.Value.ExpectedConfigVersion, cancellationToken);
        var group = groups.FirstOrDefault(candidate => candidate.GroupId == record.GroupId);
        if (record.DesiredReplicas == 0)
        {
            var drained = group is null || group.Members.Count == 0;
            record.Progress = drained
                ? "Group has no members."
                : "Waiting for 0 members. Current state: " + group!.State + ", members " + group.Members.Count + ".";
            return drained;
        }

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

        if (record.Status == KafkaChangeStatus.TimedOut)
        {
            record.Progress = "The reassignment finished after the timeout. The broker was not stopped.";
            return true;
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
        Action<ChangeRequestRecord> fill,
        bool alongsideReassignment = false)
    {
        if (string.IsNullOrWhiteSpace(reason))
            throw new KafkaOpsRejectedException("A reason is required.");
        var open = await OpenRecordsAsync(cancellationToken);
        if (alongsideReassignment)
        {
            if (open.Any(record => Open(record) && record.Kind == KafkaChangeKind.CancelReassignment))
                throw new KafkaOpsRejectedException("A cancel request is already in flight.");
        }
        else if (open.Any(Open))
        {
            throw new KafkaOpsRejectedException("Another change is already in flight in this environment.");
        }

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
        var allowed = kind is KafkaChangeKind.PartitionIncrease or KafkaChangeKind.CompleteTopicFamily ? CanManage(user) : CanScale(user);
        if (!allowed)
            throw new KafkaOpsForbiddenException("You are not allowed to run this change.");
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

    private async Task EnsureNoReassignmentAsync(Guid? except, CancellationToken cancellationToken)
    {
        var topics = await InFlightReassignmentTopicsAsync(except, cancellationToken);
        if (topics.Count > 0)
            throw new KafkaOpsRejectedException("A partition reassignment is already in flight for " + string.Join(", ", topics) + ".");
    }

    private async Task<ReassignmentListing> LiveReassignmentsAsync(CancellationToken cancellationToken)
    {
        try
        {
            var listing = await _broker.ListInFlightReassignmentsAsync(cancellationToken);
            if (listing.Known)
                return listing;
            return await _infra.ListInFlightReassignmentsAsync(cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return new ReassignmentListing();
        }
    }

    private async Task<IReadOnlyList<string>> InFlightReassignmentTopicsAsync(Guid? except, CancellationToken cancellationToken)
    {
        var names = new List<string>();
        var listing = await LiveReassignmentsAsync(cancellationToken);
        if (!listing.Known)
            throw new KafkaOpsRejectedException("Cannot confirm no reassignment is in flight.");

        foreach (var topic in listing.Topics)
        {
            if (!string.IsNullOrWhiteSpace(topic) && !names.Contains(topic, StringComparer.Ordinal))
                names.Add(topic);
        }

        foreach (var record in await OpenRecordsAsync(cancellationToken))
        {
            if (except is Guid id && record.Id == id)
                continue;
            if (!BlocksForReassignment(record))
                continue;
            foreach (var topic in TopicsOf(record))
            {
                if (!names.Contains(topic, StringComparer.Ordinal))
                    names.Add(topic);
            }
        }

        return names;
    }

    private static bool OwnsRebalanceResource(ChangeRequestRecord record) =>
        record.Kind is KafkaChangeKind.DecommissionBroker or KafkaChangeKind.Rebalance;

    private static bool BlocksForReassignment(ChangeRequestRecord record) =>
        OwnsRebalanceResource(record)
        && record.Status is KafkaChangeStatus.Executing or KafkaChangeStatus.Converging or KafkaChangeStatus.TimedOut;

    private static IEnumerable<string> TopicsOf(ChangeRequestRecord record)
    {
        var moves = ReadMoves(record.ReassignmentJson);
        if (moves.Count > 0)
            return moves.Select(move => move.Topic).Where(topic => topic.Length > 0).Distinct(StringComparer.Ordinal);
        if (!string.IsNullOrWhiteSpace(record.Topic))
            return [record.Topic];
        if (!string.IsNullOrWhiteSpace(record.Family))
            return [record.Family];
        return ["broker-" + record.BrokerId];
    }

    private static string OriginalAssignment(IEnumerable<BrokerPartitionFact> facts, IEnumerable<ReplicaMove> moves)
    {
        var keys = moves.Select(move => move.Topic + "\n" + move.Partition).ToHashSet(StringComparer.Ordinal);
        var partitions = facts
            .Where(fact => keys.Contains(fact.Topic + "\n" + fact.Partition))
            .Select(fact => new { topic = fact.Topic, partition = fact.Partition, replicas = fact.Replicas });
        return JsonSerializer.Serialize(new { partitions });
    }

    private static bool ReplicasMatch(string originalJson, ClusterSnapshot cluster)
    {
        var expected = ReadMoves(originalJson);
        if (expected.Count == 0)
            return false;
        return expected.All(move => cluster.Placements.Any(placement =>
            placement.Topic == move.Topic
            && placement.Partition == move.Partition
            && move.Replicas.SequenceEqual(placement.Replicas)));
    }

    private async Task FinishMoveAsync(ChangeRequestRecord record, CancellationToken cancellationToken)
    {
        if (record.Kind is not (KafkaChangeKind.Rebalance or KafkaChangeKind.DecommissionBroker or KafkaChangeKind.AddBroker))
            return;

        var partitions = ReadMoves(record.ReassignmentJson)
            .Where(move => move.Topic.Length > 0)
            .Select(move => new TopicPartition(move.Topic, move.Partition))
            .ToList();
        if (_infra.Name.Equals("Strimzi", StringComparison.OrdinalIgnoreCase))
        {
            record.Progress = AppendProgress(record.Progress, "Preferred leader election was skipped. The platform moves leadership.");
            return;
        }

        if (!ElectsOnAdminPath(_infra.Name))
            return;
        if (partitions.Count == 0)
        {
            record.Progress = AppendProgress(record.Progress, "Preferred leader election was skipped because this request did not move partitions.");
            return;
        }

        try
        {
            await _broker.ElectPreferredLeadersAsync(partitions, cancellationToken);
            record.Progress = AppendProgress(record.Progress, "Preferred leaders were elected for the moved partitions.");
        }
        catch (Exception ex)
        {
            record.Warning = "Preferred leader election did not finish. " + ex.Message;
        }
    }

    private static bool ElectsOnAdminPath(string name) =>
        name.Equals("Disabled", StringComparison.OrdinalIgnoreCase)
        || name.Equals("LocalCompose", StringComparison.OrdinalIgnoreCase);

    private async Task ReleaseRebalanceAsync(ChangeRequestRecord record, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(record.RebalanceName))
            return;
        try
        {
            await _infra.ReleaseRebalanceAsync(record.RebalanceName, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Rebalance resource was not deleted");
        }
    }

    private static string AppendProgress(string progress, string note) =>
        string.IsNullOrWhiteSpace(progress) ? note : progress.TrimEnd() + " " + note;
}
