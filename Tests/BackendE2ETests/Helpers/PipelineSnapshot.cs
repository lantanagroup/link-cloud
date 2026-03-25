using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Context;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models.Enums;
using LantanaGroup.Link.Normalization.Domain.Entities;
using LantanaGroup.Link.Report.Data;
using LantanaGroup.Link.Report.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Xunit.Abstractions;
using DataAcquisitionLog = LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Entities.DataAcquisitionLog;

namespace LantanaGroup.Link.Tests.E2ETests.Helpers;

/// <summary>
/// Non-asserting, read-only snapshot of the pipeline's database state.
/// Used by both <see cref="DatabaseProgressMonitor"/> (real-time) and
/// by the test's try/finally block (last-chance dump). Never throws
/// assertion exceptions — only queries and formats results.
/// </summary>
public static class PipelineSnapshot
{
    // ??????????????????????????????????????????????
    //  Report DB queries
    // ??????????????????????????????????????????????

    public static async Task<ReportSchedule?> GetReportScheduleAsync(ReportDbContext db, Guid scheduleId)
    {
        return await db.ReportSchedule.FirstOrDefaultAsync(s => s.Id == scheduleId);
    }

    public static async Task<List<ReportEntry>> GetReportEntriesAsync(ReportDbContext db, Guid scheduleId)
    {
        return await db.ReportEntry
            .Where(e => e.ReportScheduleId == scheduleId)
            .ToListAsync();
    }

    public static async Task<List<EntryMeasureReport>> GetEntryMeasureReportsAsync(ReportDbContext db, Guid scheduleId)
    {
        return await db.EntryMeasureReport
            .Include(emr => emr.ResourceCounts)
            .Where(emr => emr.ReportEntry.ReportScheduleId == scheduleId)
            .ToListAsync();
    }

    public static async Task<List<ScheduleReportType>> GetScheduleReportTypesAsync(ReportDbContext db, Guid scheduleId)
    {
        return await db.ScheduleReportType
            .Where(rt => rt.ReportScheduleId == scheduleId)
            .ToListAsync();
    }

    public record ResourceGroupSummary(string PatientId, string ResourceType, int Count);

    public static async Task<List<ResourceGroupSummary>> GetReportResourceSummaryAsync(
        ReportDbContext db, Guid scheduleId, string facilityId)
    {
        // Project to anonymous type server-side (EF can translate this),
        // then map to the record client-side after materialization.
        var raw = await db.ReportResource
            .Where(r => r.ReportScheduleId == scheduleId && r.FacilityId == facilityId)
            .GroupBy(r => new { r.PatientId, r.ResourceType })
            .Select(g => new { g.Key.PatientId, g.Key.ResourceType, Count = g.Count() })
            .OrderBy(x => x.PatientId).ThenBy(x => x.ResourceType)
            .ToListAsync();

        return raw.Select(x => new ResourceGroupSummary(x.PatientId, x.ResourceType, x.Count)).ToList();
    }

    public static async Task<List<ReportPopulation>> GetReportPopulationsAsync(
        ReportDbContext db, Guid scheduleId, string facilityId)
    {
        return await db.ReportPopulation
            .Include(p => p.GroupPopulations)
                .ThenInclude(gp => gp.MeasureReportPopulations)
            .Where(p => p.ReportScheduleId == scheduleId && p.FacilityId == facilityId)
            .ToListAsync();
    }

    // ??????????????????????????????????????????????
    //  DataAcquisition DB queries
    // ??????????????????????????????????????????????

    public static async Task<List<DataAcquisitionLog>> GetAcquisitionLogsAsync(
        DataAcquisitionDbContext db, string facilityId, string reportId)
    {
        return await db.DataAcquisitionLogs
            .Where(l => l.FacilityId == facilityId && l.ReportTrackingId == reportId)
            .ToListAsync();
    }

    // ??????????????????????????????????????????????
    //  Normalization DB queries
    // ??????????????????????????????????????????????

    public static async Task<List<Operation>> GetOperationsAsync(
        NormalizationDbContext db, string facilityId)
    {
        return await db.Operations
            .Include(o => o.OperationResourceTypes)
                .ThenInclude(ort => ort.ResourceType)
            .Where(o => o.FacilityId == facilityId)
            .ToListAsync();
    }

    public static async Task<List<OperationSequence>> GetOperationSequencesAsync(
        NormalizationDbContext db, string facilityId)
    {
        return await db.OperationSequences
            .Include(os => os.OperationResourceType)
                .ThenInclude(ort => ort.Operation)
            .Include(os => os.OperationResourceType)
                .ThenInclude(ort => ort.ResourceType)
            .Where(os => os.FacilityId == facilityId)
            .OrderBy(os => os.Sequence)
            .ToListAsync();
    }

    // ??????????????????????????????????????????????
    //  Formatted summary (non-asserting)
    // ??????????????????????????????????????????????

    /// <summary>
    /// Writes a complete, non-asserting pipeline snapshot to test output.
    /// Safe to call at any point — never throws.
    /// </summary>
    public static async Task WriteFullSnapshotAsync(
        ITestOutputHelper output,
        string facilityId,
        string reportId)
    {
        output.WriteLine("\n=== PIPELINE DIAGNOSTIC SNAPSHOT ===\n");

        var scheduleId = Guid.Parse(reportId);

        await WriteReportSnapshot(output, facilityId, scheduleId);
        await WriteDataAcquisitionSnapshot(output, facilityId, reportId);
        await WriteNormalizationSnapshot(output, facilityId);

        output.WriteLine("\n=== END SNAPSHOT ===\n");
    }

    private static async Task WriteReportSnapshot(ITestOutputHelper output, string facilityId, Guid scheduleId)
    {
        try
        {
            await using var db = DatabaseConnectionFactory.CreateReportDbContext();

            // Schedule
            var schedule = await GetReportScheduleAsync(db, scheduleId);
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

            // Report types
            var reportTypes = await GetScheduleReportTypesAsync(db, scheduleId);
            output.WriteLine($"[Snapshot][ScheduleReportType] {reportTypes.Count} row(s)" +
                             (reportTypes.Count > 0 ? $" | {string.Join(", ", reportTypes.Select(rt => rt.ReportType))}" : ""));

            // Entries
            var entries = await GetReportEntriesAsync(db, scheduleId);
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

            // Measure reports
            var measureReports = await GetEntryMeasureReportsAsync(db, scheduleId);
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

            // Resources
            var resources = await GetReportResourceSummaryAsync(db, scheduleId, facilityId);
            var totalResources = resources.Sum(r => r.Count);
            var patientCount = resources.Select(r => r.PatientId).Distinct().Count();
            output.WriteLine($"[Snapshot][ReportResource]      {totalResources} resource(s) across {patientCount} patient(s)");

            // Populations
            var populations = await GetReportPopulationsAsync(db, scheduleId, facilityId);
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

    private static async Task WriteDataAcquisitionSnapshot(ITestOutputHelper output, string facilityId, string reportId)
    {
        try
        {
            await using var db = DatabaseConnectionFactory.CreateDataAcquisitionDbContext();

            var logs = await GetAcquisitionLogsAsync(db, facilityId, reportId);
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

                // Surface failed logs with notes
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

            // Config check
            var hasConfig = await db.FhirQueryConfigurations.AnyAsync(c => c.FacilityId == facilityId);
            output.WriteLine($"[Snapshot][FhirQueryConfig]     {(hasConfig ? "exists" : "NOT FOUND")}");

            // Query plans
            var planCount = await db.QueryPlans.CountAsync(qp => qp.FacilityId == facilityId);
            output.WriteLine($"[Snapshot][QueryPlan]           {planCount} plan(s)");
        }
        catch (Exception ex)
        {
            output.WriteLine($"[Snapshot][DataAcq] Error querying DataAcquisition DB: {ex.Message}");
        }
    }

    private static async Task WriteNormalizationSnapshot(ITestOutputHelper output, string facilityId)
    {
        try
        {
            await using var db = DatabaseConnectionFactory.CreateNormalizationDbContext();

            var operations = await GetOperationsAsync(db, facilityId);

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

            var sequences = await GetOperationSequencesAsync(db, facilityId);

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
}
