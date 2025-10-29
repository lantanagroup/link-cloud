using DataAcquisition.Domain.Application.Models;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Api.Configuration;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Api.QueryLog;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Api.Requests;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Domain;
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
using MongoDB.Driver;
using System.Linq.Expressions;
using System.Reflection;
using Expression = System.Linq.Expressions.Expression;
using IDatabase = LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.IDatabase;
using RequestStatus = LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models.Enums.RequestStatus;

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
    Task<int> GetCountOfNonRefLogsIncompleteAsync(string facilityId, string reportTrackingId, string correlationId, CancellationToken cancellationToken = default);

    Task<IPagedModel<QueryLogSummaryModel>> SearchQueryLogSummaryAsync(SearchDataAcquisitionLogRequest request, CancellationToken cancellationToken = default);

    Task<PagedConfigModel<DataAcquisitionLogModel>> SearchAsync(SearchDataAcquisitionLogRequest model, CancellationToken cancellationToken = default);

    Task<DataAcquisitionLogStatistics> GetDataAcquisitionLogStatisticsByReportAsync(string reportId, CancellationToken cancellationToken = default);

    Task<bool> CheckIfReferenceResourceHasBeenSent(string referenceId, string reportTrackingId, string facilityId, string correlationId, CancellationToken cancellationToken = default);

    Task<List<string>> GetFacilitiesWithPendingAndRetryableFailedRequests(CancellationToken cancellationToken = default);

    Task<List<DataAcquisitionLogModel>> GetNextEligibleBatchForFacility(string facilityId, long? lastId, int batchSize, CancellationToken cancellationToken = default);
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

    public async Task<DataAcquisitionLogModel?> GetAsync(long id, CancellationToken cancellationToken = default)
    {
        var log = await (from l in _dbContext.DataAcquisitionLogs
                         where l.Id == id
                         select new DataAcquisitionLogModel
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
                             FhirQuery = l.FhirQueries != null ? l.FhirQueries.Select(q =>
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
                                 ResourceReferenceTypes = q.ResourceReferenceTypes != null ? q.ResourceReferenceTypes.Select(rt => new ResourceReferenceTypeModel
                                 {
                                     Id = rt.Id,
                                     FacilityId = rt.FacilityId,
                                     QueryPhase = rt.QueryPhase,
                                     ResourceType = rt.ResourceType,
                                     FhirQueryId = rt.FhirQueryId,
                                     CreateDate = rt.CreateDate,
                                     ModifyDate = rt.ModifyDate,
                                 }).ToList() : new()
                             }).ToList() : new(),
                             Status = l.Status,
                             ExecutionDate = l.ExecutionDate,
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
                             ScheduledReport = l.ScheduledReport
                         }).SingleOrDefaultAsync();

        return log;
    }

    public async Task<int> GetCountOfNonRefLogsIncompleteAsync(string facilityId, string reportTrackingId, string correlationId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(facilityId))
            throw new ArgumentNullException(nameof(facilityId), "Facility ID cannot be null or empty.");

        if (string.IsNullOrWhiteSpace(reportTrackingId))
            throw new ArgumentNullException(nameof(reportTrackingId), "Report Tracking ID cannot be null or empty.");

        if (string.IsNullOrWhiteSpace(correlationId))
            throw new ArgumentNullException(nameof(correlationId), "Correlation ID cannot be null or empty.");

        return await (from  l in _dbContext.DataAcquisitionLogs
                      where l.FacilityId == facilityId
                            && l.ReportTrackingId == reportTrackingId
                            && l.CorrelationId == correlationId
                             && l.Status != RequestStatus.Completed
                             && !l.TailSent
                             && l.FhirQueries.Any(fq => fq.IsReference == false)
                      select l).CountAsync();           
    }

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
                    TraceParentId = g.Where(x => x.TraceId != null).OrderBy(x => x.Id).Select(x => x.TraceId).FirstOrDefault() ?? string.Empty,
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

    public async Task<IPagedModel<QueryLogSummaryModel>> SearchQueryLogSummaryAsync(SearchDataAcquisitionLogRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var result = await SearchAsync(request, cancellationToken);
        return new QueryLogSummaryModelResponse
        {
            Records = result.Records.Select(QueryLogSummaryModel.FromDomain).ToList(),
            Metadata = new PaginationMetadata(request.PageSize, request.PageNumber, result.Metadata.TotalCount)
        };
    }

    public async Task<PagedConfigModel<DataAcquisitionLogModel>> SearchAsync(SearchDataAcquisitionLogRequest model, CancellationToken cancellationToken = default)
    {
        var query = _dbContext.DataAcquisitionLogs.AsNoTracking()
            .AsQueryable();

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

        if (model.RequestStatuses != null && model.RequestStatuses.Any())
        {
            query = (from l in query
                     where l.Status != null && model.RequestStatuses.Contains(l.Status.Value)
                     select l);
        }

        if (!string.IsNullOrEmpty(model.ResourceType))
        {
            query = (from l in query
                     join q in _dbContext.FhirQueries on l.Id equals q.DataAcquisitionLogId into qGroup
                     from q in qGroup.DefaultIfEmpty()
                     where q.ResourceReferenceTypes.Any(r => r.ResourceType == model.ResourceType)
                     select l);
        }

        var totalRecords = await query.CountAsync(cancellationToken);

        query = model.SortOrder switch
        {
            SortOrder.Ascending => query.OrderBy(SetSortBy<DataAcquisitionLog>(model.SortBy)),
            SortOrder.Descending => query.OrderByDescending(SetSortBy<DataAcquisitionLog>(model.SortBy)),
            _ => query
        };

        var total = await query.CountAsync();

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
                FhirQuery = l.FhirQueries != null ? l.FhirQueries.Select(q =>
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
                    ResourceReferenceTypes = q.ResourceReferenceTypes != null ? q.ResourceReferenceTypes.Select(rt => new ResourceReferenceTypeModel
                    {
                        Id = rt.Id,
                        FacilityId = rt.FacilityId,
                        QueryPhase = rt.QueryPhase,
                        ResourceType = rt.ResourceType,
                        FhirQueryId = rt.FhirQueryId,
                        CreateDate = rt.CreateDate,
                        ModifyDate = rt.ModifyDate,
                    }).ToList() : new()
                }).ToList() : new(),
                Status = l.Status,
                ExecutionDate = l.ExecutionDate,
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
                ScheduledReport = l.ScheduledReport
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


    public async Task<DataAcquisitionLogStatistics> GetDataAcquisitionLogStatisticsByReportAsync(string reportId, CancellationToken cancellationToken = default)
    {
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

    public async Task<List<DataAcquisitionLogModel>> GetNextEligibleBatchForFacility(string facilityId, long? lastId, int batchSize, CancellationToken cancellationToken = default)
    {
        var query = from log in _dbContext.DataAcquisitionLogs
                    orderby log.Id
                    where log.FacilityId == facilityId
                        && (lastId == null || log.Id > lastId)
                        && (log.Status == RequestStatus.Pending || log.Status == RequestStatus.Failed)
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

    private Expression<Func<T, object>> SetSortBy<T>(string? sortBy)
    {
        var type = typeof(T);
        var inputSortBy = sortBy?.Trim();
        string sortKey = "Id"; // default

        if (!string.IsNullOrEmpty(inputSortBy))
        {
            var prop = type.GetProperty(inputSortBy, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
            if (prop != null)
            {
                sortKey = prop.Name;
            }
        }

        var parameter = Expression.Parameter(type, "p");
        var property = Expression.Property(parameter, sortKey);
        var converted = Expression.Convert(property, typeof(object));
        return Expression.Lambda<Func<T, object>>(converted, parameter);
    }
}