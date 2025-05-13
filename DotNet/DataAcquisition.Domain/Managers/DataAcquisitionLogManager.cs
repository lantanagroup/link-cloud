using DataAcquisition.Domain;
using DataAcquisition.Domain.Entities;
using DataAcquisition.Domain.Models.Exceptions;
using LantanaGroup.Link.DataAcquisition.Domain.Models.Enums;
using LantanaGroup.Link.Shared.Application.Enums;
using LantanaGroup.Link.Shared.Application.Models.Responses;
using Microsoft.Extensions.Logging;
using System.Linq.Expressions;

namespace LantanaGroup.Link.DataAcquisition.Domain.Application.Managers;

public interface IDataAcquisitionLogManager
{
    // Define methods for managing data acquisition logs
    Task<DataAcquisitionLog> CreateAsync(DataAcquisitionLog log, CancellationToken cancellationToken = default);
    Task<DataAcquisitionLog?> UpdateAsync(DataAcquisitionLog log, CancellationToken cancellationToken = default);
    Task<DataAcquisitionLog?> GetAsync(string id, CancellationToken cancellationToken = default);
    Task DeleteAsync(string id, CancellationToken cancellationToken = default);
    Task<(List<DataAcquisitionLog>, PaginationMetadata)> GetByFacilityIdAsync(string facilityId, int page, int pageSize, string sortBy, SortOrder sortOrder, CancellationToken cancellationToken = default);
    Task<(List<DataAcquisitionLog>, PaginationMetadata)> SearchAsync(Expression<Func<DataAcquisitionLog, bool>> predicate, int page, int pageSize, string sortBy, SortOrder sortOrder, CancellationToken cancellationToken = default);
    Task<List<DataAcquisitionLog>> GetPendingRequests(CancellationToken cancellationToken = default);
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

        await _database.DataAcquisitionLogRepository.AddAsync(log, cancellationToken);

        return log;
    }

    public async Task DeleteAsync(string id, CancellationToken cancellationToken = default)
    { 
        if (string.IsNullOrEmpty(id))
        {
            throw new ArgumentNullException(nameof(id));
        }

        await _database.DataAcquisitionLogRepository.DeleteAsync(id, cancellationToken);
    }

    public async Task<DataAcquisitionLog?> GetAsync(string id, CancellationToken cancellationToken = default)
    {
        return await _database.DataAcquisitionLogRepository.GetAsync(id, cancellationToken);
    }

    public async Task<(List<DataAcquisitionLog>,PaginationMetadata)> GetByFacilityIdAsync(string facilityId, int page, int pageSize, string sortBy, SortOrder sortOrder, CancellationToken cancellationToken = default)
    {
        var result = await _database.DataAcquisitionLogRepository.SearchAsync(x => x.FacilityId.ToUpper() == facilityId.ToUpper(), sortBy, sortOrder, page, pageSize, cancellationToken);
        return result;
    }

    public async Task<(List<DataAcquisitionLog>, PaginationMetadata)> SearchAsync(Expression<Func<DataAcquisitionLog, bool>> predicate, int page, int pageSize, string sortBy, SortOrder sortOrder, CancellationToken cancellationToken = default)
    {
        var result = await _database.DataAcquisitionLogRepository.SearchAsync(predicate, sortBy, sortOrder, page, pageSize, cancellationToken);
        return result;
    }

    public async Task<DataAcquisitionLog?> UpdateAsync(DataAcquisitionLog log, CancellationToken cancellationToken = default)
    {
        if (log == null)
        {
            throw new ArgumentNullException(nameof(log));
        }

        var existingLog = await _database.DataAcquisitionLogRepository.GetAsync(log.Id, cancellationToken);

        if (existingLog == null)
        {
            throw new DomainEntityNotFoundException($"Data acquisition log with ID {log.Id} not found.");
        }

        existingLog.Priority = log.Priority;
        existingLog.PatientId = log.PatientId;
        existingLog.ResourceId = log.ResourceId;
        existingLog.FhirVersion = log.FhirVersion;
        existingLog.QueryType = log.QueryType;
        existingLog.QueryPhase = log.QueryPhase;
        existingLog.Status = log.Status;
        existingLog.ExecutionDate = log.ExecutionDate;
        existingLog.TimeZone = log.TimeZone;
        existingLog.RetryAttempts = log.RetryAttempts;
        existingLog.CompletionDate = log.CompletionDate;
        existingLog.CompletionTimeMilliseconds = log.CompletionTimeMilliseconds;
        existingLog.ResourceAcquiredIds = log.ResourceAcquiredIds;
        existingLog.ReferenceResources = log.ReferenceResources;
        existingLog.Notes = log.Notes;
        existingLog.ScheduledReport = log.ScheduledReport;
        existingLog.FhirQuery = log.FhirQuery;
        existingLog.FacilityId = log.FacilityId;

        existingLog.ModifyDate = DateTime.UtcNow;

        return await _database.DataAcquisitionLogRepository.UpdateAsync(existingLog, cancellationToken);

    }

    public async Task<List<DataAcquisitionLog>> GetPendingRequests(CancellationToken cancellationToken = default)
    {

        var resultSet = await _database.DataAcquisitionLogRepository.FindAsync(x => x.Status == RequestStatus.Pending && x.ExecutionDate >= DateTime.UtcNow, cancellationToken);
        return resultSet.OrderBy(x => x.Priority).ToList();
    }
}
