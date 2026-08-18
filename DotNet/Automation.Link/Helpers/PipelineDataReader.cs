using System.Collections.Concurrent;
using System.Net;
using LantanaGroup.Link.Sdk.Clients;
using LantanaGroup.Link.Shared.Application.Models.Integration.DataAcquisition;
using RequestStatus = LantanaGroup.Link.Shared.Application.Models.Integration.DataAcquisition.RequestStatus;
using QueryPhase = LantanaGroup.Link.Shared.Application.Models.Integration.DataAcquisition.QueryPhase;
using FhirQueryType = LantanaGroup.Link.Shared.Application.Models.Integration.DataAcquisition.FhirQueryType;

namespace LantanaGroup.Link.Automation.Link.Helpers;

public class PipelineDataReader
{
    private readonly IReportServiceClient _reportClient;
    private readonly IDataAcquisitionServiceClient _dataAcqClient;
    private readonly INormalizationServiceClient _normalizationClient;
    private readonly IFacilityServiceClient _facilityClient;

    // ---------------------------------------------------------------
    // Time-based cache collapses duplicate HTTP calls from the
    // many consumers (ProgressMonitor, PipelineProgressTracker,
    // MilestoneValidationOrchestrator, StoreBackedServicePoller,
    // PipelineSnapshot) into a single call per TTL window.
    // ---------------------------------------------------------------
    private static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(8);

    private readonly ConcurrentDictionary<string, (object? Value, DateTime ExpiresAt)> _cache = new();
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _cacheLocks = new();

    public PipelineDataReader(
        IReportServiceClient reportClient,
        IDataAcquisitionServiceClient dataAcqClient,
        INormalizationServiceClient normalizationClient,
        IFacilityServiceClient facilityClient)
    {
        _reportClient = reportClient;
        _dataAcqClient = dataAcqClient;
        _normalizationClient = normalizationClient;
        _facilityClient = facilityClient;
    }

    /// <summary>
    /// Returns a cached value if fresh, otherwise calls the factory once (per key)
    /// and caches the result. Concurrent callers for the same key wait for the
    /// single in-flight fetch rather than issuing duplicate HTTP calls.
    /// </summary>
    private async Task<T?> GetOrFetchAsync<T>(string cacheKey, Func<Task<T?>> factory)
    {
        if (_cache.TryGetValue(cacheKey, out var entry) && DateTime.UtcNow < entry.ExpiresAt)
            return (T?)entry.Value;

        var keyLock = _cacheLocks.GetOrAdd(cacheKey, _ => new SemaphoreSlim(1, 1));
        await keyLock.WaitAsync();
        try
        {
            // Double-check after acquiring lock
            if (_cache.TryGetValue(cacheKey, out entry) && DateTime.UtcNow < entry.ExpiresAt)
                return (T?)entry.Value;

            var result = await factory();
            _cache[cacheKey] = (result, DateTime.UtcNow.Add(CacheTtl));
            return result;
        }
        finally
        {
            keyLock.Release();
        }
    }

    /// <summary>
    /// Evicts all cached data. Call between test runs or when the pipeline
    /// context changes (e.g., new facilityId / reportId).
    /// </summary>
    public void InvalidateCache()
    {
        _cache.Clear();
    }


    public record PatientResourceTypeCount(string PatientId, string ResourceType, int Count);

    public record ReportScheduleInfo(
        string? FacilityId,
        string? Status,
        string? Frequency,
        string? AdHocType,
        bool EnableSubmission,
        bool EndOfReportPeriodJobHasRun,
        string? PayloadRootUri,
        DateTime? ReportStartDate,
        DateTime? ReportEndDate,
        DateTime? CreateDate,
        DateTime? SubmitReportDateTime);

    public record MeasureReportInfo(string? MeasureReportId, string? Status, string? ReportType, List<ResourceCountInfo> ResourceCounts);
    public record ResourceCountInfo(string ResourceType, int ResourceCount);
    public record ReportEntryInfo(Guid Id, string? FacilityId, string PatientId, string? ReportingStatus, string? SubmissionStatus, List<MeasureReportInfo> MeasureReports);
    public record EntryMeasureReportInfo(Guid Id, string? ReportType, string? MeasureReportId, string? Status, string PatientId, List<ResourceCountInfo> ResourceCounts);
    public record ScheduleReportTypeInfo(string ReportType);
    public record ReportPopulationInfo(string? ReportType, List<GroupPopulationInfo> GroupPopulations);
    public record GroupPopulationInfo(string? PopulationCodeJson, List<MeasureReportPopulationInfo> MeasureReportPopulations);
    public record MeasureReportPopulationInfo(string? MeasureReportId);

    public record AcquisitionLogInfo(
        long Id,
        string? PatientId,
        string? CorrelationId,
        string? ReportTrackingId,
        string? Status,
        string? QueryPhase,
        List<string> Notes,
        List<string> ResourceAcquiredIds,
        List<FhirQueryInfo> FhirQueries);

    public record StatusCountInfo(string Status, int Count);
    public record ResourceTypeCountInfo(string ResourceType, int Count);
    public record AcquisitionSummaryInfo(
        string ReportId,
        int TotalLogs,
        int TotalPatients,
        int TotalCompletedPatients,
        int TotalResourcesAcquired,
        int TotalRetryAttempts,
        long TotalCompletionTimeMs,
        long AverageCompletionTimeMs,
        List<StatusCountInfo> StatusCounts,
        List<ResourceTypeCountInfo> ResourceTypeCounts);
    public record QueryPlanInfo(string Type, string? PlanName, int InitialQueriesCount, int SupplementalQueriesCount);
    public record FhirQueryInfo(List<string> ResourceTypes);

    public record OperationInfo(string? Id, string? OperationType, string? Name, string? OperationJson, bool IsDisabled, List<string> ResourceTypes);
    public record OperationSequenceInfo(string? Id, int? Sequence, string? OperationType, string? ResourceType);

    public record FacilityScheduledReports(string[] Monthly, string[] Daily, string[] Weekly);
    public record FacilityInfo(string FacilityId, string? FacilityName, string? TimeZone, bool IsDeleted, DateTime? CreateDate, FacilityScheduledReports? ScheduledReports);
    public record OrganizationLocationConfigurationInfo(int ConfigId, bool IsActive, int ConditionsCount);
    public record OrganizationLocationMappingInfo(string? FacilityId, string? LocationId, bool IsOrgLocation, bool IsActive, string? PartOfValue);
    public record EncounterLocationInfo(string? LocationId);
    public record EncounterMappingInfo(string? FacilityId, string? PatientId, string? EncounterId, bool MappedToOrg, List<EncounterLocationInfo> EncounterLocations);

    public Task<ReportScheduleInfo?> GetReportScheduleAsync(Guid scheduleId)
    {
        return GetOrFetchAsync($"schedule:{scheduleId}", async () =>
        {
            var response = await _reportClient.SearchSchedulesAsync(scheduleId.ToString());
            var record = response.Body?.Records?.FirstOrDefault();
            if (record == null)
                return null;

            return new ReportScheduleInfo(
                record.FacilityId,
                record.Status.ToString(),
                record.Frequency.ToString(),
                record.AdHocType.ToString(),
                record.EnableSubmission,
                record.EndOfReportPeriodJobHasRun,
                record.PayloadRootUri,
                record.ReportStartDate,
                record.ReportEndDate,
                record.CreateDate,
                record.SubmitReportDateTime);
        });
    }

    public Task<List<ReportEntryInfo>> GetReportEntriesAsync(Guid scheduleId)
        => GetReportEntriesWithMeasureReportsAsync(scheduleId);

    public async Task<List<ReportEntryInfo>> GetReportEntriesWithMeasureReportsAsync(Guid scheduleId)
    {
        var result = await GetOrFetchAsync($"entries:{scheduleId}", async () =>
        {
            var response = await _reportClient.GetEntriesByScheduleAsync(scheduleId.ToString());
            var entries = response.Body;
            if (entries == null)
                return new List<ReportEntryInfo>();

            return entries.Select(e =>
            {
                var mrs = e.MeasureReports.Select(mr => new MeasureReportInfo(
                    mr.MeasureReportId,
                    mr.Status?.ToString(),
                    mr.ReportType,
                    mr.ResourceCount.Select(rc => new ResourceCountInfo(rc.Key, rc.Value)).ToList())).ToList();

                return new ReportEntryInfo(
                    e.Id,
                    e.FacilityId,
                    e.PatientId,
                    e.ReportingStatus.ToString(),
                    e.SubmissionStatus?.ToString(),
                    mrs);
            }).ToList();
        });

        return result ?? [];
    }

    public async Task<List<EntryMeasureReportInfo>> GetEntryMeasureReportsAsync(Guid scheduleId)
    {
        var entries = await GetReportEntriesWithMeasureReportsAsync(scheduleId);
        return entries
            .SelectMany(e => e.MeasureReports.Select(mr => new EntryMeasureReportInfo(
                Guid.NewGuid(),
                mr.ReportType,
                mr.MeasureReportId,
                mr.Status,
                e.PatientId,
                mr.ResourceCounts)))
            .ToList();
    }

    public async Task<List<ScheduleReportTypeInfo>> GetScheduleReportTypesAsync(Guid scheduleId)
    {
        var measureReports = await GetEntryMeasureReportsAsync(scheduleId);
        return measureReports
            .Where(x => !string.IsNullOrWhiteSpace(x.ReportType))
            .Select(x => x.ReportType!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(x => new ScheduleReportTypeInfo(x))
            .ToList();
    }

    public async Task<List<ReportPopulationInfo>> GetReportPopulationsAsync(Guid scheduleId, string facilityId)
    {
        var result = await GetOrFetchAsync($"populations:{scheduleId}:{facilityId}", async () =>
        {
            var response = await _reportClient.GetPopulationsByScheduleAsync(scheduleId.ToString());
            var pops = response.Body;
            if (pops == null)
                return new List<ReportPopulationInfo>();

            return pops.Select(p => new ReportPopulationInfo(
                p.ReportType,
                p.GroupPopulations.Select(gp => new GroupPopulationInfo(
                    gp.PopulationCodeJson,
                    gp.MeasureReportPopulations.Select(mrp => new MeasureReportPopulationInfo(mrp.MeasureReportId)).ToList())).ToList())).ToList();
        });

        return result ?? [];
    }

    public async Task<List<ReportEntryInfo>> GetSubmittedReportEntriesAsync(Guid scheduleId)
    {
        var entries = await GetReportEntriesWithMeasureReportsAsync(scheduleId);
        return entries.Where(e => string.Equals(e.SubmissionStatus, "Submitted", StringComparison.OrdinalIgnoreCase)).ToList();
    }

    public async Task<List<AcquisitionLogInfo>> GetAcquisitionLogsAsync(string facilityId, string reportId)
    {
        var results = await GetAcquisitionLogsCoreAsync(facilityId, reportId);
        if (results.Count > 0 || string.IsNullOrWhiteSpace(facilityId))
            return results;

        // Facility can occasionally drift from report context; retry report-only for deterministic validation.
        return await GetAcquisitionLogsCoreAsync(string.Empty, reportId);
    }

    public async Task<DataAcquisitionLogApiModel?> GetAcquisitionLogByIdAsync(long id)
    {
        var response = await _dataAcqClient.GetAcquisitionLogByIdAsync(id);

        if (response.IsSuccessStatusCode)
            return response.Body;

        if (response.StatusCode == (int)HttpStatusCode.NotFound)
            return null;

        throw new InvalidOperationException(
            $"Failed to get acquisition log {id}. HTTP {response.StatusCode}" +
            (!string.IsNullOrWhiteSpace(response.RawBody) ? $": {response.RawBody}" : string.Empty));
    }

    public async Task<bool> HasAnyFhirQueryRowsForReportAsync(string facilityId, string reportId)
    {
        var result = await HasAnyDetailedRowsForReportCoreAsync(
            facilityId,
            reportId,
            detailed => (detailed?.FhirQuery?.Count ?? 0) > 0);

        if (result || string.IsNullOrWhiteSpace(facilityId))
            return result;

        return await HasAnyDetailedRowsForReportCoreAsync(
            string.Empty,
            reportId,
            detailed => (detailed?.FhirQuery?.Count ?? 0) > 0);
    }

    public async Task<bool> HasAnyReferenceResourcesForReportAsync(string facilityId, string reportId)
    {
        var result = await HasAnyDetailedRowsForReportCoreAsync(
            facilityId,
            reportId,
            detailed => (detailed?.ReferenceResourceCount ?? 0) > 0);

        if (result || string.IsNullOrWhiteSpace(facilityId))
            return result;

        return await HasAnyDetailedRowsForReportCoreAsync(
            string.Empty,
            reportId,
            detailed => (detailed?.ReferenceResourceCount ?? 0) > 0);
    }

    private async Task<List<AcquisitionLogInfo>> GetAcquisitionLogsCoreAsync(string facilityId, string reportId)
    {
        var pageNumber = 1;
        const int pageSize = 100;
        var results = new List<AcquisitionLogInfo>();
        long? totalCount = null;

        while (true)
        {
            var response = await _dataAcqClient.SearchAcquisitionLogsAsync(facilityId, reportId, pageSize: pageSize, pageNumber: pageNumber);
            var page = response.Body;
            var records = page?.Records ?? [];
            if (records.Count == 0)
                break;

            totalCount ??= page?.Metadata?.TotalCount;

            results.AddRange(records.Select(log => new AcquisitionLogInfo(
                log.Id,
                log.PatientId,
                log.CorrelationId,
                log.ReportTrackingId,
                log.Status?.ToString(),
                log.QueryPhase?.ToString(),
                log.Notes?.ToList() ?? [],
                log.ResourceAcquiredIds?.ToList() ?? [],
                log.FhirQuery.Select(fq => new FhirQueryInfo(fq.ResourceTypes.Where(r => !string.IsNullOrWhiteSpace(r)).ToList())).ToList())));

            if (records.Count < pageSize)
                break;

            if (totalCount.HasValue && results.Count >= totalCount.Value)
                break;

            pageNumber++;
        }

        return results;
    }

    private async Task<bool> HasAnyDetailedRowsForReportCoreAsync(
        string facilityId,
        string reportId,
        Func<DataAcquisitionLogApiModel?, bool> predicate)
    {
        var pageNumber = 1;
        const int pageSize = 100;
        long scanned = 0;
        long? totalCount = null;

        while (true)
        {
            var response = await _dataAcqClient.SearchAcquisitionLogsAsync(facilityId, reportId, pageSize: pageSize, pageNumber: pageNumber);
            var page = response.Body;
            var records = page?.Records ?? [];
            if (records.Count == 0)
                break;

            totalCount ??= page?.Metadata?.TotalCount;
            scanned += records.Count;

            foreach (var record in records)
            {
                var detailed = await GetAcquisitionLogByIdAsync(record.Id);
                if (predicate(detailed))
                    return true;
            }

            if (records.Count < pageSize)
                break;

            if (totalCount.HasValue && scanned >= totalCount.Value)
                break;

            pageNumber++;
        }
        return false;
    }

    public async Task<bool> HasFhirQueryConfigurationAsync(string facilityId)
    {
        var response = await _dataAcqClient.GetFhirQueryConfigurationAsync(facilityId);
        return response.IsSuccessStatusCode;
    }

    public async Task<List<QueryPlanInfo>> GetQueryPlansAsync(string facilityId)
    {
        var list = new List<QueryPlanInfo>();
        foreach (var type in new[] { "Discharge", "Monthly" })
        {
            var response = await _dataAcqClient.GetQueryPlanAsync(facilityId, type);
            if (response.IsSuccessStatusCode)
                list.Add(new QueryPlanInfo(type, null, 1, 1));
        }

        return list;
    }

    public async Task<List<FhirQueryInfo>> GetFhirQueriesForReportAsync(string facilityId, string reportId)
    {
        var logs = await GetAcquisitionLogsAsync(facilityId, reportId);
        return logs.SelectMany(l => l.FhirQueries).ToList();
    }

    public async Task<int> GetReferenceResourceGroupCountAsync(string facilityId, string reportId)
    {
        var logs = await GetAcquisitionLogsAsync(facilityId, reportId);

        return logs
            .Where(l => string.Equals(l.Status, "Completed", StringComparison.OrdinalIgnoreCase))
            .SelectMany(l => (l.ResourceAcquiredIds ?? [])
                .Where(id => !string.IsNullOrWhiteSpace(id) && id.Contains('/'))
                .Select(id => new
                {
                    ResourceType = id.Split('/')[0],
                    QueryPhase = l.QueryPhase ?? string.Empty
                }))
            .Where(x => !string.IsNullOrWhiteSpace(x.ResourceType))
            .Distinct()
            .Count();
    }

    public async Task<List<OperationInfo>> GetOperationsAsync(string facilityId)
    {
        var pageNumber = 1;
        const int pageSize = 100;
        var results = new List<OperationInfo>();

        while (true)
        {
            var response = await _normalizationClient.SearchFacilityOperationsAsync(facilityId, includeDisabled: true, pageSize: pageSize, pageNumber: pageNumber);
            var page = response.Body;
            if (page?.Records == null || page.Records.Count == 0)
                break;

            results.AddRange(page.Records.Select(op => new OperationInfo(
                op.Id.ToString(),
                op.OperationType,
                op.Name,
                op.OperationJson,
                op.IsDisabled,
                op.OperationResourceTypes
                    .Select(ort => ort.Resource?.ResourceName ?? string.Empty)
                    .Where(r => !string.IsNullOrWhiteSpace(r))
                    .ToList())));

            if (page.Records.Count < pageSize)
                break;

            pageNumber++;
        }

        return results;
    }

    public async Task<List<OperationSequenceInfo>> GetOperationSequencesAsync(string facilityId)
    {
        var response = await _normalizationClient.GetOperationSequencesAsync(facilityId);
        if (!response.IsSuccessStatusCode || response.Body == null)
            return [];

        return response.Body.Select(s => new OperationSequenceInfo(
            s.Id.ToString(),
            s.Sequence,
            s.OperationResourceType?.Operation?.OperationType,
            s.OperationResourceType?.Resource?.ResourceName)).ToList();
    }

    public async Task<FacilityInfo?> GetFacilityAsync(string facilityId)
    {
        var response = await _facilityClient.GetAsync(facilityId);
        if (!response.IsSuccessStatusCode || response.Body == null)
            return null;

        var facility = response.Body;
        return new FacilityInfo(
            facility.FacilityId ?? facilityId,
            facility.FacilityName,
            facility.TimeZone,
            facility.IsDeleted ?? false,
            null,
            new FacilityScheduledReports(
                facility.ScheduledReports?.Monthly ?? [],
                facility.ScheduledReports?.Daily ?? [],
                facility.ScheduledReports?.Weekly ?? []));
    }

    public async Task<List<OrganizationLocationConfigurationInfo>> GetOrganizationLocationConfigurationsAsync(string facilityId)
    {
        var response = await _dataAcqClient.GetOrganizationLocationConfigurationsAsync(facilityId);
        if (!response.IsSuccessStatusCode || response.Body == null)
            return [];

        return response.Body.Select(c => new OrganizationLocationConfigurationInfo(
            c.ConfigId,
            c.IsActive,
            c.Conditions?.Count ?? 0)).ToList();
    }

    public async Task<List<OrganizationLocationMappingInfo>> GetOrganizationLocationMappingsAsync(string facilityId)
    {
        var response = await _dataAcqClient.GetOrganizationLocationMappingsAsync(facilityId);
        if (!response.IsSuccessStatusCode || response.Body == null)
            return [];

        return response.Body.Select(m => new OrganizationLocationMappingInfo(
            m.FacilityId,
            m.LocationId,
            m.IsOrgLocation,
            m.IsActive,
            m.PartOfValue)).ToList();
    }

    public async Task<List<EncounterMappingInfo>> GetEncounterMappingsAsync(string facilityId)
    {
        var response = await _dataAcqClient.GetEncounterMappingsAsync(facilityId);
        if (!response.IsSuccessStatusCode || response.Body == null)
            return [];

        return response.Body.Select(m => new EncounterMappingInfo(
            m.FacilityId,
            m.PatientId,
            m.EncounterId,
            m.MappedToOrg,
            m.EncounterLocations
                .Select(el => new EncounterLocationInfo(el.LocationId))
                .ToList())).ToList();
    }

    public async Task<List<PatientResourceTypeCount>> GetMeasureEvalResourceCountsByPatientTypeAsync(Guid scheduleId)
    {
        var entries = await GetReportEntriesWithMeasureReportsAsync(scheduleId);
        var rows = entries
            .SelectMany(e => e.MeasureReports.SelectMany(mr => mr.ResourceCounts.Select(rc =>
                new PatientResourceTypeCount(e.PatientId, rc.ResourceType, rc.ResourceCount))))
            .ToList();

        return rows
            .GroupBy(r => new { r.PatientId, r.ResourceType })
            .Select(g => new PatientResourceTypeCount(g.Key.PatientId, g.Key.ResourceType, g.Sum(x => x.Count)))
            .ToList();
    }

    public async Task<List<PatientResourceTypeCount>> GetDataAcquisitionResourceCountsByPatientTypeAsync(string facilityId, string reportId)
    {
        var counts = new Dictionary<(string PatientId, string ResourceType), int>();

        var pageNumber = 1;
        const int pageSize = 100;
        long scanned = 0;
        long? totalCount = null;

        while (true)
        {
            var response = await _dataAcqClient.SearchAcquisitionLogsAsync(facilityId, reportId, pageSize: pageSize, pageNumber: pageNumber);
            var page = response.Body;
            var records = page?.Records ?? [];
            if (records.Count == 0)
                break;

            totalCount ??= page?.Metadata?.TotalCount;
            scanned += records.Count;

            foreach (var record in records)
            {
                var detailed = await GetAcquisitionLogByIdAsync(record.Id);
                if (string.IsNullOrWhiteSpace(detailed?.PatientId))
                    continue;

                foreach (var resourceId in detailed.ResourceAcquiredIds ?? [])
                {
                    if (string.IsNullOrWhiteSpace(resourceId) || !resourceId.Contains('/'))
                        continue;

                    var resourceType = resourceId.Split('/')[0];
                    if (string.IsNullOrWhiteSpace(resourceType))
                        continue;

                    var key = (detailed.PatientId!, resourceType);
                    counts[key] = counts.TryGetValue(key, out var current)
                        ? current + 1
                        : 1;
                }
            }

            if (records.Count < pageSize)
                break;

            if (totalCount.HasValue && scanned >= totalCount.Value)
                break;

            pageNumber++;
        }

        return counts
            .Select(kvp => new PatientResourceTypeCount(kvp.Key.PatientId, kvp.Key.ResourceType, kvp.Value))
            .ToList();
    }

    public async Task<List<StatusCountInfo>> GetDataAcquisitionStatusCountsAsync(string reportId)
    {
        var response = await _dataAcqClient.GetReportStatusCountsAsync(reportId);
        return response.Body?.Statuses?
            .Where(s => !string.IsNullOrWhiteSpace(s.Name))
            .Select(s => new StatusCountInfo(s.Name, s.Count))
            .ToList() ?? [];
    }

    public async Task<HashSet<string>> GetAcquiredResourceIdsForReportAsync(string facilityId, string reportId)
    {
        var response = await _dataAcqClient.GetAcquiredResourceIdsForReportAsync(facilityId, reportId);
        var ids = response.Body;
        var normalized = ids?
            .Where(x => !string.IsNullOrWhiteSpace(x) && x.Contains('/'))
            .ToHashSet(StringComparer.OrdinalIgnoreCase) ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (normalized.Count > 0 || string.IsNullOrWhiteSpace(facilityId))
            return normalized;

        var fallback = await _dataAcqClient.GetAcquiredResourceIdsForReportAsync(string.Empty, reportId);
        return fallback.Body?
            .Where(x => !string.IsNullOrWhiteSpace(x) && x.Contains('/'))
            .ToHashSet(StringComparer.OrdinalIgnoreCase) ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    }

    public async Task<HashSet<string>> GetReferenceResourceIdsForReportAsync(string facilityId, string reportId)
    {
        var keys = await GetOrFetchAsync($"referenceResourceIds:{facilityId}:{reportId}", async () =>
        {
            var innerKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var logs = await GetAcquisitionLogsAsync(facilityId, reportId);

            foreach (var log in logs)
            {
                var pageNumber = 1;
                const int pageSize = 100;

                while (true)
                {
                    var page = await _dataAcqClient.GetReferenceResourcesForLogAsync(
                        log.Id,
                        pageSize: pageSize,
                        pageNumber: pageNumber);

                    var records = page?.Body?.Records ?? [];
                    if (records.Count == 0)
                        break;

                    foreach (var r in records)
                    {
                        if (string.IsNullOrWhiteSpace(r.ResourceType) || string.IsNullOrWhiteSpace(r.ResourceId))
                            continue;

                        innerKeys.Add($"{r.ResourceType}/{r.ResourceId}");
                    }

                    if (records.Count < pageSize)
                        break;

                    pageNumber++;
                }
            }

            return innerKeys;
        });

        return keys ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    }

    public Task<AcquisitionSummaryInfo?> GetDataAcquisitionReportSummaryAsync(string reportId)
    {
        return GetOrFetchAsync($"acqSummary:{reportId}", async () =>
        {
            var response = await _dataAcqClient.GetReportSummaryAsync(reportId);
            var summary = response.Body;
            if (summary == null) return null;

            return new AcquisitionSummaryInfo(
                summary.ReportId,
                summary.TotalLogs,
                summary.TotalPatients,
                summary.TotalCompletedPatients,
                summary.TotalResourcesAcquired,
                summary.TotalRetryAttempts,
                summary.TotalCompletionTimeMs,
                summary.AverageCompletionTimeMs,
                summary.StatusCounts
                    .Where(s => !string.IsNullOrWhiteSpace(s.Status))
                    .Select(s => new StatusCountInfo(s.Status, s.Count))
                    .ToList(),
                summary.ResourceTypeCounts
                    .Where(r => !string.IsNullOrWhiteSpace(r.ResourceType))
                    .Select(r => new ResourceTypeCountInfo(r.ResourceType, r.Count))
                    .ToList());
        });
    }
}
