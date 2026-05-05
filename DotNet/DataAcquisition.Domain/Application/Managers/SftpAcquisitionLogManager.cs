using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Api.QueryLog;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Exceptions;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Context;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models.Exceptions;
using LantanaGroup.Link.Shared.Application.Models.Integration.DataAcquisition;
using RequestStatus = LantanaGroup.Link.Shared.Application.Models.Integration.DataAcquisition.RequestStatus;
using QueryPhase = LantanaGroup.Link.Shared.Application.Models.Integration.DataAcquisition.QueryPhase;
using FhirQueryType = LantanaGroup.Link.Shared.Application.Models.Integration.DataAcquisition.FhirQueryType;
using LantanaGroup.Link.Shared.Application.Models.Telemetry;
using LantanaGroup.Link.Shared.Application.SerDes;
using LantanaGroup.Link.Shared.Application.Services.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Text.Json;

namespace LantanaGroup.Link.DataAcquisition.Domain.Application.Managers;

/// <summary>
/// Manages the lifecycle of SFTP acquisition log entries including creation, updates,
/// status transitions, and processing coordination across distributed workers.
/// </summary>
public interface ISftpAcquisitionLogManager
{
    /// <summary>
    /// Creates a new SFTP acquisition log entry.
    /// </summary>
    /// <param name="model">The log model containing facility, acquisition type, and scheduling information.</param>
    /// <param name="cancellationToken">Token to cancel the asynchronous operation.</param>
    /// <returns>The created log model with generated identifiers.</returns>
    /// <exception cref="MissingFacilityIdException">Thrown when the facility ID is not provided.</exception>
    Task<SftpAcquisitionLogModel> CreateAsync(SftpAcquisitionLogModel model, CancellationToken cancellationToken);

    /// <summary>
    /// Updates an existing SFTP acquisition log entry.
    /// </summary>
    /// <param name="model">The log model with updated values.</param>
    /// <param name="cancellationToken">Token to cancel the asynchronous operation.</param>
    /// <returns>The updated log model.</returns>
    /// <exception cref="MissingFacilityIdException">Thrown when the facility ID is not provided.</exception>
    /// <exception cref="DomainEntityNotFoundException">Thrown when the log entry does not exist.</exception>
    Task<SftpAcquisitionLogModel> UpdateAsync(SftpAcquisitionLogModel model, CancellationToken cancellationToken);

    /// <summary>
    /// Deletes an SFTP acquisition log entry.
    /// </summary>
    /// <param name="id">The internal database ID of the log to delete.</param>
    /// <param name="cancellationToken">Token to cancel the asynchronous operation.</param>
    /// <exception cref="NotFoundException">Thrown when the log entry does not exist.</exception>
    Task DeleteAsync(long id, CancellationToken cancellationToken);

    /// <summary>
    /// Attempts to claim a pending log for processing using optimistic concurrency.
    /// This prevents multiple workers from processing the same log simultaneously.
    /// </summary>
    /// <param name="id">The internal database ID of the log to claim.</param>
    /// <param name="cancellationToken">Token to cancel the asynchronous operation.</param>
    /// <returns><c>true</c> if the log was successfully claimed; <c>false</c> if already claimed by another worker.</returns>
    Task<bool> TryClaimForProcessingAsync(long id, CancellationToken cancellationToken);

    /// <summary>
    /// Marks a log as successfully completed with processing results.
    /// </summary>
    /// <param name="id">The internal database ID of the log to complete.</param>
    /// <param name="fileNames">The list of file names that were successfully processed.</param>
    /// <param name="benchmarks">Optional performance benchmarks collected during processing.</param>
    /// <param name="cancellationToken">Token to cancel the asynchronous operation.</param>
    Task CompleteAsync(long id, List<string> fileNames, List<SftpAcquisitionBenchmark>? benchmarks, CancellationToken cancellationToken);

    /// <summary>
    /// Marks a log as failed, increments the retry counter, and optionally schedules a future retry.
    /// </summary>
    /// <param name="id">The internal database ID of the log.</param>
    /// <param name="errorMessage">The error message describing the failure.</param>
    /// <param name="cancellationToken">Token to cancel the asynchronous operation.</param>
    /// <param name="retryAfter">
    /// Optional scheduled time for retry. If specified, the log will not be eligible for processing
    /// until this time. If <c>null</c>, the log is eligible for immediate retry.
    /// </param>
    Task FailAsync(long id, string errorMessage, CancellationToken cancellationToken, DateTime? retryAfter = null);

    /// <summary>
    /// Marks a log as having exhausted all retry attempts.
    /// Logs in this state will not be picked up for further processing.
    /// </summary>
    /// <param name="id">The internal database ID of the log.</param>
    /// <param name="cancellationToken">Token to cancel the asynchronous operation.</param>
    Task SetMaxRetriesReachedAsync(long id, CancellationToken cancellationToken);

    /// <summary>
    /// Marks a log as requiring configuration before it can be processed.
    /// Logs in this state will not be picked up for automatic retry and require
    /// manual intervention to fix the configuration issue.
    /// </summary>
    /// <param name="id">The internal database ID of the log.</param>
    /// <param name="errorMessage">The error message describing the missing configuration.</param>
    /// <param name="cancellationToken">Token to cancel the asynchronous operation.</param>
    Task SetConfigurationRequiredAsync(long id, string errorMessage, CancellationToken cancellationToken);

    /// <summary>
    /// Resets a log to pending status for retry after configuration has been fixed.
    /// This operation is only allowed for logs in <see cref="RequestStatus.ConfigurationRequired"/>
    /// or <see cref="RequestStatus.MaxRetriesReached"/> status.
    /// </summary>
    /// <param name="id">The internal database ID of the log.</param>
    /// <param name="cancellationToken">Token to cancel the asynchronous operation.</param>
    /// <returns>
    /// <c>true</c> if the log was successfully reset;
    /// <c>false</c> if the log was not found or is not in a resettable state.
    /// </returns>
    Task<bool> ResetForRetryAsync(long id, CancellationToken cancellationToken);
}


/// <summary>
/// Manages SFTP acquisition log entries in the database.
/// </summary>
public class SftpAcquisitionLogManager(ILogger<SftpAcquisitionLogManager> logger, IDatabase database, DataAcquisitionDbContext dbContext) : ISftpAcquisitionLogManager
{
    /// <inheritdoc/>
    public async Task<SftpAcquisitionLogModel> CreateAsync(SftpAcquisitionLogModel model, CancellationToken cancellationToken = default)
    {
        using var activity = Activity.Current?.Source.StartActivity();
        activity?.AddTag(DiagnosticNames.FacilityId, model.FacilityId);

        if (string.IsNullOrEmpty(model.FacilityId))
        {
            activity?.SetStatus(ActivityStatusCode.Error, "FacilityId cannot be empty or null");
            logger.LogError("Sftp Acquisition Log FacilityId cannot be empty or null");
            throw new MissingFacilityIdException($"A facility must be specified in order to create a SFTP Acquisition Log: Missing {nameof(model.FacilityId)}");
        }

        try
        {
            var entity = model.ToDomain();

            // Default ScheduledDate to now if not provided (immediate processing)
            entity.ScheduledDate ??= DateTime.UtcNow;

            if (model.ProcessDate.HasValue)
            {
                entity.ProcessDate = model.ProcessDate.Value.ToUniversalTime();
            }

            // Capture the current trace context
            entity.OriginatingTraceId = Activity.Current?.TraceId.ToString();
            entity.OriginatingSpanId = Activity.Current?.SpanId.ToString();

            await database.SftpAcquisitionLogRepository.AddAsync(entity, cancellationToken);
            await database.SaveChangesAsync();

            logger.LogInformation("Created SFTP Acquisition Log for FacilityId: {FacilityId}, FileNames: {FileNames}", model.FacilityId.Sanitize(), string.Join(", ", model.FileNames));

            return entity.ToModel();
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            activity?.AddTag(DiagnosticNames.StackTrace, ex.StackTrace);
            logger.LogError(ex, "Error creating SFTP Acquisition Log for FacilityId: {FacilityId}, FileNames: {FileNames}", model.FacilityId.Sanitize(), string.Join(", ", model.FileNames));
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task<SftpAcquisitionLogModel> UpdateAsync(SftpAcquisitionLogModel model, CancellationToken cancellationToken = default)
    {
        using var activity = Activity.Current?.Source.StartActivity();
        activity?.SetTag(DiagnosticNames.FacilityId, model.FacilityId);


        if (string.IsNullOrEmpty(model.FacilityId))
        {
            activity?.SetStatus(ActivityStatusCode.Error, "FacilityId cannot be empty or null");
            logger.LogError("Sftp Acquisition Log FacilityId cannot be empty or null");
            throw new MissingFacilityIdException(nameof(model.FacilityId));
        }

        var existingLog = await database.SftpAcquisitionLogRepository.FirstOrDefaultAsync(x => x.ExternalId == model.ExternalId, cancellationToken);

        if (existingLog is null)
        {
            activity?.SetStatus(ActivityStatusCode.Error, "SFTP Acquisition log not found");
            throw new DomainEntityNotFoundException($"SFTP Acquisition log with ID {model.ExternalId} not found.");
        }

        try
        {
            // Update existing log entry
            existingLog.FileNames = model.FileNames;
            existingLog.RetryAttempts = model.RetryAttempts;
            existingLog.Notes = model.Notes;

            if (model.ProcessDate.HasValue)
            {
                existingLog.ProcessDate = model.ProcessDate.Value.ToUniversalTime();
            }

            await database.SaveChangesAsync();

            return existingLog.ToModel();
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            activity?.SetTag("SftpAcquisitionLog", JsonSerializer.Serialize(model, LinkFhirSerializerOptions.ActivityTagging));
            activity?.AddTag(DiagnosticNames.StackTrace, ex.StackTrace);
            logger.LogError(ex, "Error updating SFTP Acquisition Log for FacilityId: {FacilityId}, FileNames: {FileNames}", model.FacilityId, string.Join(", ", model.FileNames));
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task DeleteAsync(long id, CancellationToken cancellationToken = default)
    {
        using var activity = Activity.Current?.Source.StartActivity();
        activity?.SetTag(DiagnosticNames.EntityId, id);

        // Check if log exists
        var existingLog = await database.SftpAcquisitionLogRepository.GetAsync(id, cancellationToken);

        if (existingLog is null)
        {
            activity?.SetStatus(ActivityStatusCode.Error, "SFTP Acquisition log not found");
            throw new NotFoundException($"SFTP Acquisition log with ID {id} not found.");
        }

        try
        {
            database.SftpAcquisitionLogRepository.Remove(existingLog);
            await database.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            activity?.AddTag(DiagnosticNames.StackTrace, ex.StackTrace);
            logger.LogError(ex, "Error deleting SFTP Acquisition Log with ID: {Id}", id);
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task<bool> TryClaimForProcessingAsync(long id, CancellationToken cancellationToken)
    {
        using var activity = Activity.Current?.Source.StartActivity();
        activity?.SetTag(DiagnosticNames.EntityId, id);

        var now = DateTime.UtcNow;

        // Atomic update that claims the log only if it is still in an eligible state.
        var rowsAffected = await dbContext.Database.ExecuteSqlAsync(
            $"""
            UPDATE SftpAcquisitionLog
            SET Status = 'Processing', ProcessDate = {now}
            WHERE Id = {id}
              AND (Status = 'Pending' OR (Status = 'Failed' AND (ScheduledDate IS NULL OR ScheduledDate <= {now})))
            """,
            cancellationToken);

        if (rowsAffected > 0)
        {
            logger.LogDebug("Claimed SFTP acquisition log {LogId} for processing", id);
            return true;
        }

        logger.LogDebug("SFTP acquisition log {LogId} already claimed by another worker", id);
        return false;
    }

    /// <inheritdoc/>
    public async Task CompleteAsync(long id, List<string> fileNames, List<SftpAcquisitionBenchmark>? benchmarks, CancellationToken cancellationToken)
    {
        using var activity = Activity.Current?.Source.StartActivity();
        activity?.SetTag(DiagnosticNames.EntityId, id);

        var log = await database.SftpAcquisitionLogRepository.GetAsync(id, cancellationToken);
        if (log is null)
        {
            logger.LogWarning("SFTP acquisition log {LogId} not found for completion", id);
            return;
        }

        log.Status = RequestStatus.Completed;
        log.FileNames = fileNames;
        log.Benchmarks = benchmarks;
        log.Notes.Add($"[{DateTime.UtcNow:O}] Processing completed successfully. Files: {string.Join(", ", fileNames)}");

        await database.SaveChangesAsync();
        logger.LogInformation("Completed SFTP acquisition log {LogId} with {FileCount} files", id, fileNames.Count);
    }

    /// <inheritdoc/>
    public async Task FailAsync(long id, string errorMessage, CancellationToken cancellationToken, DateTime? retryAfter = null)
    {
        using var activity = Activity.Current?.Source.StartActivity();
        activity?.SetTag(DiagnosticNames.EntityId, id);

        var log = await database.SftpAcquisitionLogRepository.GetAsync(id, cancellationToken);
        if (log is null)
        {
            logger.LogWarning("SFTP acquisition log {LogId} not found for failure update", id);
            return;
        }

        log.Status = RequestStatus.Failed;
        log.RetryAttempts = (log.RetryAttempts ?? 0) + 1;

        if (retryAfter.HasValue)
        {
            log.ScheduledDate = retryAfter.Value.ToUniversalTime();
            log.Notes.Add($"[{DateTime.UtcNow:O}] Processing failed (attempt {log.RetryAttempts}). Retry scheduled for {log.ScheduledDate:O}");
        }
        else
        {
            log.Notes.Add($"[{DateTime.UtcNow:O}] Processing failed (attempt {log.RetryAttempts})");
        }

        await database.SaveChangesAsync();

        if (retryAfter.HasValue)
        {
            logger.LogWarning("Failed SFTP acquisition log {LogId}, attempt {Attempt}: {Error}. Retry scheduled for {RetryAfter:O}",
                id, log.RetryAttempts, errorMessage, retryAfter.Value);
        }
        else
        {
            logger.LogWarning("Failed SFTP acquisition log {LogId}, attempt {Attempt}: {Error}", id, log.RetryAttempts, errorMessage);
        }
    }

    /// <inheritdoc/>
    public async Task SetMaxRetriesReachedAsync(long id, CancellationToken cancellationToken)
    {
        using var activity = Activity.Current?.Source.StartActivity();
        activity?.SetTag(DiagnosticNames.EntityId, id);

        var log = await database.SftpAcquisitionLogRepository.GetAsync(id, cancellationToken);
        if (log is null)
        {
            logger.LogWarning("SFTP acquisition log {LogId} not found for max retries update", id);
            return;
        }

        log.Status = RequestStatus.MaxRetriesReached;
        log.Notes.Add($"[{DateTime.UtcNow:O}] Maximum retry attempts reached.");

        await database.SaveChangesAsync();
        logger.LogError("SFTP acquisition log {LogId} reached maximum retry attempts", id);
    }

    /// <inheritdoc/>
    public async Task SetConfigurationRequiredAsync(long id, string errorMessage, CancellationToken cancellationToken)
    {
        using var activity = Activity.Current?.Source.StartActivity();
        activity?.SetTag(DiagnosticNames.EntityId, id);

        var log = await database.SftpAcquisitionLogRepository.GetAsync(id, cancellationToken);
        if (log is null)
        {
            logger.LogWarning("SFTP acquisition log {LogId} not found for configuration required update", id);
            return;
        }

        log.Status = RequestStatus.ConfigurationRequired;

        await database.SaveChangesAsync();
        logger.LogWarning("SFTP acquisition log {LogId} marked as configuration required: {Error}", id.SanitizeForLog(), errorMessage.SanitizeForLog());
    }

    /// <inheritdoc/>
    public async Task<bool> ResetForRetryAsync(long id, CancellationToken cancellationToken)
    {
        using var activity = Activity.Current?.Source.StartActivity();
        activity?.SetTag(DiagnosticNames.EntityId, id);

        var log = await database.SftpAcquisitionLogRepository.GetAsync(id, cancellationToken);
        if (log is null)
        {
            logger.LogWarning("SFTP acquisition log {LogId} not found for reset", id);
            return false;
        }

        // Only allow reset from terminal states that require intervention
        if (log.Status != RequestStatus.ConfigurationRequired && log.Status != RequestStatus.MaxRetriesReached)
        {
            logger.LogWarning(
                "SFTP acquisition log {LogId} cannot be reset from status {Status}. Only ConfigurationRequired or MaxRetriesReached logs can be reset.",
                id, log.Status);
            return false;
        }

        var previousStatus = log.Status;
        log.Status = RequestStatus.Pending;
        log.RetryAttempts = 0;
        log.ScheduledDate = null;
        log.Notes.Add($"[{DateTime.UtcNow:O}] Reset for retry from {previousStatus} status.");

        await database.SaveChangesAsync();
        logger.LogInformation("SFTP acquisition log {LogId} reset for retry from {PreviousStatus} status", id, previousStatus);
        return true;
    }
}