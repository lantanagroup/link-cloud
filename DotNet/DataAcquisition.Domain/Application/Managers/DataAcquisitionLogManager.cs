using LantanaGroup.Link.DataAcquisition.Domain.Application.Models;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Exceptions;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Entities;
using LantanaGroup.Link.Shared.Application.Enums;
using LantanaGroup.Link.Shared.Application.Interfaces.Models;
using LinqKit;
using Microsoft.Extensions.Logging;
using System.Linq.Expressions;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models.Enums;

namespace LantanaGroup.Link.DataAcquisition.Domain.Application.Managers;

public class SearchDataAcquisitionLogRequest
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 10;
    public string SortBy { get; set; }
    public SortOrder SortOrder { get; set; } = SortOrder.Ascending;
    public string FacilityId { get; set; }
    public string? PatientId { get; set; }
    public string? ResourceId { get; set; }
    public QueryPhaseModel QueryPhaseModel { get; set; }        
    public AcquisitionPriorityModel AcquisitionPriorityModel { get; set; }
    public RequestStatusModel RequestStatusModel { get; set; }
}

public interface IDataAcquisitionLogManager
{
    // Define methods for managing data acquisition logs
    Task<DataAcquisitionLog> CreateAsync(DataAcquisitionLog log, CancellationToken cancellationToken = default);
    Task<QueryLogSummaryModel?> UpdateAsync(UpdateDataAcquisitionLogModel updateLog, CancellationToken cancellationToken = default);
    Task<DataAcquisitionLog?> UpdateAsync(DataAcquisitionLog log, CancellationToken cancellationToken = default);
    Task<DataAcquisitionLog?> UpdateLogStatusAsync(string logId, RequestStatus status, CancellationToken cancellationToken = default);
    Task<DataAcquisitionLog?> GetAsync(string id, CancellationToken cancellationToken = default);
    Task<DataAcquisitionLogModel?> GetModelAsync(string id, CancellationToken cancellationToken = default);
    Task<QueryLogSummaryModel> GetQuerySummaryLog(string id, CancellationToken cancellationToken = default);
    Task DeleteAsync(string id, CancellationToken cancellationToken = default);
    Task<IPagedModel<QueryLogSummaryModel>> GetByFacilityIdAsync(string facilityId, int page, int pageSize, string sortBy, SortOrder sortOrder, CancellationToken cancellationToken = default);
    Task<IPagedModel<QueryLogSummaryModel>> SearchAsync(SearchDataAcquisitionLogRequest request, CancellationToken cancellationToken = default);
    Task<List<DataAcquisitionLog>> GetPendingRequests(CancellationToken cancellationToken = default);
    Task UpdateTailFlagForFacilityCorrelationIdReportTrackingId(List<string> logIds, string facilityId, string correlationId, string reportTrackingId, CancellationToken cancellationToken = default);
}

public class DataAcquisitionLogManager : IDataAcquisitionLogManager
{
    public readonly ILogger<DataAcquisitionLogManager> _logger;
    public readonly IDatabase _database;

    public DataAcquisitionLogManager(ILogger<DataAcquisitionLogManager> logger, IDatabase database)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _database = database ?? throw new ArgumentNullException(nameof(database));
    }

    public async Task<DataAcquisitionLog> CreateAsync(DataAcquisitionLog log, CancellationToken cancellationToken = default)
    {
        if (log == null)
        {
            throw new ArgumentNullException(nameof(log));
        }

        log.Id = Guid.NewGuid().ToString();
        log.CreateDate = DateTime.UtcNow;
        log.ModifyDate = DateTime.UtcNow;

        foreach(var q in log.FhirQuery)
        {
            q.Id = Guid.NewGuid().ToString();
            q.DataAcquisitionLogId = log.Id;
            q.CreateDate = DateTime.UtcNow;
            q.ModifyDate = DateTime.UtcNow;

            foreach(var r in q.ResourceReferenceTypes)
            {
                r.Id = Guid.NewGuid().ToString();
                r.FhirQueryId = q.Id;
                r.CreateDate = DateTime.UtcNow;
                r.ModifyDate = DateTime.UtcNow;
            }
        }

        await _database.DataAcquisitionLogRepository.AddAsync(log);
        await _database.DataAcquisitionLogRepository.SaveChangesAsync();

        return log;
    }

    public async Task DeleteAsync(string id, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            throw new ArgumentNullException(nameof(id));
        }

        var log = await _database.DataAcquisitionLogRepository.GetAsync(id);

        if (log == null) 
        {
            throw new NotFoundException($"No log found for id: {id}");
        }

        _database.DataAcquisitionLogRepository.Remove(log);
        await _database.DataAcquisitionLogRepository.SaveChangesAsync();
    }

    public async Task<DataAcquisitionLog?> GetAsync(string id, CancellationToken cancellationToken = default)
    {
        return await _database.DataAcquisitionLogRepository.GetAsync(id);
    }

    public async Task<DataAcquisitionLogModel?> GetModelAsync(string id, CancellationToken cancellationToken = default)
    {
        var log = await _database.DataAcquisitionLogRepository.GetAsync(id);

        if (log == null) 
        {
            throw new NotFoundException($"No log found for id: {id}");
        }

        return DataAcquisitionLogModel.FromDomain(log);
    }

    public async Task<QueryLogSummaryModel> GetQuerySummaryLog(string id, CancellationToken cancellationToken = default)
    {
        var log = await _database.DataAcquisitionLogRepository.GetAsync(id);

        if (log == null) 
        {
            throw new NotFoundException($"No log found for id: {id}");
        }

        return QueryLogSummaryModel.FromDomain(log);
    }

    public async Task<IPagedModel<QueryLogSummaryModel>> GetByFacilityIdAsync(string facilityId, int page, int pageSize, string sortBy, SortOrder sortOrder, CancellationToken cancellationToken = default)
    {
        var result = await _database.DataAcquisitionLogRepository.SearchAsync(x => x.FacilityId.ToUpper() == facilityId.ToUpper(), sortBy, sortOrder, page, pageSize);
        
        return new QueryLogSummaryModelResponse
        {
            Records = result.Item1.Select(QueryLogSummaryModel.FromDomain).ToList(),
            Metadata = result.Item2
        };
    }

    public async Task<IPagedModel<QueryLogSummaryModel>> SearchAsync(SearchDataAcquisitionLogRequest request, CancellationToken cancellationToken = default)
    {
        if(request == null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        if (string.IsNullOrEmpty(request.FacilityId))
        {
            throw new ArgumentException("FacilityId must be provided.");
        }

        if(string.IsNullOrWhiteSpace(request.PatientId) && string.IsNullOrWhiteSpace(request.ResourceId))
        {
            throw new ArgumentException("Either PatientId or ResourceId must be provided.");
        }

        Expression<Func<DataAcquisitionLog, bool>> predicate = x => true;

        if (!string.IsNullOrEmpty(request.PatientId))
        {
            predicate = predicate.And(x => x.PatientId.ToLower() == request.PatientId.ToLower());
        } else
        {
            predicate = predicate.And(x => x.ResourceId.ToLower() == request.ResourceId.ToLower());
        }
        
        if (!string.IsNullOrEmpty(request.FacilityId))
        {
            predicate = predicate.And(x => x.FacilityId.ToLower() == request.FacilityId.ToLower());
        }

        if (request.QueryPhaseModel != default)
        {
            predicate = predicate.And(x => x.QueryPhase == QueryPhaseModelUtilities.ToDomain(request.QueryPhaseModel));
        }

        if (request.AcquisitionPriorityModel != default)
        {
            predicate = predicate.And(x => x.Priority == AcquisitionPriorityModelUtilities.ToDomain(request.AcquisitionPriorityModel));
        }

        if (request.RequestStatusModel != default)
        {
            predicate = predicate.And(x => x.Status == RequestStatusModelUtilities.ToDomain(request.RequestStatusModel));
        }

        var result = await _database.DataAcquisitionLogRepository.SearchAsync(predicate, request.SortBy, request.SortOrder, request.Page, request.PageSize);
        return new QueryLogSummaryModelResponse
        {
            Records = result.Item1.Select(QueryLogSummaryModel.FromDomain).ToList(),
            Metadata = result.Item2
        };
    }

    public async Task<DataAcquisitionLog?> UpdateAsync(DataAcquisitionLog log, CancellationToken cancellationToken = default)
    {
        if (log == null)
        {
            throw new ArgumentNullException(nameof(log));
        }

        var existingLog = await _database.DataAcquisitionLogRepository.GetAsync(log.Id);

        if (existingLog == null)
        {
            throw new DataAcquisitionLogNotFoundException($"Data acquisition log with ID {log.Id} not found.");
        }

        existingLog.Status = log.Status;
        existingLog.ExecutionDate = log.ExecutionDate;
        existingLog.CompletionDate = log.CompletionDate;
        existingLog.CompletionTimeMilliseconds = log.CompletionTimeMilliseconds;
        existingLog.Notes = log.Notes;

        existingLog.ModifyDate = DateTime.UtcNow;

        await _database.DataAcquisitionLogRepository.SaveChangesAsync();

        return existingLog;
    }

    public async Task<DataAcquisitionLog?> UpdateLogStatusAsync(string logId, RequestStatus status, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(logId))
        {
            throw new ArgumentNullException(nameof(logId), "Log ID cannot be null or empty.");
        }

        var log = await _database.DataAcquisitionLogRepository.GetAsync(logId);
        if (log == null)
        {
            throw new DataAcquisitionLogNotFoundException($"Data acquisition log with ID {logId} not found.");
        }

        log.Status = status;
        log.ModifyDate = DateTime.UtcNow;
        await _database.DataAcquisitionLogRepository.SaveChangesAsync();
        return log;
    }

    public async Task<QueryLogSummaryModel?> UpdateAsync(UpdateDataAcquisitionLogModel updateLog, CancellationToken cancellationToken = default)
    {
        if (updateLog == null)
        {
            throw new ArgumentNullException(nameof(updateLog));
        }

        if(string.IsNullOrWhiteSpace(updateLog.Id))
        {
            throw new ArgumentNullException(nameof(updateLog.Id), "Log ID cannot be null or empty.");
        }

        var existingLog = await _database.DataAcquisitionLogRepository.GetAsync(updateLog.Id);

        if (existingLog == null)
        {
            throw new DataAcquisitionLogNotFoundException($"Data acquisition log with ID {updateLog.Id} not found.");
        }

        existingLog.Status = RequestStatusModelUtilities.ToDomain(updateLog.Status.Value);
        existingLog.ExecutionDate = updateLog.ScheduledExecutionDate != default ? updateLog.ScheduledExecutionDate : existingLog.ExecutionDate;

        existingLog.ModifyDate = DateTime.UtcNow;

        await _database.DataAcquisitionLogRepository.SaveChangesAsync();

        return QueryLogSummaryModel.FromDomain(existingLog);
    }

    public async Task<List<DataAcquisitionLog>> GetPendingRequests(CancellationToken cancellationToken = default)
    {
        var resultSet = await _database.DataAcquisitionLogRepository.FindAsync(x => x.Status != null && x.Status == RequestStatus.Pending && x.ExecutionDate <= DateTime.UtcNow && x.CompletionDate == null);
        return resultSet.OrderBy(x => x.Priority).ToList();
    }

    public async Task UpdateTailFlagForFacilityCorrelationIdReportTrackingId(List<string> logIds, string facilityId, string correlationId, string reportTrackingId, CancellationToken cancellationToken = default)
    {
        foreach (var logId in logIds)
        {
            var entity = await _database.DataAcquisitionLogRepository.GetAsync(logId);

            if (entity == null)
            {
                throw new NotFoundException($"Data acquisition log with ID {logId} not found.");
            }

            entity.TailSent = true;
            await _database.DataAcquisitionLogRepository.SaveChangesAsync();
        }
    }
}
