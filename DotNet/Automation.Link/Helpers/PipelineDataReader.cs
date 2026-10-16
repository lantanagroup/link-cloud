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
    // Metrics-run pollers share this 8s window with diagnostics so five domains
    // at 5s do not multiply HTTP. Lightweight runs poll schedule-only at 15s.
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
    private async Task<T?> GetOrFetchAsync<T>(string cacheKey, Func<Task<T?>> factory, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (_cache.TryGetValue(cacheKey, out var entry) && DateTime.UtcNow < entry.ExpiresAt)
            return (T?)entry.Value;

        var keyLock = _cacheLocks.GetOrAdd(cacheKey, _ => new SemaphoreSlim(1, 1));
        await keyLock.WaitAsync(cancellationToken);
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            // Double-check after acquiring lock
            if (_cache.TryGetValue(cacheKey, out entry) && DateTime.UtcNow < entry.ExpiresAt)
                return (T?)entry.Value;

            var result = await factory();
            // The SDK turns a cancelled HTTP call into a bodyless response. Do not cache that
            // empty result, or the final poll reuses it for the cache window.
            cancellationToken.ThrowIfCancellationRequested();
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
    public record ReportEntryInfo(
        Guid Id,
        string? FacilityId,
        string PatientId,
        string? ReportingStatus,
        string? SubmissionStatus,
        List<MeasureReportInfo> MeasureReports,
        DateTime? CreateDate = null,
        DateTime? ModifyDate = null);
    public record EntryMeasureReportInfo(Guid Id, string? ReportType, string? MeasureReportId, string? Status, string PatientId, List<ResourceCountInfo> ResourceCounts);
    public record ScheduleReportTypeInfo(string ReportType);
    public record ReportPopulationInfo(string? ReportType, List<GroupPopulationInfo> GroupPopulations);
    public record GroupPopulationInfo(string? PopulationCodeJson, List<MeasureReportPopulationInfo> MeasureReportPopulations);
    public record MeasureReportPopulationInfo(string? MeasureReportId);

    /// <summary>
    /// Counts for the run-history chart. Measure-report ids stay in Report.
    /// </summary>
    public record PopulationCountSnapshot(int ReportTypeCount, int GroupCount, int MeasureReportPopulationCount);

    /// <summary>
    /// Chart data for report entries. Per-patient rows stay in Report.
    /// </summary>
    public sealed record ReportEntryRollup
    {
        public int EntryCount { get; init; }
        public List<StatusCountInfo> SubmissionStatuses { get; init; } = [];
        public List<StatusCountInfo> ReportingStatuses { get; init; } = [];
        public int ReadyForValidationCount { get; init; }
        public int NotReportableMeasureCount { get; init; }
        public int NoMeasureReportCount { get; init; }
        public int EntriesWithMeasureReport { get; init; }
        public bool AllMeasureReportsTerminal { get; init; }
        public bool AllReportingTerminal { get; init; }
        public int ValidatedCount { get; init; }
        public DateTime? ValidationWindowStart { get; init; }
        public DateTime? ValidationWindowEnd { get; init; }
        public List<StatusCountInfo> ValidationBuckets { get; init; } = [];

        public static ReportEntryRollup From(IReadOnlyList<ReportEntryInfo>? entries)
        {
            if (entries == null || entries.Count == 0)
            {
                return new ReportEntryRollup
                {
                    AllMeasureReportsTerminal = true,
                    AllReportingTerminal = true
                };
            }

            var submission = new Dictionary<string, int>(StringComparer.Ordinal);
            var reporting = new Dictionary<string, int>(StringComparer.Ordinal);
            var ready = 0;
            var notReportable = 0;
            var withMeasure = 0;
            var allMeasureTerminal = true;
            var allReportingTerminal = true;
            var validated = 0;
            var times = new List<DateTimeOffset>();

            foreach (var entry in entries)
            {
                var submissionStatus = string.IsNullOrWhiteSpace(entry.SubmissionStatus) ? "Unknown" : entry.SubmissionStatus!;
                submission[submissionStatus] = submission.GetValueOrDefault(submissionStatus) + 1;

                var reportingStatus = string.IsNullOrWhiteSpace(entry.ReportingStatus) ? "Unknown" : entry.ReportingStatus!;
                reporting[reportingStatus] = reporting.GetValueOrDefault(reportingStatus) + 1;

                var reports = entry.MeasureReports ?? [];
                var hasReady = false;
                var hasNotReportable = false;
                foreach (var report in reports)
                {
                    if (string.Equals(report.Status, "ReadyForValidation", StringComparison.OrdinalIgnoreCase))
                        hasReady = true;
                    else if (string.Equals(report.Status, "NotReportable", StringComparison.OrdinalIgnoreCase))
                        hasNotReportable = true;
                }

                if (reports.Count > 0)
                    withMeasure++;
                if (hasReady)
                    ready++;
                else if (hasNotReportable)
                    notReportable++;

                var measureTerminal = false;
                foreach (var report in reports)
                {
                    if (string.Equals(report.Status, "ReadyForValidation", StringComparison.OrdinalIgnoreCase)
                        || string.Equals(report.Status, "NotReportable", StringComparison.OrdinalIgnoreCase))
                    {
                        measureTerminal = true;
                        break;
                    }
                }

                if (!measureTerminal)
                    allMeasureTerminal = false;

                if (string.Equals(entry.ReportingStatus, "PassedValidation", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(entry.ReportingStatus, "FailedValidation", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(entry.ReportingStatus, "NotReportable", StringComparison.OrdinalIgnoreCase))
                {
                    // terminal
                }
                else
                {
                    allReportingTerminal = false;
                }

                if (string.Equals(entry.ReportingStatus, "PassedValidation", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(entry.ReportingStatus, "FailedValidation", StringComparison.OrdinalIgnoreCase))
                {
                    validated++;
                    var stamp = entry.ModifyDate ?? entry.CreateDate;
                    if (stamp is DateTime value && value.Year >= 2000)
                        times.Add(AsUtc(value));
                }
            }

            DateTime? windowStart = null;
            DateTime? windowEnd = null;
            var buckets = new List<StatusCountInfo>();
            if (times.Count > 0)
            {
                var origin = times.Min();
                windowStart = origin.UtcDateTime;
                windowEnd = times.Max().UtcDateTime;
                buckets = times
                    .GroupBy(time => (int)Math.Floor(Math.Max(0, (time - origin).TotalSeconds) / 10))
                    .OrderBy(group => group.Key)
                    .Select(group => new StatusCountInfo($"{group.Key * 10}s", group.Count()))
                    .ToList();
            }

            return new ReportEntryRollup
            {
                EntryCount = entries.Count,
                SubmissionStatuses = OrderCounts(submission),
                ReportingStatuses = OrderCounts(reporting),
                ReadyForValidationCount = ready,
                NotReportableMeasureCount = notReportable,
                NoMeasureReportCount = Math.Max(0, entries.Count - ready - notReportable),
                EntriesWithMeasureReport = withMeasure,
                AllMeasureReportsTerminal = allMeasureTerminal,
                AllReportingTerminal = allReportingTerminal,
                ValidatedCount = validated,
                ValidationWindowStart = windowStart,
                ValidationWindowEnd = windowEnd,
                ValidationBuckets = buckets
            };
        }

        private static List<StatusCountInfo> OrderCounts(Dictionary<string, int> counts)
            => counts
                .Select(pair => new StatusCountInfo(pair.Key, pair.Value))
                .OrderByDescending(item => item.Count)
                .ToList();

        private static DateTimeOffset AsUtc(DateTime value)
        {
            return value.Kind switch
            {
                DateTimeKind.Utc => new DateTimeOffset(value),
                DateTimeKind.Local => new DateTimeOffset(value).ToUniversalTime(),
                _ => new DateTimeOffset(DateTime.SpecifyKind(value, DateTimeKind.Utc))
            };
        }
    }

    public record AcquisitionLogInfo(
        long Id,
        string? PatientId,
        string? CorrelationId,
        string? ReportTrackingId,
        string? Status,
        string? QueryPhase,
        List<string> Notes,
        List<string> ResourceAcquiredIds,
        List<FhirQueryInfo> FhirQueries,
        DateTime? ExecutionDate = null,
        DateTime? CreateDate = null,
        DateTime? CompletionDate = null,
        long? CompletionTimeMilliseconds = null,
        List<string>? ResourceTypes = null);

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
    public record OperationSequenceInfo(string? Id, int? Sequence, string? OperationType, string? ResourceType, string? OperationName);

    public record FacilityScheduledReports(string[] Monthly, string[] Daily, string[] Weekly);
    public record FacilityInfo(string FacilityId, string? FacilityName, string? TimeZone, bool IsDeleted, DateTime? CreateDate, FacilityScheduledReports? ScheduledReports);
    public record OrganizationLocationConfigurationInfo(int ConfigId, bool IsActive, int ConditionsCount);
    public record OrganizationLocationMappingInfo(string? FacilityId, string? LocationId, bool IsOrgLocation, bool IsActive, string? PartOfValue);
    public record EncounterLocationInfo(string? LocationId);
    public record EncounterMappingInfo(string? FacilityId, string? PatientId, string? EncounterId, bool MappedToOrg, List<EncounterLocationInfo> EncounterLocations);

    public virtual Task<ReportScheduleInfo?> GetReportScheduleAsync(Guid scheduleId, CancellationToken cancellationToken = default)
    {
        return GetOrFetchAsync($"schedule:{scheduleId}", async () =>
        {
            var response = await _reportClient.SearchSchedulesAsync(scheduleId.ToString(), cancellationToken);
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
        }, cancellationToken);
    }

    public Task<List<ReportEntryInfo>> GetReportEntriesAsync(Guid scheduleId)
        => GetReportEntriesWithMeasureReportsAsync(scheduleId);

    public virtual async Task<List<ReportEntryInfo>> GetReportEntriesWithMeasureReportsAsync(Guid scheduleId, CancellationToken cancellationToken = default)
    {
        var result = await GetOrFetchAsync($"entries:{scheduleId}", async () =>
        {
            var response = await _reportClient.GetEntriesByScheduleAsync(scheduleId.ToString(), cancellationToken);
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
                    mrs,
                    e.CreateDate,
                    e.ModifyDate);
            }).ToList();
        }, cancellationToken);

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

    public async Task<List<ReportPopulationInfo>> GetReportPopulationsAsync(Guid scheduleId, string facilityId, CancellationToken cancellationToken = default)
    {
        var result = await GetOrFetchAsync($"populations:{scheduleId}:{facilityId}", async () =>
        {
            var response = await _reportClient.GetPopulationsByScheduleAsync(scheduleId.ToString(), cancellationToken: cancellationToken);
            var pops = response.Body;
            if (pops == null)
                return new List<ReportPopulationInfo>();

            return pops.Select(p => new ReportPopulationInfo(
                p.ReportType,
                p.GroupPopulations.Select(gp => new GroupPopulationInfo(
                    gp.PopulationCodeJson,
                    gp.MeasureReportPopulations.Select(mrp => new MeasureReportPopulationInfo(mrp.MeasureReportId)).ToList())).ToList())).ToList();
        }, cancellationToken);

        return result ?? [];
    }

    public async Task<List<ReportEntryInfo>> GetSubmittedReportEntriesAsync(Guid scheduleId)
    {
        var entries = await GetReportEntriesWithMeasureReportsAsync(scheduleId);
        return entries.Where(e => string.Equals(e.SubmissionStatus, "Submitted", StringComparison.OrdinalIgnoreCase)).ToList();
    }

    public async Task<List<AcquisitionLogInfo>> GetAcquisitionLogsAsync(string facilityId, string reportId, CancellationToken cancellationToken = default)
    {
        var results = await GetAcquisitionLogsCoreAsync(facilityId, reportId, cancellationToken);
        if (results.Count > 0 || string.IsNullOrWhiteSpace(facilityId))
            return results;

        // Facility can occasionally drift from report context; retry report-only for deterministic validation.
        return await GetAcquisitionLogsCoreAsync(string.Empty, reportId, cancellationToken);
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

    private async Task<List<AcquisitionLogInfo>> GetAcquisitionLogsCoreAsync(string facilityId, string reportId, CancellationToken cancellationToken)
    {
        var pageNumber = 1;
        const int pageSize = 100;
        var results = new List<AcquisitionLogInfo>();
        long? totalCount = null;

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var response = await _dataAcqClient.SearchAcquisitionLogsAsync(
                facilityId,
                reportId,
                pageSize: pageSize,
                pageNumber: pageNumber,
                cancellationToken: cancellationToken);
            // A cancelled call can come back as a bodyless response. That must not
            // look like the last page of a list the caller already started.
            cancellationToken.ThrowIfCancellationRequested();
            var page = response.Body;
            // A failed or bodyless page is not the end of the list. Returning the
            // pages already read would replace the stored chart with a partial one.
            if (!response.IsSuccessStatusCode || page == null)
                throw new HttpRequestException($"Acquisition log search returned HTTP {response.StatusCode} without a page.");

            var records = page.Records ?? [];
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
                (log.FhirQuery ?? []).Select(fq => new FhirQueryInfo(fq.ResourceTypes.Where(r => !string.IsNullOrWhiteSpace(r)).ToList())).ToList(),
                log.ExecutionDate,
                log.CreateDate,
                log.CompletionDate,
                log.CompletionTimeMilliseconds,
                log.ResourceTypes?.Where(r => !string.IsNullOrWhiteSpace(r)).Distinct(StringComparer.OrdinalIgnoreCase).ToList() ?? [])));

            if (records.Count < pageSize)
                break;

            if (totalCount.HasValue && results.Count >= totalCount.Value)
                break;

            pageNumber++;
        }

        return results;
    }

    /// <summary>
    /// Loads notes for the failed logs in <paramref name="failureIds"/> only.
    /// The search response does not include notes.
    /// </summary>
    public async Task<List<AcquisitionLogInfo>> AttachFailureNotesAsync(
        List<AcquisitionLogInfo> logs,
        IReadOnlyList<long> failureIds,
        CancellationToken cancellationToken = default)
    {
        if (logs.Count == 0 || failureIds.Count == 0)
            return logs;

        var notesById = new Dictionary<long, List<string>>();
        foreach (var id in failureIds)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var cacheKey = $"acqNotes:{id}";
            if (_cache.TryGetValue(cacheKey, out var cached)
                && DateTime.UtcNow < cached.ExpiresAt
                && cached.Value is List<string> cachedNotes)
            {
                notesById[id] = cachedNotes;
                continue;
            }

            try
            {
                var response = await _dataAcqClient.GetAcquisitionLogNotesAsync(id, cancellationToken);
                // A cancelled call can come back as a bodyless response instead of throwing.
                cancellationToken.ThrowIfCancellationRequested();
                if (!response.IsSuccessStatusCode || response.Body == null)
                    continue;

                notesById[id] = response.Body;
                _cache[cacheKey] = (response.Body, DateTime.UtcNow.Add(CacheTtl));
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception)
            {
                // Notes are optional. One failed lookup must not drop the chart.
            }
        }

        if (notesById.Count == 0)
            return logs;

        return logs
            .Select(log => notesById.TryGetValue(log.Id, out var notes) ? log with { Notes = notes } : log)
            .ToList();
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
        foreach (var type in new[] { "Discharge", "Daily", "Monthly", "Weekly" })
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
            s.OperationResourceType?.Resource?.ResourceName,
            s.OperationResourceType?.Operation?.Name)).ToList();
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

    public async Task<List<OrganizationLocationConfigurationInfo>> GetOrganizationLocationConfigurationsAsync(string facilityId, CancellationToken cancellationToken = default)
    {
        var response = await _dataAcqClient.GetOrganizationLocationConfigurationsAsync(facilityId, cancellationToken);
        if (!response.IsSuccessStatusCode || response.Body == null)
            return [];

        return response.Body.Select(c => new OrganizationLocationConfigurationInfo(
            c.ConfigId,
            c.IsActive,
            c.Conditions?.Count ?? 0)).ToList();
    }

    public async Task<List<OrganizationLocationMappingInfo>> GetOrganizationLocationMappingsAsync(string facilityId, CancellationToken cancellationToken = default)
    {
        var response = await _dataAcqClient.GetOrganizationLocationMappingsAsync(facilityId, cancellationToken);
        if (!response.IsSuccessStatusCode || response.Body == null)
            return [];

        return response.Body.Select(m => new OrganizationLocationMappingInfo(
            m.FacilityId,
            m.LocationId,
            m.IsOrgLocation,
            m.IsActive,
            m.PartOfValue)).ToList();
    }

    public async Task<List<EncounterMappingInfo>> GetEncounterMappingsAsync(string facilityId, CancellationToken cancellationToken = default)
    {
        var response = await _dataAcqClient.GetEncounterMappingsAsync(facilityId, cancellationToken);
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

    public async Task<List<PatientResourceTypeCount>> GetMeasureEvalResourceCountsByPatientTypeAsync(Guid scheduleId, CancellationToken cancellationToken = default)
    {
        var entries = await GetReportEntriesWithMeasureReportsAsync(scheduleId, cancellationToken);
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
        var counts = await GetDataAcquisitionResourceCountsByPatientTypeCoreAsync(facilityId, reportId);
        if (counts.Count > 0 || string.IsNullOrWhiteSpace(facilityId))
            return counts;

        return await GetDataAcquisitionResourceCountsByPatientTypeCoreAsync(string.Empty, reportId);
    }

    private async Task<List<PatientResourceTypeCount>> GetDataAcquisitionResourceCountsByPatientTypeCoreAsync(string facilityId, string reportId)
    {
        var response = await _dataAcqClient.GetAcquiredResourceCountsByPatientAsync(facilityId, reportId);
        if (!response.IsSuccessStatusCode || response.Body == null)
            return [];

        return response.Body
            .Where(row => !string.IsNullOrWhiteSpace(row.PatientId) && !string.IsNullOrWhiteSpace(row.ResourceType))
            .Select(row => new PatientResourceTypeCount(row.PatientId, row.ResourceType, row.Count))
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

    public Task<AcquisitionSummaryInfo?> GetDataAcquisitionReportSummaryAsync(string reportId, CancellationToken cancellationToken = default)
    {
        return GetOrFetchAsync($"acqSummary:{reportId}", async () =>
        {
            var response = await _dataAcqClient.GetReportSummaryAsync(reportId, cancellationToken);
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
        }, cancellationToken);
    }
}
