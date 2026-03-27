using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Context;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Entities;
using LantanaGroup.Link.Normalization.Domain.Entities;
using LantanaGroup.Link.Report.Data;
using LantanaGroup.Link.Report.Data.Entities;
using LantanaGroup.Link.Tenant.Entities;
using LantanaGroup.Link.Tenant.Repository.Context;
using Microsoft.EntityFrameworkCore;

namespace LantanaGroup.Link.Automation.Helpers;

/// <summary>
/// Shared read-only data access layer for pipeline diagnostics and validators.
/// Owns EF context creation and query composition so callers only focus on
/// formatting (snapshots) or rules (validators).
/// </summary>
public class PipelineDataReader
{
    private readonly DatabaseConnectionFactory _dbFactory;

    public PipelineDataReader(DatabaseConnectionFactory dbFactory)
    {
        _dbFactory = dbFactory;
    }

    public record ResourceGroupSummary(string PatientId, string ResourceType, int Count);
    public record ReportResourceIdentity(string PatientId, string ResourceType, string ResourceId);

    // Report DB
    public async Task<ReportSchedule?> GetReportScheduleAsync(Guid scheduleId)
    {
        await using var db = _dbFactory.CreateReportDbContext();
        return await db.ReportSchedule.FirstOrDefaultAsync(s => s.Id == scheduleId);
    }

    public async Task<List<ReportEntry>> GetReportEntriesAsync(Guid scheduleId)
    {
        await using var db = _dbFactory.CreateReportDbContext();
        return await db.ReportEntry.Where(e => e.ReportScheduleId == scheduleId).ToListAsync();
    }

    public async Task<List<ReportEntry>> GetReportEntriesWithMeasureReportsAsync(Guid scheduleId)
    {
        await using var db = _dbFactory.CreateReportDbContext();
        return await db.ReportEntry
            .Include(e => e.MeasureReports)
            .Where(e => e.ReportScheduleId == scheduleId)
            .ToListAsync();
    }

    public async Task<List<EntryMeasureReport>> GetEntryMeasureReportsAsync(Guid scheduleId)
    {
        await using var db = _dbFactory.CreateReportDbContext();
        return await db.EntryMeasureReport
            .Include(emr => emr.ResourceCounts)
            .Where(emr => emr.ReportEntry.ReportScheduleId == scheduleId)
            .ToListAsync();
    }

    public async Task<List<ScheduleReportType>> GetScheduleReportTypesAsync(Guid scheduleId)
    {
        await using var db = _dbFactory.CreateReportDbContext();
        return await db.ScheduleReportType
            .Where(rt => rt.ReportScheduleId == scheduleId)
            .ToListAsync();
    }

    public async Task<List<ResourceGroupSummary>> GetReportResourceSummaryAsync(Guid scheduleId, string facilityId)
    {
        await using var db = _dbFactory.CreateReportDbContext();
        var raw = await db.ReportResource
            .Where(r => r.ReportScheduleId == scheduleId && r.FacilityId == facilityId)
            .GroupBy(r => new { r.PatientId, r.ResourceType })
            .Select(g => new { g.Key.PatientId, g.Key.ResourceType, Count = g.Count() })
            .OrderBy(x => x.PatientId).ThenBy(x => x.ResourceType)
            .ToListAsync();

        return raw.Select(x => new ResourceGroupSummary(x.PatientId, x.ResourceType, x.Count)).ToList();
    }

    public async Task<List<ReportPopulation>> GetReportPopulationsAsync(Guid scheduleId, string facilityId)
    {
        await using var db = _dbFactory.CreateReportDbContext();
        return await db.ReportPopulation
            .Include(p => p.GroupPopulations)
                .ThenInclude(gp => gp.MeasureReportPopulations)
            .Where(p => p.ReportScheduleId == scheduleId && p.FacilityId == facilityId)
            .ToListAsync();
    }

    public async Task<List<ReportResourceIdentity>> GetReportResourceIdentitiesAsync(Guid scheduleId, string facilityId)
    {
        await using var db = _dbFactory.CreateReportDbContext();
        return await db.ReportResource
            .Where(r => r.ReportScheduleId == scheduleId && r.FacilityId == facilityId)
            .Select(r => new ReportResourceIdentity(r.PatientId, r.ResourceType, r.ResourceId))
            .ToListAsync();
    }

    public async Task<List<ReportEntry>> GetSubmittedReportEntriesAsync(Guid scheduleId)
    {
        await using var db = _dbFactory.CreateReportDbContext();
        return await db.ReportEntry
            .Where(e => e.ReportScheduleId == scheduleId && e.SubmissionStatus == LantanaGroup.Link.Report.Domain.Enums.SubmissionStatus.Submitted)
            .ToListAsync();
    }

    // Data acquisition DB
    public async Task<List<DataAcquisitionLog>> GetAcquisitionLogsAsync(string facilityId, string reportId)
    {
        await using var db = _dbFactory.CreateDataAcquisitionDbContext();
        return await db.DataAcquisitionLogs
            .Where(l => l.FacilityId == facilityId && l.ReportTrackingId == reportId)
            .ToListAsync();
    }

    public async Task<bool> HasFhirQueryConfigurationAsync(string facilityId)
    {
        await using var db = _dbFactory.CreateDataAcquisitionDbContext();
        return await db.FhirQueryConfigurations.AnyAsync(c => c.FacilityId == facilityId);
    }

    public async Task<List<LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Entities.QueryPlan>> GetQueryPlansAsync(string facilityId)
    {
        await using var db = _dbFactory.CreateDataAcquisitionDbContext();
        return await db.QueryPlans.Where(qp => qp.FacilityId == facilityId).ToListAsync();
    }

    public async Task<List<LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Entities.FhirQuery>> GetFhirQueriesForReportAsync(string facilityId, string reportId)
    {
        await using var db = _dbFactory.CreateDataAcquisitionDbContext();
        return await db.FhirQueries
            .Include(q => q.FhirQueryResourceTypes)
            .Where(q => q.FacilityId == facilityId && q.DataAcquisitionLog.ReportTrackingId == reportId)
            .ToListAsync();
    }

    public async Task<int> GetReferenceResourceGroupCountAsync(string facilityId)
    {
        await using var db = _dbFactory.CreateDataAcquisitionDbContext();
        return await db.ReferenceResources
            .Where(r => r.FacilityId == facilityId)
            .GroupBy(r => new { r.ResourceType, r.QueryPhase })
            .CountAsync();
    }

    // Normalization DB
    public async Task<List<Operation>> GetOperationsAsync(string facilityId)
    {
        await using var db = _dbFactory.CreateNormalizationDbContext();
        return await db.Operations
            .Include(o => o.OperationResourceTypes)
                .ThenInclude(ort => ort.ResourceType)
            .Where(o => o.FacilityId == facilityId)
            .ToListAsync();
    }

    public async Task<List<OperationSequence>> GetOperationSequencesAsync(string facilityId)
    {
        await using var db = _dbFactory.CreateNormalizationDbContext();
        return await db.OperationSequences
            .Include(os => os.OperationResourceType)
                .ThenInclude(ort => ort.Operation)
            .Include(os => os.OperationResourceType)
                .ThenInclude(ort => ort.ResourceType)
            .Where(os => os.FacilityId == facilityId)
            .OrderBy(os => os.Sequence)
            .ToListAsync();
    }

    // Tenant DB
    public async Task<Facility?> GetFacilityAsync(string facilityId)
    {
        await using var db = _dbFactory.CreateTenantDbContext();
        return await db.Facilities.FirstOrDefaultAsync(f => f.FacilityId == facilityId);
    }
}
