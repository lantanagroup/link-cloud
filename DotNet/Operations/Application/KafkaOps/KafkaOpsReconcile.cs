using LantanaGroup.Link.Shared.Application.Models.Kafka;
namespace LantanaGroup.Link.Operations.Application.KafkaOps;

public sealed partial class KafkaOpsService
{
    public const string ReconcileStoppedDetail = "The Operations service stopped while this request was executing. Check the cluster before you request it again.";

    /// <summary>
    /// A request left in Executing by a replica that stopped. Only the executor that holds the lease runs this,
    /// and only after the reconcile window, so a request that is executing right now on another replica is left alone.
    /// </summary>
    private async Task ReconcileExecutingAsync(ChangeRequestRecord record, CancellationToken cancellationToken)
    {
        var last = record.UpdatedUtc ?? record.ExecutedUtc ?? record.CreatedUtc;
        var window = TimeSpan.FromSeconds(Math.Max(30, _options.Value.ReconcileExecutingAfterSeconds));
        if (DateTimeOffset.UtcNow - last < window)
            return;

        switch (record.Kind)
        {
            case KafkaChangeKind.Rebalance or KafkaChangeKind.DecommissionBroker or KafkaChangeKind.ReplicationFactor
                when record.ReassignmentJson.Length > 0:
                // Resubmitting a wave with the same target replicas is safe in Kafka, so tracking resumes from the last saved wave.
                record.SubmittedPartitions ??= 0;
                record.Status = KafkaChangeStatus.Converging;
                record.Progress = "Resumed after the Operations service restarted.";
                AddStep(record, "Resumed after a restart.");
                await SaveAsync(record, cancellationToken);
                await AuditAsync(record, "resumed", cancellationToken);
                return;

            case KafkaChangeKind.PartitionIncrease or KafkaChangeKind.CompleteTopicFamily:
                await ReconcilePartitionIncreaseAsync(record, cancellationToken);
                return;

            default:
                await PersistFailedAsync(record, ReconcileStoppedDetail, cancellationToken);
                return;
        }
    }

    private async Task ReconcilePartitionIncreaseAsync(ChangeRequestRecord record, CancellationToken cancellationToken)
    {
        var names = new List<string> { record.Topic };
        if (record.RetryTopic.Length > 0)
            names.Add(record.RetryTopic);
        names.Add(KafkaTopicCatalog.ErrorName(record.Topic));

        IReadOnlyList<TopicWatermark> described;
        try
        {
            described = await _broker.DescribeTopicsAsync(names, probeAlter: false, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Kafka is not readable yet. The next poll tries again.
            _logger.LogWarning(ex, "Reconcile could not read Kafka for change {Id}", record.Id);
            return;
        }

        var main = described.FirstOrDefault(item => string.Equals(item.Topic, record.Topic, StringComparison.Ordinal))?.Partitions ?? 0;
        var reached = record.Kind == KafkaChangeKind.CompleteTopicFamily
            ? names.Skip(1).All(name => (described.FirstOrDefault(item => string.Equals(item.Topic, name, StringComparison.Ordinal))?.Partitions ?? 0) >= record.RequestedPartitions)
            : main >= record.RequestedPartitions;

        if (!reached)
        {
            await PersistFailedAsync(record, ReconcileStoppedDetail + " " + record.Topic + " has " + main + " partitions, not " + record.RequestedPartitions + ".", cancellationToken);
            return;
        }

        record.PartitionsChangedUtc ??= DateTimeOffset.UtcNow;
        record.MaxReplicas = record.RequestedPartitions;
        record.Status = KafkaChangeStatus.Converging;
        record.Progress = "Resumed after the Operations service restarted. Kafka shows " + record.RequestedPartitions + " partitions.";
        AddStep(record, "Resumed after a restart.");
        await SaveAsync(record, cancellationToken);
        await AuditAsync(record, "resumed", cancellationToken);
    }
}
