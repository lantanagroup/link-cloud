using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Domain;
using LantanaGroup.Link.Shared.Application.Models.Mapping;
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
    /// <returns>
    /// How the patient resolved against the facility's organization. Returned rather than discarded
    /// because this is the only place it is computed, and it is what the report's Location Org and
    /// Encounter Mapping indicators record.
    /// </returns>
    Task<LocationOrgOutcome> FinalizeAsync(TailCompletionResult tail, CancellationToken cancellationToken = default);
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

    public async Task<LocationOrgOutcome> FinalizeAsync(TailCompletionResult tail, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(tail);

        // Encounter is cached by its ungated primary log. Strip non-org encounters before
        // the tail is produced so MeasureEval never rehydrates them.
        var locationOrgOutcome = await _locationMappingService.StripNonOrgEncountersFromCacheAsync(
            tail.FacilityId,
            tail.CorrelationId,
            tail.PatientId.SplitReference(),
            cancellationToken);

        // The strip needs no durability barrier: ReplaceResourcesAsync writes blob storage itself,
        // and it drains anything still in flight for the key before it does. Acquisition's own
        // writes are already durable, because the per-log barrier runs before each log goes terminal
        // and the tail fires only once every sibling log is terminal.

        var listed = tail.ResourcesAcquired.CacheKeys ?? [];
        if (listed.Count == 0)
        {
            return locationOrgOutcome;
        }

        var kept = new List<string>(listed.Count);

        foreach (var key in listed)
        {
            // One probe, not two. Every correlation now lives in durable storage with the cache in
            // front of it, so a key that holds nothing here holds nothing anywhere.
            if (await _resourceCache.HasResourcesAsync(key, cancellationToken))
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

        return locationOrgOutcome;
    }
}
