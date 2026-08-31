using LantanaGroup.Link.Shared.Application.Enums;

namespace LantanaGroup.Link.Automation.Link.Helpers;

/// <summary>
/// Computes a coarse pipeline progress percentage by querying service APIs through LinkSdk,
/// and detects pipeline stalls.
/// </summary>
public class PipelineProgressTracker
{
    private readonly IAutomationOutput _output;
    private readonly int _expectedPatientCount;
    private readonly int _totalUnits;
    private readonly PipelineDataReader _reader;
    private readonly bool _expectsDataAcquisition;
    private int _lastReportedPercent = -1;
    private string? _lastProgressBar;

    // Stall detection
    private int _lastCompletedUnits = -1;
    private int _lastResourcesAcquired = -1;
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

    public PipelineProgressTracker(IAutomationOutput output, int expectedPatientCount, PipelineDataReader reader, bool expectsDataAcquisition = true)
    {
        _output = output;
        _expectedPatientCount = expectedPatientCount;
        _reader = reader;
        _expectsDataAcquisition = expectsDataAcquisition;
        _totalUnits = expectsDataAcquisition
            ? (expectedPatientCount * 4) + 2
            : (expectedPatientCount * 3) + 2;
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

                if (string.Equals(schedule.Status, ScheduleStatus.Submitted.ToString(), StringComparison.OrdinalIgnoreCase))
                {
                    completedUnits++;
                    stageDetails.Add("report=finalized");
                }
                else if (string.Equals(schedule.Status, ScheduleStatus.EndOfPeriod.ToString(), StringComparison.OrdinalIgnoreCase))
                {
                    // EndOfPeriod means the period ended and the manifest is being submitted;
                    // count it as partial progress so stall detection doesn't fire.
                    completedUnits++;
                    stageDetails.Add("report=endOfPeriod");
                }
                else
                {
                    stageDetails.Add($"report={schedule.Status}");
                }
            }
            else
            {
                stageDetails.Add("report=pending");
                PrintIfChanged(completedUnits, stageDetails, resourcesAcquired: 0);
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

                if (string.Equals(entry.ReportingStatus, "PassedValidation", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(entry.ReportingStatus, "FailedValidation", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(entry.ReportingStatus, "NotReportable", StringComparison.OrdinalIgnoreCase))
                {
                    patientsValidated++;
                    completedUnits++;
                }

                if (string.Equals(entry.SubmissionStatus, "Submitted", StringComparison.OrdinalIgnoreCase))
                {
                    patientsSubmitted++;
                    completedUnits++;
                }
            }

            stageDetails.Add($"eval={patientsEvaluated}/{_expectedPatientCount}");
            stageDetails.Add($"valid={patientsValidated}/{_expectedPatientCount}");
            stageDetails.Add($"submit={patientsSubmitted}/{_expectedPatientCount}");

            var resourcesAcquired = 0;
            if (_expectsDataAcquisition)
            {
                var acqSummary = await _reader.GetDataAcquisitionReportSummaryAsync(reportId);
                var patientsAcquired = acqSummary?.TotalCompletedPatients ?? 0;
                resourcesAcquired = acqSummary?.TotalResourcesAcquired ?? 0;

                patientsAcquired = Math.Min(patientsAcquired, _expectedPatientCount);
                completedUnits += patientsAcquired;

                stageDetails.Insert(1, $"acq={patientsAcquired}/{_expectedPatientCount}");
            }

            PrintIfChanged(completedUnits, stageDetails, resourcesAcquired);
        }
        catch (Exception ex)
        {
            _output.WriteLine($"[PROGRESS] Error computing progress: {ex.Message}");
        }
    }

    private void PrintIfChanged(int completedUnits, List<string> stageDetails, int resourcesAcquired)
    {
        // Stall detection: patient-level units or DA still writing resources
        // (one large patient can sit at acq=0/1 for a long time while Observation pages).
        var activityChanged = completedUnits != _lastCompletedUnits
            || resourcesAcquired != _lastResourcesAcquired;
        if (activityChanged)
        {
            _lastCompletedUnits = completedUnits;
            _lastResourcesAcquired = resourcesAcquired;
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
            else if (key == "report" && value != "finalized" && value != "endOfPeriod")
            {
                return $"report (status={value})";
            }
        }

        return "unknown";
    }
}
