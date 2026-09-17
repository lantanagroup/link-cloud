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
/// Unlike the success path — which deletes only the per-resource-type acquisition keys
/// (<c>{correlationId}:{ResourceType}</c>) and leaves <c>{correlationId}</c> in place for Measure
/// Eval to read — a terminal failure also removes the correlation key, because nothing downstream
/// will ever consume it.
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

        // Acquisition keys are "{correlationId}:{ResourceType}"; the correlation key itself holds
        // whatever normalization managed to write before it failed, so it goes too.
        var keysToDelete = new List<string>(cacheKeys);
        keysToDelete.AddRange(cacheKeys
            .Select(ExtractCorrelationId)
            .Where(correlationId => !string.IsNullOrWhiteSpace(correlationId))
            .Distinct()
            .Where(correlationId => !keysToDelete.Contains(correlationId)));

        try
        {
            await _resourceCache
                .GetImplementation(value!.CacheType)
                .DeleteAsync(keysToDelete, cancellationToken);

            _logger.LogInformation(
                "Purged {KeyCount} resource cache entries after terminal failure ({Reason}). CacheType: {CacheType}, Keys: [{Keys}]",
                keysToDelete.Count,
                reason.SanitizeForLog(),
                value.CacheType,
                string.Join(", ", keysToDelete).SanitizeForLog());
        }
        catch (Exception ex)
        {
            // Never let cleanup failure escape: the caller is already handling a failed message, and
            // the cache expiration policy is the backstop for whatever we could not delete here.
            _logger.LogError(ex,
                "Failed to purge resource cache after terminal failure ({Reason}). CacheType: {CacheType}, Keys: [{Keys}]",
                reason.SanitizeForLog(),
                value!.CacheType,
                string.Join(", ", keysToDelete).SanitizeForLog());
        }
    }

    /// <remarks>Mirrors <c>HybridResourceCache.ExtractCorrelationId</c>.</remarks>
    private static string ExtractCorrelationId(string cacheKey)
    {
        var idx = cacheKey.IndexOf(':');
        return idx > 0 ? cacheKey[..idx] : cacheKey;
    }
}
