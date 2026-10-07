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
        var snapshot = await GetSnapshotAsync(cancellationToken);
        return snapshot.Index;
    }

    /// <summary>
    /// The same cached read as <see cref="GetAsync"/>, plus whether storage answered.
    /// An empty index with <c>Reachable</c> false is a miss, not "no automation facilities".
    /// </summary>
    public async Task<(AutomationOwnershipIndex Index, bool Reachable)> GetSnapshotAsync(CancellationToken cancellationToken)
    {
        if (_cache.TryGetValue(CacheKey, out OwnershipCache? cached) && cached is not null)
            return (cached.Index, cached.Reachable);

        var load = await _reader.LoadOwnershipAsync(cancellationToken);
        var index = load.Reachable
            ? AutomationMarkRules.Build(load.Runs, load.Tombstones)
            : AutomationOwnershipIndex.Empty;
        var snapshot = new OwnershipCache(index, load.Reachable);
        _cache.Set(
            CacheKey,
            snapshot,
            load.Reachable ? TimeSpan.FromSeconds(20) : TimeSpan.FromSeconds(15));
        return (index, load.Reachable);
    }

    private sealed record OwnershipCache(AutomationOwnershipIndex Index, bool Reachable);
}
