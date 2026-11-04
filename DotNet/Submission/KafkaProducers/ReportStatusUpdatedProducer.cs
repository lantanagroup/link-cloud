using System.Text;
using Confluent.Kafka;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Models.Kafka;
using LantanaGroup.Link.Shared.Application.Utilities;
using LantanaGroup.Link.Shared.Settings;

namespace LantanaGroup.Link.Submission.KafkaProducers;

/// <summary>
/// Sends one <see cref="ReportStatusUpdatedValue"/> and waits for the broker.
/// Librdkafka retries the send. This type does not send again, and it does not
/// change <see cref="ReportStatusUpdatedValue.Timestamp"/>.
/// </summary>
public sealed class ReportStatusUpdatedProducer(IProducer<ReportStatusUpdatedKey, ReportStatusUpdatedValue> producer)
{
    /// <summary>
    /// Java hashes keys with Murmur2. The .NET default is CRC32, so this producer
    /// must set <see cref="Partitioner.Murmur2Random"/> or the same patient lands
    /// on a different partition than Java producers.
    /// </summary>
    public static ProducerConfig CreateConfig() => new()
    {
        ClientId = "Submission_ReportStatusUpdated",
        Partitioner = Partitioner.Murmur2Random
    };

    public void Produce(ReportStatusUpdatedValue value, Headers? sourceHeaders, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(value);

        var headers = new Headers();
        if (!string.IsNullOrWhiteSpace(value.CorrelationId))
        {
            headers.Add(KafkaConstants.HeaderConstants.CorrelationId, Encoding.UTF8.GetBytes(value.CorrelationId));
        }

        KafkaHeaderHelper.CopyMetricsMode(sourceHeaders, headers);

        // The same value instance is what a producer retry delivers. Do not rebuild it.
        var message = new Message<ReportStatusUpdatedKey, ReportStatusUpdatedValue>
        {
            Key = new ReportStatusUpdatedKey
            {
                ReportId = value.ReportScheduleId,
                PatientId = value.PatientId
            },
            Value = value,
            Headers = headers
        };

        producer.ProduceAsync(nameof(KafkaTopic.ReportStatusUpdated), message, cancellationToken)
            .ConfigureAwait(false)
            .GetAwaiter()
            .GetResult();
    }
}
