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

        // Prefer ABS when a correlation has data in both stores (a replica split) so
        // Normalization does not look only in Redis and miss the ABS payload.
        if (sawAbs)
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

    private async Task<bool> KeyHasResourcesAsync(
        ResourceCacheType cacheType,
        string cacheKey,
        CancellationToken cancellationToken)
    {
        IResourceCache implementation;
        try
        {
            implementation = _resourceCache.GetImplementation(cacheType);
        }
        catch (NotSupportedException)
        {
            return false;
        }

        return await implementation.HasResourcesAsync(cacheKey, cancellationToken);
    }
}
