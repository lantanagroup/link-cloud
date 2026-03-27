using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models.Enums;
using LantanaGroup.Link.Report.Domain.Enums;
using LantanaGroup.Link.Shared.Application.Enums;
using Microsoft.EntityFrameworkCore;

namespace LantanaGroup.Link.Automation.Helpers;

/// <summary>
/// Computes a coarse pipeline progress percentage by querying the Report and
/// DataAcquisition databases, and detects pipeline stalls.
/// </summary>
public class PipelineProgressTracker
{
    private readonly IAutomationOutput _output;
    private readonly int _expectedPatientCount;
    private readonly int _totalUnits;
    private readonly PipelineDataReader _reader;
    private int _lastReportedPercent = -1;
    private string? _lastProgressBar;

    // Stall detection
    private int _lastCompletedUnits = -1;
    private DateTime _lastProgressChange = DateTime.UtcNow;
    private string? _stalledStage;

    /// <summary>
    /// How long progress has been unchanged. Returns TimeSpan.Zero if progress
    /// is still advancing.
    /// </summary>
    public TimeSpan StallDuration => _lastCompletedUnits >= 0 && _stalledStage != null
        ? DateTime.UtcNow - _lastProgressChange
        : TimeSpan.Zero;

    /// <summary>
    /// A human-readable description of where the pipeline appears stuck,
    /// or null if progress is still advancing.
    /// </summary>
    public string? StalledStage => _stalledStage;

    public PipelineProgressTracker(IAutomationOutput output, int expectedPatientCount, DatabaseConnectionFactory dbFactory)
    {
        _output = output;
        _expectedPatientCount = expectedPatientCount;
        _reader = new PipelineDataReader(dbFactory);
        _totalUnits = (expectedPatientCount * 4) + 2;
    }

    public async Task UpdateAsync(string facilityId, string reportId)
    {
        try
        {
            var scheduleId = Guid.Parse(reportId);
            var completedUnits = 0;
            var stageDetails = new List<string>();

            var schedule = await _reader.GetReportScheduleAsync(scheduleId);
            if (schedule != null)
            {
                completedUnits++;

                if (schedule.Status == ScheduleStatus.Submitted)
                {
                    completedUnits++;
                    stageDetails.Add("report=finalized");
                }
                else
                {
                    stageDetails.Add($"report={schedule.Status}");
                }
            }
            else
            {
                stageDetails.Add("report=pending");
                PrintIfChanged(completedUnits, stageDetails);
                return;
            }

            var entries = await _reader.GetReportEntriesWithMeasureReportsAsync(scheduleId);

            var patientsEvaluated = 0;
            var patientsValidated = 0;
            var patientsSubmitted = 0;

            foreach (var entry in entries)
            {
                if (entry.MeasureReports.Any(mr => !string.IsNullOrWhiteSpace(mr.MeasureReportId)))
                {
                    patientsEvaluated++;
                    completedUnits++;
                }

                if (entry.ReportingStatus is ReportingStatus.PassedValidation
                    or ReportingStatus.FailedValidation)
                {
                    patientsValidated++;
                    completedUnits++;
                }

                if (entry.SubmissionStatus == SubmissionStatus.Submitted)
                {
                    patientsSubmitted++;
                    completedUnits++;
                }
            }

            stageDetails.Add($"eval={patientsEvaluated}/{_expectedPatientCount}");
            stageDetails.Add($"valid={patientsValidated}/{_expectedPatientCount}");
            stageDetails.Add($"submit={patientsSubmitted}/{_expectedPatientCount}");

            var logs = await _reader.GetAcquisitionLogsAsync(facilityId, reportId);

            var patientsAcquired = logs
                .Where(l => l.Status == RequestStatus.Completed && l.PatientId != null)
                .Select(l => l.PatientId)
                .Distinct()
                .Count();

            patientsAcquired = Math.Min(patientsAcquired, _expectedPatientCount);
            completedUnits += patientsAcquired;

            stageDetails.Insert(1, $"acq={patientsAcquired}/{_expectedPatientCount}");

            PrintIfChanged(completedUnits, stageDetails);
        }
        catch (Exception ex)
        {
            _output.WriteLine($"[PROGRESS] Error computing progress: {ex.Message}");
        }
    }

    private void PrintIfChanged(int completedUnits, List<string> stageDetails)
    {
        // Stall detection
        if (completedUnits != _lastCompletedUnits)
        {
            _lastCompletedUnits = completedUnits;
            _lastProgressChange = DateTime.UtcNow;
            _stalledStage = null;
        }
        else if (_lastCompletedUnits >= 0)
        {
            _stalledStage = IdentifyStalledStage(stageDetails);
        }

        var percent = _totalUnits > 0 ? (int)((double)completedUnits / _totalUnits * 100) : 0;
        percent = Math.Clamp(percent, 0, 100);

        var barLength = 30;
        var filledLength = (int)(barLength * percent / 100.0);
        var bar = new string('#', filledLength) + new string('-', barLength - filledLength);
        var progressBar = $"[{bar}] {percent,3}%";

        if (progressBar == _lastProgressBar)
            return;

        var details = string.Join(" | ", stageDetails);
        _output.WriteLine($"[PROGRESS] {progressBar}  ({completedUnits}/{_totalUnits} units)  {details}");

        _lastReportedPercent = percent;
        _lastProgressBar = progressBar;
    }

    /// <summary>
    /// Examines the stage detail strings to identify which pipeline stage
    /// is the bottleneck (first stage that hasn't completed for all patients).
    /// </summary>
    private static string IdentifyStalledStage(List<string> stageDetails)
    {
        foreach (var detail in stageDetails)
        {
            var parts = detail.Split('=');
            if (parts.Length != 2) continue;

            var key = parts[0].Trim();
            var value = parts[1].Trim();

            if (value.Contains('/'))
            {
                var fractionParts = value.Split('/');
                if (fractionParts.Length == 2 &&
                    int.TryParse(fractionParts[0], out var current) &&
                    int.TryParse(fractionParts[1], out var expected) &&
                    current < expected)
                {
                    return key;
                }
            }
            else if (key == "report" && value != "finalized")
            {
                return $"report (status={value})";
            }
        }

        return "unknown";
    }
}
