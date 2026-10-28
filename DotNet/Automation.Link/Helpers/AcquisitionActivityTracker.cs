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
    /// <summary>
    /// Extra wait allowed after the original hard timeout while acquisition is
    /// still logging progress and validation is not the stage holding the run.
    /// Validation that is still working is not capped: cancel stops that run.
    /// </summary>
    public static readonly TimeSpan MaxExtraDuration = TimeSpan.FromHours(6);

    public readonly record struct PollDecision(
        bool Continue,
        DateTime Deadline,
        bool HeldForValidation,
        TimeSpan ExtendedBy);

    private int _lastLogCount = -1;
    private int _lastCompleted = -1;
    private int _lastResources = -1;
    private string? _lastBreakdown;

    public DateTime LastProgressUtc { get; private set; }
    public int LastResourcesAcquired { get; private set; }
    public bool InFlight { get; private set; }

    /// <summary>
    /// Records progress from a signal other than the DA report summary,
    /// such as FHIR paging or Validation heartbeat INFO logs scraped from Loki.
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
    /// Decides whether a submission poll that has reached <paramref name="deadline"/>
    /// keeps waiting. Ongoing validation is not on a clock. Other in-flight work
    /// can still slide the deadline up to <see cref="MaxExtraDuration"/> past the
    /// configured hard timeout. An idle pipeline stops.
    /// </summary>
    public static PollDecision Decide(
        DateTime utcNow,
        DateTime phaseStart,
        TimeSpan hardTimeout,
        DateTime deadline,
        bool validationOngoing,
        bool hasRecentProgress)
    {
        if (hardTimeout <= TimeSpan.Zero || hardTimeout == TimeSpan.MaxValue || utcNow < deadline)
            return new PollDecision(true, deadline, false, TimeSpan.Zero);

        if (validationOngoing)
        {
            var next = utcNow + DeadlineExtension;
            return new PollDecision(true, next, true, next - utcNow);
        }

        if (!TryExtendDeadline(utcNow, phaseStart, hardTimeout, hasRecentProgress, ref deadline, out var extendedBy))
            return new PollDecision(false, deadline, false, TimeSpan.Zero);

        return new PollDecision(true, deadline, false, extendedBy);
    }

    /// <summary>
    /// When the poll loop would otherwise time out, slide the deadline forward
    /// if acquisition is still logging progress. Caps total wait at
    /// <paramref name="hardTimeout"/> + <see cref="MaxExtraDuration"/>.
    /// Ongoing validation does not use this cap; <see cref="Decide"/> holds the
    /// run open instead.
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
