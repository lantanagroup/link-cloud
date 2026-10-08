using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using Confluent.Kafka;
using LantanaGroup.Link.Shared.Application.Extensions;
using Microsoft.Extensions.Logging;

namespace LantanaGroup.Link.Shared.Application.Models.Kafka;

/// <summary>
/// Remembers offsets this process has finished and commits only those offsets for the
/// partitions named in a cooperative revoke or a lost-partition callback.
/// </summary>
public sealed class KafkaAssignmentTracker
{
    private readonly ConcurrentDictionary<TopicPartition, Offset> _processed = new();

    public void MarkProcessed<TKey, TValue>(ConsumeResult<TKey, TValue> result)
    {
        ArgumentNullException.ThrowIfNull(result);
        _processed[result.TopicPartition] = new Offset(result.Offset.Value + 1);
    }

    /// <summary>
    /// Stores the offset a listener just committed. A later revoke commits this value
    /// when it is ahead of the last processed offset, and never an older one.
    /// </summary>
    public void RememberCommitted(TopicPartitionOffset committed)
    {
        if (committed.Offset.IsSpecial)
        {
            return;
        }

        _processed.AddOrUpdate(
            committed.TopicPartition,
            committed.Offset,
            (_, existing) => !existing.IsSpecial && existing.Value >= committed.Offset.Value
                ? existing
                : committed.Offset);
    }

    public void OnRevoked<TKey, TValue>(IConsumer<TKey, TValue> consumer, IReadOnlyList<TopicPartitionOffset> revoked, ILogger? logger = null)
    {
        CommitNamed(consumer, revoked, logger);
    }

    public static IReadOnlyList<TopicPartitionOffset> OffsetsForRevoked(
        IEnumerable<TopicPartitionOffset> revoked,
        IReadOnlyDictionary<TopicPartition, Offset> processed)
    {
        var batch = new List<TopicPartitionOffset>();
        foreach (var partition in revoked)
        {
            if (processed.TryGetValue(partition.TopicPartition, out var offset))
            {
                batch.Add(new TopicPartitionOffset(partition.TopicPartition, offset));
            }
        }

        return batch;
    }

    private void CommitNamed<TKey, TValue>(IConsumer<TKey, TValue> consumer, IReadOnlyList<TopicPartitionOffset> partitions, ILogger? logger)
    {
        var snapshot = new Dictionary<TopicPartition, Offset>(_processed);
        var batch = OffsetsForRevoked(partitions, snapshot);
        if (batch.Count > 0)
        {
            consumer.SafeCommit(batch, logger);
        }

        foreach (var partition in partitions)
        {
            _processed.TryRemove(partition.TopicPartition, out _);
        }
    }
}

internal static class KafkaAssignmentRegistry
{
    private static readonly ConditionalWeakTable<object, KafkaAssignmentTracker> Trackers = new();

    public static void Register(object consumer, KafkaAssignmentTracker tracker)
    {
        Trackers.Add(consumer, tracker);
    }

    public static void Remember(object consumer, IEnumerable<TopicPartitionOffset> offsets)
    {
        if (!Trackers.TryGetValue(consumer, out var tracker))
        {
            return;
        }

        foreach (var offset in offsets)
        {
            tracker.RememberCommitted(offset);
        }
    }
}
