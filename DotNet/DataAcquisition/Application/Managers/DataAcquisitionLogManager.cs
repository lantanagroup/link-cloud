using DataAcquisition.Domain;
using DataAcquisition.Domain.Entities;
using LantanaGroup.Link.DataAcquisition.Application.Models;
using LantanaGroup.Link.DataAcquisition.Application.Models.Exceptions;
using LantanaGroup.Link.Shared.Application.Enums;
using LantanaGroup.Link.Shared.Application.Models.Responses;

namespace LantanaGroup.Link.DataAcquisition.Application.Managers;

public interface IDataAcquisitionLogManager
{
    // Define methods for managing data acquisition logs
    Task<DataAcquisitionLog> CreateAsync(DataAcquisitionLog log, CancellationToken cancellationToken = default);
    Task<DataAcquisitionLog?> UpdateAsync(DataAcquisitionLog log, CancellationToken cancellationToken = default);
    Task<DataAcquisitionLog?> GetAsync(string id, CancellationToken cancellationToken = default);
    Task<(List<DataAcquisitionLog>, PaginationMetadata)> GetByFacilityIdAsync(string facilityId, int page, int pageSize, string sortBy, SortOrder sortOrder, CancellationToken cancellationToken = default);
}

public class DataAcquisitionLogManager : IDataAcquisitionLogManager
{
    public readonly ILogger<DataAcquisitionLogManager> _logger;
    public readonly IDatabase _database;

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

    public async Task<DataAcquisitionLog?> GetAsync(string id, CancellationToken cancellationToken = default)
    {
        return await _database.DataAcquisitionLogRepository.GetAsync(id, cancellationToken);
    }

    public async Task<(List<DataAcquisitionLog>,PaginationMetadata)> GetByFacilityIdAsync(string facilityId, int page, int pageSize, string sortBy, SortOrder sortOrder, CancellationToken cancellationToken = default)
    {
        return await _database.DataAcquisitionLogRepository.SearchAsync(x => x.FacilityId.ToLower() == facilityId.ToLower(), sortBy, sortOrder, page, pageSize, cancellationToken);
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
            throw new NotFoundException($"Data acquisition log with ID {log.Id} not found.");
        }

        existingLog.Priority = log.Priority;
        existingLog.PatientId = log.PatientId;
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

        existingLog.ModifyDate = DateTime.UtcNow;

        return await _database.DataAcquisitionLogRepository.UpdateAsync(existingLog, cancellationToken);

    }
}
