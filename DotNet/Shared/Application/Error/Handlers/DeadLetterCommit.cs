using System.Diagnostics.Metrics;
using Confluent.Kafka;
using Microsoft.Extensions.Logging;

namespace LantanaGroup.Link.Shared.Application.Error.Handlers;

public static class DeadLetterCommit
{
    public const string MeterName = "Link.Kafka";
    public const string RewindFailedCounterName = "link_kafka.consumer_rewind_failed.count";
    public const string RewindFailedEventName = "kafka_consumer_rewind_failed";

    private static readonly Counter<long> RewindFailed =
        new Meter(MeterName).CreateCounter<long>(RewindFailedCounterName);

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
            RewindFailed.Add(1, new KeyValuePair<string, object?>("topic", result.Topic));
            logger.LogError(
                ex,
                "Failed to rewind after the error topic publish failed. EventName={EventName} Topic={Topic} Partition={Partition} Offset={Offset}",
                RewindFailedEventName,
                result.Topic,
                result.Partition.Value,
                result.Offset.Value);
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
