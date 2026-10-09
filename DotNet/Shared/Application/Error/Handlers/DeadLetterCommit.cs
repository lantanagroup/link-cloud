using Confluent.Kafka;
using Microsoft.Extensions.Logging;

namespace LantanaGroup.Link.Shared.Application.Error.Handlers;

public static class DeadLetterCommit
{
    public static async Task RewindAsync<TKey, TValue>(
        IConsumer<TKey, TValue> consumer,
        ConsumeResult<TKey, TValue> result,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        // A later commit on this partition would cover this offset. The record was not published
        // to the error topic, so put the consumer back on it and try again.
        try
        {
            consumer.Seek(result.TopicPartitionOffset);
        }
        catch (KafkaException ex)
        {
            logger.LogError(ex, "Failed to rewind {TopicPartitionOffset} after the error topic publish failed.", result.TopicPartitionOffset);
        }

        await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
    }

    public static async Task<bool> AccountAsync<TKey, TValue>(
        bool published,
        IConsumer<TKey, TValue> consumer,
        ConsumeResult<TKey, TValue> result,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        if (published)
        {
            return true;
        }

        await RewindAsync(consumer, result, logger, cancellationToken);
        return false;
    }
}
