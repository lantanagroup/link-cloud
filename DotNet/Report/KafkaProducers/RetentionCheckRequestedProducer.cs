using System.Text;
using Confluent.Kafka;
using LantanaGroup.Link.Report.Application.Interfaces;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Models.Retention;
using LantanaGroup.Link.Shared.Settings;

namespace LantanaGroup.Link.Report.KafkaProducers;

/// <summary>
/// Publishes retention cleanup requests on <c>RetentionCheckRequested</c>, keyed by the cleanup id so every
/// message for one cleanup lands on one partition. See dev-docs/retention-events.md.
/// </summary>
public class RetentionCheckRequestedProducer : IRetentionCheckRequestedPublisher
{
    /// <summary>
    /// The Kafka client id requested for the producer. The shared KafkaProducerFactory currently replaces it
    /// with KafkaConnection's client id, as it does for every Report producer.
    /// </summary>
    public const string ClientId = "Report_RetentionCheckRequested";

    private readonly IProducer<string, RetentionCheckRequestedValue> _producer;

    /// <summary>
    /// Creates the publisher.
    /// </summary>
    public RetentionCheckRequestedProducer(IProducer<string, RetentionCheckRequestedValue> producer)
    {
        _producer = producer ?? throw new ArgumentNullException(nameof(producer));
    }

    /// <summary>
    /// Publishes <paramref name="request"/> and waits for the broker to acknowledge it. A delivery failure
    /// throws, so the caller never records an unsent request as sent.
    /// </summary>
    public async Task PublishAsync(RetentionCheckRequestedValue request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.RetentionCleanupId, nameof(request));

        var headers = new Headers
        {
            {
                KafkaConstants.HeaderConstants.CorrelationId,
                Encoding.UTF8.GetBytes(request.RetentionCleanupId)
            }
        };

        var message = new Message<string, RetentionCheckRequestedValue>
        {
            Key = request.RetentionCleanupId,
            Value = request,
            Headers = headers
        };

        await _producer.ProduceAsync(nameof(KafkaTopic.RetentionCheckRequested), message, cancellationToken);
    }
}
