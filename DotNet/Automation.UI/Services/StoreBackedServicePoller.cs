using Automation.UI.Services.Persistence;
using LantanaGroup.Link.Automation.Link.Helpers;

namespace Automation.UI.Services;

/// <summary>
/// Per-run poller. Every run polls the pipeline so Run Details stays live.
/// Metrics runs poll every 5s; ordinary runs poll every 15s. Both honor the 8s HTTP
/// cache. A final poll still runs once in <see cref="FinalPollAsync"/>.
///
/// The store keeps the counts, timestamps, and milestones the charts need.
/// Notes, acquired resource ids, FHIR queries, measure-report id lists,
/// per-patient measure rows, and org-location rows stay in Report and Data Acquisition.
/// </summary>
public sealed class StoreBackedServicePoller
{
    private readonly ISnapshotStore _store;
    private readonly PipelineDataReader _reader;
    private readonly RunSnapshotMeta _meta;
    private readonly ILogger _logger;
    private readonly IAutomationUiMetrics? _metrics;
    private readonly TimeSpan _pollInterval;
    private readonly bool _pollAllDomains;

    public StoreBackedServicePoller(
        ISnapshotStore store,
        PipelineDataReader reader,
        RunSnapshotMeta meta,
        ILogger logger,
        IAutomationUiMetrics? metrics = null)
    {
        _store = store;
        _reader = reader;
        _meta = meta;
        _logger = logger;
        _metrics = metrics;
        _pollInterval = AutomationRunPollingPolicy.PollerInterval(meta.IsMetricsRun);
        _pollAllDomains = AutomationRunPollingPolicy.PollAllDomainsDuringRun(meta.IsMetricsRun);
    }

    public string FacilityId => _meta.FacilityId;
    public string ReportId => _meta.ReportId;
    public bool IsMetricsRun => _meta.IsMetricsRun;

    public sealed record OrgLocationSnapshot(
        List<PipelineDataReader.OrganizationLocationConfigurationInfo> Configurations,
        List<PipelineDataReader.OrganizationLocationMappingInfo> LocationMappings,
        List<PipelineDataReader.EncounterMappingInfo> EncounterMappings);

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
                if (_pollAllDomains)
                    await PollAllDomainsAsync(scheduleId, ct);
                else
                    await PollDomainAsync("schedule", () => PollScheduleAsync(scheduleId, ct));

                if (firstSuccess)
                {
                    _logger.LogInformation(
                        "[Run {RunId}] First poll success ({Mode})",
                        _meta.RunId,
                        _pollAllDomains ? "all domains" : "schedule only");
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
                await Task.Delay(_pollInterval, ct);
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
            PollDomainAsync("acquisitionLogs", () => PollAcquisitionLogsAsync(ct)),
            PollDomainAsync("orgLocation", () => PollOrgLocationAsync(ct)),
            PollDomainAsync("measureResources", () => PollMeasureEvalResourcesAsync(scheduleId, ct)));
    }

    private async Task PollDomainAsync(string domain, Func<Task> action)
    {
        try
        {
            await action();
            _metrics?.IncrementPollerHttp(domain, "success");
        }
        catch (OperationCanceledException)
        {
            // Expected during shutdown - not an error.
        }
        catch (Exception ex)
        {
            _metrics?.IncrementPollerHttp(domain, "failure");
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

    private async Task<long?> EpochBeforeReadAsync(CancellationToken ct)
    {
        var meta = await _store.GetRunMetaAsync(_meta.RunId, ct);
        if (meta == null
            || !string.Equals(meta.ReportId, _meta.ReportId, StringComparison.Ordinal)
            || !string.Equals(meta.FacilityId, _meta.FacilityId, StringComparison.Ordinal))
            return null;

        return meta.SnapshotEpoch;
    }

    private async Task PollScheduleAsync(Guid scheduleId, CancellationToken ct)
    {
        var epoch = await EpochBeforeReadAsync(ct);
        if (epoch == null)
            return;

        var result = await _reader.GetReportScheduleAsync(scheduleId, ct);
        await _store.SetDomainAsync(_meta.RunId, "schedule", result, epoch.Value, ct);
    }

    private async Task PollEntriesAsync(Guid scheduleId, CancellationToken ct)
    {
        var epoch = await EpochBeforeReadAsync(ct);
        if (epoch == null)
            return;

        var result = await _reader.GetReportEntriesWithMeasureReportsAsync(scheduleId, ct);
        await _store.SetDomainAsync(_meta.RunId, "entries", result, epoch.Value, ct);
    }

    private async Task PollPopulationsAsync(Guid scheduleId, CancellationToken ct)
    {
        var epoch = await EpochBeforeReadAsync(ct);
        if (epoch == null)
            return;

        var result = await _reader.GetReportPopulationsAsync(scheduleId, _meta.FacilityId, ct);
        await _store.SetDomainAsync(_meta.RunId, "populations", RunHistorySlim.ToPopulationCounts(result), epoch.Value, ct);
    }

    private async Task PollAcquisitionAsync(CancellationToken ct)
    {
        var epoch = await EpochBeforeReadAsync(ct);
        if (epoch == null)
            return;

        var summary = await _reader.GetDataAcquisitionReportSummaryAsync(_meta.ReportId, ct);

        // Always write � even when null � so stale data from a prior report
        // (e.g., before regeneration cleared snapshots) is overwritten.
        await _store.SetDomainAsync(_meta.RunId, "acquisitionSummary", summary, epoch.Value, ct);
    }

    private async Task PollAcquisitionLogsAsync(CancellationToken ct)
    {
        var epoch = await EpochBeforeReadAsync(ct);
        if (epoch == null)
            return;

        var logs = await _reader.GetAcquisitionLogsAsync(_meta.FacilityId, _meta.ReportId, ct);
        var withNotes = await _reader.AttachFailureNotesAsync(logs, RunHistorySlim.FailureSampleIds(logs), ct);
        await _store.SetDomainAsync(_meta.RunId, "acquisitionLogs", RunHistorySlim.ToAcquisitionChart(withNotes), epoch.Value, ct);
    }

    private async Task PollOrgLocationAsync(CancellationToken ct)
    {
        var epoch = await EpochBeforeReadAsync(ct);
        if (epoch == null)
            return;

        var snapshot = new OrgLocationSnapshot(
            await _reader.GetOrganizationLocationConfigurationsAsync(_meta.FacilityId, ct),
            await _reader.GetOrganizationLocationMappingsAsync(_meta.FacilityId, ct),
            await _reader.GetEncounterMappingsAsync(_meta.FacilityId, ct));
        await _store.SetDomainAsync(_meta.RunId, "orgLocation", RunHistorySlim.SlimOrgLocation(snapshot), epoch.Value, ct);
    }

    private async Task PollMeasureEvalResourcesAsync(Guid scheduleId, CancellationToken ct)
    {
        var epoch = await EpochBeforeReadAsync(ct);
        if (epoch == null)
            return;

        var result = await _reader.GetMeasureEvalResourceCountsByPatientTypeAsync(scheduleId, ct);
        await _store.SetDomainAsync(_meta.RunId, "measureResources", RunHistorySlim.SlimMeasureResources(result), epoch.Value, ct);
    }
}
