using LantanaGroup.Link.Automation.Link.Configuration;

namespace LantanaGroup.Link.Automation.Link.Helpers;

/// <summary>
/// Central progress aggregation hub for test pipeline output.
/// Combines service-state monitoring, progress bar rendering, and stall detection
/// into a single coherent stream of test output.
/// </summary>
public class ProgressMonitor
{
    private const int ActivityCheckInterval = 6; // ~30s with 5s polling

    private readonly IAutomationOutput _output;
    private readonly PipelineProgressTracker? _progressTracker;
    private readonly PipelineDataReader _reader;
    private readonly LokiScraper? _lokiScraper;
    private readonly bool _expectsDataAcquisition;

    private string? _lastScheduleStatus;
    private int _lastReportEntryCount;
    private string? _lastReportBreakdown;
    private int _lastAcqLogCount;
    private int _lastCompletedAcqCount;
    private string? _lastAcqBreakdown;
    private readonly AcquisitionActivityTracker _acquisitionActivity = new();
    private int _progressCheckCount;
    private string? _lastMeasureEvalActivity;
    private string? _lastValidationActivity;
    private string? _lastNormalizationActivity;
    private string? _lastDataAcquisitionActivity;

    /// <summary>Most recent time DA logs completed, acquired additional resources, or paged FHIR results.</summary>
    public DateTime LastAcquisitionProgressUtc => _acquisitionActivity.LastProgressUtc;

    /// <summary>Latest <c>TotalResourcesAcquired</c> from the DA report summary.</summary>
    public int LastResourcesAcquired => _acquisitionActivity.LastResourcesAcquired;

    /// <summary>True when at least one DA log is currently Processing.</summary>
    public bool IsAcquisitionInFlight => _acquisitionActivity.InFlight;

    public bool HasRecentAcquisitionProgress(TimeSpan window, DateTime? utcNow = null)
        => _acquisitionActivity.HasRecentProgress(window, utcNow ?? DateTime.UtcNow);

    /// <summary>
    /// Returns the pipeline stage that appears stalled, or null if progress
    /// is still advancing.
    /// </summary>
    public string? StalledStage => _progressTracker?.StalledStage;

    /// <summary>
    /// How long the pipeline has been stalled at the same progress.
    /// </summary>
    public TimeSpan StallDuration => _progressTracker?.StallDuration ?? TimeSpan.Zero;

    public ProgressMonitor(IAutomationOutput output, int expectedPatientCount, LokiScraper? lokiScraper, PipelineDataReader reader, bool expectsDataAcquisition = true)
    {
        _output = output;
        _lokiScraper = lokiScraper;
        _reader = reader;
        _expectsDataAcquisition = expectsDataAcquisition;
        _progressTracker = expectedPatientCount > 0
            ? new PipelineProgressTracker(output, expectedPatientCount, reader, expectsDataAcquisition)
            : null;
    }

    /// <summary>
    /// Runs a single progress check cycle: database state, progress bar,
    /// activity confirmation, and stall detection.
    /// Returns true if a critical failure is detected.
    /// </summary>
    public async Task<bool> CheckProgressAsync(string facilityId, string reportId)
    {
        _progressCheckCount++;
        var hasCriticalFailure = false;

        hasCriticalFailure |= await CheckReportProgress(facilityId, reportId);
        if (_expectsDataAcquisition)
        {
            hasCriticalFailure |= await CheckDataAcquisitionProgress(facilityId, reportId);
        }

        if (_progressTracker != null)
        {
            await _progressTracker.UpdateAsync(facilityId, reportId);
        }

        await CheckPipelineActivityAsync();

        return hasCriticalFailure;
    }

    private async Task CheckPipelineActivityAsync()
    {
        if (_lokiScraper == null)
            return;

        if (_progressCheckCount % ActivityCheckInterval != 0)
            return;

        var measureEvalActivity = await _lokiScraper.GetMeasureEvalActivitySummaryAsync(TimeSpan.FromSeconds(60));
        if (!string.IsNullOrWhiteSpace(measureEvalActivity) && !string.Equals(measureEvalActivity, _lastMeasureEvalActivity, StringComparison.Ordinal))
        {
            _output.WriteLine($"[DIAG][MeasureEval] Active: {measureEvalActivity}");
            _lastMeasureEvalActivity = measureEvalActivity;
        }

        var dataAcquisitionActivity = await _lokiScraper.GetDataAcquisitionActivitySummaryAsync(TimeSpan.FromSeconds(60));
        if (!string.IsNullOrWhiteSpace(dataAcquisitionActivity))
        {
            _acquisitionActivity.MarkProgress(DateTime.UtcNow);
            _progressTracker?.NoteActivity();
            if (!string.Equals(dataAcquisitionActivity, _lastDataAcquisitionActivity, StringComparison.Ordinal))
            {
                _output.WriteLine($"[DIAG][DataAcq] Active: {dataAcquisitionActivity}");
                _lastDataAcquisitionActivity = dataAcquisitionActivity;
            }
        }

        var normalizationActivity = await _lokiScraper.GetNormalizationActivitySummaryAsync(TimeSpan.FromSeconds(60));
        if (!string.IsNullOrWhiteSpace(normalizationActivity) && !string.Equals(normalizationActivity, _lastNormalizationActivity, StringComparison.Ordinal))
        {
            _output.WriteLine($"[DIAG][Normalization] Active: {normalizationActivity}");
            _lastNormalizationActivity = normalizationActivity;
        }

        // Validation activity is meaningful only after acquisition has produced work for downstream services.
        if (_lastAcqLogCount <= 0)
            return;

        var validationActivity = await _lokiScraper.GetValidationActivitySummaryAsync(TimeSpan.FromSeconds(60));
        if (!string.IsNullOrWhiteSpace(validationActivity) && !string.Equals(validationActivity, _lastValidationActivity, StringComparison.Ordinal))
        {
            _output.WriteLine($"[DIAG][Validation] Active: {validationActivity}");
            _lastValidationActivity = validationActivity;
        }
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

            var currentStatus = schedule.Status ?? "unknown";
            if (currentStatus != _lastScheduleStatus)
            {
                _output.WriteLine($"[DIAG][Report] Schedule status changed: {_lastScheduleStatus ?? "(none)"} -> {currentStatus}");
                _lastScheduleStatus = currentStatus;
            }

            var entries = await _reader.GetReportEntriesAsync(scheduleId);

            var total = entries.Count;
            var submitted = entries.Count(e => string.Equals(e.SubmissionStatus, "Submitted", StringComparison.OrdinalIgnoreCase));
            var pending = entries.Count(e => string.Equals(e.SubmissionStatus, "PendingValidation", StringComparison.OrdinalIgnoreCase));
            var submitting = entries.Count(e => string.Equals(e.SubmissionStatus, "Submitting", StringComparison.OrdinalIgnoreCase));
            var failed = entries.Count(e => string.Equals(e.SubmissionStatus, "FailedSubmission", StringComparison.OrdinalIgnoreCase));

            var identified = entries.Count(e => string.Equals(e.ReportingStatus, "PatientIdentified", StringComparison.OrdinalIgnoreCase));
            var pendingValidation = entries.Count(e => string.Equals(e.ReportingStatus, "PendingValidation", StringComparison.OrdinalIgnoreCase));
            var passedValidation = entries.Count(e => string.Equals(e.ReportingStatus, "PassedValidation", StringComparison.OrdinalIgnoreCase));
            var failedValidation = entries.Count(e => string.Equals(e.ReportingStatus, "FailedValidation", StringComparison.OrdinalIgnoreCase));

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
            var summary = await _reader.GetDataAcquisitionReportSummaryAsync(reportId);
            if (summary == null) return false;

            var total = summary.TotalLogs;
            var completed = summary.StatusCounts.FirstOrDefault(s => string.Equals(s.Status, "Completed", StringComparison.OrdinalIgnoreCase))?.Count ?? 0;
            var failed = summary.StatusCounts.FirstOrDefault(s => string.Equals(s.Status, "Failed", StringComparison.OrdinalIgnoreCase))?.Count ?? 0;
            var maxRetries = summary.StatusCounts.FirstOrDefault(s => string.Equals(s.Status, "MaxRetriesReached", StringComparison.OrdinalIgnoreCase))?.Count ?? 0;
            var processing = summary.StatusCounts.FirstOrDefault(s => string.Equals(s.Status, "Processing", StringComparison.OrdinalIgnoreCase))?.Count ?? 0;
            var pending = summary.StatusCounts.FirstOrDefault(s => string.Equals(s.Status, "Pending", StringComparison.OrdinalIgnoreCase))?.Count ?? 0;

            var observation = _acquisitionActivity.Observe(
                total,
                completed,
                processing,
                pending,
                failed,
                maxRetries,
                summary.TotalResourcesAcquired,
                DateTime.UtcNow);

            if (observation.ShouldLogStatus)
            {
                _output.WriteLine($"[DIAG][DataAcq] Logs: {observation.TotalLogs} total | {observation.Breakdown}");
                _lastAcqLogCount = observation.TotalLogs;
                _lastCompletedAcqCount = observation.Completed;
                _lastAcqBreakdown = observation.Breakdown;
            }
            else if (observation.ShouldLogKeepAlive)
            {
                _output.WriteLine(
                    $"[DIAG][DataAcq] Keep-alive: still acquiring {observation.ResourceDelta} resource(s) " +
                    $"(total={observation.ResourcesAcquired}, processing={observation.Processing}, pending={observation.Pending}).");
            }

            if (maxRetries > 0)
            {
                _output.WriteLine($"[DIAG][DataAcq] CRITICAL: {maxRetries} terminal MaxRetriesReached log(s) detected.");
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
