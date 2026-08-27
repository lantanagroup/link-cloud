using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Domain;
using LantanaGroup.Link.Shared.Application.Enums;
using LantanaGroup.Link.Shared.Application.Interfaces;
using LantanaGroup.Link.Shared.Application.Services.Security;
using LantanaGroup.Link.Shared.Application.Utilities;
using Microsoft.Extensions.Logging;

namespace LantanaGroup.Link.DataAcquisition.Domain.Application.Services;

public interface IResourcesAcquiredTailFinalizer
{
    /// <summary>
    /// Applies org-location encounter stripping, then drops any cache keys that no longer
    /// contain resources so ResourcesAcquired never points at an empty location.
    /// </summary>
    Task FinalizeAsync(TailCompletionResult tail, CancellationToken cancellationToken = default);
}

public class ResourcesAcquiredTailFinalizer : IResourcesAcquiredTailFinalizer
{
    private readonly ILocationMappingService _locationMappingService;
    private readonly IResourceCache _resourceCache;
    private readonly ILogger<ResourcesAcquiredTailFinalizer> _logger;

    public ResourcesAcquiredTailFinalizer(
        ILocationMappingService locationMappingService,
        IResourceCache resourceCache,
        ILogger<ResourcesAcquiredTailFinalizer> logger)
    {
        _locationMappingService = locationMappingService ?? throw new ArgumentNullException(nameof(locationMappingService));
        _resourceCache = resourceCache ?? throw new ArgumentNullException(nameof(resourceCache));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task FinalizeAsync(TailCompletionResult tail, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(tail);

        // Encounter is cached by its ungated primary log. Strip non-org encounters before
        // the tail is produced so MeasureEval never rehydrates them.
        await _locationMappingService.StripNonOrgEncountersFromCacheAsync(
            tail.FacilityId,
            tail.CorrelationId,
            tail.PatientId.SplitReference(),
            cancellationToken);

        var listed = tail.ResourcesAcquired.CacheKeys ?? [];
        if (listed.Count == 0)
        {
            // CacheType is already stamped on the tail; Hybrid no longer needs the in-process memo.
            _resourceCache.ForgetCacheTypeForCorrelationId(tail.CorrelationId);
            return;
        }

        var kept = new List<string>(listed.Count);
        var sawAbs = false;
        var sawRedis = false;

        foreach (var key in listed)
        {
            var inAbs = await KeyHasResourcesAsync(ResourceCacheType.ABS, key, cancellationToken);
            var inRedis = await KeyHasResourcesAsync(ResourceCacheType.Redis, key, cancellationToken);
            if (!inAbs && !inRedis)
            {
                continue;
            }

            kept.Add(key);
            sawAbs |= inAbs;
            sawRedis |= inRedis;
        }

        if (kept.Count != listed.Count)
        {
            _logger.LogInformation(
                "Dropped {DroppedCount} empty ResourcesAcquired cache key(s) for FacilityId={FacilityId}, CorrelationId={CorrelationId}. Listed={ListedCount}, Kept={KeptCount}.",
                listed.Count - kept.Count,
                tail.FacilityId.SanitizeForLog(),
                tail.CorrelationId.SanitizeForLog(),
                listed.Count,
                kept.Count);
        }

        if (sawAbs && sawRedis)
        {
            await ConsolidateIntoAbsAsync(kept, cancellationToken);
            _logger.LogWarning(
                "Replica split for CorrelationId={CorrelationId}: copied Redis-only keys into ABS so ResourcesAcquired can advertise a single CacheType.",
                tail.CorrelationId.SanitizeForLog());
            tail.ResourcesAcquired.CacheType = ResourceCacheType.ABS;
        }
        else if (sawAbs)
        {
            tail.ResourcesAcquired.CacheType = ResourceCacheType.ABS;
        }
        else if (sawRedis)
        {
            tail.ResourcesAcquired.CacheType = ResourceCacheType.Redis;
        }

        tail.ResourcesAcquired.CacheKeys = kept;

        // Drop the in-process Hybrid memo. The Redis {correlation}:__cacheType memo stays
        // so a later retry or replica can still resolve ABS vs Redis.
        _resourceCache.ForgetCacheTypeForCorrelationId(tail.CorrelationId);
    }

    private async Task ConsolidateIntoAbsAsync(List<string> kept, CancellationToken cancellationToken)
    {
        var redis = TryGetImplementation(ResourceCacheType.Redis);
        var abs = TryGetImplementation(ResourceCacheType.ABS);
        if (redis == null || abs == null)
        {
            return;
        }

        var redisOnly = new List<string>();
        foreach (var key in kept)
        {
            if (await redis.HasResourcesAsync(key, cancellationToken)
                && !await abs.HasResourcesAsync(key, cancellationToken))
            {
                redisOnly.Add(key);
            }
        }

        foreach (var key in redisOnly)
        {
            var resources = await redis.GetAsync(key, cancellationToken);
            if (resources.Count == 0)
            {
                continue;
            }

            var resourceType = abs.GetResourceTypeByCacheKey(key);
            await abs.UpdateCorrelationCacheAsync(key, resources, resourceType, cancellationToken);
        }

        var redisCopies = new List<string>();
        foreach (var key in kept)
        {
            if (await redis.HasResourcesAsync(key, cancellationToken))
            {
                redisCopies.Add(key);
            }
        }

        if (redisCopies.Count > 0)
        {
            await redis.DeleteAsync(redisCopies, cancellationToken);
        }
    }

    private async Task<bool> KeyHasResourcesAsync(
        ResourceCacheType cacheType,
        string cacheKey,
        CancellationToken cancellationToken)
    {
        var implementation = TryGetImplementation(cacheType);
        if (implementation == null)
        {
            return false;
        }

        return await implementation.HasResourcesAsync(cacheKey, cancellationToken);
    }

    private IResourceCache? TryGetImplementation(ResourceCacheType cacheType)
    {
        try
        {
            return _resourceCache.GetImplementation(cacheType);
        }
        catch (NotSupportedException)
        {
            return null;
        }
    }
}
