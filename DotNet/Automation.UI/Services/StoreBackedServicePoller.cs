using Automation.UI.Services.Persistence;
using LantanaGroup.Link.Automation.Link.Helpers;

namespace Automation.UI.Services;

/// <summary>
/// Per-run poller that fetches all service domains on a single cadence and
/// persists results to the <see cref="ISnapshotStore"/> (MongoDB).
///
/// Data persists across process restarts. Multiple UI instances can read
/// the same data. The controller reads are instant (no API calls).
/// </summary>
public sealed class StoreBackedServicePoller
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(5);

    private readonly ISnapshotStore _store;
    private readonly PipelineDataReader _reader;
    private readonly RunSnapshotMeta _meta;
    private readonly ILogger _logger;

    public StoreBackedServicePoller(
        ISnapshotStore store,
        PipelineDataReader reader,
        RunSnapshotMeta meta,
        ILogger logger)
    {
        _store = store;
        _reader = reader;
        _meta = meta;
        _logger = logger;
    }

    public string FacilityId => _meta.FacilityId;
    public string ReportId => _meta.ReportId;

    public async Task RunAsync(CancellationToken ct)
    {
        if (!Guid.TryParse(_meta.ReportId, out var scheduleId))
        {
            _logger.LogWarning("Run {RunId} has invalid ReportId {ReportId}, poller will not start", _meta.RunId, _meta.ReportId);
            return;
        }

        await Task.Delay(TimeSpan.FromMilliseconds(Random.Shared.Next(100, 800)), ct);
        var firstSuccess = true;

        while (!ct.IsCancellationRequested)
        {
            try
            {
                await PollAllDomainsAsync(scheduleId, ct);
                if (firstSuccess)
                {
                    _logger.LogInformation("[Run {RunId}] First poll success — all domains now persisted", _meta.RunId);
                    firstSuccess = false;
                }
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                await ReportPollFailureAsync("all", ex);
            }

            try
            {
                await Task.Delay(PollInterval, ct);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private async Task PollAllDomainsAsync(Guid scheduleId, CancellationToken ct)
    {
        await Task.WhenAll(
            PollDomainAsync("schedule", () => PollScheduleAsync(scheduleId, ct)),
            PollDomainAsync("entries", () => PollEntriesAsync(scheduleId, ct)),
            PollDomainAsync("populations", () => PollPopulationsAsync(scheduleId, ct)),
            PollDomainAsync("acquisitionSummary", () => PollAcquisitionAsync(ct)),
            PollDomainAsync("measureResources", () => PollMeasureEvalResourcesAsync(scheduleId, ct)));
    }

    private async Task PollDomainAsync(string domain, Func<Task> action)
    {
        try
        {
            await action();
        }
        catch (OperationCanceledException)
        {
            // Expected during shutdown — not an error.
        }
        catch (Exception ex)
        {
            await ReportPollFailureAsync(domain, ex);
        }
    }

    private async Task ReportPollFailureAsync(string domain, Exception ex)
    {
        _logger.LogError(ex, "[Run {RunId}][{Domain}] Poll failure", _meta.RunId, domain);

        var line = $"[{DateTimeOffset.Now:HH:mm:ss}] [Poller][{domain}] ERROR: {ex.GetType().Name}: {ex.Message}";
        try
        {
            await _store.AppendLogsAsync(_meta.RunId, [line], CancellationToken.None);
        }
        catch
        {
            // secondary failure while reporting a failure
        }
    }

    /// <summary>
    /// Performs one final poll of all domains and writes them to the store.
    /// Called at run completion to guarantee the last state is persisted
    /// even if the polling loop was between cycles when cancellation hit.
    /// </summary>
    public async Task FinalPollAsync()
    {
        if (!Guid.TryParse(_meta.ReportId, out var scheduleId))
            return;

        var ct = CancellationToken.None;
        try
        {
            await PollAllDomainsAsync(scheduleId, ct);
            _logger.LogInformation("[Run {RunId}] Final domain snapshot persisted", _meta.RunId);
        }
        catch (Exception ex)
        {
            await ReportPollFailureAsync("final", ex);
        }
    }

    private async Task PollScheduleAsync(Guid scheduleId, CancellationToken ct)
    {
        var result = await _reader.GetReportScheduleAsync(scheduleId);
        await _store.SetDomainAsync(_meta.RunId, "schedule", result, ct);
    }

    private async Task PollEntriesAsync(Guid scheduleId, CancellationToken ct)
    {
        var result = await _reader.GetReportEntriesWithMeasureReportsAsync(scheduleId);
        await _store.SetDomainAsync(_meta.RunId, "entries", result, ct);
    }

    private async Task PollPopulationsAsync(Guid scheduleId, CancellationToken ct)
    {
        var result = await _reader.GetReportPopulationsAsync(scheduleId, _meta.FacilityId);
        await _store.SetDomainAsync(_meta.RunId, "populations", result, ct);
    }

    private async Task PollAcquisitionAsync(CancellationToken ct)
    {
        var summary = await _reader.GetDataAcquisitionReportSummaryAsync(_meta.ReportId);

        // Always write — even when null — so stale data from a prior report
        // (e.g., before regeneration cleared snapshots) is overwritten.
        await _store.SetDomainAsync(_meta.RunId, "acquisitionSummary", summary, ct);
    }

    private async Task PollMeasureEvalResourcesAsync(Guid scheduleId, CancellationToken ct)
    {
        var result = await _reader.GetMeasureEvalResourceCountsByPatientTypeAsync(scheduleId);
        await _store.SetDomainAsync(_meta.RunId, "measureResources", result, ct);
    }
}
