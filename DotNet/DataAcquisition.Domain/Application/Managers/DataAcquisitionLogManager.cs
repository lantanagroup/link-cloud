using DataAcquisition.Domain.Application.Models;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Api.QueryLog;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Exceptions;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Queries;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Context;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Entities;
using LantanaGroup.Link.DataAcquisition.Domain.Models;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Models.Integration.DataAcquisition;
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
    Task DeleteAsync(long id, CancellationToken cancellationToken = default);
    Task<int> SoftDeleteByFacilityAsync(string facilityId, CancellationToken cancellationToken = default);
    Task<int> RestoreByFacilityAsync(string facilityId, CancellationToken cancellationToken = default);
    Task<int> SoftDeleteByReportTrackingIdAsync(string reportTrackingId, CancellationToken cancellationToken = default);
    Task<int> RestoreByReportTrackingIdAsync(string reportTrackingId, CancellationToken cancellationToken = default);
    Task UpdateTailFlagForFacilityCorrelationIdReportTrackingId(List<long> logIds, string facilityId, string correlationId, string reportTrackingId, CancellationToken cancellationToken = default);
    Task ThrottleFacilityAcquisitions(string facilityId, DateTime executionDate, CancellationToken cancellationToken = default);
    Task<bool> TrySetLogStatusAsync(long logId, List<RequestStatus> validCurrentStatuses, RequestStatus newStatus, CancellationToken cancellationToken = default);
    Task<bool> TrySetLogToQueuedAsync(long logId, CancellationToken cancellationToken);
    Task<int> FailStalledQueuedLogsAsync(int stallMinutes, int maxBatches = 20, CancellationToken cancellationToken = default);
    Task<int> ResetStalledProcessingLogsAsync(int stallMinutes, int maxBatches = 20, CancellationToken cancellationToken = default);
    Task CollectPendingReferenceIdsAsync(Guid fhirQueryId, CancellationToken cancellationToken = default);
    Task CleanupPendingReferenceIdsAsync(Guid fhirQueryId, CancellationToken cancellationToken = default);
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

        var logId = updateLog.Id.Value;
        var retryAttempts = updateLog.RetryAttempts;
        var resourceAcquiredIds = (updateLog.ResourceAcquiredIds is { Count: > 0 })
            ? updateLog.ResourceAcquiredIds : null;
        var traceId = updateLog.TraceId;
        var executionDate = updateLog.ExecutionDate;
        var completionDate = updateLog.CompletionDate;
        var completionTimeMs = updateLog.CompletionTimeMilliseconds;
        var notes = updateLog.Notes;
        var status = updateLog.Status;
        var now = DateTime.UtcNow;

        if (completionTimeMs is not null)
            activity?.SetTag(DiagnosticNames.Duration, completionTimeMs);

        var updated = await _dbContext.DataAcquisitionLogs
            .Where(l => l.Id == logId)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(l => l.RetryAttempts, l => retryAttempts ?? l.RetryAttempts)
                .SetProperty(l => l.ResourceAcquiredIds, l => resourceAcquiredIds ?? l.ResourceAcquiredIds)
                .SetProperty(l => l.TraceId, l => traceId ?? l.TraceId)
                .SetProperty(l => l.ExecutionDate, l => executionDate ?? l.ExecutionDate)
                .SetProperty(l => l.CompletionDate, l => completionDate ?? l.CompletionDate)
                .SetProperty(l => l.CompletionTimeMilliseconds, l => completionTimeMs ?? l.CompletionTimeMilliseconds)
                .SetProperty(l => l.Notes, l => notes ?? l.Notes)
                .SetProperty(l => l.Status, l => status ?? l.Status)
                .SetProperty(l => l.ModifyDate, now),
            cancellationToken);

        if (updated == 0)
        {
            activity?.SetStatus(ActivityStatusCode.Error, "Data acquisition log not found");
            throw new DataAcquisitionLogNotFoundException($"Data acquisition log with ID {logId} not found.");
        }

        return new DataAcquisitionLogModel
        {
            Id = logId,
            Status = status,
            RetryAttempts = retryAttempts,
            TraceId = traceId,
            ExecutionDate = executionDate,
            CompletionDate = completionDate,
            CompletionTimeMilliseconds = completionTimeMs,
            ResourceAcquiredIds = resourceAcquiredIds,
            Notes = notes
        };
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

        var updated = await _dbContext.DataAcquisitionLogs
            .Where(l => logIds.Contains(l.Id))
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(l => l.TailSent, true)
                .SetProperty(l => l.ModifyDate, DateTime.UtcNow),
            cancellationToken);

        if (updated == 0)
        {
            throw new NotFoundException($"Data acquisition logs with IDs {string.Join(", ", logIds)} not found.");
        }
    }

    public async Task ThrottleFacilityAcquisitions(string facilityId, DateTime executionDate, CancellationToken cancellationToken = default)
    {
        using var activity = ServiceActivitySource.Instance.StartActivity("DataAcquisitionLogManager.ThrottleFacilityAcquisitions");
        activity?.SetTag(DiagnosticNames.FacilityId, facilityId);

        var eligibleStatuses = new[] { RequestStatus.Failed, RequestStatus.Ready, RequestStatus.Pending };

        int updated;
        const int batchSize = 1000;

        do
        {
            updated = await _dbContext.DataAcquisitionLogs
                .Where(l => l.FacilityId == facilityId
                    && l.Status != null && eligibleStatuses.Contains(l.Status.Value)
                    && (l.ExecutionDate == null || l.ExecutionDate < executionDate))
                .Take(batchSize)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(l => l.ExecutionDate, executionDate)
                    .SetProperty(l => l.ModifyDate, DateTime.UtcNow),
                cancellationToken);
        }
        while (updated == batchSize);
    }

    public async Task<bool> TrySetLogStatusAsync(long logId, List<RequestStatus> validCurrentStatuses,
        RequestStatus newStatus,
        CancellationToken cancellationToken = default)
    {
        using var activity = ServiceActivitySource.Instance.StartActivity("DataAcquisitionLogManager.TrySetLogStatusAsync");
        activity?.SetTag(DiagnosticNames.ReportId, logId);

        int rowsAffected = await _dbContext.DataAcquisitionLogs
            .Where(l => l.Id == logId && l.Status != null && validCurrentStatuses.Contains(l.Status.Value))
            .ExecuteUpdateAsync(setters => setters
                    .SetProperty(l => l.Status, newStatus)
                    .SetProperty(l => l.ModifyDate, DateTime.UtcNow),
                cancellationToken);

        return rowsAffected > 0;
    }

    public async Task<bool> TrySetLogToQueuedAsync(long logId, CancellationToken cancellationToken)
    {
        return await TrySetLogStatusAsync(logId, [RequestStatus.Ready, RequestStatus.Pending], RequestStatus.Queued,
            cancellationToken);
    }

    public async Task<int> FailStalledQueuedLogsAsync(int stallMinutes, int maxBatches = 20, CancellationToken cancellationToken = default)
    {
        var stallThreshold = DateTime.UtcNow.AddMinutes(-stallMinutes);
        int totalUpdated = 0;
        int batchesProcessed = 0;
        const int BatchSize = 100;

        while (batchesProcessed < maxBatches)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var batchIds = await _dbContext.DataAcquisitionLogs
                .Where(l => l.Status == RequestStatus.Queued && l.ModifyDate <= stallThreshold)
                .OrderBy(l => l.Id)
                .Select(l => l.Id)
                .Take(BatchSize)
                .ToListAsync(cancellationToken);

            if (batchIds.Count == 0)
                break;

            totalUpdated += await _dbContext.DataAcquisitionLogs
                .Where(l => batchIds.Contains(l.Id)
                    && l.Status == RequestStatus.Queued
                    && l.ModifyDate < stallThreshold)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(l => l.Status, RequestStatus.Failed)
                    .SetProperty(l => l.ModifyDate, DateTime.UtcNow),
                    cancellationToken);

            batchesProcessed++;
            if (batchIds.Count < BatchSize)
                break;
        }

        return totalUpdated;
    }

    public async Task<int> ResetStalledProcessingLogsAsync(int stallMinutes, int maxBatches = 20, CancellationToken cancellationToken = default)
    {
        var stallThreshold = DateTime.UtcNow.AddMinutes(-stallMinutes);
        int totalUpdated = 0;
        int batchesProcessed = 0;
        const int BatchSize = 100;

        while (batchesProcessed < maxBatches)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var batchIds = await _dbContext.DataAcquisitionLogs
                .Where(l => l.Status == RequestStatus.Processing && l.ModifyDate <= stallThreshold)
                .OrderBy(l => l.Id)
                .Select(l => l.Id)
                .Take(BatchSize)
                .ToListAsync(cancellationToken);

            if (batchIds.Count == 0)
                break;

            totalUpdated += await _dbContext.DataAcquisitionLogs
                .Where(l => batchIds.Contains(l.Id)
                    && l.Status == RequestStatus.Processing
                    && l.ModifyDate < stallThreshold)
                .ExecuteUpdateAsync(setters => setters
                        .SetProperty(l => l.Status, RequestStatus.Pending)
                        .SetProperty(l => l.ModifyDate, DateTime.UtcNow),
                    cancellationToken);

            batchesProcessed++;
            if (batchIds.Count < BatchSize)
                break;
        }

        return totalUpdated;
    }

    public async Task CollectPendingReferenceIdsAsync(Guid fhirQueryId, CancellationToken cancellationToken = default)
    {
        var pendingIds = await _dbContext.PendingReferenceIds
            .Where(p => p.FhirQueryId == fhirQueryId)
            .Select(p => p.ResourceId)
            .Distinct()
            .ToListAsync(cancellationToken);

        if (pendingIds.Count == 0)
            return;

        var fhirQuery = await _dbContext.FhirQueries
            .Where(q => q.Id == fhirQueryId)
            .FirstOrDefaultAsync(cancellationToken);

        if (fhirQuery == null)
            return;

        var currentParams = fhirQuery.QueryParameters ?? [];

        const string idPrefix = "_id=";
        var existingIds = currentParams
            .Where(p => p.StartsWith(idPrefix))
            .SelectMany(p => p[idPrefix.Length..].Split(','))
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .ToList();

        var mergedIds = existingIds
            .Concat(pendingIds)
            .Distinct()
            .ToList();

        var nonIdParams = currentParams
            .Where(p => !p.StartsWith(idPrefix))
            .ToList();
        nonIdParams.Add($"{idPrefix}{string.Join(',', mergedIds)}");

        await _dbContext.FhirQueries
            .Where(q => q.Id == fhirQueryId)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(q => q.QueryParameters, nonIdParams)
                .SetProperty(q => q.ModifyDate, DateTime.UtcNow),
            cancellationToken);
    }

    public async Task CleanupPendingReferenceIdsAsync(Guid fhirQueryId, CancellationToken cancellationToken = default)
    {
        await _dbContext.PendingReferenceIds
            .Where(p => p.FhirQueryId == fhirQueryId)
            .ExecuteDeleteAsync(cancellationToken);
    }
}