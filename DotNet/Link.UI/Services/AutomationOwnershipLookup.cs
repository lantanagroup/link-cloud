using Microsoft.Extensions.Caching.Memory;

namespace Link.UI.Services;

/// <summary>
/// One cached ownership read shared by Tenants, Reports, and Logs.
/// A miss reads projected run and tombstone rows. It does not load run configuration.
/// </summary>
public sealed class AutomationOwnershipLookup
{
    public const string CacheKey = "link-ui:automation-ownership";

    private readonly AutomationRunReader _reader;
    private readonly IMemoryCache _cache;

    public AutomationOwnershipLookup(AutomationRunReader reader, IMemoryCache cache)
    {
        _reader = reader;
        _cache = cache;
    }

    public async Task<AutomationOwnershipIndex> GetAsync(CancellationToken cancellationToken)
    {
        if (_cache.TryGetValue(CacheKey, out AutomationOwnershipIndex? cached) && cached is not null)
            return cached;

        var load = await _reader.LoadOwnershipAsync(cancellationToken);
        var index = load.Reachable
            ? AutomationMarkRules.Build(load.Runs, load.Tombstones)
            : AutomationOwnershipIndex.Empty;
        _cache.Set(
            CacheKey,
            index,
            load.Reachable ? TimeSpan.FromSeconds(20) : TimeSpan.FromSeconds(15));
        return index;
    }
}
