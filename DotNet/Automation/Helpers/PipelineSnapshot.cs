using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models.Enums;
using LantanaGroup.Link.Report.Domain.Enums;
using Xunit.Abstractions;

namespace LantanaGroup.Link.Automation.Helpers;

/// <summary>
/// Non-asserting, read-only snapshot of the pipeline's database state.
/// Uses <see cref="PipelineDataReader"/> for all data retrieval.
/// </summary>
public class PipelineSnapshot
{
    private readonly DatabaseConnectionFactory _dbFactory;
    private readonly PipelineDataReader _reader;

    public PipelineSnapshot(DatabaseConnectionFactory dbFactory)
    {
        _dbFactory = dbFactory;
        _reader = new PipelineDataReader(dbFactory);
    }

    /// <summary>
    /// Writes a complete, non-asserting pipeline snapshot to test output.
    /// Safe to call at any point — never throws.
    /// </summary>
    public async Task WriteFullSnapshotAsync(
        ITestOutputHelper output,
        string facilityId,
        string reportId)
    {
        output.WriteLine("\n=== PIPELINE DIAGNOSTIC SNAPSHOT ===\n");

        var scheduleId = Guid.Parse(reportId);

        await WriteReportSnapshot(output, facilityId, scheduleId);
        await WriteDataAcquisitionSnapshot(output, facilityId, reportId);
        await WriteNormalizationSnapshot(output, facilityId);
        await WriteTenantSnapshot(output, facilityId);
        await WriteValidationSnapshot(output, facilityId, reportId);

        output.WriteLine("\n=== END SNAPSHOT ===\n");
    }

    private async Task WriteReportSnapshot(ITestOutputHelper output, string facilityId, Guid scheduleId)
    {
        try
        {
            var schedule = await _reader.GetReportScheduleAsync(scheduleId);
            if (schedule == null)
            {
                output.WriteLine("[Snapshot][ReportSchedule]     NOT FOUND");
            }
            else
            {
                output.WriteLine($"[Snapshot][ReportSchedule]     Status={schedule.Status}, " +
                                 $"Frequency={schedule.Frequency}, AdHocType={schedule.AdHocType}, " +
                                 $"EnableSubmission={schedule.EnableSubmission}");
            }

            var reportTypes = await _reader.GetScheduleReportTypesAsync(scheduleId);
            output.WriteLine($"[Snapshot][ScheduleReportType] {reportTypes.Count} row(s)" +
                             (reportTypes.Count > 0 ? $" | {string.Join(", ", reportTypes.Select(rt => rt.ReportType))}" : ""));

            var entries = await _reader.GetReportEntriesAsync(scheduleId);
            if (entries.Count == 0)
            {
                output.WriteLine("[Snapshot][ReportEntry]         0 rows");
            }
            else
            {
                var byReporting = entries.GroupBy(e => e.ReportingStatus)
                    .Select(g => $"{g.Key}={g.Count()}");
                var bySubmission = entries.Where(e => e.SubmissionStatus != null)
                    .GroupBy(e => e.SubmissionStatus)
                    .Select(g => $"{g.Key}={g.Count()}");

                output.WriteLine($"[Snapshot][ReportEntry]         {entries.Count} row(s) | " +
                                 $"Reporting: {string.Join(", ", byReporting)} | " +
                                 $"Submission: {string.Join(", ", bySubmission)}");
            }

            var measureReports = await _reader.GetEntryMeasureReportsAsync(scheduleId);
            if (measureReports.Count == 0)
            {
                output.WriteLine("[Snapshot][EntryMeasureReport]  0 rows");
            }
            else
            {
                var byStatus = measureReports.GroupBy(r => r.Status)
                    .Select(g => $"{g.Key}={g.Count()}");
                var withMrId = measureReports.Count(r => !string.IsNullOrWhiteSpace(r.MeasureReportId));

                output.WriteLine($"[Snapshot][EntryMeasureReport]  {measureReports.Count} row(s) | " +
                                 $"Status: {string.Join(", ", byStatus)} | " +
                                 $"WithMeasureReportId: {withMrId}/{measureReports.Count}");
            }

            var resources = await _reader.GetReportResourceSummaryAsync(scheduleId, facilityId);
            var totalResources = resources.Sum(r => r.Count);
            var patientCount = resources.Select(r => r.PatientId).Distinct().Count();
            output.WriteLine($"[Snapshot][ReportResource]      {totalResources} resource(s) across {patientCount} patient(s)");

            var populations = await _reader.GetReportPopulationsAsync(scheduleId, facilityId);
            var groupCount = populations.SelectMany(p => p.GroupPopulations).Count();
            var mrpCount = populations.SelectMany(p => p.GroupPopulations).SelectMany(gp => gp.MeasureReportPopulations).Count();
            output.WriteLine($"[Snapshot][ReportPopulation]    {populations.Count} population(s), " +
                             $"{groupCount} group(s), {mrpCount} measure report population(s)");
        }
        catch (Exception ex)
        {
            output.WriteLine($"[Snapshot][Report] Error querying Report DB: {ex.Message}");
        }
    }

    private async Task WriteDataAcquisitionSnapshot(ITestOutputHelper output, string facilityId, string reportId)
    {
        try
        {
            var logs = await _reader.GetAcquisitionLogsAsync(facilityId, reportId);
            if (logs.Count == 0)
            {
                output.WriteLine("[Snapshot][DataAcqLog]          0 rows");
            }
            else
            {
                var byStatus = logs.GroupBy(l => l.Status)
                    .Select(g => $"{g.Key}={g.Count()}");
                var patientCount = logs.Where(l => l.PatientId != null)
                    .Select(l => l.PatientId).Distinct().Count();

                output.WriteLine($"[Snapshot][DataAcqLog]          {logs.Count} row(s) for {patientCount} patient(s) | " +
                                 $"{string.Join(", ", byStatus)}");

                var failedLogs = logs
                    .Where(l => l.Status == RequestStatus.Failed || l.Status == RequestStatus.MaxRetriesReached)
                    .Take(5)
                    .ToList();

                foreach (var log in failedLogs)
                {
                    var notes = log.Notes.Count > 0 ? string.Join(" | ", log.Notes.Take(3)) : "(no notes)";
                    output.WriteLine($"[Snapshot][DataAcqLog]          FAILED Id={log.Id}, Patient={log.PatientId}, " +
                                     $"Status={log.Status}, Phase={log.QueryPhase}, Notes={notes}");
                }
            }

            var hasConfig = await _reader.HasFhirQueryConfigurationAsync(facilityId);
            output.WriteLine($"[Snapshot][FhirQueryConfig]     {(hasConfig ? "exists" : "NOT FOUND")}");

            var plans = await _reader.GetQueryPlansAsync(facilityId);
            output.WriteLine($"[Snapshot][QueryPlan]           {plans.Count} plan(s)");
        }
        catch (Exception ex)
        {
            output.WriteLine($"[Snapshot][DataAcq] Error querying DataAcquisition DB: {ex.Message}");
        }
    }

    private async Task WriteNormalizationSnapshot(ITestOutputHelper output, string facilityId)
    {
        try
        {
            var operations = await _reader.GetOperationsAsync(facilityId);

            if (operations.Count == 0)
            {
                output.WriteLine("[Snapshot][NormOperation]       0 rows");
            }
            else
            {
                var byType = operations.GroupBy(o => o.OperationType)
                    .Select(g => $"{g.Key}={g.Count()}");
                var disabledCount = operations.Count(o => o.IsDisabled);

                output.WriteLine($"[Snapshot][NormOperation]       {operations.Count} operation(s) | " +
                                 $"Types: {string.Join(", ", byType)} | " +
                                 $"Disabled: {disabledCount}");

                foreach (var op in operations)
                {
                    var resourceTypes = op.OperationResourceTypes
                        .Select(ort => ort.ResourceType?.Name ?? "(unknown)")
                        .ToList();
                    output.WriteLine($"[Snapshot][NormOperation]         Id={op.Id}, Type={op.OperationType}, " +
                                     $"Name={op.Name}, ResourceTypes=[{string.Join(", ", resourceTypes)}]");
                }
            }

            var sequences = await _reader.GetOperationSequencesAsync(facilityId);

            if (sequences.Count == 0)
            {
                output.WriteLine("[Snapshot][NormSequence]        0 rows");
            }
            else
            {
                output.WriteLine($"[Snapshot][NormSequence]        {sequences.Count} sequence(s)");
                foreach (var seq in sequences)
                {
                    var opType = seq.OperationResourceType?.Operation?.OperationType ?? "(unknown)";
                    var resType = seq.OperationResourceType?.ResourceType?.Name ?? "(unknown)";
                    output.WriteLine($"[Snapshot][NormSequence]          Id={seq.Id}, Sequence={seq.Sequence}, " +
                                     $"OperationType={opType}, ResourceType={resType}");
                }
            }
        }
        catch (Exception ex)
        {
            output.WriteLine($"[Snapshot][Normalization] Error querying Normalization DB: {ex.Message}");
        }
    }

    private async Task WriteTenantSnapshot(ITestOutputHelper output, string facilityId)
    {
        try
        {
            var facility = await _reader.GetFacilityAsync(facilityId);

            if (facility == null)
            {
                output.WriteLine("[Snapshot][Tenant]              Facility NOT FOUND");
                return;
            }

            var monthly = facility.ScheduledReports?.Monthly ?? [];
            var daily = facility.ScheduledReports?.Daily ?? [];
            var weekly = facility.ScheduledReports?.Weekly ?? [];

            output.WriteLine($"[Snapshot][Tenant]              FacilityId={facility.FacilityId}, " +
                             $"Name={facility.FacilityName}, TimeZone={facility.TimeZone}, " +
                             $"IsDeleted={facility.IsDeleted}, Created={facility.CreateDate:O}");
            output.WriteLine($"[Snapshot][Tenant]              ScheduledReports: " +
                             $"Monthly=[{string.Join(", ", monthly)}], " +
                             $"Daily=[{string.Join(", ", daily)}], " +
                             $"Weekly=[{string.Join(", ", weekly)}]");
        }
        catch (Exception ex)
        {
            output.WriteLine($"[Snapshot][Tenant] Error querying Tenant DB: {ex.Message}");
        }
    }

    private async Task WriteValidationSnapshot(
        ITestOutputHelper output,
        string facilityId,
        string reportId)
    {
        try
        {
            var connectionString = _dbFactory.GetConnectionString(DatabaseConnectionFactory.Databases.Validation);
            await using var connection = new Microsoft.Data.SqlClient.SqlConnection(connectionString);
            await connection.OpenAsync();

            // Count results by severity
            var severityQuery = @"
                SELECT severity, COUNT(*) as cnt
                FROM result
                WHERE facility_id = @facilityId AND report_id = @reportId
                GROUP BY severity
                ORDER BY severity";

            await using var severityCmd = new Microsoft.Data.SqlClient.SqlCommand(severityQuery, connection);
            severityCmd.Parameters.AddWithValue("@facilityId", facilityId);
            severityCmd.Parameters.AddWithValue("@reportId", reportId);

            var severityCounts = new List<string>();
            var totalResults = 0;

            await using (var reader = await severityCmd.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                {
                    var severity = reader.GetString(0);
                    var count = reader.GetInt32(1);
                    totalResults += count;
                    severityCounts.Add($"{severity}={count}");
                }
            }

            output.WriteLine($"[Snapshot][Validation]          {totalResults} result(s)" +
                             (severityCounts.Count > 0 ? $" | {string.Join(", ", severityCounts)}" : ""));

            if (totalResults == 0) return;

            // Count results by patient
            var patientQuery = @"
                SELECT patient_id, COUNT(*) as cnt
                FROM result
                WHERE facility_id = @facilityId AND report_id = @reportId
                GROUP BY patient_id";

            await using var patientCmd = new Microsoft.Data.SqlClient.SqlCommand(patientQuery, connection);
            patientCmd.Parameters.AddWithValue("@facilityId", facilityId);
            patientCmd.Parameters.AddWithValue("@reportId", reportId);

            await using (var reader = await patientCmd.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                {
                    var patientId = reader.GetString(0);
                    var count = reader.GetInt32(1);
                    output.WriteLine($"[Snapshot][Validation]            Patient {patientId}: {count} result(s)");
                }
            }

            // Top 5 error messages for quick diagnosis
            var topErrorsQuery = @"
                SELECT TOP 10 severity, message, COUNT(*) as cnt
                FROM result
                WHERE facility_id = @facilityId AND report_id = @reportId
                  AND severity IN ('ERROR', 'FATAL')
                GROUP BY severity, message
                ORDER BY cnt DESC";

            await using var errorsCmd = new Microsoft.Data.SqlClient.SqlCommand(topErrorsQuery, connection);
            errorsCmd.Parameters.AddWithValue("@facilityId", facilityId);
            errorsCmd.Parameters.AddWithValue("@reportId", reportId);

            var hasErrors = false;
            await using (var reader = await errorsCmd.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                {
                    if (!hasErrors)
                    {
                        output.WriteLine("[Snapshot][Validation]          Top errors:");
                        hasErrors = true;
                    }
                    var severity = reader.GetString(0);
                    var msg      = reader.GetString(1);
                    var count    = reader.GetInt32(2);
                    output.WriteLine($"[Snapshot][Validation]            [{severity}] x{count}: {msg}");
                }
            }
        }
        catch (Exception ex)
        {
            output.WriteLine($"[Snapshot][Validation] Error querying Validation DB: {ex.Message}");
        }
    }
}
