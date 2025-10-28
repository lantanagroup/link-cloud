using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Entities;
using LantanaGroup.Link.Shared.Application.Enums;
using Microsoft.Extensions.Logging;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Api.QueryLog;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Api.Requests;
﻿using LantanaGroup.Link.DataAcquisition.Domain.Application.Models;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Exceptions;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Queries;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Entities;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models.Enums;
using LantanaGroup.Link.Shared.Application.Enums;
using LantanaGroup.Link.Shared.Application.Interfaces.Models;
using LantanaGroup.Link.Shared.Application.Models.Responses;
using Microsoft.Extensions.Logging;

namespace LantanaGroup.Link.DataAcquisition.Domain.Application.Managers;

public interface IDataAcquisitionLogManager
{
    // Define methods for managing data acquisition logs
    Task<DataAcquisitionLog> CreateAsync(DataAcquisitionLog log, CancellationToken cancellationToken = default);
    Task<QueryLogSummaryModel?> UpdateAsync(UpdateDataAcquisitionLogModel updateLog, CancellationToken cancellationToken = default);
    Task<DataAcquisitionLog?> UpdateAsync(DataAcquisitionLog log, CancellationToken cancellationToken = default);
    Task<DataAcquisitionLog?> UpdateLogStatusAsync(long logId, RequestStatus status, CancellationToken cancellationToken = default);
    Task<DataAcquisitionLog?> GetAsync(long id, CancellationToken cancellationToken = default);
    Task<DataAcquisitionLogModel?> GetModelAsync(long id, CancellationToken cancellationToken = default);
    Task<QueryLogSummaryModel> GetQuerySummaryLog(long id, CancellationToken cancellationToken = default);
    Task DeleteAsync(long id, CancellationToken cancellationToken = default);
    Task<IPagedModel<QueryLogSummaryModel>> GetByFacilityIdAsync(string facilityId, int page, int pageSize, string sortBy, SortOrder sortOrder, CancellationToken cancellationToken = default);
    Task<IPagedModel<QueryLogSummaryModel>> SearchAsync(SearchDataAcquisitionLogRequest request, CancellationToken cancellationToken = default);
    Task<List<DataAcquisitionLog>> GetPendingRequests(CancellationToken cancellationToken = default);
    Task<DataAcquisitionLogStatistics> GetStatisticsByReportAsync(string reportId, CancellationToken cancellationToken = default);
    Task UpdateTailFlagForFacilityCorrelationIdReportTrackingId(List<long> logIds, string facilityId, string correlationId, string reportTrackingId, CancellationToken cancellationToken = default);
}

public class DataAcquisitionLogManager : IDataAcquisitionLogManager
{
    public readonly ILogger<DataAcquisitionLogManager> _logger;
    public readonly IDatabase _database;
    public readonly IDataAcquisitionLogQueries _LogQueries;

    public DataAcquisitionLogManager(ILogger<DataAcquisitionLogManager> logger, IDatabase database, IDataAcquisitionLogQueries logQueries)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _database = database ?? throw new ArgumentNullException(nameof(database));
        _LogQueries = logQueries ?? throw new ArgumentNullException(nameof(logQueries));
    }

    public async Task<DataAcquisitionLog> CreateAsync(DataAcquisitionLog log, CancellationToken cancellationToken = default)
    {
        if (log == null)
        {
            throw new ArgumentNullException(nameof(log));
        }

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

    public async Task DeleteAsync(long id, CancellationToken cancellationToken = default)
    {
        if (id == default)
        {
            throw new InvalidOperationException(nameof(id));
        }

        var log = await _database.DataAcquisitionLogRepository.GetAsync(id);

        if (log == null) 
        {
            throw new NotFoundException($"No log found for id: {id}");
        }

        _database.DataAcquisitionLogRepository.Remove(log);
        await _database.DataAcquisitionLogRepository.SaveChangesAsync();
    }

    public async Task<DataAcquisitionLog?> GetAsync(long id, CancellationToken cancellationToken = default)
    {
        return await _database.DataAcquisitionLogRepository.GetAsync(id);
    }

    public async Task<DataAcquisitionLogModel?> GetModelAsync(long id, CancellationToken cancellationToken = default)
    {
        var log = await _LogQueries.GetDataAcquisitionLogAsync(id, cancellationToken);

        if (log == null) 
        {
            throw new NotFoundException($"No log found for id: {id}");
        }

        return DataAcquisitionLogModel.FromDomain(log);
    }

    public async Task<QueryLogSummaryModel> GetQuerySummaryLog(long id, CancellationToken cancellationToken = default)
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
        var result = await _database.DataAcquisitionLogRepository.SearchAsync(x => x.FacilityId.ToUpper() == facilityId.ToUpper(), sortBy, sortOrder, pageSize, page);
        
        return new QueryLogSummaryModelResponse
        {
            Records = result.Item1.Select(QueryLogSummaryModel.FromDomain).ToList(),
            Metadata = result.Item2
        };
    }

    public async Task<IPagedModel<QueryLogSummaryModel>> SearchAsync(SearchDataAcquisitionLogRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var result = await _LogQueries.SearchAsync(request, cancellationToken);
        return new QueryLogSummaryModelResponse
        {
            Records = result.searchResults,
            Metadata = new PaginationMetadata(request.PageSize, request.PageNumber, result.count)
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
        existingLog.RetryAttempts = log.RetryAttempts;
        existingLog.ExecutionDate = log.ExecutionDate;
        existingLog.CompletionDate = log.CompletionDate;
        existingLog.CompletionTimeMilliseconds = log.CompletionTimeMilliseconds;
        existingLog.Notes = log.Notes;

        existingLog.ModifyDate = DateTime.UtcNow;

        await _database.DataAcquisitionLogRepository.SaveChangesAsync();

        return existingLog;
    }

    public async Task<DataAcquisitionLog?> UpdateLogStatusAsync(long logId, RequestStatus status, CancellationToken cancellationToken = default)
    {
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

        if(updateLog.Id == default)
        {
            throw new InvalidOperationException("Log ID cannot be zero or null");
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

    public Task<DataAcquisitionLogStatistics> GetStatisticsByReportAsync(string reportId, CancellationToken cancellationToken = default)
    {
        if (!string.IsNullOrEmpty(reportId))
        {
            return _LogQueries.GetDataAcquisitionLogStatisticsByReportAsync(reportId, cancellationToken);
        }

        throw new ArgumentNullException(nameof(reportId), "Report ID cannot be null or empty.");
    }

    public async Task UpdateTailFlagForFacilityCorrelationIdReportTrackingId(List<long> logIds, string facilityId, string correlationId, string reportTrackingId, CancellationToken cancellationToken = default)
    {
        foreach (var logId in logIds)
        {
            var entity = await _database.DataAcquisitionLogRepository.GetAsync(logId);

            if (entity == null)
            {
                throw new NotFoundException($"Data acquisition log with ID {logId} not found.");
            }

            entity.TailSent = true;
            entity.ModifyDate = DateTime.UtcNow;
            entity.Notes ??= new();
            entity.Notes.Add("Tail Message Sent");
            await _database.DataAcquisitionLogRepository.SaveChangesAsync();
        }
    }
}
