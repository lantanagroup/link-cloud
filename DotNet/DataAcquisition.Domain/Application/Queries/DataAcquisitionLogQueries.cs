using LantanaGroup.Link.DataAcquisition.Domain.Application.Models;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Kafka;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Context;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Entities;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models.Enums;
using LantanaGroup.Link.Shared.Application.Enums;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Services.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;
using System.Linq.Expressions;
using IDatabase = LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.IDatabase;
using RequestStatus = LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models.Enums.RequestStatus;

namespace LantanaGroup.Link.DataAcquisition.Domain.Application.Queries;

public interface IDataAcquisitionLogQueries
{
    /// <summary>
    /// Get Logs that are in a Failed or Pending state that have not reach 10 retry attempts.
    /// </summary>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<List<DataAcquisitionLog>> GetPendingAndRetryableFailedRequests(CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a list of TailingMessageModel objects that represent the tailing messages for data acquisition logs.
    /// </summary>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<IEnumerable<TailingMessageModel>> GetTailingMessages(CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a complete data acquisition log by its ID, including related entities such as ScheduledReport, ReportableEvent, and FhirQuery.
    /// </summary>
    /// <param name="logId"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentNullException"></exception>
    /// <exception cref="KeyNotFoundException"></exception>
    Task<DataAcquisitionLog?> GetCompleteLogAsync(long logId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a data acquisition log entry based on the specified facility ID, report tracking ID, and resource
    /// type.
    /// </summary>
    /// <param name="facilityId">The unique identifier of the facility associated with the log entry. Cannot be null or empty.</param>
    /// <param name="reportTrackingId">The unique identifier of the report tracking entry associated with the log. Cannot be null or empty.</param>
    /// <param name="resourceType">The type of resource associated with the log entry. Cannot be null or empty.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests. The default value is <see cref="CancellationToken.None"/>.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the <see cref="DataAcquisitionLog"/>
    /// object  matching the specified criteria, or <see langword="null"/> if no matching log entry is found.</returns>
    Task<DataAcquisitionLog> GetLogByFacilityIdAndReportTrackingIdAndResourceType(string facilityId, string reportTrackingId, string resourceType, string correlationId, CancellationToken cancellationToken = default);

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
    Task<int> GetCountOfNonRefLogsIncompleteAsync(string facilityId, string reportTrackingId, string correlationId, CancellationToken cancellationToken = default);

    Task<(List<QueryLogSummaryModel> searchResults, int count)> SearchAsync(SearchDataAcquisitionLogRequest model,
        CancellationToken cancellationToken = default);

    Task<DataAcquisitionLog?> GetDataAcquisitionLogAsync(long logId, CancellationToken cancellationToken = default);

    Task<DataAcquisitionLogStatistics> GetDataAcquisitionLogStatisticsByReportAsync(string reportId, CancellationToken cancellationToken = default);

    Task<bool> CheckIfReferenceResourceHasBeenSent(string referenceId, string reportTrackingId, string facilityId, string correlationId, CancellationToken cancellationToken = default);

    Task<List<string>> GetFacilitiesWithPendingAndRetryableFailedRequests(CancellationToken cancellationToken = default);

    Task<List<DataAcquisitionLog>> GetNextEligibleBatchForFacility(string facilityId, long? lastId, int batchSize, CancellationToken cancellationToken = default);
}

public class DataAcquisitionLogQueries : IDataAcquisitionLogQueries
{
    private readonly IDatabase _database;
    private readonly DataAcquisitionDbContext _dbContext;
    private readonly ILogger<DataAcquisitionLogQueries> _logger;

    public DataAcquisitionLogQueries(IDatabase database, DataAcquisitionDbContext dbContext, ILogger<DataAcquisitionLogQueries> logger)
    {
        _database = database ?? throw new ArgumentNullException(nameof(database));
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Retrieves a complete data acquisition log by its ID, including related entities such as ScheduledReport, ReportableEvent, and FhirQuery.
    /// </summary>
    /// <param name="logId"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentNullException"></exception>
    /// <exception cref="KeyNotFoundException"></exception>
    public async Task<DataAcquisitionLog?> GetCompleteLogAsync(long logId, CancellationToken cancellationToken = default)
    {
        var log = await _dbContext.DataAcquisitionLogs
            .Include(l => l.FhirQuery)
            .ThenInclude(l => l.ResourceReferenceTypes)
            .FirstOrDefaultAsync(l => l.Id == logId, cancellationToken);

        return log;
    }

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
    public async Task<int> GetCountOfNonRefLogsIncompleteAsync(string facilityId, string reportTrackingId, string correlationId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(facilityId))
            throw new ArgumentNullException(nameof(facilityId), "Facility ID cannot be null or empty.");

        if (string.IsNullOrWhiteSpace(reportTrackingId))
            throw new ArgumentNullException(nameof(reportTrackingId), "Report Tracking ID cannot be null or empty.");

        if (string.IsNullOrWhiteSpace(correlationId))
            throw new ArgumentNullException(nameof(correlationId), "Correlation ID cannot be null or empty.");

        return await _dbContext.DataAcquisitionLogs
            .CountAsync(log => log.FacilityId == facilityId &&
                               log.ReportTrackingId == reportTrackingId &&
                               log.CorrelationId == correlationId &&
                               (log.Status == null || log.Status != RequestStatus.Completed) &&
                               !log.TailSent &&
                               log.FhirQuery.Any(fq => fq.isReference == false) // Ensure we only count non-reference logs
                               , cancellationToken);
    }

    /// <summary>
    /// Retrieves a data acquisition log based on the specified facility ID, report tracking ID, and resource type.
    /// </summary>
    /// <param name="facilityId">The unique identifier of the facility. Cannot be null or empty.</param>
    /// <param name="reportTrackingId">The unique identifier of the report tracking. Cannot be null or empty.</param>
    /// <param name="resourceType">The type of resource associated with the log. Cannot be null or empty.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests. Optional.</param>
    /// <returns>A <see cref="DataAcquisitionLog"/> object that matches the specified criteria, or <see langword="null"/> if no
    /// matching log is found.</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="facilityId"/>, <paramref name="reportTrackingId"/>, or <paramref name="resourceType"/>
    /// is null or empty.</exception>
    public async Task<DataAcquisitionLog> GetLogByFacilityIdAndReportTrackingIdAndResourceType(string facilityId, string reportTrackingId, string resourceType, string correlationId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(facilityId))
            throw new ArgumentNullException(nameof(facilityId), "Facility ID cannot be null or empty.");
        if (string.IsNullOrWhiteSpace(reportTrackingId))
            throw new ArgumentNullException(nameof(reportTrackingId), "Report Tracking ID cannot be null or empty.");
        if (string.IsNullOrWhiteSpace(resourceType))
            throw new ArgumentNullException(nameof(resourceType), "Resource Type cannot be null or empty.");

        var resourceTypeEnum = Enum.Parse<Hl7.Fhir.Model.ResourceType>(resourceType, ignoreCase: true);

        // I don't believe this will be performant as it will load all logs for the facility and report tracking ID.
        // Consider the following optimizations for whoever looks at this in the future:
        // 1. Add an index on FacilityId and ReportTrackingId in the database.
        // 2. Consider either abstracting the ResourceTypes to a separate table or changing the structure of ResourceTypes to comma-separated string.
        // 3. In-line sql to query this information.
        var candidates = await _dbContext.DataAcquisitionLogs
            .Include(dl => dl.FhirQuery)
            .Where(dl => dl.FacilityId == facilityId &&
                 dl.ReportTrackingId == reportTrackingId &&
                 dl.CorrelationId == correlationId)
            .ToListAsync(cancellationToken);

        var log = candidates
            .FirstOrDefault(dl =>
                dl.FhirQuery.SelectMany(fq => fq.ResourceTypes)
                           .Contains(resourceTypeEnum));

        return log;
    }

    /// <summary>
    /// Here is the T-SQL equivalent of the LINQ query:
    /// SELECT
    /// l.FacilityId,
    /// l.ReportTrackingId,
    /// l.CorrelationId,
    /// l.ReportStartDate,
    /// l.ReportEndDate,
    /// l.QueryPhase,
    /// -- Aggregate log IDs as a comma-separated string (SQL Server syntax)
    /// STRING_AGG(l.Id, ',') AS LogIds,
    /// -- Get the first PatientId, QueryPhase, ReportableEvent, ScheduledReport (if needed, use subqueries or window functions)
    /// MIN(l.PatientId) AS PatientId,
    /// MIN(l.QueryPhase) AS QueryType,
    /// MIN(l.ReportableEvent) AS ReportableEvent
    /// FROM
    /// DataAcquisitionLog l
    /// WHERE
    /// l.ReportTrackingId IS NOT NULL
    /// AND l.CorrelationId IS NOT NULL
    /// AND l.ReportStartDate IS NOT NULL
    /// AND l.ReportEndDate IS NOT NULL
    /// GROUP BY
    /// l.FacilityId,
    /// l.ReportTrackingId,
    /// l.CorrelationId,
    /// l.ReportStartDate,
    /// l.ReportEndDate,
    /// l.QueryPhase
    /// HAVING
    /// -- All logs in the group must have Status = 'Completed' and TailSent = 0(false)
    /// COUNT(*) = SUM(CASE WHEN l.Status = 'Completed' AND l.TailSent = 0 THEN 1 ELSE 0 END)
    /// </summary>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public async Task<IEnumerable<TailingMessageModel>> GetTailingMessages(CancellationToken cancellationToken = default)
    {
        var completedOrFailedStatuses = new[] { RequestStatus.Completed, RequestStatus.MaxRetriesReached };

        try
        {
            // Group and aggregate in SQL
            var query = _dbContext.DataAcquisitionLogs
                .Where(log =>
                    log.ReportTrackingId != null &&
                    log.CorrelationId != null &&
                    log.ReportStartDate != null &&
                    log.ReportEndDate != null)
                .GroupBy(log => new
                {
                    log.FacilityId,
                    log.ReportTrackingId,
                    log.CorrelationId,
                    log.ReportStartDate,
                    log.ReportEndDate,
                    log.QueryPhase,
                })
                .Where(g => g.All(log => log.Status != null && completedOrFailedStatuses.Contains(log.Status.Value) && !log.TailSent))
                .Select(g => new TailingMessageModel
                {
                    FacilityId = g.Key.FacilityId ?? string.Empty,
                    CorrelationId = g.Key.CorrelationId ?? string.Empty,
                    LogIds = g.Select(x => x.Id).ToList(),
                    ResourceAcquired = new ResourceAcquired
                    {
                        PatientId = g.Select(x => x.PatientId).FirstOrDefault() ?? string.Empty,
                        QueryType = g.Select(x => x.QueryPhase.ToString()).FirstOrDefault() ?? string.Empty,
                        ReportableEvent = g.Select(x => x.ReportableEvent).FirstOrDefault() ?? default,
                        AcquisitionComplete = true,
                        ScheduledReports = new List<ScheduledReport>
                        {
                             g.FirstOrDefault().ScheduledReport
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

    /// <summary>
    /// 
    /// </summary>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public async Task<List<DataAcquisitionLog>> GetPendingAndRetryableFailedRequests(CancellationToken cancellationToken = default)
    {
        return await _dbContext.DataAcquisitionLogs
            .AsNoTracking()
            .Where(l => l.Status == RequestStatus.Pending || l.Status == RequestStatus.Failed)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="model"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public async Task<(List<QueryLogSummaryModel> searchResults, int count)> SearchAsync(SearchDataAcquisitionLogRequest model, CancellationToken cancellationToken = default)
    {
        var query = _dbContext.DataAcquisitionLogs.AsNoTracking()
            .Include(x => x.FhirQuery)
            .AsQueryable();

        if (!string.IsNullOrEmpty(model.FacilityId))
        {
            query = query.Where(log => log.FacilityId == model.FacilityId);
        }

        if (!string.IsNullOrEmpty(model.PatientId))
        {
            query = query.Where(log => log.PatientId == model.PatientId);
        }

        if (!string.IsNullOrEmpty(model.ReportId))
        {
            query = query.Where(log => log.ReportTrackingId == model.ReportId);
        }

        if (!string.IsNullOrEmpty(model.ResourceId))
        {
            query = query.Where(log => log.ResourceId != null && log.ResourceId == model.ResourceId);
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

        if (model.RequestStatus.HasValue)
        {
            query = query.Where(log => log.Status == model.RequestStatus.Value);
        }

        var totalRecords = await query.CountAsync(cancellationToken);

        query = model.SortOrder switch
        {
            SortOrder.Ascending => query.OrderBy(SetSortBy<DataAcquisitionLog>(model.SortBy)),
            SortOrder.Descending => query.OrderByDescending(SetSortBy<DataAcquisitionLog>(model.SortBy)),
            _ => query
        };

        var logs = await query
            .Skip((model.PageNumber - 1) * model.PageSize)
            .Take(model.PageSize)
            .Select(log => QueryLogSummaryModel.FromDomain(log))
            .ToListAsync(cancellationToken);

        return (logs, totalRecords);

    }

    private Expression<Func<T, object>> SetSortBy<T>(string? sortBy)
    {
        var sortKey = sortBy?.ToLower() ?? "";
        var parameter = Expression.Parameter(typeof(T), "p");
        var sortExpression = Expression.Lambda<Func<T, object>>(Expression.Convert(Expression.Property(parameter, sortKey), typeof(object)), parameter);

        return sortExpression;
    }

    public async Task<DataAcquisitionLog?> GetDataAcquisitionLogAsync(long logId, CancellationToken cancellationToken = default)
    {
        var log = await _dbContext.DataAcquisitionLogs.AsNoTracking()
            .Include(x => x.FhirQuery)
            .Include(x => x.ReferenceResources)
            .SingleOrDefaultAsync(x => x.Id == logId, cancellationToken);

        return log;
    }

    public async Task<DataAcquisitionLogStatistics> GetDataAcquisitionLogStatisticsByReportAsync(string reportId, CancellationToken cancellationToken = default)
    {
        var logs = await _dbContext.DataAcquisitionLogs.AsNoTracking()
                .Include(i => i.FhirQuery)
                .Include(i => i.ReferenceResources)
            .Where(log => log.ReportTrackingId == reportId)
            .ToListAsync(cancellationToken);

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
                string.Join(",", fastestLog.FhirQuery.SelectMany(x => x.ResourceTypes)),
                fastestLog.CompletionTimeMilliseconds.Value);
        }

        var slowestLog = logs.OrderByDescending(log => log.CompletionTimeMilliseconds).FirstOrDefault();
        if (slowestLog is { CompletionTimeMilliseconds: not null })
        {
            statistics.SlowestCompletionTimeMilliseconds = new ResourceCompletionTime(
                string.Join(",", slowestLog.FhirQuery.SelectMany(x => x.ResourceTypes)),
                slowestLog.CompletionTimeMilliseconds.Value);
        }


        // Populate counts
        foreach (var log in logs)
        {
            // Process Query Type
            if (log.QueryType.HasValue)
            {
                var queryType = (FhirQueryType)log.QueryType;
                if (!statistics.QueryTypeCounts.TryGetValue(queryType, out var value))
                {
                    value = 0;
                    statistics.QueryTypeCounts[queryType] = value;
                }
                statistics.QueryTypeCounts[queryType] = ++value;
            }

            // Process Query Phase
            if (log.QueryPhase.HasValue)
            {
                if (!statistics.QueryPhaseCounts.TryGetValue(log.QueryPhase.Value, out var value))
                {
                    value = 0;
                    statistics.QueryPhaseCounts[log.QueryPhase.Value] = value;
                }
                statistics.QueryPhaseCounts[log.QueryPhase.Value] = ++value;
            }

            // Process Request Status
            if (log.Status.HasValue)
            {
                if (!statistics.RequestStatusCounts.TryGetValue(log.Status.Value, out var value))
                {
                    value = 0;
                    statistics.RequestStatusCounts[log.Status.Value] = value;
                }
                statistics.RequestStatusCounts[log.Status.Value] = ++value;
            }

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
                if (!statistics.ResourceTypeCounts.TryGetValue(resourceType, out var value))
                {
                    value = 0;
                    statistics.ResourceTypeCounts[resourceType] = value;
                }
                statistics.ResourceTypeCounts[resourceType] = ++value;
            }

            // Add completion time for this resource types
            if (!log.CompletionTimeMilliseconds.HasValue) continue;

            var resourceTypes = log.FhirQuery.SelectMany(x => x.ResourceTypes).ToList();

            var combinedResourceTypes = string.Join(",", resourceTypes);
            if (!statistics.ResourceTypeCompletionTimeMilliseconds.TryGetValue(combinedResourceTypes, out var totalCompletionTime))
            {
                totalCompletionTime = 0;
                statistics.ResourceTypeCompletionTimeMilliseconds[combinedResourceTypes] = totalCompletionTime;
            }
            statistics.ResourceTypeCompletionTimeMilliseconds[combinedResourceTypes] += log.CompletionTimeMilliseconds.Value;

        }

        return statistics;
    }

    public async Task<bool> CheckIfReferenceResourceHasBeenSent(string referenceId, string reportTrackingId, string facilityId, string correlationId, CancellationToken cancellationToken = default)
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

    public async Task<List<string>> GetFacilitiesWithPendingAndRetryableFailedRequests(CancellationToken cancellationToken = default)
    {
        return await _dbContext.DataAcquisitionLogs
            .Where(l => l.Status == RequestStatus.Pending || l.Status == RequestStatus.Failed)
            .Select(l => l.FacilityId)
            .Distinct()
            .ToListAsync(cancellationToken);
    }

    public async Task<List<DataAcquisitionLog>> GetNextEligibleBatchForFacility(string facilityId, long? lastId, int batchSize, CancellationToken cancellationToken = default)
    {
        var query = _dbContext.DataAcquisitionLogs
            .AsNoTracking()
            .OrderBy(l => l.Id)
            .Where(l => l.FacilityId == facilityId
                        && (lastId == null || l.Id > lastId)
                        && (l.Status == RequestStatus.Pending || l.Status == RequestStatus.Failed));

        return await query
            .OrderBy(l => l.Id)
            .Take(batchSize)
            .ToListAsync(cancellationToken);
    }
}