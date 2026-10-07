using Microsoft.Extensions.Caching.Memory;

namespace Link.UI.Services;

/// <summary>
/// One search per owned facility, capped and cached, for the Automation filter.
/// Reports and logs are paged by a single facility id upstream.
/// </summary>
public static class AutomationFacilitySearch
{
    private static readonly SemaphoreSlim Gate = new(4, 4);

    public static async Task<FacilitySearchCache<T>> CachedAsync<T>(
        IMemoryCache cache,
        string kind,
        string fingerprint,
        IReadOnlyList<string> facilityIds,
        Func<string, CancellationToken, Task<FacilitySearchPage<T>>> search,
        CancellationToken cancellationToken)
    {
        var key = AutomationMarkRules.CacheKey(kind, facilityIds, fingerprint);
        if (cache.TryGetValue(key, out FacilitySearchCache<T>? cached) && cached is not null)
            return cached;

        if (facilityIds.Count == 0)
        {
            cached = new FacilitySearchCache<T>([], false, null);
            cache.Set(key, cached, TimeSpan.FromSeconds(15));
            return cached;
        }

        var found = await AllAsync(facilityIds, search, cancellationToken);
        var rows = new List<T>();
        string? error = null;
        var partial = false;
        foreach (var page in found)
        {
            if (!string.IsNullOrWhiteSpace(page.Error))
                error ??= page.Error;
            if (page.Total > page.Rows.Count)
                partial = true;
            rows.AddRange(page.Rows);
        }

        cached = new FacilitySearchCache<T>(rows, partial, rows.Count == 0 ? error : null);
        cache.Set(key, cached, TimeSpan.FromSeconds(15));
        return cached;
    }

    private static async Task<T[]> AllAsync<T>(
        IReadOnlyList<string> facilityIds,
        Func<string, CancellationToken, Task<T>> search,
        CancellationToken cancellationToken)
    {
        var tasks = facilityIds.Select(async facilityId =>
        {
            await Gate.WaitAsync(cancellationToken);
            try
            {
                return await search(facilityId, cancellationToken);
            }
            finally
            {
                Gate.Release();
            }
        });
        return await Task.WhenAll(tasks);
    }
}

public sealed record FacilitySearchPage<T>(IReadOnlyList<T> Rows, long Total, string? Error);

public sealed record FacilitySearchCache<T>(IReadOnlyList<T> Rows, bool Partial, string? Error);
