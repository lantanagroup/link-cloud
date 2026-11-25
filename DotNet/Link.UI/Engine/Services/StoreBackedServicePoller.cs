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
    /// <summary>Per-patient domains (entry list, populations, logs, measure resources) are read this often, not every tick.</summary>
    public static readonly TimeSpan FullPollInterval = TimeSpan.FromSeconds(30);
    private DateTime _lastFullPollUtc = DateTime.MinValue;
    private PipelineDataReader.ReportEntryRollup? _lastRollup;
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

    private async Task PollAllDomainsAsync(Guid scheduleId, CancellationToken ct, bool forceFull = false)
    {
        // Every tick reads only per-report aggregates. The per-patient payloads are read on a slower
        // cadence and once more at the end, so a long run never re-reads every entry every few seconds.
        var full = forceFull || DateTime.UtcNow - _lastFullPollUtc >= FullPollInterval;
        if (full)
            _lastFullPollUtc = DateTime.UtcNow;

        var tasks = new List<Task>
        {
            PollDomainAsync("schedule", () => PollScheduleAsync(scheduleId, ct)),
            PollDomainAsync("entries", () => PollEntriesAsync(scheduleId, full, ct)),
            PollDomainAsync("acquisitionSummary", () => PollAcquisitionAsync(ct)),
            PollDomainAsync("orgLocation", () => PollOrgLocationAsync(ct))
        };
        if (full)
        {
            tasks.Add(PollDomainAsync("populations", () => PollPopulationsAsync(scheduleId, ct)));
            tasks.Add(PollDomainAsync("acquisitionLogs", () => PollAcquisitionLogsAsync(ct)));
            tasks.Add(PollDomainAsync("measureResources", () => PollMeasureEvalResourcesAsync(scheduleId, ct)));
        }

        await Task.WhenAll(tasks);
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
    public async Task FinalPollAsync(CancellationToken cancellationToken = default)
    {
        if (!Guid.TryParse(_meta.ReportId, out var scheduleId))
            return;

        try
        {
            await PollAllDomainsAsync(scheduleId, cancellationToken, forceFull: true);
            _logger.LogInformation("[Run {RunId}] Final domain snapshot persisted", _meta.RunId);
        }
        catch (Exception ex)
        {
            await ReportPollFailureAsync("final", ex);
        }
    }

    private async Task<bool> ReportStillCurrentAsync(CancellationToken ct)
    {
        var meta = await _store.GetRunMetaAsync(_meta.RunId, ct);
        return meta != null
            && string.Equals(meta.ReportId, _meta.ReportId, StringComparison.Ordinal)
            && string.Equals(meta.FacilityId, _meta.FacilityId, StringComparison.Ordinal);
    }

    private async Task PollScheduleAsync(Guid scheduleId, CancellationToken ct)
    {
        if (!await ReportStillCurrentAsync(ct))
            return;

        var result = await _reader.GetReportScheduleAsync(scheduleId, ct);
        await _store.SetDomainAsync(_meta.RunId, "schedule", result, ct);
    }

    private async Task PollEntriesAsync(Guid scheduleId, bool full, CancellationToken ct)
    {
        if (!await ReportStillCurrentAsync(ct))
            return;

        if (full || _lastRollup is null)
        {
            var result = await _reader.GetReportEntriesWithMeasureReportsAsync(scheduleId, ct);
            _lastRollup = PipelineDataReader.ReportEntryRollup.From(result);
            await _store.SetDomainAsync(_meta.RunId, "entries", _lastRollup, ct);
            return;
        }

        var counts = await _reader.GetReportEntryStatusCountsAsync(scheduleId, ct);
        if (counts is null)
            return;
        _lastRollup = MergeCounts(_lastRollup, counts.Value.Reporting, counts.Value.Submission);
        await _store.SetDomainAsync(_meta.RunId, "entries", _lastRollup, ct);
    }

    /// <summary>Fresh status counts over the last full rollup; measure-level fields wait for the next full read.</summary>
    public static PipelineDataReader.ReportEntryRollup MergeCounts(
        PipelineDataReader.ReportEntryRollup last,
        IReadOnlyDictionary<string, int> reporting,
        IReadOnlyDictionary<string, int> submission)
    {
        int R(string key) => reporting.Where(p => string.Equals(p.Key, key, StringComparison.OrdinalIgnoreCase)).Sum(p => p.Value);
        var total = reporting.Values.Sum();
        return last with
        {
            EntryCount = total,
            ReportingStatuses = reporting.Where(p => p.Value > 0).Select(p => new PipelineDataReader.StatusCountInfo(p.Key, p.Value)).ToList(),
            SubmissionStatuses = submission.Where(p => p.Value > 0).Select(p => new PipelineDataReader.StatusCountInfo(p.Key, p.Value)).ToList(),
            ValidatedCount = R("PassedValidation") + R("FailedValidation"),
            AllReportingTerminal = total > 0 && R("PatientIdentified") + R("PendingValidation") == 0
        };
    }

    private async Task PollPopulationsAsync(Guid scheduleId, CancellationToken ct)
    {
        if (!await ReportStillCurrentAsync(ct))
            return;

        var result = await _reader.GetReportPopulationsAsync(scheduleId, _meta.FacilityId, ct);
        await _store.SetDomainAsync(_meta.RunId, "populations", RunHistorySlim.ToPopulationCounts(result), ct);
    }

    private async Task PollAcquisitionAsync(CancellationToken ct)
    {
        if (!await ReportStillCurrentAsync(ct))
            return;

        var summary = await _reader.GetDataAcquisitionReportSummaryAsync(_meta.ReportId, ct);

        // Always write, even when null, so stale data from a prior report
        // (e.g., before regeneration cleared snapshots) is overwritten.
        await _store.SetDomainAsync(_meta.RunId, "acquisitionSummary", summary, ct);
    }

    private async Task PollAcquisitionLogsAsync(CancellationToken ct)
    {
        if (!await ReportStillCurrentAsync(ct))
            return;

        var logs = await _reader.GetAcquisitionLogsAsync(_meta.FacilityId, _meta.ReportId, ct);
        var withNotes = await _reader.AttachFailureNotesAsync(logs, RunHistorySlim.FailureSampleIds(logs), ct);
        await _store.SetDomainAsync(_meta.RunId, "acquisitionLogs", RunHistorySlim.ToAcquisitionChart(withNotes), ct);
    }

    private async Task PollOrgLocationAsync(CancellationToken ct)
    {
        if (!await ReportStillCurrentAsync(ct))
            return;

        var snapshot = new OrgLocationSnapshot(
            await _reader.GetOrganizationLocationConfigurationsAsync(_meta.FacilityId, ct),
            await _reader.GetOrganizationLocationMappingsAsync(_meta.FacilityId, ct),
            await _reader.GetEncounterMappingsAsync(_meta.FacilityId, ct));
        await _store.SetDomainAsync(_meta.RunId, "orgLocation", RunHistorySlim.SlimOrgLocation(snapshot), ct);
    }

    private async Task PollMeasureEvalResourcesAsync(Guid scheduleId, CancellationToken ct)
    {
        if (!await ReportStillCurrentAsync(ct))
            return;

        var result = await _reader.GetMeasureEvalResourceCountsByPatientTypeAsync(scheduleId, ct);
        await _store.SetDomainAsync(_meta.RunId, "measureResources", RunHistorySlim.SlimMeasureResources(result), ct);
    }
}
