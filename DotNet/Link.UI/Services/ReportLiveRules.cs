using Link.UI.Models;

namespace Link.UI.Services;

/// <summary>
/// The report live region polls every few seconds. One failed read must not mark the
/// region final: live-region.js clears the timer on data-au-final and nothing starts
/// it again. A confirmed missing report or a terminal status still stops. A failed
/// read is tried once more after <see cref="RetryBackoff"/>; if it is still failing,
/// the endpoint returns 503 so the page stays and the 5 second timer keeps going.
/// </summary>
public static class ReportLiveRules
{
    public static readonly TimeSpan RetryBackoff = TimeSpan.FromSeconds(1);

    /// <summary>The report was not read. Distinct from a confirmed missing report.</summary>
    public static bool IsTransient(ReportDetailModel page) =>
        page.Report is null
        && !page.NotFound
        && !string.IsNullOrWhiteSpace(page.LoadError)
        && page.LoadError != FacilityViewService.ReportNotConfigured;

    /// <summary>Try the read once more. A missing report and a finished report do not retry.</summary>
    public static bool ShouldRetry(ReportDetailModel page) =>
        IsTransient(page)
        || (page.Report is not null
            && page.Progress is null
            && !ReportProgressService.IsTerminal(page.Report.Status));

    /// <summary>True when the partial should carry data-au-final.</summary>
    public static bool StopPolling(ReportDetailModel page)
    {
        if (IsTransient(page))
            return false;
        if (page.Report is null)
            return true;
        if (page.Progress?.Terminal == true)
            return true;
        return ReportProgressService.IsTerminal(page.Report.Status);
    }

    public static async Task<ReportDetailModel> LoadWithRetryAsync(
        Func<CancellationToken, Task<ReportDetailModel>> load,
        Func<ReportDetailModel, CancellationToken, Task> attachProgress,
        TimeSpan backoff,
        CancellationToken cancellationToken)
    {
        var page = await load(cancellationToken);
        await attachProgress(page, cancellationToken);
        if (!ShouldRetry(page))
            return page;

        await Task.Delay(backoff, cancellationToken);
        page = await load(cancellationToken);
        await attachProgress(page, cancellationToken);
        return page;
    }
}
