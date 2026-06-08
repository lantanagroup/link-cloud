using DataAcquisition.Domain.Application.Models;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Api.Configuration;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Api.QueryLog;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Api.Requests;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Domain;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Kafka;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Context;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Entities;
using LantanaGroup.Link.DataAcquisition.Domain.Models;
using LantanaGroup.Link.Shared.Application.Enums;
using LantanaGroup.Link.Shared.Application.Interfaces;
using LantanaGroup.Link.Shared.Application.Interfaces.Models;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Models.Integration.DataAcquisition;
using LantanaGroup.Link.Shared.Application.Models.Responses;
using LantanaGroup.Link.Shared.Application.Models.Telemetry;
using LantanaGroup.Link.Shared.Application.Services.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RequestStatus = LantanaGroup.Link.Shared.Application.Models.Integration.DataAcquisition.RequestStatus;
using FhirQueryType = LantanaGroup.Link.Shared.Application.Models.Integration.DataAcquisition.FhirQueryType;
using IDatabase = LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.IDatabase;
using ResourceType = Hl7.Fhir.Model.ResourceType;

namespace LantanaGroup.Link.DataAcquisition.Domain.Application.Queries;

public interface IDataAcquisitionLogQueries
{
    /// <summary>
    /// Retrieves a complete data acquisition log by its ID, including related data such as ScheduledReport, ReportableEvent, and FhirQuery.
    /// </summary>
    /// <param name="logId"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentNullException"></exception>
    /// <exception cref="KeyNotFoundException"></exception>
    Task<DataAcquisitionLogModel?> GetAsync(long id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a list of TailingMessageModel objects that represent the tailing messages for data acquisition logs.
    /// </summary>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<IEnumerable<TailingMessageModel>> GetTailingMessages(CancellationToken cancellationToken = default);

    /// <summary>
    /// Asynchronously retrieves the count of non-reference logs that are incomplete for a specified facility, report,
    /// and correlation.
    /// </summary>
    /// <param name="facilityId">The unique identifier of the facility. Cannot be null or empty.</param>
    /// <param name="reportTrackingId">The unique identifier of the report tracking. Cannot be null or empty.</param>
    /// <param name="correlationId">The unique identifier used to correlate related logs. Cannot be null or empty.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests. Optional.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the count of non-reference logs 
    /// that are incomplete for the specified parameters.</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="facilityId"/>, <paramref name="reportTrackingId"/>, or <paramref
    /// name="correlationId"/> is null or empty.</exception>
    Task<IPagedModel<QueryLogSummaryModel>> SearchQueryLogSummaryAsync(SearchDataAcquisitionLogRequest request,
        CancellationToken cancellationToken = default);

    Task<PagedConfigModel<DataAcquisitionLogSummaryModel>> SearchAsync(SearchDataAcquisitionLogRequest model,
        CancellationToken cancellationToken = default);

    Task<DataAcquisitionLogStatistics> GetDataAcquisitionLogStatisticsByReportAsync(string reportId,
        CancellationToken cancellationToken = default);

    Task<DataAcquisitionLogStatusStatistics> GetDataAcquisitionLogStatusStatisticsByReportAsync(string reportId,
        string? patientId = null, CancellationToken cancellationToken = default);

    Task<bool> CheckIfReferenceResourceHasBeenSent(string referenceId, string reportTrackingId, string facilityId,
        string correlationId, CancellationToken cancellationToken = default);

    Task<List<string>>
        GetFacilitiesWithPendingAndRetryableFailedRequests(CancellationToken cancellationToken = default);

    Task<List<DataAcquisitionLogModel>> GetNextEligibleBatchForFacility(string facilityId, long? lastId, int batchSize,
        List<RequestStatus> statuses, DateTime? designagtedExecutionTime = null,
        CancellationToken cancellationToken = default);

    Task<List<string>> GetResourceIdsForReportPatient(string correlationId, string facilityId, string resourceType,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// For each resource type in <paramref name="resourceTypes"/>, returns the ones that still
    /// have at least one non-terminal DataAcquisitionLog for the given correlation + facility.
    /// A resource type with no matching logs at all is NOT returned.
    /// </summary>
    Task<List<string>> GetNonTerminalDependencyResourceTypes(string correlationId, string facilityId,
        List<string> resourceTypes, CancellationToken cancellationToken = default);

    /// <summary>
    /// Safety-net query: finds one representative log ID per group that is
    /// fully terminal, has SiblingCount stamped, but TailSent is still false
    /// and the last ModifyDate is older than <paramref name="minAge"/>.
    /// </summary>
    Task<List<long>> GetOrphanedTailLogIds(TimeSpan minAge, int maxResults = 50, CancellationToken cancellationToken = default);

}

public class DataAcquisitionLogQueries : IDataAcquisitionLogQueries
{
    private readonly IDatabase _database;
    private readonly DataAcquisitionDbContext _dbContext;
    private readonly ILogger<DataAcquisitionLogQueries> _logger;
    private readonly IResourceCache _resourceCache;

    public DataAcquisitionLogQueries(IDatabase database, DataAcquisitionDbContext dbContext,
        ILogger<DataAcquisitionLogQueries> logger, IResourceCache resourceCache)
    {
        _database = database ?? throw new ArgumentNullException(nameof(database));
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _resourceCache = resourceCache ?? throw new ArgumentNullException(nameof(resourceCache));
    }

    public async Task<List<string>> GetResourceIdsForReportPatient(string correlationId, string facilityId,
        string resourceType, CancellationToken cancellationToken = default)
    {
        using var activity = ServiceActivitySource.Instance.StartActivity("DataAcquisitionLogQueries.GetResourceIdsForReportPatient");
        activity?.SetTag(DiagnosticNames.CorrelationId, correlationId);
        activity?.SetTag(DiagnosticNames.FacilityId, facilityId);
        activity?.SetTag(DiagnosticNames.ResourceType, resourceType);

        if (!Enum.TryParse<ResourceType>(resourceType, out var parsedResourceType))
        {
            _logger.LogError("Failed to parse resource type: {ResourceType}", resourceType);
            return new List<string>();
        }

        var logsWithResources = await (from log in _dbContext.DataAcquisitionLogs
                                       join query in _dbContext.FhirQueries on log.Id equals query.DataAcquisitionLogId
                                       join resourceTypeEntry in _dbContext.FhirQueryResourceTypes on query.Id equals resourceTypeEntry.FhirQueryId
                                       where query.FacilityId == facilityId
                                             && log.CorrelationId == correlationId
                                             && resourceTypeEntry.ResourceType == parsedResourceType
                                       select log.ResourceIds.Select(r => r.ResourceId).ToList()).ToListAsync(cancellationToken);

        var result = new List<string>();
        var resourceTypePrefix = $"{resourceType}/";

        foreach (var resourceReferences in logsWithResources)
        {
            if (resourceReferences != null)
            {
                var filteredIds = resourceReferences
                    .Where(r => r.StartsWith(resourceTypePrefix, StringComparison.OrdinalIgnoreCase))
                    .Select(r => r.Substring(resourceTypePrefix.Length))
                    .ToList();

                result.AddRange(filteredIds);
            }
        }

        return result;
    }

    public async Task<List<string>> GetNonTerminalDependencyResourceTypes(string correlationId, string facilityId,
        List<string> resourceTypes, CancellationToken cancellationToken = default)
    {
        using var activity = ServiceActivitySource.Instance.StartActivity("DataAcquisitionLogQueries.GetNonTerminalDependencyResourceTypes");
        activity?.SetTag(DiagnosticNames.CorrelationId, correlationId);
        activity?.SetTag(DiagnosticNames.FacilityId, facilityId);

        if (string.IsNullOrWhiteSpace(correlationId) || string.IsNullOrWhiteSpace(facilityId) ||
            resourceTypes is null || resourceTypes.Count == 0)
        {
            return [];
        }

        var terminalStatuses = new[]
        {
            RequestStatus.Completed, RequestStatus.Skipped, RequestStatus.MaxRetriesReached, RequestStatus.Cancelled
        };

        // Parse supplied strings to the ResourceType enum. Anything that doesn't parse is skipped.
        var parsed = new List<(string Original, ResourceType Parsed)>();
        foreach (var rt in resourceTypes.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (Enum.TryParse<ResourceType>(rt, ignoreCase: true, out var parsedType))
            {
                parsed.Add((rt, parsedType));
            }
            else
            {
                _logger.LogWarning("Could not parse dependency resource type {ResourceType}; treating as non-blocking.", rt.SanitizeForLog());
            }
        }

        if (parsed.Count == 0)
            return [];

        var parsedTypes = parsed.Select(p => p.Parsed).Distinct().ToList();

        // Find which resource types have at least one non-terminal log in this correlation/facility.
        // Also collect which types have any log at all (so missing types fail-open).
        var logStatusByType = await (
            from log in _dbContext.DataAcquisitionLogs.AsNoTracking()
            join query in _dbContext.FhirQueries on log.Id equals query.DataAcquisitionLogId
            join resourceTypeEntry in _dbContext.FhirQueryResourceTypes on query.Id equals resourceTypeEntry.FhirQueryId
            where log.FacilityId == facilityId
                  && log.CorrelationId == correlationId
                  && parsedTypes.Contains(resourceTypeEntry.ResourceType)
            select new { resourceTypeEntry.ResourceType, log.Status })
            .ToListAsync(cancellationToken);

        if (logStatusByType.Count == 0)
            return [];

        var blocking = new HashSet<ResourceType>();
        foreach (var grouping in logStatusByType.GroupBy(x => x.ResourceType))
        {
            var hasNonTerminal = grouping.Any(g =>
                g.Status is null || !terminalStatuses.Contains(g.Status.Value));
            if (hasNonTerminal)
                blocking.Add(grouping.Key);
        }

        return parsed
            .Where(p => blocking.Contains(p.Parsed))
            .Select(p => p.Original)
            .ToList();
    }

    public async Task<DataAcquisitionLogModel?> GetAsync(long id, CancellationToken cancellationToken = default)
    {
        using var activity = ServiceActivitySource.Instance.StartActivity("DataAcquisitionLogQueries.GetAsync");
        activity?.SetTag(DiagnosticNames.DataAcquisitionLogId, id);

        return await ProjectLogById(id)
            .FirstOrDefaultAsync(cancellationToken);
    }

    /// <summary>
    /// Builds a LINQ-to-SQL projection query for a single log by ID.
    /// All columns are projected directly into <see cref="DataAcquisitionLogModel"/>
    /// without materialising the entity first.
    /// </summary>
    private IQueryable<DataAcquisitionLogModel> ProjectLogById(long id)
    {
        return _dbContext.DataAcquisitionLogs
            .AsNoTracking()
            .Where(l => l.Id == id)
            .Select(l => new DataAcquisitionLogModel
            {
                Id = l.Id,
                Priority = l.Priority,
                FacilityId = l.FacilityId,
                IsCensus = l.IsCensus,
                PatientId = l.PatientId,
                ReportableEvent = l.ReportableEvent,
                ReportTrackingId = l.ReportTrackingId != null ? l.ReportTrackingId.ToString().ToLower() : null,
                CorrelationId = l.CorrelationId,
                ReferenceResourceType = l.ReferenceResourceType,
                FhirVersion = l.FhirVersion,
                QueryType = l.QueryType,
                QueryPhase = l.QueryPhase,
                FhirQuery = l.FhirQueries.Select(q => new FhirQueryModel
                {
                    Id = q.Id,
                    FacilityId = q.FacilityId,
                    MeasureId = q.MeasureId,
                    IsReference = q.IsReference,
                    QueryType = q.QueryType,
                    ResourceTypes = q.FhirQueryResourceTypes.Select(r => r.ResourceType).ToList(),
                    QueryParameters = q.QueryParameters,
                    Paged = q.Paged,
                    DataAcquisitionLogId = q.DataAcquisitionLogId,
                    CensusListId = q.CensusListId,
                    CensusPatientStatus = q.CensusPatientStatus,
                    CensusTimeFrame = q.CensusTimeFrame,
                    ResourceReferenceTypes = q.ResourceReferenceTypes.Select(rt => new ResourceReferenceTypeModel
                    {
                        Id = rt.Id,
                        FacilityId = rt.FacilityId,
                        QueryPhase = rt.QueryPhase,
                        ResourceType = rt.ResourceType,
                        FhirQueryId = rt.FhirQueryId,
                        CreateDate = rt.CreateDate,
                        ModifyDate = rt.ModifyDate,
                    }).ToList()
                }).ToList(),
                Status = l.Status,
                ExecutionDate = l.ExecutionDate,
                CreateDate = l.CreateDate,
                TraceId = l.TraceId,
                RetryAttempts = l.RetryAttempts,
                CompletionDate = l.CompletionDate,
                CompletionTimeMilliseconds = l.CompletionTimeMilliseconds,
                ResourceAcquiredIds = l.ResourceIds.Select(r => r.ResourceId).ToList(),
                ReferenceResourceCount = l.ReferenceResources.Count(),
                Notes = null,
                ScheduledReport = l.ScheduledReportEntity != null ? new ScheduledReport
                {
                    ReportTrackingId = l.ScheduledReportEntity.ReportTrackingId.ToString().ToLower(),
                    Frequency = l.ScheduledReportEntity.Frequency,
                    StartDate = DateTime.SpecifyKind(l.ScheduledReportEntity.StartDate, DateTimeKind.Utc),
                    EndDate = DateTime.SpecifyKind(l.ScheduledReportEntity.EndDate, DateTimeKind.Utc),
                    ReportTypes = l.ScheduledReportEntity.ReportTypes != null
                        ? l.ScheduledReportEntity.ReportTypes.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries).ToList()
                        : new List<string>()
                } : null,
                IsDeleted = l.IsDeleted
            });
    }

    public async Task<IEnumerable<TailingMessageModel>> GetTailingMessages(
        CancellationToken cancellationToken = default)
    {
        var completedOrFailedStatuses = new[]
            { RequestStatus.Completed, RequestStatus.MaxRetriesReached, RequestStatus.Skipped, RequestStatus.Cancelled };

        try
        {
            // Two-phase approach to avoid a full-table GROUP BY:
            //
            // Phase 1: Find (FacilityId, CorrelationId, QueryPhase) groups that have
            //          at least one completed, non-TailSent log. These are the only
            //          groups that COULD be ready for tailing.
            //
            // Phase 2: For each candidate group, verify that it has zero
            //          non-terminal logs (i.e. all logs are finished).

            // Phase 1 ? narrow candidate groups via terminal-status logs only.
            var candidateGroups = await _dbContext.DataAcquisitionLogs.AsNoTracking()
                .Where(log =>
                    !log.TailSent &&
                    log.Status != null && completedOrFailedStatuses.Contains(log.Status.Value) &&
                    log.ReportTrackingId != null &&
                    log.CorrelationId != null)
                .GroupBy(log => new
                {
                    log.FacilityId,
                    log.ReportTrackingId,
                    log.CorrelationId,
                    log.QueryPhase,
                })
                .Select(g => new
                {
                    Key = g.Key,
                    EarliestStart = g.Min(x => x.ScheduledReportEntity != null ? x.ScheduledReportEntity.StartDate : DateTime.MaxValue)
                })
                .OrderBy(x => x.EarliestStart)
                .Take(100)
                .Select(x => x.Key)
                .ToListAsync(cancellationToken);

            if (candidateGroups.Count == 0)
                return [];

            // Phase 2 for each candidate, check if ANY non-terminal log exists.
            var results = new List<TailingMessageModel>();

            foreach (var group in candidateGroups)
            {
                // Check for incomplete logs in this correlation group
                var hasIncomplete = await _dbContext.DataAcquisitionLogs.AsNoTracking()
                    .AnyAsync(log =>
                        !log.TailSent &&
                        log.FacilityId == group.FacilityId &&
                        log.CorrelationId == group.CorrelationId &&
                        log.ReportTrackingId == group.ReportTrackingId &&
                        log.QueryPhase == group.QueryPhase &&
                        (log.Status == null || !completedOrFailedStatuses.Contains(log.Status.Value)),
                    cancellationToken);

                if (hasIncomplete)
                    continue;

                // All logs are terminal collect data for the tail message
                var groupLogs = await _dbContext.DataAcquisitionLogs.AsNoTracking()
                    .Where(log =>
                        !log.TailSent &&
                        log.FacilityId == group.FacilityId &&
                        log.CorrelationId == group.CorrelationId &&
                        log.ReportTrackingId == group.ReportTrackingId &&
                        log.QueryPhase == group.QueryPhase)
                    .Select(log => new
                    {
                        log.Id,
                        log.TraceId,
                        log.PatientId,
                        log.ReportableEvent,
                        ScheduledReport = log.ScheduledReportEntity != null ? new ScheduledReport
                        {
                            ReportTrackingId = log.ScheduledReportEntity.ReportTrackingId.ToString().ToLower(),
                            Frequency = log.ScheduledReportEntity.Frequency,
                            StartDate = DateTime.SpecifyKind(log.ScheduledReportEntity.StartDate, DateTimeKind.Utc),
                            EndDate = DateTime.SpecifyKind(log.ScheduledReportEntity.EndDate, DateTimeKind.Utc),
                            ReportTypes = log.ScheduledReportEntity.ReportTypes != null
                                ? log.ScheduledReportEntity.ReportTypes.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries).ToList()
                                : new List<string>()
                        } : null,
                    })
                    .ToListAsync(cancellationToken);

                if (groupLogs.Count == 0)
                    continue;

                var first = groupLogs.First();

                // Only include cache keys for resource types that had at least one resource actually acquired.
                // ResourceId rows use the format "TypeName/id" (e.g. "Patient/abc-123"), so the type name
                // is the substring before the first '/'.
                var acquiredResourceTypes = await _dbContext.DataAcquisitionLogResourceIds
                    .Join(_dbContext.DataAcquisitionLogs,
                        rid => rid.DataAcquisitionLogId,
                        l => l.Id,
                        (rid, l) => new { rid, l })
                    .Where(x => x.l.FacilityId == group.FacilityId
                        && x.l.CorrelationId == group.CorrelationId
                        && x.l.QueryPhase == group.QueryPhase)
                    .Select(x => x.rid.ResourceId.Substring(0, x.rid.ResourceId.IndexOf('/')))
                    .Distinct()
                    .ToListAsync(cancellationToken);

                results.Add(new TailingMessageModel
                {
                    FacilityId = group.FacilityId ?? string.Empty,
                    CorrelationId = group.CorrelationId ?? string.Empty,
                    LogIds = groupLogs.Select(x => x.Id).ToList(),
                    TraceParentId = groupLogs.FirstOrDefault(x => x.TraceId != null)?.TraceId ?? string.Empty,
                    ResourcesAcquired = new ResourcesAcquired
                    {
                        QueryType = QueryPhaseUtilities.ToWireQueryType(group.QueryPhase),
                        ReportableEvent = first.ReportableEvent ?? default,
                        ScheduledReports = first.ScheduledReport != null
                            ? new List<ScheduledReport> { first.ScheduledReport }
                            : new List<ScheduledReport>(),
                        CacheType = _resourceCache.GetCacheTypeForCorrelationId(group.CorrelationId ?? string.Empty),
                        CacheKeys = acquiredResourceTypes
                            .Select(rt => $"{group.CorrelationId}:{rt}")
                            .Distinct()
                            .ToList()
                    }
                });

                if (results.Count >= 50)
                    break;
            }

            return results;
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("GetTailingMessages operation was cancelled.");
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while retrieving tailing messages.");
            throw new InvalidOperationException("An error occurred while retrieving tailing messages.", ex);
        }
    }

    public async Task<IPagedModel<QueryLogSummaryModel>> SearchQueryLogSummaryAsync(
        SearchDataAcquisitionLogRequest request, CancellationToken cancellationToken = default)
    {
        using var activity = ServiceActivitySource.Instance.StartActivity("DataAcquisitionLogQueries.SearchQueryLogSummaryAsync");
        activity?.SetTag(DiagnosticNames.FacilityId, request.FacilityId);

        ArgumentNullException.ThrowIfNull(request);

        // Build the filtered (unsorted) query once ? reused by both count and page.
        var baseQuery = BuildSearchQuery(request);

        // Count doesn't need a sort ? avoid the expensive ORDER BY for the count scan.
        var total = await baseQuery.CountAsync(cancellationToken);

        // Only fetch the page if there are results to show
        if (total == 0 || (request.PageNumber - 1) * request.PageSize >= total)
        {
            return new QueryLogSummaryModelResponse
            {
                Records = new List<QueryLogSummaryModel>(),
                Metadata = new PaginationMetadata(request.PageSize, request.PageNumber, total)
            };
        }

        var sortedQuery = ApplySort(baseQuery, request.SortBy, request.SortOrder);
        var pageLogs = await sortedQuery
                .Skip((request.PageNumber - 1) * request.PageSize)
                .Take(request.PageSize)
                .Select(log => new
                {
                    log.Id,
                    log.Priority,
                    log.FacilityId,
                    log.PatientId,
                    log.ReportTrackingId,
                    log.FhirVersion,
                    log.QueryType,
                    log.QueryPhase,
                    log.ExecutionDate,
                    log.CreateDate,
                    log.RetryAttempts,
                    log.Status,
                    log.IsDeleted
                })
                .ToListAsync(cancellationToken);

            List<QueryLogSummaryModel> records;

            if (pageLogs.Count == 0)
            {
                records = new List<QueryLogSummaryModel>();
            }
            else
            {
                var logIds = pageLogs.Select(log => log.Id).ToList();
                var queryInfo = await _dbContext.FhirQueries
                    .AsNoTracking()
                    .Where(q => logIds.Contains(q.DataAcquisitionLogId))
                    .Select(q => new
                    {
                        q.Id,
                        q.DataAcquisitionLogId,
                        q.IsReference,
                        q.QueryParameters,
                        ResourceTypes = q.FhirQueryResourceTypes.Select(rt => rt.ResourceType).ToList()
                    })
                    .ToListAsync(cancellationToken);

                var firstQueryByLogId = queryInfo
                    .GroupBy(q => q.DataAcquisitionLogId)
                    .ToDictionary(g => g.Key, g => g.FirstOrDefault(q => q.IsReference != true) ?? g.First());

                // Collect ALL resource types from ALL FhirQueries per log (not just the first).
                var allQueryResourceTypesByLogId = queryInfo
                    .GroupBy(q => q.DataAcquisitionLogId)
                    .ToDictionary(
                        g => g.Key,
                        g => g.SelectMany(q => q.ResourceTypes ?? [])
                              .Distinct()
                              .Select(rt => rt.ToString())
                              .ToList());

                // Determine which logs have any reference FhirQuery.
                var logsWithReferenceQuery = queryInfo
                    .Where(q => q.IsReference == true)
                    .Select(q => q.DataAcquisitionLogId)
                    .ToHashSet();

                records = pageLogs.Select(log =>
                {
                    firstQueryByLogId.TryGetValue(log.Id, out var fhirQuery);
                    allQueryResourceTypesByLogId.TryGetValue(log.Id, out var allQueryTypes);

                    // Reference logs exist as first-class rows with their
                    // own FhirQueryResourceTypes, so the log's resource types are strictly
                    // what its own FhirQueries declare. No synthesis off the
                    // ReferenceResources junction.
                    var resourceTypes = allQueryTypes
                        ?? fhirQuery?.ResourceTypes?.Select(rt => rt.ToString()).ToList()
                        ?? new List<string>();

                    string? resourceId;

                    if (resourceTypes.Count > 0 && resourceTypes[0] == ResourceType.Patient.ToString())
                    {
                        resourceId = log.PatientId;
                    }
                    else if (log.QueryType == FhirQueryType.Read)
                    {
                        resourceId = fhirQuery?.QueryParameters?.FirstOrDefault();
                    }
                    else
                    {
                        resourceId = string.Empty;
                    }

                    var isReferenceLog = logsWithReferenceQuery.Contains(log.Id);

                    return new QueryLogSummaryModel
                    {
                        Id = log.Id,
                        Priority = log.Priority,
                        FacilityId = log.FacilityId,
                        PatientId = log.PatientId,
                        ResourceTypes = resourceTypes,
                        ResourceId = resourceId,
                        FhirVersion = log.FhirVersion ?? string.Empty,
                        QueryType = log.QueryType,
                        QueryPhase = log.QueryPhase,
                        ExecutionDate = log.ExecutionDate,
                        CreateDate = log.CreateDate,
                        RetryAttempts = log.RetryAttempts,
                        Status = log.Status,
                        IsDeleted = log.IsDeleted,
                        ReportTrackingId = log.ReportTrackingId != null ? log.ReportTrackingId.ToString().ToLower() : null,
                        IsReferenceLog = isReferenceLog
                    };
                }).ToList();
            }

        return new QueryLogSummaryModelResponse
        {
            Records = records,
            Metadata = new PaginationMetadata(request.PageSize, request.PageNumber, total)
        };
    }


    public async Task<PagedConfigModel<DataAcquisitionLogSummaryModel>> SearchAsync(SearchDataAcquisitionLogRequest model,
        CancellationToken cancellationToken = default)
    {
        using var activity = ServiceActivitySource.Instance.StartActivity("DataAcquisitionLogQueries.SearchAsync");
        activity?.SetTag(DiagnosticNames.FacilityId, model.FacilityId);

        var query = BuildSearchQuery(model);
        query = ApplySort(query, model.SortBy, model.SortOrder);

        var total = await query.CountAsync(cancellationToken);

        var logs = await query
            .Skip((model.PageNumber - 1) * model.PageSize)
            .Take(model.PageSize)
            .Select(l => new DataAcquisitionLogSummaryModel
            {
                Id = l.Id,
                Priority = l.Priority,
                FacilityId = l.FacilityId,
                IsCensus = l.IsCensus,
                PatientId = l.PatientId,
                ReportableEvent = l.ReportableEvent,
                ReportTrackingId = l.ReportTrackingId != null ? l.ReportTrackingId.ToString().ToLower() : null,
                CorrelationId = l.CorrelationId,
                FhirVersion = l.FhirVersion,
                QueryType = l.QueryType,
                QueryPhase = l.QueryPhase,
                Status = l.Status,
                ExecutionDate = l.ExecutionDate,
                CreateDate = l.CreateDate,
                TraceId = l.TraceId,
                RetryAttempts = l.RetryAttempts,
                CompletionDate = l.CompletionDate,
                CompletionTimeMilliseconds = l.CompletionTimeMilliseconds,
                ResourceAcquiredCount = l.ResourceIds.Count,
                Notes = null,
                IsDeleted = l.IsDeleted
            }).ToListAsync(cancellationToken);

        return new PagedConfigModel<DataAcquisitionLogSummaryModel>
        {
            Metadata = new PaginationMetadata
            {
                PageNumber = model.PageNumber,
                PageSize = model.PageSize,
                TotalCount = total,
                TotalPages = (long)MathF.Round(total / model.PageSize, MidpointRounding.ToPositiveInfinity),
            },
            Records = logs
        };
    }


    public async Task<DataAcquisitionLogStatistics> GetDataAcquisitionLogStatisticsByReportAsync(string reportId,
        CancellationToken cancellationToken = default)
    {
        using var activity = ServiceActivitySource.Instance.StartActivity("DataAcquisitionLogQueries.GetDataAcquisitionLogStatisticsByReportAsync");
        activity?.SetTag(DiagnosticNames.ReportTrackingId, reportId);

        if (!Guid.TryParse(reportId, out var reportTrackingIdGuid))
        {
            throw new ArgumentException("Report ID must be a valid GUID.", nameof(reportId));
        }

        var baseQuery = _dbContext.DataAcquisitionLogs.AsNoTracking()
            .Where(l => l.ReportTrackingId == reportTrackingIdGuid && !l.IsDeleted);

        // Scalar aggregates in a single DB pass
        var totals = await baseQuery
            .GroupBy(_ => 1)
            .Select(g => new
            {
                TotalLogs = g.Count(),
                TotalPatients = g.Select(l => l.PatientId).Where(p => p != null).Distinct().Count(),
                TotalRetryAttempts = g.Sum(l => (int?)l.RetryAttempts ?? 0),
                TotalCompletionTimeMs = g.Sum(l => (long?)l.CompletionTimeMilliseconds ?? 0L)
            })
            .FirstOrDefaultAsync(cancellationToken);

        var statistics = new DataAcquisitionLogStatistics
        {
            TotalLogs = totals?.TotalLogs ?? 0,
            TotalPatients = totals?.TotalPatients ?? 0,
            TotalRetryAttempts = totals?.TotalRetryAttempts ?? 0,
            TotalCompletionTimeMilliseconds = totals?.TotalCompletionTimeMs ?? 0
        };

        // Status counts via DB GroupBy
        var statusCounts = await baseQuery
            .Where(l => l.Status != null)
            .GroupBy(l => l.Status!.Value)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        foreach (var sc in statusCounts)
            statistics.RequestStatusCounts[sc.Status] = sc.Count;

        // QueryType counts via DB GroupBy
        var queryTypeCounts = await baseQuery
            .Where(l => l.QueryType != null)
            .GroupBy(l => l.QueryType)
            .Select(g => new { QueryType = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        foreach (var qt in queryTypeCounts)
            statistics.QueryTypeCounts[(FhirQueryType)qt.QueryType] = qt.Count;

        // QueryPhase counts via DB GroupBy
        var queryPhaseCounts = await baseQuery
            .Where(l => l.QueryPhase != null)
            .GroupBy(l => l.QueryPhase!.Value)
            .Select(g => new { Phase = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        foreach (var qp in queryPhaseCounts)
            statistics.QueryPhaseCounts[qp.Phase] = qp.Count;

        // Resource type counts + total.
        var resourceTypeCounts = await _dbContext.DataAcquisitionLogResourceIds
            .AsNoTracking()
            .Where(r =>
                r.DataAcquisitionLog.ReportTrackingId == reportTrackingIdGuid
                && !r.DataAcquisitionLog.IsDeleted
                && r.DataAcquisitionLog.Status == RequestStatus.Completed
                && r.ResourceId != null
                && r.ResourceId != "")
            .GroupBy(r => r.ResourceId.IndexOf("/") > 0
                ? r.ResourceId.Substring(0, r.ResourceId.IndexOf("/"))
                : r.ResourceId)
            .Select(g => new { ResourceType = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        foreach (var rt in resourceTypeCounts)
        {
            if (!string.IsNullOrEmpty(rt.ResourceType))
                statistics.ResourceTypeCounts[rt.ResourceType] = rt.Count;
        }

        statistics.TotalResourcesAcquired = statistics.ResourceTypeCounts.Values.Sum();

        // Distinct patients where ALL logs are terminal (Completed, MaxRetriesReached, Skipped, Cancelled)
        var terminalStatuses = new[] { RequestStatus.Completed, RequestStatus.MaxRetriesReached, RequestStatus.Skipped, RequestStatus.Cancelled };
        statistics.TotalCompletedPatients = await baseQuery
            .Where(l => l.PatientId != null)
            .GroupBy(l => l.PatientId)
            .Where(g => g.Count(l => l.Status == null || !terminalStatuses.Contains(l.Status.Value)) == 0)
            .CountAsync(cancellationToken);

        // Fastest / slowest completion times
        var fastestLog = await baseQuery
            .Where(l => l.CompletionTimeMilliseconds != null)
            .OrderBy(l => l.CompletionTimeMilliseconds)
            .Select(l => new { l.Id, l.CompletionTimeMilliseconds })
            .FirstOrDefaultAsync(cancellationToken);

        if (fastestLog != null)
        {
            var fastestResourceTypes = await (
                from fq in _dbContext.FhirQueries.AsNoTracking()
                join fqrt in _dbContext.FhirQueryResourceTypes on fq.Id equals fqrt.FhirQueryId
                where fq.DataAcquisitionLogId == fastestLog.Id
                select fqrt.ResourceType
            ).ToListAsync(cancellationToken);

            statistics.FastestCompletionTimeMilliseconds = new ResourceCompletionTime(
                string.Join(",", fastestResourceTypes.Select(r => r.ToString()).Distinct().OrderBy(r => r)),
                fastestLog.CompletionTimeMilliseconds!.Value);
        }

        var slowestLog = await baseQuery
            .Where(l => l.CompletionTimeMilliseconds != null)
            .OrderByDescending(l => l.CompletionTimeMilliseconds)
            .Select(l => new { l.Id, l.CompletionTimeMilliseconds })
            .FirstOrDefaultAsync(cancellationToken);

        if (slowestLog != null)
        {
            var slowestResourceTypes = await (
                from fq in _dbContext.FhirQueries.AsNoTracking()
                join fqrt in _dbContext.FhirQueryResourceTypes on fq.Id equals fqrt.FhirQueryId
                where fq.DataAcquisitionLogId == slowestLog.Id
                select fqrt.ResourceType
            ).ToListAsync(cancellationToken);

            statistics.SlowestCompletionTimeMilliseconds = new ResourceCompletionTime(
                string.Join(",", slowestResourceTypes.Select(r => r.ToString()).Distinct().OrderBy(r => r)),
                slowestLog.CompletionTimeMilliseconds!.Value);
        }

        // Per-resource-type completion time aggregation.
        var completionTimeRows = await (
            from log in _dbContext.DataAcquisitionLogs.AsNoTracking()
            join fq in _dbContext.FhirQueries on log.Id equals fq.DataAcquisitionLogId
            join fqrt in _dbContext.FhirQueryResourceTypes on fq.Id equals fqrt.FhirQueryId
            where log.ReportTrackingId == reportTrackingIdGuid
                  && !log.IsDeleted
                  && log.CompletionTimeMilliseconds != null
            select new
            {
                log.Id,
                log.CompletionTimeMilliseconds,
                fqrt.ResourceType
            }
        ).ToListAsync(cancellationToken);

        foreach (var logGroup in completionTimeRows.GroupBy(x => x.Id))
        {
            var key = string.Join(",", logGroup.Select(x => x.ResourceType.ToString()).Distinct().OrderBy(r => r));
            statistics.ResourceTypeCompletionTimeMilliseconds.TryGetValue(key, out var existing);
            statistics.ResourceTypeCompletionTimeMilliseconds[key] = existing + logGroup.First().CompletionTimeMilliseconds!.Value;
        }

        return statistics;
    }

    public async Task<DataAcquisitionLogStatusStatistics> GetDataAcquisitionLogStatusStatisticsByReportAsync(
        string reportId, string? patientId = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(reportId))
        {
            throw new ArgumentNullException(nameof(reportId), "Report ID cannot be null or empty.");
        }

        if (!Guid.TryParse(reportId, out var reportTrackingIdGuid))
        {
            throw new ArgumentException("Report ID must be a valid GUID.", nameof(reportId));
        }

        var query = _dbContext.DataAcquisitionLogs
            .AsNoTracking()
            .Where(log => log.ReportTrackingId == reportTrackingIdGuid);

        if (!string.IsNullOrWhiteSpace(patientId))
        {
            query = query.Where(log => log.PatientId == patientId);
        }

        var statuses = await query
            .Where(log => log.Status != null)
            .GroupBy(log => log.Status!.Value)
            .OrderBy(g => g.Key)
            .Select(g => new DataAcquisitionLogStatusCount
            {
                Name = g.Key.ToString(),
                Count = g.Count()
            })
            .ToListAsync(cancellationToken);

        return new DataAcquisitionLogStatusStatistics
        {
            ReportId = reportId,
            PatientId = string.IsNullOrWhiteSpace(patientId) ? null : patientId,
            Statuses = statuses
        };
    }

    public async Task<bool> CheckIfReferenceResourceHasBeenSent(string referenceId, string reportTrackingId,
        string facilityId, string correlationId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(referenceId))
            throw new ArgumentNullException(nameof(referenceId), "Reference ID cannot be null or empty.");
        if (string.IsNullOrWhiteSpace(facilityId))
            throw new ArgumentNullException(nameof(facilityId), "Facility ID cannot be null or empty.");
        if (string.IsNullOrWhiteSpace(reportTrackingId))
            throw new ArgumentNullException(nameof(reportTrackingId), "Report Tracking ID cannot be null or empty.");
        if (string.IsNullOrWhiteSpace(correlationId))
            throw new ArgumentNullException(nameof(correlationId), "Correlation ID cannot be null or empty.");

        if (!Guid.TryParse(reportTrackingId, out var reportTrackingIdGuid))
            throw new ArgumentException("Report Tracking ID must be a valid GUID.", nameof(reportTrackingId));

        return await _dbContext.DataAcquisitionLogResourceIds
            .AnyAsync(r =>
                r.ResourceId == referenceId
                && r.DataAcquisitionLog.ReportTrackingId == reportTrackingIdGuid
                && r.DataAcquisitionLog.FacilityId == facilityId
                && r.DataAcquisitionLog.CorrelationId == correlationId,
                cancellationToken);
    }

    public async Task<List<string>> GetFacilitiesWithPendingAndRetryableFailedRequests(
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.DataAcquisitionLogs.AsNoTracking()
            .Where(l => l.Status == RequestStatus.Pending || l.Status == RequestStatus.Failed)
            .Select(l => l.FacilityId)
            .Distinct()
            .ToListAsync(cancellationToken);
    }

    private IQueryable<DataAcquisitionLog> BuildSearchQuery(SearchDataAcquisitionLogRequest model)
    {
        var query = _dbContext.DataAcquisitionLogs.AsNoTracking().AsQueryable();

        if (!model.IncludeDeleted)
        {
            query = query.Where(log => !log.IsDeleted);
        }

        if (!string.IsNullOrEmpty(model.FacilityId))
        {
            query = query.Where(log => log.FacilityId == model.FacilityId);
        }

        if (!string.IsNullOrEmpty(model.CorrelationId))
        {
            query = query.Where(log => log.CorrelationId == model.CorrelationId);
        }

        if (!string.IsNullOrEmpty(model.PatientId))
        {
            query = query.Where(log => log.PatientId == model.PatientId);
        }

        if (!string.IsNullOrEmpty(model.ReportTrackingId))
        {
            if (Guid.TryParse(model.ReportTrackingId, out var parsedReportTrackingId))
            {
                query = query.Where(log => log.ReportTrackingId == parsedReportTrackingId);
            }
            else
            {
                query = query.Where(_ => false);
            }
        }

        if (model.QueryPhase.HasValue)
        {
            query = query.Where(log => log.QueryPhase == model.QueryPhase.Value);
        }

        if (model.QueryType.HasValue)
        {
            query = query.Where(log => log.QueryType == model.QueryType.Value);
        }

        if (model.AcquisitionPriority.HasValue)
        {
            query = query.Where(log => log.Priority == model.AcquisitionPriority.Value);
        }

        if (model.RequestStatuses != null && model.RequestStatuses.Any())
        {
            query = query.Where(log => log.Status != null && model.RequestStatuses.Contains(log.Status.Value));
        }

        if (!string.IsNullOrEmpty(model.ResourceType))
        {
            var resourceType = Enum.Parse<ResourceType>(model.ResourceType, ignoreCase: true);
            query = query.Where(log =>
                log.FhirQueries.Any(q => q.FhirQueryResourceTypes.Any(r => r.ResourceType == resourceType)));
        }

        if (model.CreatedBefore.HasValue)
        {
            query = query.Where(log => log.CreateDate <= model.CreatedBefore.Value);
        }

        // Free-text search: applied as an OR across the columns surfaced in the UI's
        // log table. Each branch is gated by whether the term is structurally compatible
        // with that column so we never ship a guaranteed-no-match clause to SQL:
        //   * PatientId   — case-insensitive substring (LIKE %term%) — the dominant use case.
        //   * Id          — exact match, only when the term parses as long.
        //   * ResourceType — exact match against the FHIR enum value, only when the term
        //                    parses as a known ResourceType (the column is stored as the
        //                    enum string via EnumToStringConverter, so a partial match
        //                    would require raw-SQL escape hatches; exact is enough for
        //                    "Observation", "Encounter", etc.).
        // Combined with the structured filters above via AND, as expected.
        if (!string.IsNullOrWhiteSpace(model.SearchTerm))
        {
            var term = model.SearchTerm.Trim();
            var likeTerm = $"%{term}%";
            var hasIdMatch = long.TryParse(term, out var parsedId);
            var hasTypeMatch = Enum.TryParse<Hl7.Fhir.Model.ResourceType>(term, ignoreCase: true, out var parsedResourceType);

            query = query.Where(log =>
                (log.PatientId != null && EF.Functions.Like(log.PatientId, likeTerm))
                || (hasIdMatch && log.Id == parsedId)
                || (hasTypeMatch && log.FhirQueries.Any(q =>
                        q.FhirQueryResourceTypes.Any(rt => rt.ResourceType == parsedResourceType))));
        }

        return query;
    }

    private static IQueryable<DataAcquisitionLog> ApplySort(IQueryable<DataAcquisitionLog> query, string? sortBy,
        SortOrder sortOrder)
    {
        var normalizedSortBy = sortBy?.Trim().ToLowerInvariant();
        var descending = sortOrder == SortOrder.Descending;

        return normalizedSortBy switch
        {
            "executiondate" => descending ? query.OrderByDescending(log => log.ExecutionDate) : query.OrderBy(log => log.ExecutionDate),
            "createdate" => descending ? query.OrderByDescending(log => log.CreateDate) : query.OrderBy(log => log.CreateDate),
            "facilityid" => descending ? query.OrderByDescending(log => log.FacilityId) : query.OrderBy(log => log.FacilityId),
            "patientid" => descending ? query.OrderByDescending(log => log.PatientId) : query.OrderBy(log => log.PatientId),
            "querytype" => descending ? query.OrderByDescending(log => log.QueryType) : query.OrderBy(log => log.QueryType),
            "queryphase" => descending ? query.OrderByDescending(log => log.QueryPhase) : query.OrderBy(log => log.QueryPhase),
            "status" => descending ? query.OrderByDescending(log => log.Status) : query.OrderBy(log => log.Status),
            "priority" => descending ? query.OrderByDescending(log => log.Priority) : query.OrderBy(log => log.Priority),
            "retryattempts" => descending ? query.OrderByDescending(log => log.RetryAttempts) : query.OrderBy(log => log.RetryAttempts),
            "isdeleted" => descending ? query.OrderByDescending(log => log.IsDeleted) : query.OrderBy(log => log.IsDeleted),
            "reporttrackingid" => descending ? query.OrderByDescending(log => log.ReportTrackingId) : query.OrderBy(log => log.ReportTrackingId),
            _ => descending ? query.OrderByDescending(log => log.Id) : query.OrderBy(log => log.Id)
        };
    }

    public async Task<List<DataAcquisitionLogModel>> GetNextEligibleBatchForFacility(string facilityId, long? lastId,
        int batchSize, List<RequestStatus> statuses, DateTime? designagtedExecutionTime = null,
        CancellationToken cancellationToken = default)
    {
        designagtedExecutionTime ??= DateTime.UtcNow;
        var terminalStatuses = new[]
        {
            RequestStatus.Completed,
            RequestStatus.MaxRetriesReached,
            RequestStatus.Skipped,
            RequestStatus.Cancelled,
            RequestStatus.ConfigurationMissing,
        };

        var query = from log in _dbContext.DataAcquisitionLogs.AsNoTracking()
                    where log.FacilityId == facilityId
                          && (lastId == null || log.Id > lastId)
                          && (log.ExecutionDate == null || log.ExecutionDate <= designagtedExecutionTime)
                          && (log.Status == null || statuses.Contains(log.Status.Value))
                          && (log.ReferenceResourceType == null
                              || (log.CorrelationId != null
                                  && log.QueryPhase != null
                                  && !_dbContext.DataAcquisitionLogs.Any(sibling =>
                                      sibling.FacilityId == log.FacilityId
                                      && sibling.CorrelationId == log.CorrelationId
                                      && sibling.QueryPhase == log.QueryPhase
                                      && sibling.ReferenceResourceType == null
                                      && (sibling.Status == null || !terminalStatuses.Contains(sibling.Status.Value)))))
                    orderby log.Id
                    select new DataAcquisitionLogModel
                    {
                        Id = log.Id,
                        Priority = log.Priority,
                        FacilityId = log.FacilityId,
                        IsCensus = log.IsCensus,
                        PatientId = log.PatientId,
                        ReportableEvent = log.ReportableEvent,
                        ReportTrackingId = log.ReportTrackingId != null ? log.ReportTrackingId.ToString().ToLower() : null,
                        CorrelationId = log.CorrelationId,
                        ReferenceResourceType = log.ReferenceResourceType,
                        FhirVersion = log.FhirVersion,
                        QueryType = log.QueryType,
                        QueryPhase = log.QueryPhase,
                        Status = log.Status,
                        ExecutionDate = log.ExecutionDate,
                        TraceId = log.TraceId,
                        RetryAttempts = log.RetryAttempts,
                        CompletionDate = log.CompletionDate,
                        CompletionTimeMilliseconds = log.CompletionTimeMilliseconds,
                        ResourceAcquiredIds = log.ResourceIds.Select(r => r.ResourceId).ToList(),
                        Notes = null,
                        ScheduledReport = log.ScheduledReportEntity != null ? new ScheduledReport
                        {
                            ReportTrackingId = log.ScheduledReportEntity.ReportTrackingId.ToString().ToLower(),
                            Frequency = log.ScheduledReportEntity.Frequency,
                            StartDate = DateTime.SpecifyKind(log.ScheduledReportEntity.StartDate, DateTimeKind.Utc),
                            EndDate = DateTime.SpecifyKind(log.ScheduledReportEntity.EndDate, DateTimeKind.Utc),
                            ReportTypes = log.ScheduledReportEntity.ReportTypes != null
                                ? log.ScheduledReportEntity.ReportTypes.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries).ToList()
                                : new List<string>()
                        } : null
                    };

        return await query
            .Take(batchSize)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<long>> GetOrphanedTailLogIds(TimeSpan minAge, int maxResults = 50, CancellationToken cancellationToken = default)
    {
        var terminalStatuses = new[] { RequestStatus.Completed, RequestStatus.MaxRetriesReached, RequestStatus.Skipped, RequestStatus.Cancelled };
        var cutoff = DateTime.UtcNow.Subtract(minAge);

        // Find groups where:
        //  - TailSent is still false
        //  - SiblingCount is stamped (creation completed)
        //  - Last activity was > minAge ago (avoids racing with the inline path)
        //  - ALL logs in the group are terminal (no incomplete siblings)
        var orphanedGroups = await _dbContext.DataAcquisitionLogs.AsNoTracking()
            .Where(l =>
                !l.TailSent
                && l.SiblingCount != null
                && l.CorrelationId != null
                && l.QueryPhase != null
                && l.ModifyDate != null && l.ModifyDate <= cutoff
                && l.Status != null && terminalStatuses.Contains(l.Status.Value))
            .GroupBy(l => new { l.FacilityId, l.CorrelationId, l.QueryPhase })
            .Where(g =>
                g.Count() == g.Max(l => l.SiblingCount)
                && !_dbContext.DataAcquisitionLogs.Any(sibling =>
                    sibling.FacilityId == g.Key.FacilityId
                    && sibling.CorrelationId == g.Key.CorrelationId
                    && sibling.QueryPhase == g.Key.QueryPhase
                    && !sibling.TailSent
                    && (sibling.Status == null || !terminalStatuses.Contains(sibling.Status.Value))))
            .Select(g => g.Min(l => l.Id))
            .Take(maxResults)
            .ToListAsync(cancellationToken);

        return orphanedGroups;
    }
}