using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models.Enums;
using LantanaGroup.Link.Report.Domain.Enums;
using Xunit.Abstractions;

namespace LantanaGroup.Link.Tests.E2ETests.Helpers;

/// <summary>
/// Monitors database state during the report generation pipeline to detect
/// stuck or failed records before the final polling timeout.
/// Delegates all queries to <see cref="PipelineSnapshot"/> to avoid duplication.
/// </summary>
public class DatabaseProgressMonitor(ITestOutputHelper output)
{
    private int _lastReportEntryCount;
    private int _lastAcqLogCount;
    private int _lastCompletedAcqCount;

    /// <summary>
    /// Checks the Report and DataAcquisition databases for progress and failures.
    /// Returns true if a critical failure is detected that warrants early termination.
    /// </summary>
    public async Task<bool> CheckProgressAsync(string facilityId, string reportId)
    {
        var hasCriticalFailure = false;

        hasCriticalFailure |= await CheckReportProgress(facilityId, reportId);
        hasCriticalFailure |= await CheckDataAcquisitionProgress(facilityId, reportId);

        return hasCriticalFailure;
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
                output.WriteLine($"[DIAG][Report] Schedule {reportId} not yet created in database");
                return false;
            }

            output.WriteLine($"[DIAG][Report] Schedule status: {schedule.Status}");

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

            if (total != _lastReportEntryCount)
            {
                output.WriteLine($"[DIAG][Report] Entries: {total} total | " +
                                 $"Reporting: identified={identified}, pendingValidation={pendingValidation}, " +
                                 $"passed={passedValidation}, failedValidation={failedValidation} | " +
                                 $"Submission: pending={pending}, submitting={submitting}, " +
                                 $"submitted={submitted}, failed={failed}");
                _lastReportEntryCount = total;
            }

            if (failed > 0)
            {
                output.WriteLine($"[DIAG][Report] ? {failed} entry/entries have FailedSubmission status!");
                return true;
            }
        }
        catch (Exception ex)
        {
            output.WriteLine($"[DIAG][Report] Check error: {ex.Message}");
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

            if (total != _lastAcqLogCount || completed != _lastCompletedAcqCount)
            {
                output.WriteLine($"[DIAG][DataAcq] Logs: {total} total | " +
                                 $"completed={completed}, processing={processing}, " +
                                 $"pending={pending}, failed={failed}, maxRetries={maxRetries}");
                _lastAcqLogCount = total;
                _lastCompletedAcqCount = completed;
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
                    output.WriteLine($"[DIAG][DataAcq] ? FAILED Log Id={log.Id}, Patient={log.PatientId}, " +
                                     $"Status={log.Status}, Phase={log.QueryPhase}, Notes={notes}");
                }

                return true;
            }
        }
        catch (Exception ex)
        {
            output.WriteLine($"[DIAG][DataAcq] Check error: {ex.Message}");
        }

        return false;
    }
}
