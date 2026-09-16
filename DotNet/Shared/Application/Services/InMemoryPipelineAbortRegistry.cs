using System.Collections.Concurrent;
using LantanaGroup.Link.Shared.Application.Interfaces;

namespace LantanaGroup.Link.Shared.Application.Services;

public sealed class InMemoryPipelineAbortRegistry : IPipelineAbortRegistry
{
    private readonly ConcurrentDictionary<string, long> _expiresAtTicks = new(StringComparer.OrdinalIgnoreCase);
    private readonly TimeProvider _time;

    public InMemoryPipelineAbortRegistry() : this(TimeProvider.System)
    {
    }

    public InMemoryPipelineAbortRegistry(TimeProvider time)
    {
        _time = time;
    }

    public Task AbortAsync(string? facilityId, string? reportId, TimeSpan timeToLive, CancellationToken cancellationToken = default)
    {
        if (timeToLive <= TimeSpan.Zero)
            timeToLive = TimeSpan.FromDays(14);

        var expires = _time.GetUtcNow().Add(timeToLive).UtcTicks;
        Add(FacilityKey(facilityId), expires);
        Add(ReportKey(reportId), expires);
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

    private void Add(string? key, long expiresAtTicks)
    {
        if (!string.IsNullOrWhiteSpace(key))
            _expiresAtTicks[key] = expiresAtTicks;
    }

    private bool Has(string? key)
    {
        if (string.IsNullOrWhiteSpace(key))
            return false;
        if (!_expiresAtTicks.TryGetValue(key, out var expiresAtTicks))
            return false;
        if (expiresAtTicks > _time.GetUtcNow().UtcTicks)
            return true;
        _expiresAtTicks.TryRemove(key, out _);
        return false;
    }

    private void Remove(string? key)
    {
        if (!string.IsNullOrWhiteSpace(key))
            _expiresAtTicks.TryRemove(key, out _);
    }

    internal static string? FacilityKey(string? facilityId) =>
        string.IsNullOrWhiteSpace(facilityId) ? null : $"link:pipeline-abort:facility:{facilityId.Trim()}";

    internal static string? ReportKey(string? reportId) =>
        string.IsNullOrWhiteSpace(reportId) ? null : $"link:pipeline-abort:report:{reportId.Trim()}";
}
