using Confluent.Kafka;
using DataAcquisition.Domain.Entities;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Managers;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Exceptions;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Kafka;
using LantanaGroup.Link.DataAcquisition.Domain.Models.Enums;
using LantanaGroup.Link.Shared.Application.Enums;
using LantanaGroup.Link.Shared.Application.Interfaces.Models;
using LantanaGroup.Link.Shared.Application.Models;
using LinqKit;
using Microsoft.Extensions.Logging;
using System.Linq.Expressions;

namespace LantanaGroup.Link.DataAcquisition.Domain.Application.Services;

public interface IDataAcquisitionLogService
{
    Task<DataAcquisitionLogModel> GetLogEntryById(string id, CancellationToken cancellationToken = default);
    Task<IPagedModel<QueryLogSummaryModel>> GetQueryLogSummariesForFacility(string facilityId, int page, int pageSize, string sortBy, SortOrder sortOrder, CancellationToken cancellationToken = default);
    Task<IPagedModel<QueryLogSummaryModel>> GetQueryLogSummariesByFacilityAndPatient(string facilityId, string patientId, int page, int pageSize, string sortBy, SortOrder sortOrder, CancellationToken cancellationToken = default);
    Task<QueryLogSummaryModel> UpdateLogEntry(string id, UpdateDataAcquisitionLogModel updateLog, CancellationToken cancellationToken);
    Task<IPagedModel<QueryLogSummaryModel>> Search(QueryPhaseModel? queryPhase, RequestStatusModel? status, AcquisitionPriorityModel? priority, int page, int pageSize, string sortBy, SortOrder sortOrder, string? patientId = default, string? facilityId = default, CancellationToken cancellationToken = default);
    Task<IPagedModel<QueryLogSummaryModel>> Search(int page, int pageSize, string sortBy, SortOrder sortOrder, string? patientId = default, string? facilityId = default, CancellationToken cancellationToken = default);
    Task DeleteLogEntry(string id, CancellationToken cancellationToken);
    Task<bool> StartRetrievalProcess(string logId, CancellationToken cancellationToken = default);
}

public class DataAcquisitionLogService : IDataAcquisitionLogService
{
    private readonly ILogger<DataAcquisitionLogService> _logger;
    private readonly IDataAcquisitionLogManager _dataAcquisitionLogManager;
    IProducer<string, ReadyToAcquire> _readyToAcquireProducer;

    public DataAcquisitionLogService(ILogger<DataAcquisitionLogService> logger, IDataAcquisitionLogManager dataAcquisitionLogManager, IProducer<string, ReadyToAcquire> readyToAcquireProducer)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _dataAcquisitionLogManager = dataAcquisitionLogManager ?? throw new ArgumentNullException(nameof(_dataAcquisitionLogManager));
        _readyToAcquireProducer = readyToAcquireProducer ?? throw new ArgumentNullException(nameof(readyToAcquireProducer));
    }

    public async Task<DataAcquisitionLogModel> GetLogEntryById(string id, CancellationToken cancellationToken = default)
    {
        return DataAcquisitionLogModel.FromDomain(await _dataAcquisitionLogManager.GetAsync(id, cancellationToken));
    }

    public async Task<IPagedModel<QueryLogSummaryModel>> GetQueryLogSummariesForFacility(string facilityId, int page, int pageSize, string sortBy, SortOrder sortOrder, CancellationToken cancellationToken = default)
    {
        var result = await _dataAcquisitionLogManager.GetByFacilityIdAsync(facilityId, page, pageSize, sortBy, sortOrder, cancellationToken);
        return new QueryLogSummaryModelResponse
        {
            Records = result.Item1.Select(QueryLogSummaryModel.FromDomain).ToList(),
            Metadata = result.Item2
        };
    }

    public async Task<IPagedModel<QueryLogSummaryModel>> GetQueryLogSummariesByFacilityAndPatient(string facilityId, string patientId, int page, int pageSize, string sortBy, SortOrder sortOrder, CancellationToken cancellationToken = default)
    {
        var result = await _dataAcquisitionLogManager.SearchAsync(x => x.FacilityId.ToUpper() == facilityId.ToUpper() && x.PatientId.ToUpper() == patientId.ToUpper(), page, pageSize, sortBy, sortOrder, cancellationToken);
        return new QueryLogSummaryModelResponse
        {
            Records = result.Item1.Select(QueryLogSummaryModel.FromDomain).ToList(),
            Metadata = result.Item2
        };
    }

    public async Task<QueryLogSummaryModel> UpdateLogEntry(string id, UpdateDataAcquisitionLogModel updateLog, CancellationToken cancellationToken)
    {
        if (updateLog == null)
        {
            throw new ArgumentNullException(nameof(updateLog));
        }

        var log = await _dataAcquisitionLogManager.GetAsync(id, cancellationToken);
        if (log == null)
        {
            throw new DataAcquisitionLogNotFoundException($"Data acquisition log with ID {id} not found.");
        }

        if(updateLog.ScheduledExecutionDate != default)
            log.ExecutionDate = updateLog.ScheduledExecutionDate;

        log.Status = RequestStatusModelUtilities.ToDomain(updateLog.Status.Value);
        await _dataAcquisitionLogManager.UpdateAsync(log, cancellationToken);

        return QueryLogSummaryModel.FromDomain(log);
    }

    public async Task<IPagedModel<QueryLogSummaryModel>> Search(int page, int pageSize, string sortBy, SortOrder sortOrder, string? patientId = default, string? facilityId = default, CancellationToken cancellationToken = default)
    {
        if(string.IsNullOrEmpty(patientId) && string.IsNullOrEmpty(facilityId))
        {
            throw new ArgumentException("Either patientId or facilityId must be provided.");
        }

        Expression<Func<DataAcquisitionLog, bool>> predicate = x => true;
        if (!string.IsNullOrEmpty(patientId))
        {
            predicate = predicate.And(x => x.PatientId.ToLower() == patientId.ToLower());
        }
        if (!string.IsNullOrEmpty(facilityId))
        {
            predicate = predicate.And(x => x.FacilityId.ToLower() == facilityId.ToLower());
        }

        var result =  await _dataAcquisitionLogManager.SearchAsync(predicate, page, pageSize, sortBy, sortOrder, cancellationToken);
        return new QueryLogSummaryModelResponse
        {
            Records = result.Item1.Select(QueryLogSummaryModel.FromDomain).ToList(),
            Metadata = result.Item2
        };
    }

    public async Task DeleteLogEntry(string id, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(id))
        {
            throw new ArgumentNullException(nameof(id));
        }

        var log = await _dataAcquisitionLogManager.GetAsync(id, cancellationToken);
        if (log == null)
        {
            throw new DataAcquisitionLogNotFoundException($"Data acquisition log with ID {id} not found.");
        }

        // Logic to delete the log entry from the database
        await _dataAcquisitionLogManager.DeleteAsync(id, cancellationToken);
    }

    public async Task<IPagedModel<QueryLogSummaryModel>> Search(QueryPhaseModel? queryPhase, RequestStatusModel? status, AcquisitionPriorityModel? priority, int page, int pageSize, string sortBy, SortOrder sortOrder, string? patientId = null, string? facilityId = null, CancellationToken cancellationToken = default)
    {
        Expression<Func<DataAcquisitionLog, bool>> predicate = PredicateBuilder.New<DataAcquisitionLog>();
        if (queryPhase.HasValue)
        {
            predicate = predicate.And(x => x.QueryPhase == QueryPhaseModelUtilities.ToDomain(queryPhase.Value));
        }

        if (status.HasValue)
        {
            predicate = predicate.And(x => x.Status == RequestStatusModelUtilities.ToDomain(status.Value));
        }

        if (priority.HasValue)
        {
            predicate = predicate.And(x => x.Priority == AcquisitionPriorityModelUtilities.ToDomain(priority.Value));
        }

        if (!string.IsNullOrEmpty(patientId))
        {
            predicate = predicate.And(x => x.PatientId.ToLower() == patientId.ToLower());
        }

        if (!string.IsNullOrEmpty(facilityId))
        {
            predicate = predicate.And(x => x.FacilityId.ToLower() == facilityId.ToLower());
        }

        var result = await _dataAcquisitionLogManager.SearchAsync(predicate, page, pageSize, sortBy, sortOrder, cancellationToken);
        return new QueryLogSummaryModelResponse
        {
            Records = result.Item1.Select(QueryLogSummaryModel.FromDomain).ToList(),
            Metadata = result.Item2
        };
    }

    public async Task StartRetrievalProcess(string logId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(logId))
        {
            throw new ArgumentNullException(nameof(logId));
        }

        var log = await _dataAcquisitionLogManager.GetAsync(logId, cancellationToken);

        if (log == null)
        {
            throw new DataAcquisitionLogNotFoundException($"Data acquisition log with ID {logId} not found.");
        }

        log.Status = RequestStatus.Pending;
        await _dataAcquisitionLogManager.UpdateAsync(log, cancellationToken);

        await _readyToAcquireProducer.ProduceAsync(
            KafkaTopic.ReadyToAcquire.ToString(),
            new Message<string, ReadyToAcquire>
            {
                Key = log.Id,
                Value = new ReadyToAcquire
                {
                    LogId = log.Id,
                    FacilityId = log.FacilityId
                }
            }, cancellationToken);
    }
}
