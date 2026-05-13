using DataAcquisition.Domain.Application.Models;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Api.QueryLog;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Exceptions;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Queries;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Entities;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models.Enums;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Context;
using LantanaGroup.Link.DataAcquisition.Domain.Models;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Models.Telemetry;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace LantanaGroup.Link.DataAcquisition.Domain.Application.Managers;

public interface IDataAcquisitionLogManager
{
    Task<DataAcquisitionLogModel> CreateAsync(CreateDataAcquisitionLogModel log, CancellationToken cancellationToken = default);
    Task<DataAcquisitionLogModel?> UpdateAsync(UpdateDataAcquisitionLogModel updateLog, CancellationToken cancellationToken = default);
    Task<int> UpdateStatusBatchAsync(IEnumerable<long> ids, RequestStatus newStatus, CancellationToken cancellationToken = default);
    Task<List<DataAcquisitionLog>> GetLogsByIdsAsync(List<long> ids, CancellationToken cancellationToken = default);
    Task DeleteAsync(long id, CancellationToken cancellationToken = default);
    Task UpdateTailFlagForFacilityCorrelationIdReportTrackingId(List<long> logIds, string facilityId, string correlationId, string reportTrackingId, CancellationToken cancellationToken = default);
    Task ThrottleFacilityAcquisitions(string facilityId, DateTime executionDate, CancellationToken cancellationToken = default);
}

public class DataAcquisitionLogManager : IDataAcquisitionLogManager
{
    public readonly ILogger<DataAcquisitionLogManager> _logger;
    public readonly IDatabase _database;
    private readonly DataAcquisitionDbContext _dbContext;
    private readonly IDataAcquisitionLogQueries _logQueries;

    public DataAcquisitionLogManager(ILogger<DataAcquisitionLogManager> logger, IDatabase database, DataAcquisitionDbContext dbContext, IDataAcquisitionLogQueries logQueries)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _database = database ?? throw new ArgumentNullException(nameof(database));
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _logQueries = logQueries;
    }

    public async Task<DataAcquisitionLogModel> CreateAsync(CreateDataAcquisitionLogModel model, CancellationToken cancellationToken = default)
    {
        using var activity = ServiceActivitySource.Instance.StartActivity("DataAcquisitionLogManager.CreateAsync");
        activity?.SetTag(DiagnosticNames.FacilityId, model.FacilityId);
        activity?.SetTag(DiagnosticNames.CorrelationId, model.CorrelationId);

        if(model.ScheduledReport == null)
        {
            throw new ArgumentNullException("Required property ScheduledReport must not be null");
        }

        var log = new DataAcquisitionLog
        {
            Status = model.Status,
            FacilityId = model.FacilityId,
            QueryPhase = model.QueryPhase,
            FhirVersion = model.FhirVersion,
            QueryType = model.QueryType,
            ResourceAcquiredIds = model.ResourceAcquiredIds,
            FhirQueries = model.FhirQuery.Select(q => new FhirQuery
            {
                FacilityId = model.FacilityId,
                IdQueryParameterValues = q.IdQueryParameterValues,
                IsReference = q.IsReference,
                MeasureId = q.MeasureId,
                QueryParameters = q.QueryParameters,
                Paged = q.Paged,
                QueryType = q.QueryType,
                CensusPatientStatus = q.CensusPatientStatus,
                CensusTimeFrame = q.CensusTimeFrame,
                CensusListId = q.CensusListId,
                FhirQueryResourceTypes = q.ResourceTypes.Select(r => new FhirQueryResourceType
                {
                    ResourceType = r,
                }).ToList(),
                ResourceReferenceTypes = q.ResourceReferenceTypes.Select(r => new ResourceReferenceType
                {
                    FacilityId = model.FacilityId,
                    QueryPhase = r.QueryPhase,
                    ResourceType = r.ResourceType,
                }).ToList()
            }).ToList(),
            ScheduledReport = model.ScheduledReport,
            CompletionDate = null,
            CompletionTimeMilliseconds = null,
            ReportTrackingId = model.ScheduledReport.ReportTrackingId,
            ReportStartDate = model.ScheduledReport.StartDate,
            ReportEndDate = model.ScheduledReport.EndDate,
            ExecutionDate = model.ExecutionDate,
            CorrelationId = model.CorrelationId,
            TraceId = model.TraceId,
            Notes = model.Notes,
            Priority = model.Priority,
            TailSent = false,
            PatientId = model.PatientId,
            ReportableEvent = model.ReportableEvent,
            RetryAttempts = 0,
            IsCensus = model.IsCensus,
            CreateDate = DateTime.UtcNow,
            ModifyDate = DateTime.UtcNow,
        };

        await _database.DataAcquisitionLogRepository.AddAsync(log);
        await _database.SaveChangesAsync();

        return DataAcquisitionLogModel.FromDomain(log);
    }

    public async Task DeleteAsync(long id, CancellationToken cancellationToken = default)
    {
        using var activity = ServiceActivitySource.Instance.StartActivity("DataAcquisitionLogManager.DeleteAsync");
        activity?.SetTag(DiagnosticNames.ReportId, id);

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


    public async Task<DataAcquisitionLogModel?> UpdateAsync(UpdateDataAcquisitionLogModel updateLog, CancellationToken cancellationToken = default)
    {
        using var activity = ServiceActivitySource.Instance.StartActivity("DataAcquisitionLogManager.UpdateAsync");
        activity?.SetTag(DiagnosticNames.ReportId, updateLog.Id);

        if (updateLog.Id is null or 0)
        {
            activity?.SetStatus(ActivityStatusCode.Error, "Log ID cannot be zero or null");
            throw new InvalidOperationException("Log ID cannot be zero or null");
        }

        var existingLog = await _database.DataAcquisitionLogRepository.GetAsync(updateLog.Id, cancellationToken);

        if (existingLog is null)
        {
            activity?.SetStatus(ActivityStatusCode.Error, "Data acquisition log not found");
            throw new DataAcquisitionLogNotFoundException($"Data acquisition log with ID {updateLog.Id} not found.");
        }

        if(updateLog.RetryAttempts is not null)
        {
            existingLog.RetryAttempts = updateLog.RetryAttempts;
        }

        if (updateLog.ResourceAcquiredIds is not null && updateLog.ResourceAcquiredIds.Count > 0)
        {
            existingLog.ResourceAcquiredIds = updateLog.ResourceAcquiredIds;
        }

        if (updateLog.TraceId is not null)
        {
            existingLog.TraceId = updateLog.TraceId;
        }

        if (updateLog.ExecutionDate is not null)
        {
            existingLog.ExecutionDate = updateLog.ExecutionDate;
        }

        if (updateLog.CompletionDate is not null)
        {
            existingLog.CompletionDate = updateLog.CompletionDate;
        }

        if (updateLog.CompletionTimeMilliseconds is not null)
        {
            existingLog.CompletionTimeMilliseconds = updateLog.CompletionTimeMilliseconds;
            activity?.SetTag(DiagnosticNames.Duration, updateLog.CompletionTimeMilliseconds);
        }

        if (updateLog.Notes is not null)
        {
            existingLog.Notes = updateLog.Notes;
        }

        if (updateLog.Status is not null)
        {
            existingLog.Status = updateLog.Status.Value;
        }

        existingLog.ModifyDate = DateTime.UtcNow;
        _database.DataAcquisitionLogRepository.Update(existingLog);
        await _database.DataAcquisitionLogRepository.SaveChangesAsync(cancellationToken);

        return DataAcquisitionLogModel.FromDomain(existingLog);
    }

    public async Task<List<DataAcquisitionLog>> GetLogsByIdsAsync(List<long> ids, CancellationToken cancellationToken = default)
    {
        return await _database.DataAcquisitionLogRepository.FindAsync(x => ids.Contains(x.Id), cancellationToken);
    }

    public async Task<int> UpdateStatusBatchAsync(IEnumerable<long> ids, RequestStatus newStatus, CancellationToken cancellationToken = default)
    {
        using var activity = ServiceActivitySource.Instance.StartActivity("DataAcquisitionLogManager.UpdateStatusBatchAsync");

        // High-speed bulk update without fetching entities. 
        return await _dbContext.DataAcquisitionLogs
            .Where(l => ids.Contains(l.Id))
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(l => l.Status, newStatus)
                .SetProperty(l => l.ModifyDate, DateTime.UtcNow),
                cancellationToken);
    }

    public async Task<List<DataAcquisitionLog>> GetPendingRequests(CancellationToken cancellationToken = default)
    {
        var resultSet = await _database.DataAcquisitionLogRepository.FindAsync(x => x.Status != null && x.Status == RequestStatus.Pending && x.ExecutionDate <= DateTime.UtcNow && x.CompletionDate == null);
        return resultSet.OrderBy(x => x.Priority).ToList();
    }

    public async Task UpdateTailFlagForFacilityCorrelationIdReportTrackingId(List<long> logIds, string facilityId, string correlationId, string reportTrackingId, CancellationToken cancellationToken = default)
    {
        using var activity = ServiceActivitySource.Instance.StartActivity("DataAcquisitionLogManager.UpdateTailFlagForFacilityCorrelationIdReportTrackingId");
        activity?.SetTag(DiagnosticNames.FacilityId, facilityId);
        activity?.SetTag(DiagnosticNames.CorrelationId, correlationId);
        activity?.SetTag(DiagnosticNames.ReportTrackingId, reportTrackingId);

        if (logIds == null || logIds.Count == 0) return;

        // Use ExecuteUpdateAsync for a high-performance batch update if available on the repository/context
        // Since we are using a generic repository, we might need to fall back to a manual query or range update
        
        var logs = await _database.DataAcquisitionLogRepository.FindAsync(x => logIds.Contains(x.Id), cancellationToken);

        if (logs.Count == 0)
        {
            throw new NotFoundException($"Data acquisition logs with IDs {string.Join(", ", logIds)} not found.");
        }
        
        foreach (var entity in logs)
        {
            entity.TailSent = true;
            entity.ModifyDate = DateTime.UtcNow;
            entity.Notes ??= new();
            entity.Notes.Add("Tail Message Sent");
        }
        
        await _database.DataAcquisitionLogRepository.SaveChangesAsync(cancellationToken);
    }

    public async Task ThrottleFacilityAcquisitions(string facilityId, DateTime executionDate, CancellationToken cancellationToken = default)
    {
        using var activity = ServiceActivitySource.Instance.StartActivity("DataAcquisitionLogManager.ThrottleFacilityAcquisitions");
        activity?.SetTag(DiagnosticNames.FacilityId, facilityId);

        long? lastId = null;
        var batchSize = 1000;
        while (true)
        {
            // Get All Active Logs For Batch
            var toThrottle = await _logQueries.GetNextEligibleBatchForFacility(facilityId, lastId, batchSize, [RequestStatus.Failed, RequestStatus.Ready, RequestStatus.Pending], executionDate, cancellationToken);

            if(toThrottle.Count == 0)
            {
                break;
            }

            //Update their next processing time
            foreach (var log in toThrottle)
            {
                log.ExecutionDate = executionDate;
            }

            await _database.DataAcquisitionLogRepository.SaveChangesAsync(cancellationToken);

            lastId = toThrottle.Max(l => l.Id);
        }
    }
}
