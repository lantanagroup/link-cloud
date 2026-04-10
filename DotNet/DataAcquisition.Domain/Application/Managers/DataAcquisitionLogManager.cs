using DataAcquisition.Domain.Application.Models;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Api.QueryLog;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Api.Requests;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Exceptions;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Queries;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Entities;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models.Enums;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Context;
using LantanaGroup.Link.DataAcquisition.Domain.Models;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Models.Telemetry;
using LantanaGroup.Link.DataAcquisition.Domain.Settings;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using ResourceType = Hl7.Fhir.Model.ResourceType;

namespace LantanaGroup.Link.DataAcquisition.Domain.Application.Managers;

public interface IDataAcquisitionLogManager
{
    Task<DataAcquisitionLogModel> CreateAsync(CreateDataAcquisitionLogModel log, CancellationToken cancellationToken = default);
    Task<DataAcquisitionLogModel?> UpdateAsync(UpdateDataAcquisitionLogModel updateLog, CancellationToken cancellationToken = default);
    Task<int> UpdateStatusBatchAsync(IEnumerable<long> ids, RequestStatus newStatus, CancellationToken cancellationToken = default);
    Task<int> CancelBulkAsync(IEnumerable<long> ids, int minAgeHours, CancellationToken cancellationToken = default);
    Task<(int requested, int cancelled)> CancelByFilterAsync(SearchDataAcquisitionLogRequest filter, int minAgeHours, CancellationToken cancellationToken = default);
    Task<List<DataAcquisitionLog>> GetLogsByIdsAsync(List<long> ids, CancellationToken cancellationToken = default);
    Task DeleteAsync(long id, CancellationToken cancellationToken = default);
    Task<int> SoftDeleteByFacilityAsync(string facilityId, CancellationToken cancellationToken = default);
    Task<int> RestoreByFacilityAsync(string facilityId, CancellationToken cancellationToken = default);
    Task<int> SoftDeleteByReportTrackingIdAsync(string reportTrackingId, CancellationToken cancellationToken = default);
    Task<int> RestoreByReportTrackingIdAsync(string reportTrackingId, CancellationToken cancellationToken = default);
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

        if (model.ScheduledReport == null)
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

    private const int SoftDeleteBatchSize = 1000;

    // Soft deletes all logs for a facility in batches to avoid large update locks
    public async Task<int> SoftDeleteByFacilityAsync(string facilityId, CancellationToken cancellationToken = default)
    {
        using var activity = ServiceActivitySource.Instance.StartActivity("DataAcquisitionLogManager.SoftDeleteByFacilityAsync");
        activity?.SetTag(DiagnosticNames.FacilityId, facilityId);

        if (string.IsNullOrWhiteSpace(facilityId))
        {
            throw new ArgumentNullException(nameof(facilityId), "Facility ID cannot be null or empty.");
        }

        int totalUpdated = 0;
        int updated;

        do
        {
            updated = await _dbContext.DataAcquisitionLogs
                .Where(l => l.FacilityId == facilityId && !l.IsDeleted)
                .Take(SoftDeleteBatchSize)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(l => l.IsDeleted, true)
                    .SetProperty(l => l.ModifyDate, DateTime.UtcNow),
                    cancellationToken);

            totalUpdated += updated;
        }
        while (updated == SoftDeleteBatchSize);

        return totalUpdated;
    }

    public async Task<int> RestoreByFacilityAsync(string facilityId, CancellationToken cancellationToken = default)
    {
        using var activity = ServiceActivitySource.Instance.StartActivity("DataAcquisitionLogManager.RestoreByFacilityAsync");
        activity?.SetTag(DiagnosticNames.FacilityId, facilityId);

        if (string.IsNullOrWhiteSpace(facilityId))
        {
            throw new ArgumentNullException(nameof(facilityId), "Facility ID cannot be null or empty.");
        }

        int totalUpdated = 0;
        int updated;

        do
        {
            updated = await _dbContext.DataAcquisitionLogs
                .Where(l => l.FacilityId == facilityId && l.IsDeleted)
                .Take(SoftDeleteBatchSize)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(l => l.IsDeleted, false)
                    .SetProperty(l => l.ModifyDate, DateTime.UtcNow),
                    cancellationToken);

            totalUpdated += updated;
        }
        while (updated == SoftDeleteBatchSize);

        return totalUpdated;
    }

    public async Task<int> SoftDeleteByReportTrackingIdAsync(string reportTrackingId, CancellationToken cancellationToken = default)
    {
        using var activity = ServiceActivitySource.Instance.StartActivity("DataAcquisitionLogManager.SoftDeleteByReportTrackingIdAsync");
        activity?.SetTag(DiagnosticNames.ReportTrackingId, reportTrackingId);

        if (string.IsNullOrWhiteSpace(reportTrackingId))
        {
            throw new ArgumentNullException(nameof(reportTrackingId), "Report tracking ID cannot be null or empty.");
        }

        int totalUpdated = 0;
        int updated;

        do
        {
            updated = await _dbContext.DataAcquisitionLogs
                .Where(l => l.ReportTrackingId == reportTrackingId && !l.IsDeleted)
                .Take(SoftDeleteBatchSize)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(l => l.IsDeleted, true)
                    .SetProperty(l => l.ModifyDate, DateTime.UtcNow),
                    cancellationToken);

            totalUpdated += updated;
        }
        while (updated == SoftDeleteBatchSize);

        return totalUpdated;
    }

    public async Task<int> RestoreByReportTrackingIdAsync(string reportTrackingId, CancellationToken cancellationToken = default)
    {
        using var activity = ServiceActivitySource.Instance.StartActivity("DataAcquisitionLogManager.RestoreByReportTrackingIdAsync");
        activity?.SetTag(DiagnosticNames.ReportTrackingId, reportTrackingId);

        if (string.IsNullOrWhiteSpace(reportTrackingId))
        {
            throw new ArgumentNullException(nameof(reportTrackingId), "Report tracking ID cannot be null or empty.");
        }

        int totalUpdated = 0;
        int updated;

        do
        {
            updated = await _dbContext.DataAcquisitionLogs
                .Where(l => l.ReportTrackingId == reportTrackingId && l.IsDeleted)
                .Take(SoftDeleteBatchSize)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(l => l.IsDeleted, false)
                    .SetProperty(l => l.ModifyDate, DateTime.UtcNow),
                    cancellationToken);

            totalUpdated += updated;
        }
        while (updated == SoftDeleteBatchSize);

        return totalUpdated;
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

        if (updateLog.RetryAttempts is not null)
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

        var updatedCount = 0;
        // Batch updates to avoid exceeding MaxBulkIds per database call
        foreach (var batch in ids.Chunk(DataAcquisitionConstants.DatabaseSettings.MaxBulkIds))
        {
            updatedCount += await _dbContext.DataAcquisitionLogs
                .Where(l => batch.Contains(l.Id))
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(l => l.Status, newStatus)
                    .SetProperty(l => l.ModifyDate, DateTime.UtcNow),
                    cancellationToken);
        }

        return updatedCount;
    }

    public async Task<int> CancelBulkAsync(IEnumerable<long> ids, int minAgeHours, CancellationToken cancellationToken = default)
    {
        using var activity = ServiceActivitySource.Instance.StartActivity("DataAcquisitionLogManager.CancelBulkAsync");

        var terminalStatuses = new[] { RequestStatus.Completed, RequestStatus.MaxRetriesReached, RequestStatus.Skipped, RequestStatus.Cancelled };
        var minAgeCutoff = DateTime.UtcNow.AddHours(-minAgeHours);

        var cancelledCount = 0;
        // Batch cancellations to avoid exceeding MaxBulkIds per database call
        foreach (var batch in ids.Chunk(DataAcquisitionConstants.DatabaseSettings.MaxBulkIds))
        {
            cancelledCount += await _dbContext.DataAcquisitionLogs
                .Where(l => batch.Contains(l.Id)
                    && l.Status != null
                    && !terminalStatuses.Contains(l.Status.Value)
                    && l.CreateDate <= minAgeCutoff)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(l => l.Status, RequestStatus.Cancelled)
                    .SetProperty(l => l.ModifyDate, DateTime.UtcNow),
                    cancellationToken);
        }

        return cancelledCount;
    }

    public async Task<(int requested, int cancelled)> CancelByFilterAsync(SearchDataAcquisitionLogRequest filter, int minAgeHours, CancellationToken cancellationToken = default)
    {
        using var activity = ServiceActivitySource.Instance.StartActivity("DataAcquisitionLogManager.CancelByFilterAsync");

        var terminalStatuses = new[] { RequestStatus.Completed, RequestStatus.MaxRetriesReached, RequestStatus.Skipped, RequestStatus.Cancelled };
        var minAgeCutoff = DateTime.UtcNow.AddHours(-minAgeHours);

        var query = _dbContext.DataAcquisitionLogs.AsQueryable();

        if (!filter.IncludeDeleted)
            query = query.Where(l => !l.IsDeleted);

        if (!string.IsNullOrEmpty(filter.FacilityId))
            query = query.Where(l => l.FacilityId == filter.FacilityId);

        if (!string.IsNullOrEmpty(filter.PatientId))
            query = query.Where(l => l.PatientId == filter.PatientId);

        if (!string.IsNullOrEmpty(filter.ReportTrackingId))
            query = query.Where(l => l.ReportTrackingId == filter.ReportTrackingId);

        if (!string.IsNullOrEmpty(filter.ResourceId))
            query = query.Where(l => l.ResourceAcquiredIds != null && l.ResourceAcquiredIds.Contains(filter.ResourceId));

        if (filter.QueryPhase.HasValue)
            query = query.Where(l => l.QueryPhase == filter.QueryPhase.Value);

        if (filter.QueryType.HasValue)
            query = query.Where(l => l.QueryType == filter.QueryType.Value);

        if (filter.AcquisitionPriority.HasValue)
            query = query.Where(l => l.Priority == filter.AcquisitionPriority.Value);

        if (filter.RequestStatuses != null && filter.RequestStatuses.Any())
            query = query.Where(l => l.Status != null && filter.RequestStatuses.Contains(l.Status.Value));

        if (!string.IsNullOrEmpty(filter.ResourceType))
        {
            var resourceType = Enum.Parse<ResourceType>(filter.ResourceType, ignoreCase: true);
            query = query.Where(l => l.FhirQueries.Any(q => q.FhirQueryResourceTypes.Any(r => r.ResourceType == resourceType)));
        }

        if (filter.CreatedBefore.HasValue)
            query = query.Where(l => l.CreateDate <= filter.CreatedBefore.Value);

        // Count all matching logs before applying eligibility filters
        var requested = await query.CountAsync(cancellationToken);

        // Get IDs of logs eligible for cancellation
        var eligibleIds = await query.Where(l => l.Status != null
            && !terminalStatuses.Contains(l.Status.Value)
            && l.CreateDate <= minAgeCutoff)
            .Select(l => l.Id)
            .ToListAsync(cancellationToken);

        var cancelled = 0;
        if (eligibleIds.Any())
        {
            // Batch cancellations to avoid exceeding MaxBulkIds per database call
            foreach (var batch in eligibleIds.Chunk(DataAcquisitionConstants.DatabaseSettings.MaxBulkIds))
            {
                cancelled += await _dbContext.DataAcquisitionLogs
                    .Where(l => batch.Contains(l.Id))
                    .ExecuteUpdateAsync(setters => setters
                        .SetProperty(l => l.Status, RequestStatus.Cancelled)
                        .SetProperty(l => l.ModifyDate, DateTime.UtcNow),
                        cancellationToken);
            }
        }

        return (requested, cancelled);
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

            if (toThrottle.Count == 0)
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
