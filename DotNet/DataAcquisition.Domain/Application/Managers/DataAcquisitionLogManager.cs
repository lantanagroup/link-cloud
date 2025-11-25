using DataAcquisition.Domain.Application.Models;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Api.QueryLog;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Exceptions;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Entities;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models.Enums;
using LantanaGroup.Link.DataAcquisition.Domain.Models;
using Microsoft.Extensions.Logging;

namespace LantanaGroup.Link.DataAcquisition.Domain.Application.Managers;

public interface IDataAcquisitionLogManager
{
    Task<DataAcquisitionLogModel> CreateAsync(CreateDataAcquisitionLogModel log, CancellationToken cancellationToken = default);
    Task<DataAcquisitionLogModel?> UpdateAsync(UpdateDataAcquisitionLogModel updateLog, CancellationToken cancellationToken = default);
    Task DeleteAsync(long id, CancellationToken cancellationToken = default);
    Task UpdateTailFlagForFacilityCorrelationIdReportTrackingId(List<long> logIds, string facilityId, string correlationId, string reportTrackingId, CancellationToken cancellationToken = default);
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

    public async Task<DataAcquisitionLogModel> CreateAsync(CreateDataAcquisitionLogModel model, CancellationToken cancellationToken = default)
    {
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

        if(updateLog.RetryAttempts != null)
        {
            existingLog.RetryAttempts = updateLog.RetryAttempts;
        }

        if (updateLog.ResourceAcquiredIds?.Any() ?? false)
        {
            existingLog.ResourceAcquiredIds = updateLog.ResourceAcquiredIds;
        }

        if (updateLog.TraceId != null)
        {
            existingLog.TraceId = updateLog.TraceId;
        }

        if (updateLog.ExecutionDate != null)
        {
            existingLog.ExecutionDate = updateLog.ExecutionDate;
        }

        if (updateLog.CompletionDate != null)
        {
            existingLog.CompletionDate = updateLog.CompletionDate;
        }

        if (updateLog.CompletionTimeMilliseconds != null)
        {
            existingLog.CompletionTimeMilliseconds = updateLog.CompletionTimeMilliseconds;
        }

        if (updateLog.Notes != null)
        {
            existingLog.Notes = updateLog.Notes;
        }

        if (updateLog.Status != null)
        {
            existingLog.Status = updateLog.Status.Value;
        }

        existingLog.ExecutionDate = updateLog.ExecutionDate != default ? updateLog.ExecutionDate : existingLog.ExecutionDate;

        existingLog.ModifyDate = DateTime.UtcNow;

        await _database.DataAcquisitionLogRepository.SaveChangesAsync();

        return DataAcquisitionLogModel.FromDomain(existingLog);
    }

    public async Task<List<DataAcquisitionLog>> GetPendingRequests(CancellationToken cancellationToken = default)
    {
        var resultSet = await _database.DataAcquisitionLogRepository.FindAsync(x => x.Status != null && x.Status == RequestStatus.Pending && x.ExecutionDate <= DateTime.UtcNow && x.CompletionDate == null);
        return resultSet.OrderBy(x => x.Priority).ToList();
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
