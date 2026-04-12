using DataAcquisition.Domain.Application.Models;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Api.Configuration;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Api.QueryLog;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Api.Requests;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Domain;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Kafka;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Context;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Entities;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models.Enums;
using LantanaGroup.Link.Shared.Application.Models.Integration.DataAcquisition;
using RequestStatus = LantanaGroup.Link.Shared.Application.Models.Integration.DataAcquisition.RequestStatus;
using QueryPhase = LantanaGroup.Link.Shared.Application.Models.Integration.DataAcquisition.QueryPhase;
using FhirQueryType = LantanaGroup.Link.Shared.Application.Models.Integration.DataAcquisition.FhirQueryType;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models;
using LantanaGroup.Link.DataAcquisition.Domain.Models;
using LantanaGroup.Link.Shared.Application.Interfaces.Models;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Models.Responses;
using LantanaGroup.Link.Shared.Application.Models.Telemetry;
using LantanaGroup.Link.Shared.Application.Services.Security;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
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

    public DataAcquisitionLogQueries(IDatabase database, DataAcquisitionDbContext dbContext,
        ILogger<DataAcquisitionLogQueries> logger)
    {
        _database = database ?? throw new ArgumentNullException(nameof(database));
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
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

    public async Task<DataAcquisitionLogModel?> GetAsync(long id, CancellationToken cancellationToken = default)
    {
        using var activity = ServiceActivitySource.Instance.StartActivity("DataAcquisitionLogQueries.GetAsync");
        activity?.SetTag(DiagnosticNames.ReportId, id);

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
                ReportTrackingId = l.ReportTrackingId,
                CorrelationId = l.CorrelationId,
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
                    IdQueryParameterValues = q.IdQueryParameterValues.ToList(),
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
                    ReportTrackingId = l.ScheduledReportEntity.ReportTrackingId,
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

            // Phase 1 — narrow candidate groups via terminal-status logs only.
            var candidateGroups = await _dbContext.DataAcquisitionLogs.AsNoTracking()
                .Where(log =>
                    !log.TailSent &&
                    log.Status != null && completedOrFailedStatuses.Contains(log.Status.Value) &&
                    log.ReportTrackingId != null &&
                    log.CorrelationId != null &&
                    log.ReportStartDate != null &&
                    log.ReportEndDate != null)
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
                    EarliestStart = g.Min(x => x.ReportStartDate)
                })
                .OrderBy(x => x.EarliestStart)
                .Take(100)
                .Select(x => x.Key)
                .ToListAsync(cancellationToken);

            if (candidateGroups.Count == 0)
                return [];

            // Phase 2 — for each candidate, check if ANY non-terminal log exists.
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

                // All logs are terminal — collect data for the tail message
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
                            ReportTrackingId = log.ScheduledReportEntity.ReportTrackingId,
                            Frequency = log.ScheduledReportEntity.Frequency,
                            StartDate = DateTime.SpecifyKind(log.ScheduledReportEntity.StartDate, DateTimeKind.Utc),
                            EndDate = DateTime.SpecifyKind(log.ScheduledReportEntity.EndDate, DateTimeKind.Utc),
                            ReportTypes = log.ScheduledReportEntity.ReportTypes != null
                                ? log.ScheduledReportEntity.ReportTypes.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries).ToList()
                                : new List<string>()
                        } : null,
                        log.ReportStartDate,
                        log.ReportEndDate
                    })
                    .ToListAsync(cancellationToken);

                if (groupLogs.Count == 0)
                    continue;

                var first = groupLogs.First();

                results.Add(new TailingMessageModel
                {
                    FacilityId = group.FacilityId ?? string.Empty,
                    CorrelationId = group.CorrelationId ?? string.Empty,
                    LogIds = groupLogs.Select(x => x.Id).ToList(),
                    TraceParentId = groupLogs.FirstOrDefault(x => x.TraceId != null)?.TraceId ?? string.Empty,
                    ResourceAcquired = new ResourceAcquired
                    {
                        PatientId = first.PatientId ?? string.Empty,
                        QueryType = group.QueryPhase.ToString() ?? string.Empty,
                        ReportableEvent = first.ReportableEvent ?? default,
                        AcquisitionComplete = true,
                        ScheduledReports = new List<ScheduledReport>
                        {
                            first.ScheduledReport
                        }
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

                records = pageLogs.Select(log =>
                {
                    firstQueryByLogId.TryGetValue(log.Id, out var fhirQuery);
                    var resourceTypes = fhirQuery?.ResourceTypes?.Select(rt => rt.ToString()).ToList() ??
                                        new List<string>();
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
                        ReportTrackingId = log.ReportTrackingId
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
                ReportTrackingId = l.ReportTrackingId,
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
        activity?.SetTag(DiagnosticNames.ReportId, reportId);

        var baseQuery = _dbContext.DataAcquisitionLogs.AsNoTracking()
            .Where(l => l.ReportTrackingId == reportId && !l.IsDeleted);

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

        // Resource type counts + total from ResourceIds junction table.
        // Include reference resources linked to completed logs when they are stored
        // separately in ReferenceResources.
        var completedResourceIds = await baseQuery
            .Where(l => l.Status == RequestStatus.Completed)
            .SelectMany(l => l.ResourceIds.Select(r => r.ResourceId))
            .ToListAsync(cancellationToken);

        var completedReferenceResourceIds = await baseQuery
            .Where(l => l.Status == RequestStatus.Completed)
            .SelectMany(l => l.ReferenceResources.Select(r => r.ResourceType + "/" + r.ResourceId))
            .ToListAsync(cancellationToken);

        var completedLogs = new List<string>(completedResourceIds.Count + completedReferenceResourceIds.Count);
        completedLogs.AddRange(completedResourceIds);

        // Avoid double-counting resources that already exist in ResourceIds.
        var existingResourceSet = completedResourceIds
            .Where(r => !string.IsNullOrWhiteSpace(r))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var referenceResourceId in completedReferenceResourceIds)
        {
            if (string.IsNullOrWhiteSpace(referenceResourceId))
                continue;

            if (existingResourceSet.Contains(referenceResourceId))
                continue;

            completedLogs.Add(referenceResourceId);
        }

        foreach (var resource in completedLogs)
        {
            if (string.IsNullOrWhiteSpace(resource)) continue;
            var slashIdx = resource.IndexOf('/');
            var resourceType = slashIdx > 0 ? resource[..slashIdx] : resource;
            if (string.IsNullOrEmpty(resourceType)) continue;

            statistics.ResourceTypeCounts.TryGetValue(resourceType, out var val);
            statistics.ResourceTypeCounts[resourceType] = val + 1;
        }

        statistics.TotalResourcesAcquired = statistics.ResourceTypeCounts.Values.Sum();

        // Distinct patients where ALL logs are terminal (Completed, MaxRetriesReached, Skipped, Cancelled)
        var terminalStatuses = new[] { RequestStatus.Completed, RequestStatus.MaxRetriesReached, RequestStatus.Skipped, RequestStatus.Cancelled };
        statistics.TotalCompletedPatients = await baseQuery
            .Where(l => l.PatientId != null)
            .GroupBy(l => l.PatientId)
            .Where(g => g.All(l => l.Status != null && terminalStatuses.Contains(l.Status.Value)))
            .CountAsync(cancellationToken);

        // Fastest / slowest completion times
        var fastest = await baseQuery
            .Where(l => l.CompletionTimeMilliseconds != null)
            .OrderBy(l => l.CompletionTimeMilliseconds)
            .Select(l => new
            {
                l.CompletionTimeMilliseconds,
                ResourceTypes = l.FhirQueries.SelectMany(q => q.FhirQueryResourceTypes.Select(r => r.ResourceType)).ToList()
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (fastest != null)
        {
            statistics.FastestCompletionTimeMilliseconds = new ResourceCompletionTime(
                string.Join(",", fastest.ResourceTypes),
                fastest.CompletionTimeMilliseconds!.Value);
        }

        var slowest = await baseQuery
            .Where(l => l.CompletionTimeMilliseconds != null)
            .OrderByDescending(l => l.CompletionTimeMilliseconds)
            .Select(l => new
            {
                l.CompletionTimeMilliseconds,
                ResourceTypes = l.FhirQueries.SelectMany(q => q.FhirQueryResourceTypes.Select(r => r.ResourceType)).ToList()
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (slowest != null)
        {
            statistics.SlowestCompletionTimeMilliseconds = new ResourceCompletionTime(
                string.Join(",", slowest.ResourceTypes),
                slowest.CompletionTimeMilliseconds!.Value);
        }

        // Per-resource-type completion time aggregation – lightweight projection
        var completionTimes = await baseQuery
            .Where(l => l.CompletionTimeMilliseconds != null)
            .Select(l => new
            {
                l.CompletionTimeMilliseconds,
                ResourceTypes = l.FhirQueries.SelectMany(q => q.FhirQueryResourceTypes.Select(r => r.ResourceType)).ToList()
            })
            .ToListAsync(cancellationToken);

        foreach (var ct in completionTimes)
        {
            var key = string.Join(",", ct.ResourceTypes);
            statistics.ResourceTypeCompletionTimeMilliseconds.TryGetValue(key, out var existing);
            statistics.ResourceTypeCompletionTimeMilliseconds[key] = existing + ct.CompletionTimeMilliseconds!.Value;
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

        var query = _dbContext.DataAcquisitionLogs
            .AsNoTracking()
            .Where(log => log.ReportTrackingId == reportId);

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

        return await _dbContext.DataAcquisitionLogResourceIds
            .AnyAsync(r =>
                r.ResourceId == referenceId
                && r.DataAcquisitionLog.ReportTrackingId == reportTrackingId
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
            query = query.Where(log => log.ReportTrackingId == model.ReportTrackingId);
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

        return query;
    }

    private static IQueryable<DataAcquisitionLog> ApplySort(IQueryable<DataAcquisitionLog> query, string? sortBy,
        LantanaGroup.Link.Shared.Application.Enums.SortOrder sortOrder)
    {
        var normalizedSortBy = sortBy?.Trim().ToLowerInvariant();
        var descending = sortOrder == LantanaGroup.Link.Shared.Application.Enums.SortOrder.Descending;

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

        var query = from log in _dbContext.DataAcquisitionLogs.AsNoTracking()
                    where log.FacilityId == facilityId
                          && (lastId == null || log.Id > lastId)
                          && (log.ExecutionDate == null || log.ExecutionDate <= designagtedExecutionTime)
                          && (log.Status == null || statuses.Contains(log.Status.Value))
                    orderby log.Id
                    select new DataAcquisitionLogModel
                    {
                        Id = log.Id,
                        Priority = log.Priority,
                        FacilityId = log.FacilityId,
                        IsCensus = log.IsCensus,
                        PatientId = log.PatientId,
                        ReportableEvent = log.ReportableEvent,
                        ReportTrackingId = log.ReportTrackingId,
                        CorrelationId = log.CorrelationId,
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
                            ReportTrackingId = log.ScheduledReportEntity.ReportTrackingId,
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
        var terminalStatuses = new[] { RequestStatus.Completed, RequestStatus.MaxRetriesReached, RequestStatus.Skipped };
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