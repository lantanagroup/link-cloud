using System.Collections.Concurrent;
using LantanaGroup.Link.Shared.Application.Interfaces;

namespace LantanaGroup.Link.Shared.Application.Services;

public sealed class InMemoryPipelineAbortRegistry : IPipelineAbortRegistry
{
    private readonly ConcurrentDictionary<string, byte> _keys = new(StringComparer.OrdinalIgnoreCase);

    public Task AbortAsync(string? facilityId, string? reportId, TimeSpan timeToLive, CancellationToken cancellationToken = default)
    {
        Add(FacilityKey(facilityId));
        Add(ReportKey(reportId));
        return Task.CompletedTask;
    }

    public Task<bool> IsAbortedAsync(string? facilityId, string? reportId, CancellationToken cancellationToken = default)
    {
        var aborted = Has(FacilityKey(facilityId)) || Has(ReportKey(reportId));
        return Task.FromResult(aborted);
    }

    public Task ClearAsync(string? facilityId, string? reportId, CancellationToken cancellationToken = default)
    {
        Remove(FacilityKey(facilityId));
        Remove(ReportKey(reportId));
        return Task.CompletedTask;
    }

    private void Add(string? key)
    {
        if (!string.IsNullOrWhiteSpace(key))
            _keys.TryAdd(key, 0);
    }

    private bool Has(string? key) =>
        !string.IsNullOrWhiteSpace(key) && _keys.ContainsKey(key);

    private void Remove(string? key)
    {
        if (!string.IsNullOrWhiteSpace(key))
            _keys.TryRemove(key, out _);
    }

    internal static string? FacilityKey(string? facilityId) =>
        string.IsNullOrWhiteSpace(facilityId) ? null : $"link:pipeline-abort:facility:{facilityId.Trim()}";

    internal static string? ReportKey(string? reportId) =>
        string.IsNullOrWhiteSpace(reportId) ? null : $"link:pipeline-abort:report:{reportId.Trim()}";
}
