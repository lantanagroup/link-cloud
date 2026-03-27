using LantanaGroup.Link.Automation.Configuration;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models.Enums;
using LantanaGroup.Link.Report.Domain.Enums;

namespace LantanaGroup.Link.Automation.Helpers;

/// <summary>
/// Central progress aggregation hub for test pipeline output.
/// Combines database state monitoring, progress bar rendering, and stall detection
/// into a single coherent stream of test output.
/// </summary>
public class ProgressMonitor
{
    private readonly IAutomationOutput _output;
    private readonly PipelineProgressTracker? _progressTracker;
    private readonly PipelineDataReader _reader;

    private string? _lastScheduleStatus;
    private int _lastReportEntryCount;
    private string? _lastReportBreakdown;
    private int _lastAcqLogCount;
    private int _lastCompletedAcqCount;
    private string? _lastAcqBreakdown;

    /// <summary>
    /// Returns the pipeline stage that appears stalled, or null if progress
    /// is still advancing.
    /// </summary>
    public string? StalledStage => _progressTracker?.StalledStage;

    /// <summary>
    /// How long the pipeline has been stalled at the same progress.
    /// </summary>
    public TimeSpan StallDuration => _progressTracker?.StallDuration ?? TimeSpan.Zero;

    public ProgressMonitor(IAutomationOutput output, int expectedPatientCount, LokiScraper? lokiScraper, AutomationConfig config)
    {
        _output = output;
        var dbFactory = new DatabaseConnectionFactory(config.Database);
        _reader = new PipelineDataReader(dbFactory);
        _progressTracker = expectedPatientCount > 0
            ? new PipelineProgressTracker(output, expectedPatientCount, dbFactory)
            : null;
    }

    /// <summary>
    /// Runs a single progress check cycle: database state, progress bar,
    /// and stall detection.
    /// Returns true if a critical failure is detected.
    /// </summary>
    public async Task<bool> CheckProgressAsync(string facilityId, string reportId)
    {
        var hasCriticalFailure = false;

        hasCriticalFailure |= await CheckReportProgress(facilityId, reportId);
        hasCriticalFailure |= await CheckDataAcquisitionProgress(facilityId, reportId);

        if (_progressTracker != null)
        {
            await _progressTracker.UpdateAsync(facilityId, reportId);
        }

        return hasCriticalFailure;
    }

    private async Task<bool> CheckReportProgress(string facilityId, string reportId)
    {
        try
        {
            var scheduleId = Guid.Parse(reportId);

            var schedule = await _reader.GetReportScheduleAsync(scheduleId);
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

            var entries = await _reader.GetReportEntriesAsync(scheduleId);

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
            var logs = await _reader.GetAcquisitionLogsAsync(facilityId, reportId);

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

            if (maxRetries > 0)
            {
                var terminalLogs = logs
                    .Where(l => l.Status == RequestStatus.MaxRetriesReached)
                    .Take(5)
                    .ToList();

                foreach (var log in terminalLogs)
                {
                    var notes = log.Notes.Count > 0 ? string.Join(" | ", log.Notes.Take(3)) : "(no notes)";
                    _output.WriteLine($"[DIAG][DataAcq] CRITICAL: TERMINAL Log Id={log.Id}, Patient={log.PatientId}, " +
                                     $"Status={log.Status}, Phase={log.QueryPhase}, Notes={notes}");
                }

                return true;
            }

            if (failed > 0)
            {
                _output.WriteLine($"[DIAG][DataAcq] WARN: {failed} log(s) currently in Failed status (retriable). Monitoring for recovery...");
            }
        }
        catch (Exception ex)
        {
            _output.WriteLine($"[DIAG][DataAcq] Check error: {ex.Message}");
        }

        return false;
    }
}
