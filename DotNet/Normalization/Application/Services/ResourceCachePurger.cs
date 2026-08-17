using LantanaGroup.Link.Normalization.Application.Models.Messages;
using LantanaGroup.Link.Shared.Application.Interfaces;
using LantanaGroup.Link.Shared.Application.Services.Security;

namespace LantanaGroup.Link.Normalization.Application.Services;

/// <summary>
/// Which cache keys a terminal-failure purge is allowed to remove. The scope encodes what the
/// caller can prove about whether <c>ResourcesNormalized</c> was already published for the message.
/// </summary>
public enum ResourceCachePurgeScope
{
    /// <summary>
    /// Remove the per-resource-type acquisition keys and the <c>{correlationId}</c> key. Only valid
    /// where the failure provably occurred before <c>ResourcesNormalized</c> was produced (the
    /// immediate dead-letter path: validation raises before the processing loop), so nothing
    /// downstream can be holding the correlation key.
    /// </summary>
    All,

    /// <summary>
    /// Remove only the per-resource-type acquisition keys. For paths that cannot rule out a prior
    /// publish — retry exhaustion follows attempts that may have produced <c>ResourcesNormalized</c>
    /// and then failed on the trailing cache delete — where Measure Eval may already be holding
    /// <c>{correlationId}</c> for its SUPPLEMENTAL pass. The correlation key is left to the cache
    /// expiration policy.
    /// </summary>
    AcquisitionKeysOnly
}

/// <summary>
/// Releases the resource cache entries belonging to a <c>ResourcesAcquired</c> message after a
/// terminal (non-retryable) normalization failure.
/// </summary>
/// <remarks>
/// Only call this on terminal failures. A message bound for <c>ResourcesAcquired-Retry</c> still
/// needs its cached resources when it is redelivered, so purging on a transient failure would
/// guarantee the retry fails too.
/// <para>
/// The success path deletes only the per-resource-type acquisition keys and leaves
/// <c>{correlationId}</c> in place for Measure Eval to read. Whether a terminal failure may also
/// remove the correlation key depends on what the caller can prove — see
/// <see cref="ResourceCachePurgeScope"/>.
/// </para>
/// </remarks>
public interface IResourceCachePurger
{
    Task PurgeAsync(ResourcesAcquiredValue? value, string reason, ResourceCachePurgeScope scope, CancellationToken cancellationToken = default);
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

    public async Task PurgeAsync(ResourcesAcquiredValue? value, string reason, ResourceCachePurgeScope scope, CancellationToken cancellationToken = default)
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

        // Acquisition keys are "{correlationId}:{ResourceType}". The correlation key itself is only
        // removed when the caller can prove ResourcesNormalized was never published for this message
        // (scope All); otherwise Measure Eval may still be reading it, and it is left to expire.
        var keysToDelete = new List<string>(cacheKeys);
        if (scope == ResourceCachePurgeScope.All)
        {
            keysToDelete.AddRange(cacheKeys
                .Select(ExtractCorrelationId)
                .Where(correlationId => !string.IsNullOrWhiteSpace(correlationId))
                .Distinct()
                .Where(correlationId => !keysToDelete.Contains(correlationId)));
        }

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
