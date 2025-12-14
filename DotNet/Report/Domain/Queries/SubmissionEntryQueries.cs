using Hl7.Fhir.Model;
using LantanaGroup.Link.Report.Domain.Enums;
using LantanaGroup.Link.Report.Entities;
using LantanaGroup.Link.Shared.Application.Models.Report;
using LantanaGroup.Link.Shared.Application.Models.Responses;
using Microsoft.EntityFrameworkCore;
using System.Threading.Tasks;
using Task = System.Threading.Tasks.Task;

namespace LantanaGroup.Link.Report.Domain.Queries;

public interface ISubmissionEntryQueries
{
    Task<PatientSubmissionEntry?> GetByIdAsync(string id, CancellationToken cancellationToken = default);

    Task<List<PatientSubmissionEntry>> SearchAsync(
        string? id = null,
        string? patientId = null,
        string? facilityId = null,
        string? reportScheduleId = null,
        string? reportType = null,
        CancellationToken cancellationToken = default);

    Task<PatientReportData> GetPatientReportData(
        string facilityId,
        string reportScheduleId,
        string patientId,
        string? patientEntryId = null,
        CancellationToken cancellationToken = default);

    Task<(PatientSubmissionEntry?, FhirResource?)> GetPatientResourceData(
        string facilityId, 
        string reportScheduleId, 
        string patientId, 
        string reportType, 
        string resourceType, 
        string resourceId, 
        CancellationToken cancellationToken = default);

    Task<bool> PatientAllReadyForValidation(
        string facilityId,
        string reportScheduleId,
        string patientId,
        CancellationToken cancellationToken = default);

    Task<PagedConfigModel<ResourceSummary>> GetResourceSummary(
        string facilityId,
        string reportScheduleId,
        ResourceType? resourceType,
        int pageSize,
        int pageNumber,
        CancellationToken cancellationToken = default);
    Task<List<string>> GetMeasureReportResourceTypeList(
        string facilityId, 
        string reportId, 
        CancellationToken requestAborted);

    Task<bool> PatientEntryReadyForValidation(
        string reportScheduleId, 
        string entryId, 
        CancellationToken cancellationToken);
}

/// <summary>
/// Query class for MeasureReportSubmissionEntry entities using EF Core with MongoDB.
/// Provides read-only query methods, including a performant fetch for ResourceEvaluatedListener data.
/// </summary>
public class SubmissionEntryQueries : ISubmissionEntryQueries
{
    private readonly MongoDbContext _context;

    public SubmissionEntryQueries(MongoDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    /// <summary>
    /// Gets a single MeasureReportSubmissionEntry by its ID.
    /// </summary>
    public async Task<PatientSubmissionEntry?> GetByIdAsync(string id, CancellationToken cancellationToken = default)
    {
        return await _context.PatientSubmissionEntries
            .FirstOrDefaultAsync(e => e.Id == id, cancellationToken);
    }

    /// <summary>
    /// Searches for MeasureReportSubmissionEntry based on optional filters.
    /// </summary>
    public async Task<List<PatientSubmissionEntry>> SearchAsync(
        string? id = null,
        string? patientId = null,
        string? facilityId = null,
        string? reportScheduleId = null,
        string? reportType = null,
        CancellationToken cancellationToken = default)
    {
        IQueryable<PatientSubmissionEntry> query = _context.PatientSubmissionEntries;

        if (!string.IsNullOrWhiteSpace(id))
        {
            query = query.Where(e => e.Id == id);
        }

        if (!string.IsNullOrWhiteSpace(patientId))
        {
            query = query.Where(e => e.PatientId == patientId);
        }

        if (!string.IsNullOrWhiteSpace(facilityId))
        {
            query = query.Where(e => e.FacilityId == facilityId);
        }

        if (!string.IsNullOrWhiteSpace(reportScheduleId))
        {
            query = query.Where(e => e.ReportScheduleId == reportScheduleId);
        }

        if (!string.IsNullOrWhiteSpace(reportType))
        {
            query = query.Where(e => e.ReportType == reportType);
        }

        return await query.ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Performant query to fetch all data needed for ResourceEvaluatedListener processing.
    /// Fetches the ReportSchedule, all relevant SubmissionEntries for the patient, and pre-fetches all referenced resources.
    /// To optimize for MongoDB with EF Core, the query is broken into separate server-executable steps:
    /// 1. Fetch the ReportSchedule.
    /// 2. Fetch and group the filtered SubmissionEntries client-side (assuming limited entries per patient).
    /// 3. Fetch distinct FhirResourceIds from ReportScheduleResourceMaps.
    /// 4. Fetch the corresponding FhirResources.
    /// This avoids complex correlated subqueries or joins that may lead to client-side evaluation.
    /// </summary>
    public async Task<PatientReportData> GetPatientReportData(
    string facilityId,
    string reportScheduleId,
    string patientId,
    string? patientEntryId = null,
    CancellationToken cancellationToken = default)
    {
        var report = await _context.ReportSchedules
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == reportScheduleId && r.FacilityId == facilityId, cancellationToken);

        if (report == null)
        {
            throw new InvalidOperationException($"No ReportSchedule found for FacilityId: {facilityId}, ReportScheduleId: {reportScheduleId}");
        }

        var entriesQuery = _context.PatientSubmissionEntries
            .AsNoTracking()
            .Where(e => e.ReportScheduleId == reportScheduleId
                && e.PatientId == patientId
                && (patientEntryId == null || e.Id == patientEntryId)
                && report.ReportTypes.Contains(e.ReportType));

        var entries = await entriesQuery.ToListAsync(cancellationToken);
        var entryIds = entries.Select(e => e.Id).ToList();

        var groupedEntries = entries
            .GroupBy(e => e.ReportType)
            .ToDictionary(g => g.Key, g => g.ToList());

        var resourceIds = await _context.PatientEntryResourceMaps
            .AsNoTracking()
            .Where(m => entryIds.Contains(m.SubmissionEntryId))
            .Select(m => m.FhirResourceId)
            .Distinct()
            .ToListAsync(cancellationToken);

        // Batch fetching of resources
        const int batchSize = 1000;
        var resourceBatches = resourceIds.Chunk(batchSize).ToList();
        var resourceTasks = resourceBatches.Select(batch =>
            _context.FhirResources
                .AsNoTracking()
                .Where(r => batch.Contains(r.Id))
                .ToListAsync(cancellationToken)
        );

        var batchedResources = await Task.WhenAll(resourceTasks);
        var resources = batchedResources.SelectMany(r => r).ToList();

        var reportData = groupedEntries.ToDictionary(
            kv => kv.Key,
            kv => new ReportData
            {
                Entries = kv.Value,
                Resources = resources
            });
        return new PatientReportData
        {
            Schedule = report,
            ReportData = reportData
        };
    }

    public async Task<(PatientSubmissionEntry?, FhirResource?)> GetPatientResourceData(string facilityId, string reportScheduleId, string patientId, string reportType, string resourceType, string resourceId, CancellationToken cancellationToken = default)
    {
        var entry = await _context.PatientSubmissionEntries.SingleAsync(e => e.FacilityId == facilityId && e.ReportScheduleId == reportScheduleId && e.PatientId == patientId && e.ReportType == reportType);

        var resourceMap = await _context.PatientEntryResourceMaps.SingleOrDefaultAsync(m => m.SubmissionEntryId == entry.Id && m.ResourceId == resourceId && m.ResourceType == resourceType);

        if (resourceMap == null)
        {
            return (entry, null);
        }

        var resource = await _context.FhirResources.SingleOrDefaultAsync(r => r.Id == resourceMap.FhirResourceId);

        return (entry, resource);
    }

    public async Task<bool> PatientAllReadyForValidation(
        string facilityId,
        string reportScheduleId,
        string patientId,
        CancellationToken cancellationToken = default)
    {
        return await (from entry in _context.PatientSubmissionEntries
                      where entry.ReportScheduleId == reportScheduleId && entry.FacilityId == facilityId && entry.PatientId == patientId 
                      select entry.Status).AllAsync(s => s == PatientSubmissionStatus.ReadyForValidation || s == PatientSubmissionStatus.NotReportable);
    }

    public async Task<PagedConfigModel<ResourceSummary>> GetResourceSummary(string facilityId, string reportScheduleId, ResourceType? resourceType, int pageSize, int pageNumber,
            CancellationToken cancellationToken = default)
    {
        var resourceIds = await _context.PatientEntryResourceMaps
            .Where(m => m.ReportScheduleId == reportScheduleId)
            .Select(m => m.FhirResourceId)
            .Distinct()
            .ToListAsync(cancellationToken);

        var dataQuery = _context.FhirResources
            .Where(r => resourceIds.Contains(r.Id));

        if (resourceType.HasValue)
        {
            var resourceTypeStr = resourceType.Value.ToString();
            dataQuery = dataQuery.Where(r => r.ResourceType == resourceTypeStr);
        }

        dataQuery = dataQuery.OrderBy(r => r.ResourceType);

        var count = await dataQuery.CountAsync(cancellationToken);

        var resourceData = await dataQuery
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var summaries = resourceData.Select(resource => new ResourceSummary
        {
            FacilityId = facilityId,
            MeasureReportId = reportScheduleId,
            PatientId = resource.PatientId ?? string.Empty,  
            ResourceId = resource.Id,
            FhirId = resource.ResourceId,
            ResourceType = Enum.Parse<ResourceType>(resource.ResourceType),
            ResourceCategory = resource.ResourceCategoryType.ToString()
        }).ToList();

        return new PagedConfigModel<ResourceSummary>(summaries, new PaginationMetadata
        {
            PageSize = pageSize,
            PageNumber = pageNumber,
            TotalCount = count
        });
    }

    public async Task<List<string>> GetMeasureReportResourceTypeList(string facilityId, string reportId, CancellationToken cancellationToken)
    {
        var resourceIds = await _context.PatientEntryResourceMaps
            .Where(r => r.ReportScheduleId == reportId)
            .Select(r => r.FhirResourceId)
            .Distinct()
            .ToListAsync(cancellationToken);

        var data = await _context.FhirResources
            .Where(resource => resourceIds.Contains(resource.Id))
            .Select(resource => resource.ResourceType)
            .Distinct()
            .ToListAsync(cancellationToken);

        return data;
    }

    public async Task<bool> PatientEntryReadyForValidation(string reportScheduleId, string entryId, CancellationToken cancellationToken)
    {
        bool hasMeasureReport = await (from entry in _context.PatientSubmissionEntries
                                       where entry.Id == entryId
                                       select entry).AllAsync(e => e.MeasureReport != null);
        if(!hasMeasureReport)
        {
            return false;
        }

        return await (from map in _context.PatientEntryResourceMaps
                      where map.SubmissionEntryId == entryId && map.ReportScheduleId == reportScheduleId
                      select map).AllAsync(e => e.FhirResourceId != null);
    }
}

/// <summary>
/// DTO for ResourceEvaluatedListener data.
/// </summary>
public class PatientReportData
{
    public ReportSchedule Schedule { get; set; } = null!;

    public Dictionary<string, ReportData> ReportData { get; set; } = new();
}

public class ReportData
{
    public List<PatientSubmissionEntry> Entries { get; set; } = new();
    public List<FhirResource> Resources { get; set; } = new();
}