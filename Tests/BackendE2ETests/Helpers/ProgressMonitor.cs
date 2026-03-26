using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models.Enums;
using LantanaGroup.Link.Report.Domain.Enums;
using Xunit.Abstractions;

namespace LantanaGroup.Link.Tests.E2ETests.Helpers;

/// <summary>
/// Central progress aggregation hub for test pipeline output.
/// Combines database state monitoring, progress bar rendering, Loki service
/// activity queries, stall detection, and periodic heartbeats into a single
/// coherent stream of test output.
///
/// Data sources:
///   - Report DB: schedule status, report entries, submission/validation state
///   - DataAcquisition DB: acquisition logs, completion/failure counts
///   - Loki (MeasureEval): resource processing throughput and active patients
///   - PipelineProgressTracker: overall progress bar and stall detection
/// </summary>
public class ProgressMonitor
{
    private const int HeartbeatInterval = 12; // ~60s at 5s poll interval
    private static readonly TimeSpan StallWarningThreshold = TimeSpan.FromSeconds(120);

    private readonly ITestOutputHelper _output;
    private readonly PipelineProgressTracker? _progressTracker;
    private readonly LokiScraper? _lokiScraper;

    private string? _lastScheduleStatus;
    private int _lastReportEntryCount;
    private string? _lastReportBreakdown;
    private int _lastAcqLogCount;
    private int _lastCompletedAcqCount;
    private string? _lastAcqBreakdown;
    private int _checkCount;
    private readonly DateTime _startTime = DateTime.UtcNow;

    /// <summary>
    /// Returns the pipeline stage that appears stalled, or null if progress
    /// is still advancing.
    /// </summary>
    public string? StalledStage => _progressTracker?.StalledStage;

    /// <summary>
    /// How long the pipeline has been stalled at the same progress.
    /// </summary>
    public TimeSpan StallDuration => _progressTracker?.StallDuration ?? TimeSpan.Zero;

    public ProgressMonitor(ITestOutputHelper output, int expectedPatientCount = 0, LokiScraper? lokiScraper = null)
    {
        _output = output;
        _lokiScraper = lokiScraper;
        _progressTracker = expectedPatientCount > 0
            ? new PipelineProgressTracker(output, expectedPatientCount)
            : null;
    }

    /// <summary>
    /// Runs a single progress check cycle: database state, progress bar,
    /// heartbeat with service activity, and stall detection.
    /// Returns true if a critical failure is detected.
    /// </summary>
    public async Task<bool> CheckProgressAsync(string facilityId, string reportId)
    {
        _checkCount++;
        var hasCriticalFailure = false;

        hasCriticalFailure |= await CheckReportProgress(facilityId, reportId);
        hasCriticalFailure |= await CheckDataAcquisitionProgress(facilityId, reportId);

        if (_progressTracker != null)
        {
            await _progressTracker.UpdateAsync(facilityId, reportId);
        }

        if (_checkCount % HeartbeatInterval == 0)
        {
            await PrintHeartbeatAsync();
        }

        return hasCriticalFailure;
    }

    private async Task PrintHeartbeatAsync()
    {
        var elapsed = DateTime.UtcNow - _startTime;
        var heartbeat = $"[DIAG] Heartbeat -- {elapsed.TotalSeconds:F0}s elapsed, " +
                        $"schedule={_lastScheduleStatus ?? "?"}, " +
                        $"entries={_lastReportEntryCount}, acqLogs={_lastAcqLogCount}";

        if (_progressTracker != null && StallDuration > StallWarningThreshold && StalledStage != null)
        {
            heartbeat += $" | STALLED at '{StalledStage}' for {StallDuration.TotalSeconds:F0}s";
        }

        _output.WriteLine(heartbeat);

        // When the DB progress bar isn't moving, query Loki for service activity
        if (_lokiScraper != null && _progressTracker?.StalledStage != null)
        {
            var activity = await _lokiScraper.GetMeasureEvalActivitySummaryAsync(TimeSpan.FromSeconds(60));
            if (activity != null)
            {
                _output.WriteLine($"[DIAG] MeasureEval: {activity}");
            }
        }
    }

    private async Task<bool> CheckReportProgress(string facilityId, string reportId)
    {
        try
        {
            await using var db = DatabaseConnectionFactory.CreateReportDbContext();
            var scheduleId = Guid.Parse(reportId);

            var schedule = await PipelineSnapshot.GetReportScheduleAsync(db, scheduleId);
            if (schedule == null)
            {
                if (_lastScheduleStatus != "NOT_FOUND")
                {
                    _output.WriteLine($"[DIAG][Report] Schedule {reportId} not yet created in database");
                    _lastScheduleStatus = "NOT_FOUND";
                }
                return false;
            }

            var currentStatus = schedule.Status.ToString();
            if (currentStatus != _lastScheduleStatus)
            {
                _output.WriteLine($"[DIAG][Report] Schedule status changed: {_lastScheduleStatus ?? "(none)"} -> {currentStatus}");
                _lastScheduleStatus = currentStatus;
            }

            var entries = await PipelineSnapshot.GetReportEntriesAsync(db, scheduleId);

            var total = entries.Count;
            var submitted = entries.Count(e => e.SubmissionStatus == SubmissionStatus.Submitted);
            var pending = entries.Count(e => e.SubmissionStatus == SubmissionStatus.PendingValidation);
            var submitting = entries.Count(e => e.SubmissionStatus == SubmissionStatus.Submitting);
            var failed = entries.Count(e => e.SubmissionStatus == SubmissionStatus.FailedSubmission);

            var identified = entries.Count(e => e.ReportingStatus == ReportingStatus.PatientIdentified);
            var pendingValidation = entries.Count(e => e.ReportingStatus == ReportingStatus.PendingValidation);
            var passedValidation = entries.Count(e => e.ReportingStatus == ReportingStatus.PassedValidation);
            var failedValidation = entries.Count(e => e.ReportingStatus == ReportingStatus.FailedValidation);

            var breakdown = $"identified={identified}, pendingValidation={pendingValidation}, " +
                            $"passed={passedValidation}, failedValidation={failedValidation} | " +
                            $"Submission: pending={pending}, submitting={submitting}, " +
                            $"submitted={submitted}, failed={failed}";

            if (total != _lastReportEntryCount || breakdown != _lastReportBreakdown)
            {
                _output.WriteLine($"[DIAG][Report] Entries: {total} total | Reporting: {breakdown}");
                _lastReportEntryCount = total;
                _lastReportBreakdown = breakdown;
            }

            if (failed > 0)
            {
                _output.WriteLine($"[DIAG][Report] CRITICAL: {failed} entry/entries have FailedSubmission status!");
                return true;
            }
        }
        catch (Exception ex)
        {
            _output.WriteLine($"[DIAG][Report] Check error: {ex.Message}");
        }

        return false;
    }

    private async Task<bool> CheckDataAcquisitionProgress(string facilityId, string reportId)
    {
        try
        {
            await using var db = DatabaseConnectionFactory.CreateDataAcquisitionDbContext();

            var logs = await PipelineSnapshot.GetAcquisitionLogsAsync(db, facilityId, reportId);

            var total = logs.Count;
            var completed = logs.Count(l => l.Status == RequestStatus.Completed);
            var failed = logs.Count(l => l.Status == RequestStatus.Failed);
            var maxRetries = logs.Count(l => l.Status == RequestStatus.MaxRetriesReached);
            var processing = logs.Count(l => l.Status == RequestStatus.Processing);
            var pending = logs.Count(l => l.Status == RequestStatus.Pending);

            var breakdown = $"completed={completed}, processing={processing}, " +
                            $"pending={pending}, failed={failed}, maxRetries={maxRetries}";

            if (total != _lastAcqLogCount || completed != _lastCompletedAcqCount || breakdown != _lastAcqBreakdown)
            {
                _output.WriteLine($"[DIAG][DataAcq] Logs: {total} total | {breakdown}");
                _lastAcqLogCount = total;
                _lastCompletedAcqCount = completed;
                _lastAcqBreakdown = breakdown;
            }

            if (failed > 0 || maxRetries > 0)
            {
                var problemLogs = logs
                    .Where(l => l.Status == RequestStatus.Failed || l.Status == RequestStatus.MaxRetriesReached)
                    .Take(5)
                    .ToList();

                foreach (var log in problemLogs)
                {
                    var notes = log.Notes.Count > 0 ? string.Join(" | ", log.Notes.Take(3)) : "(no notes)";
                    _output.WriteLine($"[DIAG][DataAcq] CRITICAL: FAILED Log Id={log.Id}, Patient={log.PatientId}, " +
                                     $"Status={log.Status}, Phase={log.QueryPhase}, Notes={notes}");
                }

                return true;
            }
        }
        catch (Exception ex)
        {
            _output.WriteLine($"[DIAG][DataAcq] Check error: {ex.Message}");
        }

        return false;
    }
}
