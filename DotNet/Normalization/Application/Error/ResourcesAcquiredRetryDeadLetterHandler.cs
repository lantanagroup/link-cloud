using Confluent.Kafka;
using LantanaGroup.Link.Normalization.Application.Models.Messages;
using LantanaGroup.Link.Normalization.Application.Services;
using LantanaGroup.Link.Shared.Application.Error.Handlers;
using LantanaGroup.Link.Shared.Application.Error.Interfaces;
using LantanaGroup.Link.Shared.Application.Interfaces;
using LantanaGroup.Link.Shared.Application.Listeners;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Services.Security;
using LantanaGroup.Link.Shared.Application.Models.Kafka;
using LantanaGroup.Link.Shared.Settings;
using System.Text.Json;

namespace LantanaGroup.Link.Normalization.Application.Error;

/// <summary>
/// Dead letter handler for <see cref="RetryListener"/> that also releases the resource cache.
/// </summary>
/// <remarks>
/// A <c>ResourcesAcquired</c> message that fails transiently is republished to
/// <c>ResourcesAcquired-Retry</c> and redelivered on a schedule. Once the retry count is exhausted,
/// <see cref="RetryListener"/> — shared, service-agnostic code with no knowledge of the resource
/// cache — dead-letters it to <c>ResourcesAcquired-Error</c>. That is the second and final terminal
/// path for the message (the first being a <c>DeadLetterException</c> raised directly in
/// <c>ResourcesAcquiredListener</c>), so it is where the cached resources have to be released.
/// <para>
/// Normalization registers <see cref="RetryListener"/> for <c>ResourcesAcquired-Retry</c> only, so
/// every message reaching this handler is a <see cref="ResourcesAcquiredValue"/>.
/// </para>
/// </remarks>
public class ResourcesAcquiredRetryDeadLetterHandler : DeadLetterExceptionHandler<RetryListener, string, string>
{
    private static readonly JsonSerializerOptions DeserializerOptions = new()
    {
        // Mirrors JsonWithFhirMessageDeserializer, which is how the value was read off the topic.
        PropertyNameCaseInsensitive = true,
        AllowTrailingCommas = true
    };

    private readonly IResourceCachePurger _resourceCachePurger;
    private readonly ILogger<ResourcesAcquiredRetryDeadLetterHandler> _logger;
    private readonly INormalizationStatusPublisher _statusPublisher;

    public ResourcesAcquiredRetryDeadLetterHandler(
        IKafkaProducerFactory<string, string> producerFactory,
        IKafkaProducerFactory<string, string> nullConsumeResultProducerFactory,
        ServiceInformation serviceInformation,
        IExceptionLogger<RetryListener> exceptionHandler,
        IResourceCachePurger resourceCachePurger,
        ILogger<ResourcesAcquiredRetryDeadLetterHandler> logger,
        INormalizationStatusPublisher statusPublisher)
        : base(producerFactory, nullConsumeResultProducerFactory, serviceInformation, exceptionHandler)
    {
        _resourceCachePurger = resourceCachePurger ?? throw new ArgumentNullException(nameof(resourceCachePurger));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _statusPublisher = statusPublisher ?? throw new ArgumentNullException(nameof(statusPublisher));
    }

    /// <remarks>
    /// Both <c>HandleException</c> overloads funnel through here. The dead letter is produced first so
    /// that the durable record of the failure is never at the mercy of the cache purge.
    /// </remarks>
    public override void ProduceDeadLetter(ConsumeResult<string, string> consumeResult, string exceptionMessage)
    {
        base.ProduceDeadLetter(consumeResult, exceptionMessage);

        PurgeResourceCache(consumeResult, exceptionMessage);
    }

    private void PurgeResourceCache(ConsumeResult<string, string> consumeResult, string exceptionMessage)
    {
        ResourcesAcquiredValue? value;

        try
        {
            value = JsonSerializer.Deserialize<ResourcesAcquiredValue>(
                consumeResult.Message.Value, DeserializerOptions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Could not deserialize a retry-exhausted message from {Topic} to determine its resource cache keys. " +
                "Any cached resources for it will be released by the cache expiration policy instead.",
                consumeResult.Topic.SanitizeForLog());
            return;
        }

        // RetryListener runs its consume callback on a background thread with no synchronization
        // context, so blocking here cannot deadlock. PurgeAsync does not throw.
        ResourceKey? key = null;
        try
        {
            if (!string.IsNullOrWhiteSpace(consumeResult.Message.Key))
                key = JsonSerializer.Deserialize<ResourceKey>(consumeResult.Message.Key, DeserializerOptions);
        }
        catch (JsonException)
        {
            // Invalid keys cannot identify a patient, but must still release their resource cache.
        }

        var error = consumeResult.Message.Headers.TryGetLastBytes(KafkaConstants.HeaderConstants.RetryExceptionMessage, out var bytes)
            && bytes is not null
            ? System.Text.Encoding.UTF8.GetString(bytes).Split('\n')[0].TrimEnd('\r')
            : exceptionMessage;
        try
        {
            _statusPublisher.PublishAsync(key, value, consumeResult.Message.Headers,
                    false, null, error, CancellationToken.None)
                .GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to publish normalization failure for a retry-exhausted message from {Topic}. Continuing with resource cache cleanup.",
                consumeResult.Topic.SanitizeForLog());
        }

        _resourceCachePurger
            .PurgeAsync(value, $"retry count exhausted: {exceptionMessage}")
            .GetAwaiter()
            .GetResult();
    }
}
