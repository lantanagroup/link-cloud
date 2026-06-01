using System.Diagnostics;
using System.Net.Sockets;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Managers;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Api.Configuration;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Queries;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Services;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Services.Sftp;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Services.Sftp.Processors;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Entities;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models.Enums;
using LantanaGroup.Link.DataAcquisition.Domain.Settings;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Models.Telemetry;
using LantanaGroup.Link.Shared.Application.Services.Security;
using Microsoft.Extensions.Options;

namespace LantanaGroup.Link.DataAcquisition.AcquisitionWorker.Services;

public class SftpAcquisitionHandler(
    ILogger<SftpAcquisitionHandler> logger,
    IServiceScopeFactory serviceScopeFactory,
    IOptions<SftpAcquisitionSettings> settings)
{
    public async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        logger.LogDebug("SftpAcquisitionHandler starting execution");

        try
        {
            using var scope = serviceScopeFactory.CreateScope();
            var logQueries = scope.ServiceProvider.GetRequiredService<ISftpAcquisitionLogQueries>();

            // Sftp acquisition types to process
            var acquisitionTypes = new[] { SftpAcquisitionType.Census };

            var allLogs = new List<SftpAcquisitionLog>();

            foreach (var acquisitionType in acquisitionTypes)
            {
                // Get pending logs for this acquisition type
                var pendingLogs = await logQueries.GetPendingLogsAsync(
                    acquisitionType,
                    settings.Value.MaxBatchSize,
                    cancellationToken);

                // Also get failed logs eligible for retry
                var retryLogs = await logQueries.GetFailedLogsForRetryAsync(
                    acquisitionType,
                    settings.Value.MaxRetryAttempts,
                    settings.Value.MaxBatchSize,
                    cancellationToken);

                allLogs.AddRange(pendingLogs);
                allLogs.AddRange(retryLogs);
            }

            if (allLogs.Count == 0)
            {
                logger.LogDebug("No pending SFTP acquisition logs to process");
                return;
            }

            // Order by scheduled date, then by ID for consistent processing
            allLogs = allLogs
                .OrderBy(x => x.ScheduledDate ?? DateTime.MinValue)
                .ThenBy(x => x.Id)
                .ToList();

            // Group logs by facility to avoid multiple concurrent SFTP connections to the same server
            var logsByFacility = allLogs
                .GroupBy(x => x.FacilityId)
                .ToList();

            // Determine processing mode based on settings and number of facilities
            var useParallel = settings.Value.EnableParallelProcessing
                              && logsByFacility.Count > settings.Value.ParallelProcessingThreshold;

            if (useParallel)
            {
                logger.LogInformation(
                    "Processing {LogCount} SFTP acquisition logs across {FacilityCount} facilities in PARALLEL (threshold: {Threshold}, max concurrency: {MaxConcurrency})",
                    allLogs.Count, logsByFacility.Count, settings.Value.ParallelProcessingThreshold, settings.Value.MaxConcurrency);

                var options = new ParallelOptions
                {
                    MaxDegreeOfParallelism = settings.Value.MaxConcurrency,
                    CancellationToken = cancellationToken
                };

                // Parallelize across facilities, but process logs within each facility sequentially with a shared session
                await Parallel.ForEachAsync(logsByFacility, options, async (facilityGroup, ct) =>
                {
                    // Each parallel task needs its own scope for DbContext thread safety
                    using var innerScope = serviceScopeFactory.CreateScope();
                    await ProcessFacilityLogsAsync(facilityGroup.Key, facilityGroup.ToList(), innerScope.ServiceProvider, ct);
                });
            }
            else
            {
                logger.LogInformation(
                    "Processing {LogCount} SFTP acquisition logs across {FacilityCount} facilities SEQUENTIALLY (threshold: {Threshold}, parallel enabled: {ParallelEnabled})",
                    allLogs.Count, logsByFacility.Count, settings.Value.ParallelProcessingThreshold, settings.Value.EnableParallelProcessing);

                // Process each facility's logs with a shared session
                foreach (var facilityGroup in logsByFacility)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    await ProcessFacilityLogsAsync(facilityGroup.Key, facilityGroup.ToList(), scope.ServiceProvider, cancellationToken);
                }
            }

            logger.LogInformation("SftpAcquisitionHandler completed processing");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error in SftpAcquisitionHandler execution");
            throw;
        }
    }

    private async Task ProcessFacilityLogsAsync(
        string facilityId,
        List<SftpAcquisitionLog> logs,
        IServiceProvider serviceProvider,
        CancellationToken cancellationToken)
    {
        var logManager = serviceProvider.GetRequiredService<ISftpAcquisitionLogManager>();
        var sftpConfigQueries = serviceProvider.GetRequiredService<ISftpConfigurationQueries>();
        var processorFactory = serviceProvider.GetRequiredService<ISftpAcquisitionProcessorFactory>();
        var sftpClientService = serviceProvider.GetRequiredService<ISftpClientService>();

        // Get SFTP configuration for the facility
        var sftpConfig = await sftpConfigQueries.GetByOrganizationIdAsync(facilityId, cancellationToken);
        if (sftpConfig is null)
        {
            logger.LogError("No SFTP configuration found for facility {FacilityId}, skipping {Count} logs",
                facilityId, logs.Count);

            // Mark all logs as requiring configuration - these won't retry automatically
            foreach (var log in logs)
            {
                await logManager.SetConfigurationRequiredAsync(log.Id, $"No SFTP configuration found for facility {facilityId}", cancellationToken);
            }
            return;
        }

        // Open a SFTP session for this facility
        ISftpSession? session;
        try
        {
            session = await sftpClientService.OpenSessionAsync(sftpConfig, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "Failed to open SFTP session for facility {FacilityId}, {Count} logs will be scheduled for retry",
                facilityId, logs.Count);

            // Schedule all logs for retry
            foreach (var log in logs)
            {
                await FailLogWithRetryAsync(log, $"SFTP connection failed: {ex.Message}", logManager, cancellationToken);
            }
            return;
        }

        try
        {
            logger.LogDebug("Opened shared SFTP session for facility {FacilityId}, processing {Count} logs",
                facilityId, logs.Count);

            foreach (var log in logs)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var result = await ProcessLogWithSessionAsync(log, session, sftpConfig, logManager, processorFactory, cancellationToken);

                // If the session may be unhealthy, attempt to reconnect before processing the next log
                if (!result.SessionMayBeUnhealthy || logs.IndexOf(log) >= logs.Count - 1) continue;

                logger.LogWarning(
                    "SFTP session may be unhealthy after processing log {LogId}, attempting to reconnect for facility {FacilityId}",
                    log.Id, facilityId);

                try
                {
                    await session.DisposeAsync();
                }
                catch (Exception ex)
                {
                    logger.LogDebug(ex, "Error disposing unhealthy SFTP session for facility {FacilityId}", facilityId);
                }

                try
                {
                    session = await sftpClientService.OpenSessionAsync(sftpConfig, cancellationToken);
                    logger.LogInformation("Successfully reconnected SFTP session for facility {FacilityId}", facilityId);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex,
                        "Failed to reconnect SFTP session for facility {FacilityId}, remaining logs will be scheduled for retry",
                        facilityId);

                    // Schedule remaining logs for retry
                    var remainingLogs = logs.Skip(logs.IndexOf(log) + 1);
                    foreach (var remainingLog in remainingLogs)
                    {
                        await FailLogWithRetryAsync(remainingLog,
                            $"SFTP session reconnection failed: {ex.Message}", logManager, cancellationToken);
                    }
                    return;
                }
            }
        }
        finally
        {
            await session.DisposeAsync();
        }
    }

    /// <summary>
    /// Result of processing a single log entry.
    /// </summary>
    private readonly record struct LogProcessingResult(bool SessionMayBeUnhealthy);

    /// <summary>
    /// Fails a log with proper retry logic - schedules for retry with exponential backoff
    /// if max retries not reached, otherwise marks as MaxRetriesReached.
    /// </summary>
    private async Task FailLogWithRetryAsync(
        SftpAcquisitionLog log,
        string errorMessage,
        ISftpAcquisitionLogManager logManager,
        CancellationToken cancellationToken)
    {
        var nextRetryAttempt = (log.RetryAttempts ?? 0) + 1;

        if (nextRetryAttempt >= settings.Value.MaxRetryAttempts)
        {
            await logManager.SetMaxRetriesReachedAsync(log.Id, cancellationToken);
        }
        else
        {
            var retryAfter = CalculateRetryTime(nextRetryAttempt);
            await logManager.FailAsync(log.Id, errorMessage, cancellationToken, retryAfter);
        }
    }

    /// <summary>
    /// Calculates the retry time using exponential backoff.
    /// Formula: now + min(BaseRetryDelaySeconds * 2^(attempt-1), MaxRetryDelaySeconds)
    /// </summary>
    private DateTime CalculateRetryTime(int retryAttempt)
    {
        // Exponential backoff: base * 2^(attempt-1)
        var exponent = Math.Max(0, retryAttempt - 1);
        var delaySeconds = settings.Value.BaseRetryDelaySeconds * Math.Pow(2, exponent);

        // Cap at max delay
        delaySeconds = Math.Min(delaySeconds, settings.Value.MaxRetryDelaySeconds);

        return DateTime.UtcNow.AddSeconds(delaySeconds);
    }

    private async Task<LogProcessingResult> ProcessLogWithSessionAsync(
        SftpAcquisitionLog log,
        ISftpSession session,
        SftpConfigurationModel sftpConfig,
        ISftpAcquisitionLogManager logManager,
        ISftpAcquisitionProcessorFactory processorFactory,
        CancellationToken cancellationToken)
    {
        // Build links to originating trace context for async correlation
        var links = new List<ActivityLink>();
        if (!string.IsNullOrEmpty(log.OriginatingTraceId) && !string.IsNullOrEmpty(log.OriginatingSpanId))
        {
            var originatingContext = new ActivityContext(
                ActivityTraceId.CreateFromString(log.OriginatingTraceId),
                ActivitySpanId.CreateFromString(log.OriginatingSpanId),
                ActivityTraceFlags.Recorded);
            links.Add(new ActivityLink(originatingContext));
        }

        using var activity = ServiceActivitySource.Instance.StartActivity(
            "ProcessSftpAcquisitionLog",
            ActivityKind.Internal,
            parentContext: default,
            tags: null,
            links: links);

        activity?.AddTag(DiagnosticNames.DataAcquisitionLogId, log.ExternalId);
        activity?.AddTag(DiagnosticNames.FacilityId, log.FacilityId);

        // Try to claim the log
        var claimed = await logManager.TryClaimForProcessingAsync(log.Id, cancellationToken);
        if (!claimed)
        {
            logger.LogDebug("Log {LogId} already claimed by another worker", log.Id);
            return new LogProcessingResult(SessionMayBeUnhealthy: false);
        }

        try
        {
            // Find the acquisition configuration for this log's type and subtype
            var acquisitionConfig = sftpConfig.AcquisitionConfigurations
                .FirstOrDefault(c => c.AcquisitionType == log.AcquisitionType && c.SubType == log.SubType);

            if (acquisitionConfig is null)
            {
                throw new InvalidOperationException(
                    $"No acquisition configuration found for type {log.AcquisitionType}/{log.SubType} in facility {log.FacilityId}");
            }

            // Initialize benchmarking if enabled for this facility
            var benchmark = sftpConfig.EnableBenchmarking
                ? new SftpBenchmarkCollector(log.RetryAttempts ?? 0)
                : null;

            // Get the appropriate processor for this acquisition type and subtype
            var processor = processorFactory.GetProcessor(log.AcquisitionType, log.SubType);

            // Use the shared SFTP session
            var processedFiles =
                await processor.ProcessWithSessionAsync(log, session, sftpConfig, acquisitionConfig, cancellationToken, benchmark);

            benchmark?.RecordSuccess(true);

            // Complete the log
            await logManager.CompleteAsync(log.Id, processedFiles, GetBenchmarks(benchmark), cancellationToken);

            return new LogProcessingResult(SessionMayBeUnhealthy: false);
        }
        catch (InvalidOperationException ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            activity?.AddException(ex);

            logger.LogError(ex, "Invalid operation for SFTP acquisition log {LogId} for facility {FacilityId}",
                log.Id.SanitizeForLog(), log.FacilityId.SanitizeForLog());

            // InvalidOperationException typically indicates a configuration issue (e.g., missing acquisition config, invalid remote directory)
            await logManager.SetConfigurationRequiredAsync(log.Id, ex.Message, cancellationToken);

            return new LogProcessingResult(SessionMayBeUnhealthy: false);
        }
        catch (NotSupportedException ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            activity?.AddException(ex);

            // NotSupportedException indicates an unsupported acquisition type - configuration issue
            await logManager.SetConfigurationRequiredAsync(log.Id, ex.Message, cancellationToken);

            return new LogProcessingResult(SessionMayBeUnhealthy: false);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error processing SFTP acquisition log {LogId} for facility {FacilityId}",
                log.Id.SanitizeForLog(), log.FacilityId.SanitizeForLog());

            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            activity?.AddException(ex);

            // Use retry logic with exponential backoff
            await FailLogWithRetryAsync(log, ex.Message, logManager, cancellationToken);

            // Check if exception indicates potential session/connection issues
            var sessionMayBeUnhealthy = IsSessionRelatedException(ex);
            return new LogProcessingResult(SessionMayBeUnhealthy: sessionMayBeUnhealthy);
        }
    }

    /// <summary>
    /// Determines if an exception may indicate that the SFTP session is unhealthy
    /// and should be reconnected before processing additional logs.
    /// </summary>
    private static bool IsSessionRelatedException(Exception ex)
    {
        // Check the exception type name to avoid direct dependency on Renci.SshNet
        var exceptionTypeName = ex.GetType().Name;

        // SSH/SFTP connection-related exceptions from Renci.SshNet
        if (exceptionTypeName.Contains("Ssh", StringComparison.OrdinalIgnoreCase) ||
            exceptionTypeName.Contains("Sftp", StringComparison.OrdinalIgnoreCase))
        {
            // These specific exceptions typically don't indicate session problems
            if (exceptionTypeName.Contains("Permission", StringComparison.OrdinalIgnoreCase) ||
                exceptionTypeName.Contains("PathNotFound", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
            return true;
        }

        // Network/IO exceptions that may indicate connection issues
        if (ex is IOException or SocketException)
        {
            return true;
        }

        // Check inner exceptions recursively
        if (ex.InnerException is not null)
        {
            return IsSessionRelatedException(ex.InnerException);
        }

        return false;
    }

    private static List<SftpAcquisitionBenchmark>? GetBenchmarks(SftpBenchmarkCollector? collector)
        => collector?.Build() is { } b ? [b] : null;
}
