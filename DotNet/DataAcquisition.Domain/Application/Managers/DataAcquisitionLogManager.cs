using System.Diagnostics;
using DataAcquisition.Domain.Application.Models;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Api.QueryLog;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Api.Requests;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Domain;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Exceptions;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Kafka;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Queries;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Context;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Entities;
using LantanaGroup.Link.DataAcquisition.Domain.Models;
using LantanaGroup.Link.DataAcquisition.Domain.Settings;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Models.Telemetry;
using Medallion.Threading;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RequestStatus = LantanaGroup.Link.Shared.Application.Models.Integration.DataAcquisition.RequestStatus;
using QueryPhase = LantanaGroup.Link.Shared.Application.Models.Integration.DataAcquisition.QueryPhase;
using ResourceType = Hl7.Fhir.Model.ResourceType;
using LantanaGroup.Link.Shared.Application.Models.Integration.DataAcquisition;
using LantanaGroup.Link.Shared.Application.Enums;
using LantanaGroup.Link.Shared.Application.Interfaces;

namespace LantanaGroup.Link.DataAcquisition.Domain.Application.Managers;

public interface IDataAcquisitionLogManager
{
    Task<DataAcquisitionLogModel> CreateAsync(CreateDataAcquisitionLogModel log, CancellationToken cancellationToken = default);
    Task<int> UpdateStatusBatchAsync(IEnumerable<long> ids, RequestStatus newStatus, CancellationToken cancellationToken = default);
    Task<int> CancelBulkAsync(IEnumerable<long> ids, int minAgeHours, CancellationToken cancellationToken = default);
    Task<(int requested, int cancelled)> CancelByFilterAsync(SearchDataAcquisitionLogRequest filter, int minAgeHours, CancellationToken cancellationToken = default);
    Task<List<DataAcquisitionLog>> GetLogsByIdsAsync(List<long> ids, CancellationToken cancellationToken = default);
    Task UpdateAsync(UpdateDataAcquisitionLogModel updateLog, CancellationToken cancellationToken = default);
    Task<int> UpdateStatusBatchAsync(IEnumerable<long> ids, RequestStatus newStatus, bool incrementRetry = false, CancellationToken cancellationToken = default);
    Task DeleteAsync(long id, CancellationToken cancellationToken = default);
    Task<int> SoftDeleteByFacilityAsync(string facilityId, CancellationToken cancellationToken = default);
    Task<int> RestoreByFacilityAsync(string facilityId, CancellationToken cancellationToken = default);
    Task<int> SoftDeleteByReportTrackingIdAsync(string reportTrackingId, CancellationToken cancellationToken = default);
    Task<int> RestoreByReportTrackingIdAsync(string reportTrackingId, CancellationToken cancellationToken = default);
    Task UpdateTailFlagForFacilityCorrelationIdReportTrackingId(List<long> logIds, string facilityId, string correlationId, string reportTrackingId, CancellationToken cancellationToken = default);
    Task ThrottleFacilityAcquisitions(string facilityId, DateTime executionDate, CancellationToken cancellationToken = default);
    Task<bool> TrySetLogStatusAsync(long logId, List<RequestStatus> validCurrentStatuses, RequestStatus newStatus, string? note = null, CancellationToken cancellationToken = default);
    Task<bool> TrySetLogToQueuedAsync(long logId, CancellationToken cancellationToken);
    Task<int> FailStalledQueuedLogsAsync(int stallMinutes, int maxBatches = 20, CancellationToken cancellationToken = default);
    Task<int> ResetStalledProcessingLogsAsync(int stallMinutes, int maxBatches = 20, CancellationToken cancellationToken = default);
    Task<int> SetMaxRetriesReachedWithNoteBatchAsync(IEnumerable<long> ids, string note, CancellationToken cancellationToken = default);
    Task<int> SetConfigurationMissingWithNoteBatchAsync(IEnumerable<long> ids, string note, CancellationToken cancellationToken = default);

    /// <summary>
    /// Stamps the SiblingCount on all logs in a (FacilityId, CorrelationId, QueryPhase) group.
    /// Called once after all logs have been committed by CreateLogEntries.
    /// </summary>
    Task<int> StampSiblingCountAsync(string facilityId, string correlationId, QueryPhase queryPhase, int siblingCount, CancellationToken cancellationToken = default);

    /// <summary>
    /// Atomically checks whether all sibling logs in a group are terminal
    /// and the SiblingCount matches. If so, marks TailSent = true and
    /// returns the data needed to produce the tail Kafka message.
    /// Returns null if the group is not yet complete.
    /// </summary>
    Task<TailCompletionResult?> TryCompleteTailAsync(long completedLogId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Clears TailSent after a claimed tail fails to finalize or produce so the
    /// safety-net poller can retry. No-op when <paramref name="queryPhase"/> is null.
    /// </summary>
    Task RevertTailSentAsync(string facilityId, string correlationId, QueryPhase? queryPhase, CancellationToken cancellationToken = default);
}

public class DataAcquisitionLogManager : IDataAcquisitionLogManager
{
    public readonly ILogger<DataAcquisitionLogManager> _logger;
    public readonly IDatabase _database;
    private readonly DataAcquisitionDbContext _dbContext;
    private readonly IDataAcquisitionLogQueries _logQueries;
    private readonly IDistributedSemaphoreProvider _distributedSemaphoreProvider;
    private readonly IResourceCache _resourceCache;

    public DataAcquisitionLogManager(ILogger<DataAcquisitionLogManager> logger, IDatabase database, DataAcquisitionDbContext dbContext, IDataAcquisitionLogQueries logQueries, IDistributedSemaphoreProvider distributedSemaphoreProvider, IResourceCache resourceCache)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _database = database ?? throw new ArgumentNullException(nameof(database));
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _logQueries = logQueries;
        _distributedSemaphoreProvider = distributedSemaphoreProvider ?? throw new ArgumentNullException(nameof(distributedSemaphoreProvider));
        _resourceCache = resourceCache ?? throw new ArgumentNullException(nameof(resourceCache));
    }

    public async Task<DataAcquisitionLogModel> CreateAsync(CreateDataAcquisitionLogModel model, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(model);

        if (string.IsNullOrWhiteSpace(model.FacilityId))
        {
            throw new ArgumentNullException(nameof(model.FacilityId));
        }

        using var activity = ServiceActivitySource.Instance.StartActivity("DataAcquisitionLogManager.CreateAsync");
        activity?.SetTag(DiagnosticNames.FacilityId, model.FacilityId);
        activity?.SetTag(DiagnosticNames.CorrelationId, model.CorrelationId);

        Guid? reportTrackingId = null;
        if (!string.IsNullOrWhiteSpace(model.ReportTrackingId))
        {
            if (!Guid.TryParse(model.ReportTrackingId, out var parsedReportTrackingId))
            {
                throw new ArgumentException("ReportTrackingId must be a valid GUID.", nameof(model.ReportTrackingId));
            }

            reportTrackingId = parsedReportTrackingId;
        }

        var log = new DataAcquisitionLog
        {
            Status = model.Status,
            FacilityId = model.FacilityId,
            QueryPhase = model.QueryPhase,
            ReferenceResourceType = model.ReferenceResourceType,
            FhirVersion = model.FhirVersion,
            QueryType = model.QueryType,
            FhirQueries = model.FhirQuery.Select(q => new FhirQuery
            {
                FacilityId = model.FacilityId,
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
            CompletionDate = null,
            CompletionTimeMilliseconds = null,
            ReportTrackingId = reportTrackingId,
            ExecutionDate = model.ExecutionDate,
            CorrelationId = model.CorrelationId,
            TraceId = model.TraceId,
            NoteEntries = (model.Notes ?? []).Select(n => new DataAcquisitionLogNote
            {
                Note = n,
                CreateDate = DateTime.UtcNow
            }).ToList(),
            Priority = model.Priority,
            TailSent = false,
            PatientId = model.PatientId,
            ReportableEvent = model.ReportableEvent,
            RetryAttempts = 0,
            IsCensus = model.IsCensus,
            CreateDate = DateTime.UtcNow,
            ModifyDate = DateTime.UtcNow,
            SiblingCount = model.SiblingCount,
        };

        await _database.DataAcquisitionLogRepository.AddAsync(log);
        await _database.SaveChangesAsync();

        return DataAcquisitionLogModel.FromDomain(log);
    }

    public async Task DeleteAsync(long id, CancellationToken cancellationToken = default)
    {
        using var activity = ServiceActivitySource.Instance.StartActivity("DataAcquisitionLogManager.DeleteAsync");
        activity?.SetTag(DiagnosticNames.DataAcquisitionLogId, id);

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

        if (!Guid.TryParse(reportTrackingId, out var reportTrackingGuid))
        {
            throw new ArgumentException("Report tracking ID must be a valid GUID.", nameof(reportTrackingId));
        }

        int totalUpdated = 0;
        int updated;

        do
        {
            updated = await _dbContext.DataAcquisitionLogs
                .Where(l => l.ReportTrackingId == reportTrackingGuid && !l.IsDeleted)
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

        if (!Guid.TryParse(reportTrackingId, out var reportTrackingGuid))
        {
            throw new ArgumentException("Report tracking ID must be a valid GUID.", nameof(reportTrackingId));
        }

        int totalUpdated = 0;
        int updated;

        do
        {
            updated = await _dbContext.DataAcquisitionLogs
                .Where(l => l.ReportTrackingId == reportTrackingGuid && l.IsDeleted)
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

    public async Task UpdateAsync(UpdateDataAcquisitionLogModel updateLog, CancellationToken cancellationToken = default)
    {
        using var activity = ServiceActivitySource.Instance.StartActivity("DataAcquisitionLogManager.UpdateAsync");
        activity?.SetTag(DiagnosticNames.DataAcquisitionLogId, updateLog.Id);

        if (updateLog.Id is null or 0)
        {
            activity?.SetStatus(ActivityStatusCode.Error, "Log ID cannot be zero or null");
            throw new InvalidOperationException("Log ID cannot be zero or null");
        }

        var logId = updateLog.Id.Value;
        var retryAttempts = updateLog.RetryAttempts;
        var resourceAcquiredIds = updateLog.ResourceAcquiredIds;
        var traceId = updateLog.TraceId;
        var executionDate = updateLog.ExecutionDate;
        var completionDate = updateLog.CompletionDate;
        var completionTimeMs = updateLog.CompletionTimeMilliseconds;
        var newNotes = updateLog.NewNotes;
        var status = updateLog.Status;
        var now = DateTime.UtcNow;

        if (completionTimeMs is not null)
            activity?.SetTag(DiagnosticNames.Duration, completionTimeMs);

        // 1. Atomic scalar update ? single DB round-trip
        var updated = await _dbContext.DataAcquisitionLogs
            .Where(l => l.Id == logId)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(l => l.RetryAttempts, l => retryAttempts ?? l.RetryAttempts)
                .SetProperty(l => l.TraceId, l => traceId ?? l.TraceId)
                .SetProperty(l => l.ExecutionDate, l => executionDate ?? l.ExecutionDate)
                .SetProperty(l => l.CompletionDate, l => completionDate ?? l.CompletionDate)
                .SetProperty(l => l.CompletionTimeMilliseconds, l => completionTimeMs ?? l.CompletionTimeMilliseconds)
                .SetProperty(l => l.Status, l => status ?? l.Status)
                .SetProperty(l => l.ModifyDate, now),
            cancellationToken);

        if (updated == 0)
        {
            activity?.SetStatus(ActivityStatusCode.Error, "Data acquisition log not found");
            throw new DataAcquisitionLogNotFoundException($"Data acquisition log with ID {logId} not found.");
        }

        // 2. Append-only notes ? never deletes existing notes (fixes data-loss on retry)
        if (newNotes is { Count: > 0 })
        {
            var noteRows = newNotes.Select(n => new DataAcquisitionLogNote
            {
                DataAcquisitionLogId = logId,
                Note = n,
                CreateDate = now
            }).ToList();

            _dbContext.DataAcquisitionLogNotes.AddRange(noteRows);
        }

        // 3. Persist resource IDs into the child table
        if (resourceAcquiredIds is { Count: > 0 })
        {
            // Delete old rows first (idempotent on re-process)
            await _dbContext.DataAcquisitionLogResourceIds
                .Where(r => r.DataAcquisitionLogId == logId)
                .ExecuteDeleteAsync(cancellationToken);

            var resourceRows = resourceAcquiredIds.Select(rid => new DataAcquisitionLogResourceId
            {
                DataAcquisitionLogId = logId,
                ResourceId = rid,
                CreateDate = now
            }).ToList();

            _dbContext.DataAcquisitionLogResourceIds.AddRange(resourceRows);
        }

        // 4. Single SaveChangesAsync for all pending note/resource inserts
        if (_dbContext.ChangeTracker.HasChanges())
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        // No reload here - hot path under load. Callers that need fresh state
        // should query explicitly.
    }

    public async Task<List<DataAcquisitionLog>> GetLogsByIdsAsync(List<long> ids, CancellationToken cancellationToken = default)
    {
        return await _database.DataAcquisitionLogRepository.FindAsync(x => ids.Contains(x.Id), cancellationToken);
    }

    public Task<int> UpdateStatusBatchAsync(IEnumerable<long> ids, RequestStatus newStatus, CancellationToken cancellationToken = default)
    {
        return UpdateStatusBatchAsync(ids, newStatus, incrementRetry: false, cancellationToken);
    }

    public async Task<int> UpdateStatusBatchAsync(IEnumerable<long> ids, RequestStatus newStatus, bool incrementRetry = false, CancellationToken cancellationToken = default)
    {
        using var activity = ServiceActivitySource.Instance.StartActivity("DataAcquisitionLogManager.UpdateStatusBatchAsync");
        var now = DateTime.UtcNow;

        // High-speed bulk update without fetching entities. 
        return await _dbContext.DataAcquisitionLogs
            .Where(l => ids.Contains(l.Id))
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(l => l.Status, newStatus)
                .SetProperty(l => l.RetryAttempts, l => incrementRetry ? l.RetryAttempts + 1 : l.RetryAttempts)
                .SetProperty(l => l.CompletionDate, l => newStatus == RequestStatus.Completed ? l.CompletionDate ?? now : l.CompletionDate)
                .SetProperty(l => l.ModifyDate, now),
                cancellationToken);
    }

    public async Task<int> CancelBulkAsync(IEnumerable<long> ids, int minAgeHours, CancellationToken cancellationToken = default)
    {
        using var activity = ServiceActivitySource.Instance.StartActivity("DataAcquisitionLogManager.CancelBulkAsync");

        var cancellableStatuses = RequestStatusExtensions.CancellableStatuses;

        var cancelledCount = 0;
        foreach (var batch in ids.Chunk(DataAcquisitionConstants.DatabaseSettings.MaxBulkIds))
        {
            var query = _dbContext.DataAcquisitionLogs
                .Where(l => batch.Contains(l.Id)
                    && l.Status != null
                    && cancellableStatuses.Contains(l.Status.Value));

            if (minAgeHours > 0)
                query = query.Where(l => l.CreateDate <= DateTime.UtcNow.AddHours(-minAgeHours));

            cancelledCount += await query
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

        var cancellableStatuses = RequestStatusExtensions.CancellableStatuses;

        var query = _dbContext.DataAcquisitionLogs.AsQueryable();

        if (!filter.IncludeDeleted)
            query = query.Where(l => !l.IsDeleted);

        if (!string.IsNullOrEmpty(filter.FacilityId))
            query = query.Where(l => l.FacilityId == filter.FacilityId);

        if (!string.IsNullOrEmpty(filter.PatientId))
            query = query.Where(l => l.PatientId == filter.PatientId);

        if (!string.IsNullOrEmpty(filter.ReportTrackingId))
        {
            if (Guid.TryParse(filter.ReportTrackingId, out var filterReportTrackingGuid))
            {
                query = query.Where(l => l.ReportTrackingId == filterReportTrackingGuid);
            }
            else
            {
                query = query.Where(_ => false);
            }
        }

        if (!string.IsNullOrEmpty(filter.ResourceId))
            query = query.Where(l => l.ResourceIds.Any(r => r.ResourceId == filter.ResourceId));

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
        var eligibleQuery = query.Where(l => l.Status != null
            && cancellableStatuses.Contains(l.Status.Value));

        if (minAgeHours > 0)
            eligibleQuery = eligibleQuery.Where(l => l.CreateDate <= DateTime.UtcNow.AddHours(-minAgeHours));

        var eligibleIds = await eligibleQuery
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

        if (!Guid.TryParse(reportTrackingId, out var reportTrackingGuid))
        {
            throw new ArgumentException("Report tracking ID must be a valid GUID.", nameof(reportTrackingId));
        }

        if (logIds == null || logIds.Count == 0) return;

        var updated = await _dbContext.DataAcquisitionLogs
            .Where(l => logIds.Contains(l.Id)
                && l.FacilityId == facilityId
                && l.CorrelationId == correlationId
                && l.ReportTrackingId == reportTrackingGuid)
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
        string? note = null,
        CancellationToken cancellationToken = default)
    {
        using var activity = ServiceActivitySource.Instance.StartActivity("DataAcquisitionLogManager.TrySetLogStatusAsync");
        activity?.SetTag(DiagnosticNames.DataAcquisitionLogId, logId);
        var now = DateTime.UtcNow;

        int rowsAffected = await _dbContext.DataAcquisitionLogs
            .Where(l => l.Id == logId && l.Status != null && validCurrentStatuses.Contains(l.Status.Value))
            .ExecuteUpdateAsync(setters => setters
                    .SetProperty(l => l.Status, newStatus)
                    .SetProperty(l => l.CompletionDate, l => newStatus == RequestStatus.Completed ? l.CompletionDate ?? now : l.CompletionDate)
                    .SetProperty(l => l.ModifyDate, now),
                cancellationToken);

        if (rowsAffected > 0 && !string.IsNullOrWhiteSpace(note))
        {
            _dbContext.DataAcquisitionLogNotes.Add(new DataAcquisitionLogNote
            {
                DataAcquisitionLogId = logId,
                Note = note,
                CreateDate = now
            });
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        return rowsAffected > 0;
    }

    public async Task<bool> TrySetLogToQueuedAsync(long logId, CancellationToken cancellationToken)
    {
        return await TrySetLogStatusAsync(logId, [RequestStatus.Ready, RequestStatus.Pending], RequestStatus.Queued,
            cancellationToken: cancellationToken);
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

    public async Task<int> SetMaxRetriesReachedWithNoteBatchAsync(IEnumerable<long> ids, string note, CancellationToken cancellationToken = default)
    {
        return await SetTerminalStatusWithNoteBatchAsync(ids, RequestStatus.MaxRetriesReached, note, cancellationToken);
    }

    public async Task<int> SetConfigurationMissingWithNoteBatchAsync(IEnumerable<long> ids, string note, CancellationToken cancellationToken = default)
    {
        return await SetTerminalStatusWithNoteBatchAsync(ids, RequestStatus.ConfigurationMissing, note, cancellationToken);
    }

    private async Task<int> SetTerminalStatusWithNoteBatchAsync(IEnumerable<long> ids, RequestStatus status, string note, CancellationToken cancellationToken = default)
    {
        using var activity = ServiceActivitySource.Instance.StartActivity("DataAcquisitionLogManager.SetTerminalStatusWithNoteBatchAsync");

        var idList = ids as ICollection<long> ?? ids.ToList();

        int updated = await _dbContext.DataAcquisitionLogs
            .Where(l => idList.Contains(l.Id))
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(l => l.Status, status)
                .SetProperty(l => l.ModifyDate, DateTime.UtcNow),
                cancellationToken);

        if (updated > 0)
        {
            var now = DateTime.UtcNow;
            var noteRows = idList.Select(id => new DataAcquisitionLogNote
            {
                DataAcquisitionLogId = id,
                Note = note,
                CreateDate = now
            });
            _dbContext.DataAcquisitionLogNotes.AddRange(noteRows);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        return updated;
    }

    public async Task<int> StampSiblingCountAsync(string facilityId, string correlationId, QueryPhase queryPhase, int siblingCount, CancellationToken cancellationToken = default)
    {
        using var activity = ServiceActivitySource.Instance.StartActivity("DataAcquisitionLogManager.StampSiblingCountAsync");
        activity?.SetTag(DiagnosticNames.FacilityId, facilityId);
        activity?.SetTag(DiagnosticNames.CorrelationId, correlationId);

        // SELECT the target IDs in ascending PK order first, then UPDATE by PK.
        // A broad WHERE-clause UPDATE acquires row/page locks in query-plan order (non-deterministic),
        // which can deadlock against any other concurrent UPDATE that touches overlapping pages.
        // Fetching IDs ordered by PK and then updating by PK ensures every caller acquires
        // locks in the same ascending order, eliminating the lock-ordering inversion that
        // causes deadlocks regardless of which other operation is running concurrently.
        var logIds = await _dbContext.DataAcquisitionLogs
            .Where(l => l.FacilityId == facilityId
                && l.CorrelationId == correlationId
                && l.QueryPhase == queryPhase
                && l.SiblingCount == null)
            .OrderBy(l => l.Id)
            .Select(l => l.Id)
            .ToListAsync(cancellationToken);

        if (logIds.Count == 0) return 0;

        return await _dbContext.DataAcquisitionLogs
            .Where(l => logIds.Contains(l.Id) && l.SiblingCount == null)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(l => l.SiblingCount, siblingCount)
                .SetProperty(l => l.ModifyDate, DateTime.UtcNow),
                cancellationToken);
    }

    public async Task<TailCompletionResult?> TryCompleteTailAsync(long completedLogId, CancellationToken cancellationToken = default)
    {
        using var activity = ServiceActivitySource.Instance.StartActivity("DataAcquisitionLogManager.TryCompleteTailAsync");
        activity?.SetTag(DiagnosticNames.DataAcquisitionLogId, completedLogId);

        var terminalStatuses = RequestStatusExtensions.TerminalStatuses;

        // Load group identity for the completed log (PK lookup)
        var groupInfo = await _dbContext.DataAcquisitionLogs.AsNoTracking()
            .Where(l => l.Id == completedLogId)
            .Select(l => new
            {
                l.FacilityId,
                l.CorrelationId,
                l.QueryPhase,
                l.SiblingCount,
            })
            .FirstOrDefaultAsync(cancellationToken);

        // Short-circuit: nothing to do if the log doesn't exist, SiblingCount
        // isn't stamped yet, or the group isn't fully terminal.
        if (groupInfo == null
            || groupInfo.SiblingCount == null
            || groupInfo.CorrelationId == null
            || groupInfo.QueryPhase == null)
        {
            return null;
        }

        // Acquire a per-group mutex so at most one worker evaluates tail completion
        // at a time for a given (FacilityId, CorrelationId, QueryPhase) group.
        // This prevents redundant competing COUNT + UPDATE calls from concurrent workers.
        var lockKey = $"da:tail:{groupInfo.FacilityId}:{groupInfo.CorrelationId}:{groupInfo.QueryPhase}";
        var semaphore = _distributedSemaphoreProvider.CreateSemaphore(lockKey, maxCount: 1);
        await using var handle = await semaphore.TryAcquireAsync(TimeSpan.FromSeconds(30), cancellationToken);
        if (handle == null)
        {
            // Another worker is already evaluating this group's tail.
            // TailMessageRecoveryJob will cover any missed tail.
            return null;
        }

        // Tail threshold is derived from the live count of stamped siblings rather
        // than the SiblingCount value on any single row. Same-phase reference logs
        // are added mid-flow and stamp ONLY their own row (single-row UPDATE) to
        // avoid deadlocking with this method's wide TailSent UPDATE; that means a
        // single row's SiblingCount can lag behind the true group size, but the
        // count of stamped rows is always authoritative.
        var stampedSiblingCount = await _dbContext.DataAcquisitionLogs.AsNoTracking()
            .CountAsync(l =>
                l.SiblingCount != null
                && l.CorrelationId != null
                && l.QueryPhase != null
                && l.FacilityId == groupInfo.FacilityId
                && l.CorrelationId == groupInfo.CorrelationId
                && l.QueryPhase == groupInfo.QueryPhase,
                cancellationToken);

        // Count terminal siblings.
        var terminalCount = await _dbContext.DataAcquisitionLogs.AsNoTracking()
            .CountAsync(l =>
                !l.TailSent
                && l.SiblingCount != null
                && l.CorrelationId != null
                && l.QueryPhase != null
                && l.FacilityId == groupInfo.FacilityId
                && l.CorrelationId == groupInfo.CorrelationId
                && l.QueryPhase == groupInfo.QueryPhase
                && l.Status != null
                && terminalStatuses.Contains(l.Status.Value),
                cancellationToken);

        if (terminalCount < stampedSiblingCount)
        {
            return null;
        }

        // Atomically claim the tail ? only one worker can win this race.
        var claimed = await _dbContext.DataAcquisitionLogs
            .Where(l =>
                l.FacilityId == groupInfo.FacilityId
                && l.CorrelationId == groupInfo.CorrelationId
                && l.QueryPhase == groupInfo.QueryPhase
                && l.SiblingCount != null
                && !l.TailSent)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(l => l.TailSent, true)
                .SetProperty(l => l.ModifyDate, DateTime.UtcNow),
                cancellationToken);

        if (claimed == 0)
        {
            return null; // Another worker already sent the tail.
        }

        try
        {
            // Read the data we need for the tail Kafka message.
            var representative = await _dbContext.DataAcquisitionLogs.AsNoTracking()
                .Where(l =>
                    l.FacilityId == groupInfo.FacilityId
                    && l.CorrelationId == groupInfo.CorrelationId
                    && l.QueryPhase == groupInfo.QueryPhase)
                .Select(l => new
                {
                    l.PatientId,
                    l.ReportableEvent,
                    l.TraceId,
                    ScheduledReport = l.ScheduledReportEntity != null ? new ScheduledReport
                    {
                        ReportTrackingId = l.ScheduledReportEntity.ReportTrackingId.ToString().ToLower(),
                        Frequency = l.ScheduledReportEntity.Frequency.HasValue ? l.ScheduledReportEntity.Frequency.Value : default,
                        StartDate = DateTime.SpecifyKind(l.ScheduledReportEntity.StartDate, DateTimeKind.Utc),
                        EndDate = DateTime.SpecifyKind(l.ScheduledReportEntity.EndDate, DateTimeKind.Utc),
                        ReportTypes = l.ScheduledReportEntity.ReportTypes != null
                            ? l.ScheduledReportEntity.ReportTypes.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries).ToList()
                            : new List<string>()
                    } : null,
                })
                .FirstOrDefaultAsync(cancellationToken);

            if (representative == null)
            {
                await RevertTailSentAsync(groupInfo.FacilityId, groupInfo.CorrelationId!, groupInfo.QueryPhase, cancellationToken);
                return null;
            }

            // Only include cache keys for resource types that had at least one resource actually acquired.
            // ResourceId rows use the format "TypeName/id" (e.g. "Patient/abc-123"), so the type name
            // is the substring before the first '/'.
            var acquiredResourceTypes = await _dbContext.DataAcquisitionLogResourceIds
                .Join(_dbContext.DataAcquisitionLogs,
                    rid => rid.DataAcquisitionLogId,
                    l => l.Id,
                    (rid, l) => new { rid, l })
                .Where(x => x.l.FacilityId == groupInfo.FacilityId
                    && x.l.CorrelationId == groupInfo.CorrelationId
                    && x.l.QueryPhase == groupInfo.QueryPhase)
                .Select(x => x.rid.ResourceId.Substring(0, x.rid.ResourceId.IndexOf('/')))
                .Distinct()
                .ToListAsync(cancellationToken);

            return new TailCompletionResult
            {
                FacilityId = groupInfo.FacilityId,
                PatientId = representative.PatientId ?? "",
                CorrelationId = groupInfo.CorrelationId!,
                QueryPhase = groupInfo.QueryPhase,
                TraceParentId = representative.TraceId,
                ResourcesAcquired = new ResourcesAcquired
                {
                    QueryType = QueryPhaseUtilities.ToWireQueryType(groupInfo.QueryPhase),
                    ReportableEvent = representative.ReportableEvent ?? default,
                    ScheduledReports = representative.ScheduledReport != null
                        ? new List<ScheduledReport> { representative.ScheduledReport }
                        : new List<ScheduledReport>(),
                    CacheType = await _resourceCache.GetCacheTypeForCorrelationIdAsync(
                        groupInfo.CorrelationId ?? string.Empty,
                        cancellationToken),
                    CacheKeys = acquiredResourceTypes
                        .Select(rt => $"{groupInfo.CorrelationId}:{rt}")
                        .Distinct()
                        .ToList()
                }
            };
        }
        catch
        {
            await RevertTailSentAsync(groupInfo.FacilityId, groupInfo.CorrelationId!, groupInfo.QueryPhase, CancellationToken.None);
            throw;
        }
    }

    public async Task RevertTailSentAsync(
        string facilityId,
        string correlationId,
        QueryPhase? queryPhase,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(facilityId) || string.IsNullOrWhiteSpace(correlationId) || queryPhase is null)
        {
            return;
        }

        await _dbContext.DataAcquisitionLogs
            .Where(l =>
                l.FacilityId == facilityId
                && l.CorrelationId == correlationId
                && l.QueryPhase == queryPhase
                && l.TailSent)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(l => l.TailSent, false)
                .SetProperty(l => l.ModifyDate, DateTime.UtcNow),
                cancellationToken);
    }
}
