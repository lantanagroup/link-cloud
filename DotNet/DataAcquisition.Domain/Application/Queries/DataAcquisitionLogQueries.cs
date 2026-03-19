using System.Diagnostics;
using Polly;
using Polly.Retry;
using Microsoft.Data.SqlClient;
using LantanaGroup.Link.Shared.Application.Models.Telemetry;
using DataAcquisition.Domain.Application.Models;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Api.Configuration;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Api.QueryLog;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Api.Requests;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Domain;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Exceptions;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Kafka;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Context;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Entities;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models.Enums;
using LantanaGroup.Link.DataAcquisition.Domain.Models;
using LantanaGroup.Link.Shared.Application.Enums;
using LantanaGroup.Link.Shared.Application.Interfaces.Models;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Models.Responses;
using LantanaGroup.Link.Shared.Application.Services.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using IDatabase = LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.IDatabase;
using RequestStatus = LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models.Enums.RequestStatus;
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
    Task<int> GetCountOfNonRefLogsIncompleteAsync(string facilityId, string reportTrackingId, string correlationId,
        CancellationToken cancellationToken = default);

    Task<IPagedModel<QueryLogSummaryModel>> SearchQueryLogSummaryAsync(SearchDataAcquisitionLogRequest request,
        CancellationToken cancellationToken = default);

    Task<PagedConfigModel<DataAcquisitionLogModel>> SearchAsync(SearchDataAcquisitionLogRequest model,
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

    Task<bool> TrySetLogToQueuedAsync(long logId, CancellationToken cancellationToken);

    Task<bool> TrySetLogStatusAsync(long logId, List<RequestStatus> validCurrentStatuses, RequestStatus newStatus,
        CancellationToken cancellationToken = default);

    Task<int> FailStalledQueuedLogsAsync(int stallMinutes, int maxBatches = 20, CancellationToken cancellationToken = default);

    Task<DataAcquisitionLogModel?> UpdateAsync(UpdateDataAcquisitionLogModel updateLog,
        CancellationToken cancellationToken = default);
}

public class DataAcquisitionLogQueries : IDataAcquisitionLogQueries
{
    private readonly IDatabase _database;
    private readonly DataAcquisitionDbContext _dbContext;
    private readonly ILogger<DataAcquisitionLogQueries> _logger;
    private readonly AsyncRetryPolicy _deadlockRetryPolicy;

    public DataAcquisitionLogQueries(IDatabase database, DataAcquisitionDbContext dbContext,
        ILogger<DataAcquisitionLogQueries> logger)
    {
        _database = database ?? throw new ArgumentNullException(nameof(database));
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        var random = new Random();
        _deadlockRetryPolicy = Policy
            .Handle<SqlException>(ex => ex.Number == 1205) // SQL Deadlock error number
            .WaitAndRetryAsync(5, retryAttempt => 
                TimeSpan.FromMilliseconds(Math.Pow(2, retryAttempt) * 100) + TimeSpan.FromMilliseconds(random.Next(0, 100)),
                (exception, timeSpan, retryCount, context) =>
                {
                    _logger.LogWarning(exception, "Deadlock detected (retry {RetryCount}). Retrying in {SleepDuration}ms...", retryCount, timeSpan.TotalMilliseconds);
                });
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
            select log.ResourceAcquiredIds).ToListAsync(cancellationToken);

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

        var entity = await _dbContext.DataAcquisitionLogs
            .Include(l => l.FhirQueries)
            .ThenInclude(q => q.FhirQueryResourceTypes)
            .Include(l => l.FhirQueries)
            .ThenInclude(q => q.ResourceReferenceTypes)
            .Include(l => l.ReferenceResources)
            .SingleOrDefaultAsync(l => l.Id == id, cancellationToken);
        
        return entity == null ? null : DataAcquisitionLogModel.FromDomain(entity);
    }

    public async Task<int> GetCountOfNonRefLogsIncompleteAsync(string facilityId, string reportTrackingId,
        string correlationId, CancellationToken cancellationToken = default)
    {
        using var activity = ServiceActivitySource.Instance.StartActivity("DataAcquisitionLogQueries.GetCountOfNonRefLogsIncompleteAsync");
        activity?.SetTag(DiagnosticNames.FacilityId, facilityId);
        activity?.SetTag(DiagnosticNames.ReportTrackingId, reportTrackingId);
        activity?.SetTag(DiagnosticNames.CorrelationId, correlationId);

        if (string.IsNullOrWhiteSpace(facilityId))
            throw new ArgumentNullException(nameof(facilityId), "Facility ID cannot be null or empty.");

        if (string.IsNullOrWhiteSpace(reportTrackingId))
            throw new ArgumentNullException(nameof(reportTrackingId), "Report Tracking ID cannot be null or empty.");

        if (string.IsNullOrWhiteSpace(correlationId))
            throw new ArgumentNullException(nameof(correlationId), "Correlation ID cannot be null or empty.");

        return await _deadlockRetryPolicy.ExecuteAsync(async () =>
        {
            return await (from l in _dbContext.DataAcquisitionLogs.AsNoTracking()
                where l.FacilityId == facilityId
                      && l.ReportTrackingId == reportTrackingId
                      && l.CorrelationId == correlationId
                      && !(l.Status == RequestStatus.Completed || l.Status == RequestStatus.MaxRetriesReached || l.Status == RequestStatus.Skipped)
                      && !l.TailSent
                      && l.FhirQueries.Any(fq => fq.IsReference == false)
                select l).CountAsync(cancellationToken);
        });
    }

    public async Task<IEnumerable<TailingMessageModel>> GetTailingMessages(
        CancellationToken cancellationToken = default)
    {
        var completedOrFailedStatuses = new[]
            { RequestStatus.Completed, RequestStatus.MaxRetriesReached, RequestStatus.Skipped };

        try
        {
            // Optimization: Filter logs that definitely haven't had their tail sent yet
            // and have the necessary identifiers for grouping.
            var baseQuery = _dbContext.DataAcquisitionLogs.AsNoTracking()
                .Where(log =>
                    !log.TailSent &&
                    log.ReportTrackingId != null &&
                    log.CorrelationId != null &&
                    log.ReportStartDate != null &&
                    log.ReportEndDate != null);

            var query = baseQuery
                .GroupBy(log => new
                {
                    log.FacilityId,
                    log.ReportTrackingId,
                    log.CorrelationId,
                    log.ReportStartDate,
                    log.ReportEndDate,
                    log.QueryPhase,
                })
                .Select(g => new
                {
                    g.Key,
                    // Use Sum or Min/Max logic instead of g.All to avoid correlated subqueries
                    TotalCount = g.Count(),
                    FinishedCount = g.Count(log => log.Status != null && completedOrFailedStatuses.Contains(log.Status.Value)),
                    LogIds = g.Select(x => x.Id).ToList(),
                    TraceParentId = g.Where(x => x.TraceId != null).OrderBy(x => x.Id).Select(x => x.TraceId).FirstOrDefault(),
                    PatientId = g.Select(x => x.PatientId).FirstOrDefault(),
                    ReportableEvent = g.Select(x => x.ReportableEvent).FirstOrDefault(),
                    ScheduledReport = g.Select(x => x.ScheduledReport).FirstOrDefault()
                })
                // Only groups where ALL logs are finished
                .Where(x => x.TotalCount == x.FinishedCount)
                .Select(x => new TailingMessageModel
                {
                    FacilityId = x.Key.FacilityId ?? string.Empty,
                    CorrelationId = x.Key.CorrelationId ?? string.Empty,
                    LogIds = x.LogIds,
                    TraceParentId = x.TraceParentId ?? string.Empty,
                    ResourceAcquired = new ResourceAcquired
                    {
                        PatientId = x.PatientId ?? string.Empty,
                        QueryType = x.Key.QueryPhase.ToString() ?? string.Empty,
                        ReportableEvent = x.ReportableEvent ?? default,
                        AcquisitionComplete = true,
                        ScheduledReports = new List<ScheduledReport>
                        {
                            x.ScheduledReport
                        }
                    }
                });

            return await query.ToListAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            // Log cancellation if needed
            _logger.LogWarning("GetTailingMessages operation was cancelled.");
            throw;
        }
        catch (Exception ex)
        {
            // Log the error (replace with your logger if available)
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

        var query = BuildSearchQuery(request);
        query = ApplySort(query, request.SortBy, request.SortOrder);

        var total = await query.CountAsync(cancellationToken);
        var pageLogs = await query
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(log => new
            {
                log.Id,
                log.Priority,
                log.FacilityId,
                log.PatientId,
                log.FhirVersion,
                log.QueryType,
                log.QueryPhase,
                log.ExecutionDate,
                log.CreateDate,
                log.RetryAttempts,
                log.Status,
                log.IsDeleted,
                log.ReportTrackingId
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
                    q.QueryParameters,
                    ResourceTypes = q.FhirQueryResourceTypes.Select(rt => rt.ResourceType).ToList()
                })
                .ToListAsync(cancellationToken);

            var firstQueryByLogId = queryInfo
                .OrderBy(q => q.Id)
                .GroupBy(q => q.DataAcquisitionLogId)
                .ToDictionary(g => g.Key, g => g.First());

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


    public async Task<bool> TrySetLogStatusAsync(long logId, List<RequestStatus> validCurrentStatuses,
        RequestStatus newStatus,
        CancellationToken cancellationToken = default)
    {
        using var activity = ServiceActivitySource.Instance.StartActivity("DataAcquisitionLogQueries.TrySetLogStatusAsync");
        activity?.SetTag(DiagnosticNames.ReportId, logId);

        return await _deadlockRetryPolicy.ExecuteAsync(async () =>
        {
            int rowsAffected = await _dbContext.DataAcquisitionLogs
                .Where(l => l.Id == logId && l.Status != null && validCurrentStatuses.Contains(l.Status.Value))
                .ExecuteUpdateAsync(setters => setters
                        .SetProperty(l => l.Status, newStatus)
                        .SetProperty(l => l.ModifyDate, DateTime.UtcNow),
                    cancellationToken);

            return rowsAffected > 0;
        });
    }

    public async Task<bool> TrySetLogToQueuedAsync(long logId, CancellationToken cancellationToken)
    {
        return await TrySetLogStatusAsync(logId, [RequestStatus.Ready, RequestStatus.Pending], RequestStatus.Queued,
            cancellationToken);
    }

    public async Task<int> FailStalledQueuedLogsAsync(int stallMinutes, int maxBatches = 20, CancellationToken cancellationToken = default)
    {
        var stallThreshold = DateTime.UtcNow.AddMinutes(-stallMinutes);

        // High-speed bulk update without fetching entities or using transactions.
        // This avoids LINQ translation errors with JSON collections and minimizes lock duration.
        return await _deadlockRetryPolicy.ExecuteAsync(async () =>
        {
            return await _dbContext.DataAcquisitionLogs
                .Where(l => l.Status == RequestStatus.Queued && l.ModifyDate <= stallThreshold)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(l => l.Status, RequestStatus.Failed)
                    .SetProperty(l => l.ModifyDate, DateTime.UtcNow),
                    cancellationToken);
        });
    }

    public async Task<PagedConfigModel<DataAcquisitionLogModel>> SearchAsync(SearchDataAcquisitionLogRequest model,
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
                FhirQuery = l.FhirQueries != null
                    ? l.FhirQueries.Select(q =>
                        new FhirQueryModel
                        {
                            Id = q.Id,
                            FacilityId = q.FacilityId,
                            MeasureId = q.MeasureId,
                            IdQueryParameterValues = q.IdQueryParameterValues.ToList(),
                            IsReference = q.IsReference,
                            QueryType = q.QueryType,
                            ResourceTypes = q.FhirQueryResourceTypes.Select(r => r.ResourceType).ToList(),
                            QueryParameters = q.QueryParameters,
                            Paged = q.Paged,
                            DataAcquisitionLogId = q.DataAcquisitionLogId,
                            CensusListId = q.CensusListId,
                            CensusPatientStatus = q.CensusPatientStatus,
                            CensusTimeFrame = q.CensusTimeFrame,
                            ResourceReferenceTypes = q.ResourceReferenceTypes != null
                                ? q.ResourceReferenceTypes.Select(rt => new ResourceReferenceTypeModel
                                {
                                    Id = rt.Id,
                                    FacilityId = rt.FacilityId,
                                    QueryPhase = rt.QueryPhase,
                                    ResourceType = rt.ResourceType,
                                    FhirQueryId = rt.FhirQueryId,
                                    CreateDate = rt.CreateDate,
                                    ModifyDate = rt.ModifyDate,
                                }).ToList()
                                : new()
                        }).ToList()
                    : new(),
                Status = l.Status,
                ExecutionDate = l.ExecutionDate,
                CreateDate = l.CreateDate,
                TraceId = l.TraceId,
                RetryAttempts = l.RetryAttempts,
                CompletionDate = l.CompletionDate,
                CompletionTimeMilliseconds = l.CompletionTimeMilliseconds,
                ResourceAcquiredIds = l.ResourceAcquiredIds,
                ReferenceResources = l.ReferenceResources.Select(r => new ReferenceResourceModel
                {
                    Id = r.Id,
                    FacilityId = r.FacilityId,
                    ResourceId = r.ResourceId,
                    ResourceType = r.ResourceType,
                    ReferenceResource = r.ReferenceResource,
                    QueryPhase = r.QueryPhase,
                    DataAcquisitionLogId = r.DataAcquisitionLogId
                }).ToList(),
                Notes = l.Notes,
                ScheduledReport = l.ScheduledReport,
                IsDeleted = l.IsDeleted
            }).ToListAsync(cancellationToken);

        return new PagedConfigModel<DataAcquisitionLogModel>
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

        var logs = (await SearchAsync(new SearchDataAcquisitionLogRequest
        {
            ReportTrackingId = reportId,
            PageSize = int.MaxValue
        })).Records;

        var statistics = new DataAcquisitionLogStatistics
        {
            TotalLogs = logs.Count,
            TotalPatients = logs.DistinctBy(x => x.PatientId).Count(x => !string.IsNullOrEmpty(x.PatientId)),
            TotalResourcesAcquired = logs.Sum(log => log.ResourceAcquiredIds?.Count ?? 0),
            TotalRetryAttempts = logs.Sum(log => log.RetryAttempts ?? 0),
            TotalCompletionTimeMilliseconds = logs.Sum(log => log.CompletionTimeMilliseconds ?? 0)
        };

        // Calculate fastest and slowest completion times

        var fastestLog = logs.OrderBy(log => log.CompletionTimeMilliseconds).FirstOrDefault();
        if (fastestLog is { CompletionTimeMilliseconds: not null })
        {
            statistics.FastestCompletionTimeMilliseconds = new ResourceCompletionTime(
                string.Join(",", fastestLog.FhirQuery.SelectMany(x => x.ResourceTypes.Select(r => r.ToString()))),
                fastestLog.CompletionTimeMilliseconds.Value);
        }

        var slowestLog = logs.OrderByDescending(log => log.CompletionTimeMilliseconds).FirstOrDefault();
        if (slowestLog is { CompletionTimeMilliseconds: not null })
        {
            statistics.SlowestCompletionTimeMilliseconds = new ResourceCompletionTime(
                string.Join(",", slowestLog.FhirQuery.SelectMany(x => x.ResourceTypes.Select(r => r.ToString()))),
                slowestLog.CompletionTimeMilliseconds.Value);
        }


        // Populate counts
        foreach (var log in logs)
        {
            // Process Query Type
            var queryType = (FhirQueryType)log.QueryType;
            if (!statistics.QueryTypeCounts.TryGetValue(queryType, out var value))
            {
                value = 0;
                statistics.QueryTypeCounts[queryType] = value;
            }

            statistics.QueryTypeCounts[queryType] = ++value;

            // Process Query Phase
            if (!statistics.QueryPhaseCounts.TryGetValue(log.QueryPhase!.Value, out var qpValue))
            {
                qpValue = 0;
                statistics.QueryPhaseCounts[log.QueryPhase.Value] = qpValue;
            }

            statistics.QueryPhaseCounts[log.QueryPhase.Value] = ++qpValue;

            // Process Request Status
            if (!statistics.RequestStatusCounts.TryGetValue(log.Status.Value, out var scValue))
            {
                scValue = 0;
                statistics.RequestStatusCounts[log.Status.Value] = scValue;
            }

            statistics.RequestStatusCounts[log.Status.Value] = ++scValue;


            // Process Resources Acquired

            foreach (var resource in log.ResourceAcquiredIds ?? [])
            {
                if (string.IsNullOrEmpty(resource)) continue;

                var resourceTypeParts = resource.Trim().Split("/");

                if (resourceTypeParts.Length == 0) continue;

                var resourceType = resourceTypeParts[0];

                if (string.IsNullOrEmpty(resourceType))
                {
                    _logger.LogWarning("Invalid resource Id format: {Resource}", resource.Sanitize());
                    continue;
                }

                // Increment resource type count
                if (!statistics.ResourceTypeCounts.TryGetValue(resourceType, out var val))
                {
                    val = 0;
                    statistics.ResourceTypeCounts[resourceType] = val;
                }

                statistics.ResourceTypeCounts[resourceType] = ++val;
            }

            // Add completion time for this resource types
            if (!log.CompletionTimeMilliseconds.HasValue) continue;

            var resourceTypes = log.FhirQuery.SelectMany(x => x.ResourceTypes.Select(r => r.ToString())).ToList();

            var combinedResourceTypes = string.Join(",", resourceTypes);
            if (!statistics.ResourceTypeCompletionTimeMilliseconds.TryGetValue(combinedResourceTypes,
                    out var totalCompletionTime))
            {
                totalCompletionTime = 0;
                statistics.ResourceTypeCompletionTimeMilliseconds[combinedResourceTypes] = totalCompletionTime;
            }

            statistics.ResourceTypeCompletionTimeMilliseconds[combinedResourceTypes] +=
                log.CompletionTimeMilliseconds.Value;
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

        return await _dbContext.DataAcquisitionLogs
            .Where(x =>
                x.ReportTrackingId == reportTrackingId &&
                x.FacilityId == facilityId &&
                x.CorrelationId == correlationId)
            .AnyAsync(x => x.ResourceAcquiredIds != null &&
                           x.ResourceAcquiredIds.Contains(referenceId), cancellationToken);
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
                ResourceAcquiredIds = log.ResourceAcquiredIds,
                Notes = log.Notes,
                ScheduledReport = log.ScheduledReport
            };

        return await query
            .Take(batchSize)
            .ToListAsync(cancellationToken);
    }

    public Task<List<DataAcquisitionLogModel>> GetNextBatchForFacility(string facilityId, long? lastId, int batchSize,
        CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public async Task<DataAcquisitionLogModel?> UpdateAsync(UpdateDataAcquisitionLogModel updateLog,
        CancellationToken cancellationToken = default)
    {
        using var activity = Activity.Current?.Source.StartActivity();

        if (updateLog.Id is null or 0)
        {
            activity?.SetStatus(ActivityStatusCode.Error, "Log ID cannot be zero or null");
            throw new InvalidOperationException("Log ID cannot be zero or null");
        }

        return await _deadlockRetryPolicy.ExecuteAsync(async () =>
        {
            // 1. Fetch the existing entity
            var existingLog = await _dbContext.DataAcquisitionLogs
                .FirstOrDefaultAsync(l => l.Id == updateLog.Id, cancellationToken);

            if (existingLog is null)
            {
                activity?.SetStatus(ActivityStatusCode.Error, "Data acquisition log not found");
                throw new DataAcquisitionLogNotFoundException($"Data acquisition log with ID {updateLog.Id} not found.");
            }

            // 2. Apply updates
            if (updateLog.RetryAttempts is not null)
                existingLog.RetryAttempts = updateLog.RetryAttempts;

            if (updateLog.ResourceAcquiredIds is not null && updateLog.ResourceAcquiredIds.Count > 0)
                existingLog.ResourceAcquiredIds = updateLog.ResourceAcquiredIds;

            if (updateLog.TraceId is not null)
                existingLog.TraceId = updateLog.TraceId;

            if (updateLog.ExecutionDate is not null)
                existingLog.ExecutionDate = updateLog.ExecutionDate;

            if (updateLog.CompletionDate is not null)
                existingLog.CompletionDate = updateLog.CompletionDate;

            if (updateLog.CompletionTimeMilliseconds is not null)
                existingLog.CompletionTimeMilliseconds = updateLog.CompletionTimeMilliseconds;

            if (updateLog.Notes is not null)
                existingLog.Notes = updateLog.Notes;

            if (updateLog.Status is not null)
                existingLog.Status = updateLog.Status.Value;

            existingLog.ModifyDate = DateTime.UtcNow;

            // 3. Save changes
            _dbContext.DataAcquisitionLogs.Update(existingLog);
            await _dbContext.SaveChangesAsync(cancellationToken);

            return await GetAsync(updateLog.Id.Value, cancellationToken);
        });
    }
}