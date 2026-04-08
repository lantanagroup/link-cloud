using LantanaGroup.Link.Automation.Link.Configuration;
using LantanaGroup.Link.Automation.Link.Helpers;

namespace LantanaGroup.Link.Automation.Link.Validation;

/// <summary>
/// Runs lightweight, idempotent milestone validations as the pipeline progresses.
/// This is intended for early signal and root-cause localization during long-running E2E tests.
/// </summary>
public class MilestoneValidationOrchestrator
{
    public enum Milestone
    {
        ReportScheduleCreated,
        ReportEntriesCreated,
        AcquisitionStarted,
        AcquisitionCompleted,
        MeasureReportsGenerated,
        SubmissionCompleted
    }

    private readonly IAutomationOutput _output;
    private readonly PipelineDataReader _reader;
    private readonly int _expectedPatientCount;
    private readonly bool _expectsDataAcquisition;
    private readonly HashSet<Milestone> _completed = [];
    private readonly List<string> _issues = [];

    // Prevent eager AcquisitionCompleted when additional DataAcq logs are still being emitted.
    private static readonly TimeSpan AcquisitionCompletionStabilizationWindow = TimeSpan.FromSeconds(12);
    private DateTime? _acquisitionCompletionCandidateSinceUtc;
    private int _acquisitionCompletionCandidateLogCount;
    private int _acquisitionCompletionCandidateCompletedPatients;

    public bool HasCriticalFailure { get; private set; }
    public IReadOnlyCollection<Milestone> CompletedMilestones => _completed;
    public IReadOnlyList<string> Issues => _issues;

    public MilestoneValidationOrchestrator(
        IAutomationOutput output,
        PipelineDataReader reader,
        int expectedPatientCount,
        bool expectsDataAcquisition = true)
    {
        _output = output;
        _expectedPatientCount = expectedPatientCount;
        _reader = reader;
        _expectsDataAcquisition = expectsDataAcquisition;
    }

    public async Task CheckAsync(string facilityId, string reportId)
    {
        if (!Guid.TryParse(reportId, out var scheduleId))
            return;

        try
        {
            await CheckReportScheduleCreated(scheduleId, facilityId);
            await CheckReportEntriesCreated(scheduleId);

            if (_expectsDataAcquisition)
            {
                await CheckAcquisitionStarted(facilityId, reportId);
                await CheckAcquisitionCompleted(facilityId, reportId);
            }
            else
            {
                AutoCompleteAcquisitionMilestones();
            }

            await CheckMeasureReportsGenerated(scheduleId);
            await CheckSubmissionCompleted(scheduleId);
        }
        catch (Exception ex)
        {
            RecordIssue($"Milestone check exception: {ex.Message}", critical: false);
        }
    }

    public void WriteSummary()
    {
        var milestoneList = _completed.Count == 0
            ? "(none)"
            : string.Join(", ", _completed.OrderBy(m => (int)m));

        _output.WriteLine($"[DIAG][Milestone] Completed milestones: {milestoneList}");

        if (_issues.Count == 0)
            return;

        _output.WriteLine($"[DIAG][Milestone] Recorded {_issues.Count} issue(s):");
        foreach (var issue in _issues.Take(10))
            _output.WriteLine($"[DIAG][Milestone]   - {issue}");

        if (_issues.Count > 10)
            _output.WriteLine($"[DIAG][Milestone]   - Additional issues omitted: {_issues.Count - 10}");
    }

    private async Task CheckReportScheduleCreated(Guid scheduleId, string facilityId)
    {
        if (_completed.Contains(Milestone.ReportScheduleCreated))
            return;

        var schedule = await _reader.GetReportScheduleAsync(scheduleId);
        if (schedule == null)
            return;

        if (!string.Equals(schedule.FacilityId, facilityId, StringComparison.Ordinal))
            RecordIssue($"ReportSchedule.FacilityId mismatch: expected {facilityId}, actual {schedule.FacilityId}", critical: true);

        _completed.Add(Milestone.ReportScheduleCreated);
        _output.WriteLine("[DIAG][Milestone] Reached: ReportScheduleCreated");
    }

    private async Task CheckReportEntriesCreated(Guid scheduleId)
    {
        if (_completed.Contains(Milestone.ReportEntriesCreated))
            return;

        var entries = await _reader.GetReportEntriesAsync(scheduleId);
        if (entries.Count == 0)
            return;

        if (_expectedPatientCount > 0 && entries.Count > _expectedPatientCount)
            RecordIssue($"ReportEntry count exceeded expected patient count: entries={entries.Count}, expected={_expectedPatientCount}", critical: true);

        _completed.Add(Milestone.ReportEntriesCreated);
        _output.WriteLine($"[DIAG][Milestone] Reached: ReportEntriesCreated ({entries.Count} entries)");
    }

    private async Task CheckAcquisitionStarted(string facilityId, string reportId)
    {
        if (_completed.Contains(Milestone.AcquisitionStarted))
            return;

        var summary = await _reader.GetDataAcquisitionReportSummaryAsync(reportId);
        if (summary == null || summary.TotalLogs == 0)
            return;

        _completed.Add(Milestone.AcquisitionStarted);
        _output.WriteLine($"[DIAG][Milestone] Reached: AcquisitionStarted ({summary.TotalLogs} log rows)");
    }

    private async Task CheckAcquisitionCompleted(string facilityId, string reportId)
    {
        if (_completed.Contains(Milestone.AcquisitionCompleted))
            return;

        var summary = await _reader.GetDataAcquisitionReportSummaryAsync(reportId);
        if (summary == null || summary.TotalLogs == 0)
            return;

        var maxRetries = summary.StatusCounts.FirstOrDefault(s => string.Equals(s.Status, "MaxRetriesReached", StringComparison.OrdinalIgnoreCase))?.Count ?? 0;
        if (maxRetries > 0)
        {
            RecordIssue($"Data acquisition terminal failures detected: {maxRetries} max-retry log(s).", critical: true);
            ResetAcquisitionCompletionCandidate();
            return;
        }

        var failed = summary.StatusCounts.FirstOrDefault(s => string.Equals(s.Status, "Failed", StringComparison.OrdinalIgnoreCase))?.Count ?? 0;
        if (failed > 0)
            RecordIssue($"Data acquisition has {failed} retriable failed log(s); waiting for retry recovery.", critical: false);

        if (_expectedPatientCount <= 0)
            return;

        var completedPatients = summary.TotalCompletedPatients;

        // Must satisfy basic completion criteria first.
        if (completedPatients < _expectedPatientCount)
        {
            ResetAcquisitionCompletionCandidate();
            return;
        }

        // Guardrail: if any in-flight statuses exist, do not complete.
        var inFlight = (summary.StatusCounts.FirstOrDefault(s => string.Equals(s.Status, "Pending", StringComparison.OrdinalIgnoreCase))?.Count ?? 0)
                     + (summary.StatusCounts.FirstOrDefault(s => string.Equals(s.Status, "Processing", StringComparison.OrdinalIgnoreCase))?.Count ?? 0);

        if (inFlight > 0)
        {
            ResetAcquisitionCompletionCandidate();
            return;
        }

        // Debounce: require stable log-count and completed-patient count across a short window.
        var nowUtc = DateTime.UtcNow;
        if (_acquisitionCompletionCandidateSinceUtc == null ||
            _acquisitionCompletionCandidateLogCount != summary.TotalLogs ||
            _acquisitionCompletionCandidateCompletedPatients != completedPatients)
        {
            _acquisitionCompletionCandidateSinceUtc = nowUtc;
            _acquisitionCompletionCandidateLogCount = summary.TotalLogs;
            _acquisitionCompletionCandidateCompletedPatients = completedPatients;
            return;
        }

        if ((nowUtc - _acquisitionCompletionCandidateSinceUtc.Value) < AcquisitionCompletionStabilizationWindow)
            return;

        _completed.Add(Milestone.AcquisitionCompleted);
        _output.WriteLine($"[DIAG][Milestone] Reached: AcquisitionCompleted ({completedPatients}/{_expectedPatientCount} patients, stable logs={summary.TotalLogs})");
    }

    private void ResetAcquisitionCompletionCandidate()
    {
        _acquisitionCompletionCandidateSinceUtc = null;
        _acquisitionCompletionCandidateLogCount = 0;
        _acquisitionCompletionCandidateCompletedPatients = 0;
    }

    private void AutoCompleteAcquisitionMilestones()
    {
        if (_completed.Add(Milestone.AcquisitionStarted))
            _output.WriteLine("[DIAG][Milestone] Reached: AcquisitionStarted (auto-completed — data acquisition not expected for this scenario)");

        if (_completed.Add(Milestone.AcquisitionCompleted))
            _output.WriteLine("[DIAG][Milestone] Reached: AcquisitionCompleted (auto-completed — data acquisition not expected for this scenario)");
    }

    private async Task CheckMeasureReportsGenerated(Guid scheduleId)
    {
        if (_completed.Contains(Milestone.MeasureReportsGenerated))
            return;

        // Cannot mark this milestone until acquisition is fully complete —
        // MeasureEval may have processed early patients while later ones
        // are still failing/retrying in data acquisition.
        if (!_completed.Contains(Milestone.AcquisitionCompleted))
            return;

        var reports = await _reader.GetEntryMeasureReportsAsync(scheduleId);
        if (reports.Count == 0)
            return;

        var withIds = reports.Count(r => !string.IsNullOrWhiteSpace(r.MeasureReportId));

        // With multi-measure, there are patients × measures rows.
        // Derive the measure count from the distinct ReportType values observed so far.
        var distinctMeasures = reports
            .Where(r => !string.IsNullOrWhiteSpace(r.ReportType))
            .Select(r => r.ReportType!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Count();
        var measureMultiplier = Math.Max(1, distinctMeasures);
        var expectedTotal = _expectedPatientCount > 0
            ? _expectedPatientCount * measureMultiplier
            : reports.Count;

        // Wait until all expected rows have a MeasureReportId before completing this milestone.
        if (withIds < expectedTotal)
            return;

        _completed.Add(Milestone.MeasureReportsGenerated);
        _output.WriteLine($"[DIAG][Milestone] Reached: MeasureReportsGenerated ({withIds} measure reports across {distinctMeasures} measure(s))");
    }

    private async Task CheckSubmissionCompleted(Guid scheduleId)
    {
        if (_completed.Contains(Milestone.SubmissionCompleted))
            return;

        // Cannot mark submission complete until measure reports are generated.
        if (!_completed.Contains(Milestone.MeasureReportsGenerated))
            return;

        var entries = await _reader.GetReportEntriesAsync(scheduleId);
        if (entries.Count == 0)
            return;

        var failedSubmission = entries.Count(e => string.Equals(e.SubmissionStatus, "FailedSubmission", StringComparison.OrdinalIgnoreCase));
        if (failedSubmission > 0)
        {
            RecordIssue($"Submission failures detected: {failedSubmission} ReportEntry row(s) in FailedSubmission.", critical: true);
            return;
        }

        var submitted = entries.Count(e => string.Equals(e.SubmissionStatus, "Submitted", StringComparison.OrdinalIgnoreCase));
        if (_expectedPatientCount > 0 && submitted < _expectedPatientCount)
            return;

        if (submitted < entries.Count)
            return;

        _completed.Add(Milestone.SubmissionCompleted);
        _output.WriteLine($"[DIAG][Milestone] Reached: SubmissionCompleted ({submitted} submitted entries)");
    }

    private void RecordIssue(string message, bool critical)
    {
        if (_issues.Contains(message, StringComparer.Ordinal))
            return;

        _issues.Add(message);
        _output.WriteLine($"[DIAG][Milestone] ISSUE: {message}");

        if (!critical)
            return;

        HasCriticalFailure = true;
        _output.WriteLine("[DIAG][Milestone] ISSUE marked CRITICAL");
    }
}
