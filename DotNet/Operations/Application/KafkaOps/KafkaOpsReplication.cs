using System.Security.Claims;
using LantanaGroup.Link.Shared.Application.Services.Security;

namespace LantanaGroup.Link.Operations.Application.KafkaOps;

public sealed partial class KafkaOpsService
{
    public async Task<ReplicationFactorPlan> PlanReplicationFactorAsync(string topic, int replicationFactor, long throttleBytesPerSecond, string? search, int page, int pageSize, CancellationToken cancellationToken)
    {
        var evaluation = await EvaluateReplicationAsync(topic, replicationFactor, throttleBytesPerSecond, search, page, pageSize, cancellationToken);
        return evaluation.Plan;
    }

    public async Task<ChangeRequestRecord> CreateReplicationFactorAsync(ClaimsPrincipal user, string topic, int replicationFactor, long throttleBytesPerSecond, string reason, string? confirmation, string? correlationId, CancellationToken cancellationToken)
    {
        EnsureWritable();
        EnsureActor(user, KafkaChangeKind.ReplicationFactor);
        var typed = (topic ?? "").Trim();
        if (!string.Equals((confirmation ?? "").Trim(), typed, StringComparison.Ordinal))
            throw new KafkaOpsRejectedException("Type the topic name to confirm. The replication factor changes only after the request.");
        if (string.IsNullOrWhiteSpace(reason))
            throw new KafkaOpsRejectedException("A reason is required.");
        if (reason.Trim().Length > KafkaProduceGuard.MaxReason)
            throw new KafkaOpsRejectedException(KafkaProduceGuard.ReasonLengthSentence);

        var evaluation = await EvaluateReplicationAsync(typed, replicationFactor, throttleBytesPerSecond, "", 1, 25, cancellationToken);
        if (!evaluation.Plan.Accepted)
            throw new KafkaOpsRejectedException(evaluation.Plan.Summary);
        await EnsureNoReassignmentAsync(null, cancellationToken);
        var original = await OriginalAssignmentAsync(evaluation.Plan.Topic, cancellationToken);

        // A replication-factor change moves replicas across brokers, so it waits for approval like every other reassignment.
        return await SaveNewAsync(user, reason, correlationId, _options.Value.SecondApprover, evaluation.Plan.Summary, cancellationToken, filled =>
        {
            filled.Kind = KafkaChangeKind.ReplicationFactor;
            filled.Topic = evaluation.Plan.Topic;
            filled.Family = evaluation.Plan.Topic;
            filled.BeforeReplicationFactor = evaluation.Plan.CurrentFactor;
            filled.TargetReplicationFactor = evaluation.Plan.TargetFactor;
            filled.ThrottleBytesPerSecond = evaluation.Plan.ThrottleBytesPerSecond;
            filled.BeforePartitions = evaluation.Plan.PartitionCount;
            filled.RequestedPartitions = evaluation.Plan.PartitionCount;
            filled.ReassignmentJson = ReplicationFactorPlanner.AssignmentJson(evaluation.Plan.Topic, evaluation.Assignments);
            filled.RebalanceName = KafkaRebalanceNames.For(filled.Id);
            filled.OriginalAssignmentJson = original;
        });
    }

    private async Task<ChangeRequestRecord> ExecuteReplicationFactorAsync(ClaimsPrincipal user, ChangeRequestRecord record, CancellationToken cancellationToken)
    {
        EnsureActor(user, record.Kind);
        await EnsureNoReassignmentAsync(record.Id, cancellationToken);
        var error = ChangeRequestWorkflow.MarkExecuting(record, UserName(user), DateTimeOffset.UtcNow, _options.Value.DifferentApprover);
        if (error is not null)
            throw new KafkaOpsRejectedException(error);
        // Saved before Kafka is touched: a second replica executing the same request loses the version check here.
        await SaveAsync(record, cancellationToken);

        try
        {
            // The cluster can change between approval and execution, so the plan is evaluated again before anything is submitted.
            var evaluation = await EvaluateReplicationAsync(record.Topic, record.TargetReplicationFactor, record.ThrottleBytesPerSecond, "", 1, 25, cancellationToken);
            if (!evaluation.Plan.Accepted)
                throw new KafkaOpsRejectedException(evaluation.Plan.Summary);
            record.ReassignmentJson = ReplicationFactorPlanner.AssignmentJson(evaluation.Plan.Topic, evaluation.Assignments);
            record.OriginalAssignmentJson = await OriginalAssignmentAsync(evaluation.Plan.Topic, cancellationToken);
            record.RequestedPartitions = evaluation.Plan.PartitionCount;
            record.Steps.Add("Throttle set to " + ReplicationFactorPlanner.ThrottleText(evaluation.Plan.ThrottleBytesPerSecond) + ".");
            record.Steps.Add("Reassignment submitted for " + evaluation.Plan.PartitionCount + " partitions.");
            record.Progress = "Reassignment submitted. Waiting until every replica is in the ISR.";
            await SaveAsync(record, cancellationToken);
            record.SubmittedPartitions = await _mover.SubmitAsync(record.ReassignmentJson, 0, record.ThrottleBytesPerSecond, cancellationToken);
        }
        catch (Exception ex)
        {
            var detail = ex is KafkaOpsRejectedException ? ex.Message : "The reassignment was not submitted. " + ex.Message;
            await PersistFailedAsync(record, detail, cancellationToken);
            throw new KafkaOpsRejectedException(detail);
        }

        record.Status = KafkaChangeStatus.Converging;
        await SaveAsync(record, cancellationToken);
        await AuditAsync(record, "executed", cancellationToken);
        return record;
    }

    // Kept so an in-flight change can be cancelled and checked against where the replicas started.
    private async Task<string> OriginalAssignmentAsync(string topic, CancellationToken cancellationToken) =>
        System.Text.Json.JsonSerializer.Serialize(new
        {
            partitions = (await FactsAsync(cancellationToken))
                .Where(fact => string.Equals(fact.Topic, topic, StringComparison.Ordinal))
                .Select(fact => new { topic = fact.Topic, partition = fact.Partition, replicas = fact.Replicas })
        });

    public async Task<ChangeRequestRecord> ProduceMessageAsync(ClaimsPrincipal user, string topic, string? headers, string? key, string? value, string reason, string? confirmation, string? correlationId, CancellationToken cancellationToken)
    {
        EnsureWritable();
        EnsureActor(user, KafkaChangeKind.Produce);
        var typed = (topic ?? "").Trim();
        if (!string.Equals((confirmation ?? "").Trim(), typed, StringComparison.Ordinal))
            throw new KafkaOpsRejectedException(KafkaProduceGuard.ConfirmSentence);
        if (string.IsNullOrWhiteSpace(reason))
            throw new KafkaOpsRejectedException(KafkaProduceGuard.ReasonSentence);
        if (reason.Trim().Length > KafkaProduceGuard.MaxReason)
            throw new KafkaOpsRejectedException(KafkaProduceGuard.ReasonLengthSentence);

        var review = KafkaProduceGuard.Evaluate(typed, headers, key, value);
        if (!review.Accepted)
            throw new KafkaOpsRejectedException(review.Error);

        try
        {
            await _broker.ProduceRecordAsync(review.Topic, review.Key, review.Value, review.Headers, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            var detail = ex is KafkaOpsRejectedException ? ex.Message : "The message was not produced. " + ex.Message;
            _logger.LogWarning(ex, "Kafka produce failed for {Topic}", review.Topic.SanitizeAndRemove());
            throw new KafkaOpsRejectedException(detail);
        }

        var now = DateTimeOffset.UtcNow;
        var record = await SaveNewAsync(user, reason, correlationId, false, review.Summary, cancellationToken, filled =>
        {
            filled.Kind = KafkaChangeKind.Produce;
            filled.Topic = review.Topic;
            filled.Family = review.Topic;
            filled.Status = KafkaChangeStatus.Done;
            filled.Progress = "One message was produced.";
            filled.ExecutedUtc = now;
            filled.ConvergedUtc = now;
            filled.ClosedUtc = now;
            filled.SecondApproverRequired = false;
        });
        return record;
    }

    private async Task<ReplicationFactorEvaluation> EvaluateReplicationAsync(string topic, int target, long throttle, string? search, int page, int pageSize, CancellationToken cancellationToken)
    {
        ClusterSnapshot cluster;
        try
        {
            cluster = await _broker.DescribeClusterAsync(cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            throw new KafkaOpsRejectedException("Kafka could not be read. " + ex.Message);
        }

        if (cluster.Error is not null)
            throw new KafkaOpsRejectedException(cluster.Error);

        var minIsr = 1;
        var configured = false;
        IReadOnlyList<TopicWatermark> described;
        try
        {
            described = await _broker.DescribeTopicsAsync([topic], probeAlter: false, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            throw new KafkaOpsRejectedException("Kafka could not be read. " + ex.Message);
        }

        var row = described.FirstOrDefault(item => string.Equals(item.Topic, topic, StringComparison.Ordinal));
        if (row?.Configs is not null && row.Configs.TryGetValue("min.insync.replicas", out var raw))
        {
            configured = true;
            if (!int.TryParse(raw, out var parsed) || parsed < 1)
            {
                var invalid = new ReplicationFactorPlan { Topic = topic ?? "", TargetFactor = target, ThrottleBytesPerSecond = throttle };
                invalid.Errors.Add("min.insync.replicas is not a number.");
                invalid.Summary = invalid.Errors[0];
                return new ReplicationFactorEvaluation { Plan = invalid };
            }

            minIsr = parsed;
        }

        return ReplicationFactorPlanner.Evaluate(
            topic,
            target,
            throttle,
            minIsr,
            configured,
            cluster.Brokers,
            cluster.Placements,
            search,
            page,
            pageSize);
    }

    private async Task<bool> ReplicationFactorSettledAsync(ChangeRequestRecord record, CancellationToken cancellationToken)
    {
        var cluster = await _broker.DescribeClusterAsync(cancellationToken);
        if (cluster.Error is not null)
            throw new KafkaOpsRejectedException(cluster.Error);

        var moves = ReadMoves(record.ReassignmentJson);
        var settled = moves.Count > 0 && moves.All(move =>
        {
            var placement = cluster.Placements.FirstOrDefault(item =>
                string.Equals(item.Topic, move.Topic, StringComparison.Ordinal) && item.Partition == move.Partition);
            if (placement is null)
                return false;
            var replicas = placement.Replicas.ToHashSet();
            var isr = placement.Isr.ToHashSet();
            var target = move.Replicas.ToHashSet();
            return replicas.SetEquals(target) && target.All(isr.Contains);
        });
        record.Progress = settled
            ? "Every replica is in the ISR."
            : "Waiting until every replica is in the ISR.";
        return settled;
    }

    private async Task<bool> ClearReplicationThrottleAsync(ChangeRequestRecord record, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(record.RebalanceName))
        {
            record.Progress = "The replicas are in the ISR. This request has no reassignment to verify.";
            record.Failure = record.Progress;
            await SaveAsync(record, cancellationToken);
            return false;
        }

        try
        {
            await _mover.ClearThrottleAsync(record.ReassignmentJson, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            record.Progress = "The replicas are in the ISR. The throttle was not cleared. " + ex.Message;
            record.Failure = record.Progress;
            await SaveAsync(record, cancellationToken);
            return false;
        }

        record.ThrottleCleared = true;
        AddStep(record, "Every replica is in the ISR.");
        AddStep(record, "Throttle cleared.");
        record.Progress = "Throttle cleared. Every replica is in the ISR.";
        record.Failure = "";
        return true;
    }

    private static void AddStep(ChangeRequestRecord record, string step)
    {
        if (!record.Steps.Contains(step, StringComparer.Ordinal))
            record.Steps.Add(step);
    }
}
