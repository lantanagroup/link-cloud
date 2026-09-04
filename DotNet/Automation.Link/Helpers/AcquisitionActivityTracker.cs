namespace LantanaGroup.Link.Automation.Link.Helpers;

/// <summary>
/// Tracks Data Acquisition activity across poll cycles so a long-running
/// acquisition (one large patient, thousands of resources, multi-page FHIR
/// searches) is treated as progress instead of a stall or a hard timeout.
/// </summary>
public sealed class AcquisitionActivityTracker
{
    public static readonly TimeSpan ProgressWindow = TimeSpan.FromMinutes(2);
    public static readonly TimeSpan DeadlineExtension = TimeSpan.FromMinutes(5);
    public static readonly TimeSpan MaxExtraDuration = TimeSpan.FromMinutes(60);

    private int _lastLogCount = -1;
    private int _lastCompleted = -1;
    private int _lastResources = -1;
    private string? _lastBreakdown;

    public DateTime LastProgressUtc { get; private set; }
    public int LastResourcesAcquired { get; private set; }
    public bool InFlight { get; private set; }

    /// <summary>
    /// Records progress from a signal other than the DA report summary,
    /// such as FHIR paging INFO logs scraped from Loki.
    /// </summary>
    public void MarkProgress(DateTime utcNow) => LastProgressUtc = utcNow;

    public readonly record struct Observation(
        bool ShouldLogStatus,
        bool ShouldLogKeepAlive,
        int ResourceDelta,
        int ResourcesAcquired,
        int Processing,
        int Pending,
        int Completed,
        int TotalLogs,
        string Breakdown);

    public Observation Observe(
        int totalLogs,
        int completed,
        int processing,
        int pending,
        int failed,
        int maxRetries,
        int resourcesAcquired,
        DateTime utcNow)
    {
        var breakdown =
            $"completed={completed}, processing={processing}, pending={pending}, failed={failed}, maxRetries={maxRetries}";
        var statusChanged = totalLogs != _lastLogCount
            || completed != _lastCompleted
            || breakdown != _lastBreakdown;
        var resourcesGrew = _lastResources >= 0 && resourcesAcquired > _lastResources;
        var resourceDelta = _lastResources >= 0 ? Math.Max(resourcesAcquired - _lastResources, 0) : 0;

        InFlight = processing > 0;
        if (statusChanged || resourcesGrew)
            LastProgressUtc = utcNow;
        LastResourcesAcquired = resourcesAcquired;

        var shouldLogKeepAlive = resourcesGrew && !statusChanged;

        _lastLogCount = totalLogs;
        _lastCompleted = completed;
        _lastResources = resourcesAcquired;
        _lastBreakdown = breakdown;

        return new Observation(
            ShouldLogStatus: statusChanged,
            ShouldLogKeepAlive: shouldLogKeepAlive,
            ResourceDelta: resourceDelta,
            ResourcesAcquired: resourcesAcquired,
            Processing: processing,
            Pending: pending,
            Completed: completed,
            TotalLogs: totalLogs,
            Breakdown: breakdown);
    }

    public bool HasRecentProgress(TimeSpan window, DateTime utcNow)
        => LastProgressUtc != default && utcNow - LastProgressUtc <= window;

    /// <summary>
    /// When the poll loop would otherwise time out, slide the deadline forward
    /// if acquisition is still producing resources. Caps total wait at
    /// <paramref name="hardTimeout"/> + <see cref="MaxExtraDuration"/>.
    /// </summary>
    public static bool TryExtendDeadline(
        DateTime utcNow,
        DateTime startUtc,
        TimeSpan hardTimeout,
        bool hasRecentProgress,
        ref DateTime deadline,
        out TimeSpan extendedBy)
    {
        extendedBy = TimeSpan.Zero;
        if (hardTimeout <= TimeSpan.Zero || hardTimeout == TimeSpan.MaxValue)
            return false;
        if (utcNow < deadline)
            return false;
        if (!hasRecentProgress)
            return false;

        var maxDeadline = startUtc + hardTimeout + MaxExtraDuration;
        if (deadline >= maxDeadline)
            return false;

        var next = utcNow + DeadlineExtension;
        if (next > maxDeadline)
            next = maxDeadline;
        if (next <= deadline)
            return false;

        extendedBy = next - utcNow;
        deadline = next;
        return true;
    }
}
