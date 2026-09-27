using System.Collections.Frozen;
using LantanaGroup.Link.Normalization.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace LantanaGroup.Link.Normalization.Domain.Queries;

public interface IHSLOCLookupCache
{
    Task<IReadOnlyDictionary<string, Guid>> GetActiveLookup(
        NormalizationDbContext context, CancellationToken cancellationToken = default);
    void Invalidate();
}

public sealed class HSLOCLookupCache(IMemoryCache cache) : IHSLOCLookupCache, IDisposable
{
    private readonly SemaphoreSlim _loadGate = new(1, 1);
    private readonly object _sync = new();
    private object _cacheKey = new();

    public async Task<IReadOnlyDictionary<string, Guid>> GetActiveLookup(
        NormalizationDbContext context, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_sync)
        {
            if (cache.TryGetValue(_cacheKey, out FrozenDictionary<string, Guid>? existing) && existing != null)
            {
                return existing;
            }
        }
        await _loadGate.WaitAsync(cancellationToken);
        try
        {
            object cacheKey;
            lock (_sync)
            {
                cacheKey = _cacheKey;
                if (cache.TryGetValue(cacheKey, out FrozenDictionary<string, Guid>? existing) && existing != null)
                {
                    return existing;
                }
            }

            var codes = await context.HSLOCS.AsNoTracking().Where(code => code.IsActive)
                .Select(code => new { code.HSLOCCode, code.Id }).ToListAsync(cancellationToken);
            var lookup = codes.GroupBy(code => code.HSLOCCode, StringComparer.Ordinal)
                .ToFrozenDictionary(group => group.Key, group => group.First().Id, StringComparer.Ordinal);

            lock (_sync)
            {
                if (ReferenceEquals(cacheKey, _cacheKey))
                {
                    cache.Set(cacheKey, lookup, TimeSpan.FromMinutes(1));
                }
            }

            return lookup;
        }
        finally
        {
            _loadGate.Release();
        }
    }

    public void Invalidate()
    {
        lock (_sync)
        {
            cache.Remove(_cacheKey);
            _cacheKey = new object();
        }
    }

    public void Dispose() => _loadGate.Dispose();
}