using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Domain;
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
            return;
        }

        var cache = _resourceCache.GetImplementation(tail.ResourcesAcquired.CacheType);
        var kept = new List<string>(listed.Count);
        foreach (var key in listed)
        {
            var resources = await cache.GetAsync(key, cancellationToken);
            if (resources.Count > 0)
            {
                kept.Add(key);
            }
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

        tail.ResourcesAcquired.CacheKeys = kept;
    }
}
