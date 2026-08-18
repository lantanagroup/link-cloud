using LantanaGroup.Link.Normalization.Application.Models.Messages;
using LantanaGroup.Link.Shared.Application.Interfaces;
using LantanaGroup.Link.Shared.Application.Services.Security;

namespace LantanaGroup.Link.Normalization.Application.Services;

/// <summary>
/// Releases the resource cache entries belonging to a <c>ResourcesAcquired</c> message after a
/// terminal (non-retryable) normalization failure.
/// </summary>
/// <remarks>
/// Only call this on terminal failures. A message bound for <c>ResourcesAcquired-Retry</c> still
/// needs its cached resources when it is redelivered, so purging on a transient failure would
/// guarantee the retry fails too.
/// <para>
/// Only the per-resource-type acquisition keys (<c>{correlationId}:{ResourceType}</c>) are ever
/// deleted — they are Normalization's input, consumed by this message alone. The
/// <c>{correlationId}</c> key is Normalization's output and belongs to its reader: Measure Eval
/// deletes it after evaluation, and the cache expiration policy reclaims it if no reader ever
/// comes (e.g. the failure occurred before <c>ResourcesNormalized</c> was published). Normalization
/// never deletes it, on any path — it cannot know whether an earlier attempt already published, or
/// whether the key still holds kept INITIAL-phase data a SUPPLEMENTAL evaluation needs.
/// </para>
/// </remarks>
public interface IResourceCachePurger
{
    Task PurgeAsync(ResourcesAcquiredValue? value, string reason, CancellationToken cancellationToken = default);
}

/// <inheritdoc cref="IResourceCachePurger"/>
public class ResourceCachePurger : IResourceCachePurger
{
    private readonly IResourceCache _resourceCache;
    private readonly ILogger<ResourceCachePurger> _logger;

    public ResourceCachePurger(IResourceCache resourceCache, ILogger<ResourceCachePurger> logger)
    {
        _resourceCache = resourceCache ?? throw new ArgumentNullException(nameof(resourceCache));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task PurgeAsync(ResourcesAcquiredValue? value, string reason, CancellationToken cancellationToken = default)
    {
        var cacheKeys = value?.CacheKeys?.Where(k => !string.IsNullOrWhiteSpace(k)).ToList();

        if (cacheKeys == null || cacheKeys.Count == 0)
        {
            _logger.LogWarning(
                "Cannot purge resource cache after terminal failure ({Reason}): the message carries no cache keys. " +
                "Any cached resources for it will be released by the cache expiration policy instead.",
                reason.SanitizeForLog());
            return;
        }

        try
        {
            await _resourceCache
                .GetImplementation(value!.CacheType)
                .DeleteAsync(cacheKeys, cancellationToken);

            _logger.LogInformation(
                "Purged {KeyCount} resource cache entries after terminal failure ({Reason}). CacheType: {CacheType}, Keys: [{Keys}]",
                cacheKeys.Count,
                reason.SanitizeForLog(),
                value.CacheType,
                string.Join(", ", cacheKeys).SanitizeForLog());
        }
        catch (Exception ex)
        {
            // Never let cleanup failure escape: the caller is already handling a failed message, and
            // the cache expiration policy is the backstop for whatever we could not delete here.
            _logger.LogError(ex,
                "Failed to purge resource cache after terminal failure ({Reason}). CacheType: {CacheType}, Keys: [{Keys}]",
                reason.SanitizeForLog(),
                value!.CacheType,
                string.Join(", ", cacheKeys).SanitizeForLog());
        }
    }
}
