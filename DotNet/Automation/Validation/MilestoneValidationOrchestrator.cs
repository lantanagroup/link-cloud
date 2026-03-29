using LantanaGroup.Link.Automation.Configuration;
using LantanaGroup.Link.Automation.Helpers;

namespace LantanaGroup.Link.Automation.Validation;

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
    private readonly HashSet<Milestone> _completed = [];
    private readonly List<string> _issues = [];

    public bool HasCriticalFailure { get; private set; }
    public IReadOnlyCollection<Milestone> CompletedMilestones => _completed;
    public IReadOnlyList<string> Issues => _issues;

    public MilestoneValidationOrchestrator(
        IAutomationOutput output,
        PipelineDataReader reader,
        int expectedPatientCount)
    {
        _output = output;
        _expectedPatientCount = expectedPatientCount;
        _reader = reader;
    }

    public async Task CheckAsync(string facilityId, string reportId)
    {
        if (!Guid.TryParse(reportId, out var scheduleId))
            return;

        try
        {
            await CheckReportScheduleCreated(scheduleId, facilityId);
            await CheckReportEntriesCreated(scheduleId);
            await CheckAcquisitionStarted(facilityId, reportId);
            await CheckAcquisitionCompleted(facilityId, reportId);
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

        var logs = await _reader.GetAcquisitionLogsAsync(facilityId, reportId);
        if (logs.Count == 0)
            return;

        _completed.Add(Milestone.AcquisitionStarted);
        _output.WriteLine($"[DIAG][Milestone] Reached: AcquisitionStarted ({logs.Count} log rows)");
    }

    private async Task CheckAcquisitionCompleted(string facilityId, string reportId)
    {
        if (_completed.Contains(Milestone.AcquisitionCompleted))
            return;

        var logs = await _reader.GetAcquisitionLogsAsync(facilityId, reportId);
        if (logs.Count == 0)
            return;

        var maxRetries = logs.Count(l => string.Equals(l.Status, "MaxRetriesReached", StringComparison.OrdinalIgnoreCase));
        if (maxRetries > 0)
        {
            RecordIssue($"Data acquisition terminal failures detected: {maxRetries} max-retry log(s).", critical: true);
            return;
        }

        var failed = logs.Count(l => string.Equals(l.Status, "Failed", StringComparison.OrdinalIgnoreCase));
        if (failed > 0)
            RecordIssue($"Data acquisition has {failed} retriable failed log(s); waiting for retry recovery.", critical: false);

        if (_expectedPatientCount <= 0)
            return;

        var completedPatients = logs
            .Where(l => string.Equals(l.Status, "Completed", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(l.PatientId))
            .Select(l => l.PatientId!)
            .Distinct(StringComparer.Ordinal)
            .Count();

        if (completedPatients < _expectedPatientCount)
            return;

        _completed.Add(Milestone.AcquisitionCompleted);
        _output.WriteLine($"[DIAG][Milestone] Reached: AcquisitionCompleted ({completedPatients}/{_expectedPatientCount} patients)");
    }

    private async Task CheckMeasureReportsGenerated(Guid scheduleId)
    {
        if (_completed.Contains(Milestone.MeasureReportsGenerated))
            return;

        var reports = await _reader.GetEntryMeasureReportsAsync(scheduleId);
        if (reports.Count == 0)
            return;

        var withIds = reports.Count(r => !string.IsNullOrWhiteSpace(r.MeasureReportId));
        if (_expectedPatientCount > 0 && withIds < _expectedPatientCount)
            return;

        if (withIds < reports.Count)
            RecordIssue($"Some EntryMeasureReport rows are missing MeasureReportId ({withIds}/{reports.Count}).", critical: true);

        _completed.Add(Milestone.MeasureReportsGenerated);
        _output.WriteLine($"[DIAG][Milestone] Reached: MeasureReportsGenerated ({withIds} measure reports)");
    }

    private async Task CheckSubmissionCompleted(Guid scheduleId)
    {
        if (_completed.Contains(Milestone.SubmissionCompleted))
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
