using Confluent.Kafka;
using Microsoft.Extensions.Logging;

namespace LantanaGroup.Link.Shared.Application.Extensions;

/// <summary>
/// Extension methods that wrap Kafka <see cref="IConsumer{TKey,TValue}.Commit()"/> calls
/// so that transient <see cref="KafkaException"/> errors (partition assignment lost,
/// unknown member, etc.) are logged as warnings instead of propagating and potentially
/// killing the consumer loop or host process.
///
/// The Kafka client will automatically re-join the consumer group on the next poll,
/// so a failed commit is recoverable — the worst case is that the message will be
/// redelivered (at-least-once semantics).
/// </summary>
public static class KafkaConsumerCommitExtensions
{
    /// <summary>
    /// Commits the current offsets, swallowing <see cref="KafkaException"/>.
    /// </summary>
    public static void SafeCommit<TKey, TValue>(
        this IConsumer<TKey, TValue> consumer,
        ILogger? logger = null)
    {
        try
        {
            consumer.Commit();
        }
        catch (KafkaException ex)
        {
            logger?.LogWarning(ex, "Kafka commit failed, will retry on next message. Reason: {Reason}", ex.Error.Reason);
        }
    }

    /// <summary>
    /// Commits the offset for a specific consume result, swallowing <see cref="KafkaException"/>.
    /// </summary>
    public static void SafeCommit<TKey, TValue>(
        this IConsumer<TKey, TValue> consumer,
        ConsumeResult<TKey, TValue> result,
        ILogger? logger = null)
    {
        try
        {
            consumer.Commit(result);
        }
        catch (KafkaException ex)
        {
            logger?.LogWarning(ex, "Kafka commit failed for {TopicPartitionOffset}, will retry on next message. Reason: {Reason}",
                result.TopicPartitionOffset, ex.Error.Reason);
        }
    }

    /// <summary>
    /// Commits specific offsets, swallowing <see cref="KafkaException"/>.
    /// </summary>
    public static void SafeCommit<TKey, TValue>(
        this IConsumer<TKey, TValue> consumer,
        IEnumerable<TopicPartitionOffset> offsets,
        ILogger? logger = null)
    {
        try
        {
            consumer.Commit(offsets);
        }
        catch (KafkaException ex)
        {
            logger?.LogWarning(ex, "Kafka commit failed, will retry on next message. Reason: {Reason}", ex.Error.Reason);
        }
    }
}
