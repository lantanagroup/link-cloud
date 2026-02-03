using System.Diagnostics;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Managers;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Queries;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Services;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Services.Sftp;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Entities;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models.Enums;
using LantanaGroup.Link.DataAcquisition.Domain.Settings;
using LantanaGroup.Link.Shared.Application.Models.Telemetry;
using Microsoft.Extensions.Options;
using Quartz;

namespace LantanaGroup.Link.DataAcquisition.AcquisitionWorker.Jobs;

[DisallowConcurrentExecution]
public class SftpAcquisitionProcessingJob(
    ILogger<SftpAcquisitionProcessingJob> logger,
    IServiceScopeFactory serviceScopeFactory,
    IOptions<SftpAcquisitionSettings> settings)
    : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        logger.LogDebug("SftpAcquisitionProcessingJob starting execution");

        try
        {
            using var scope = serviceScopeFactory.CreateScope();
            var logQueries = scope.ServiceProvider.GetRequiredService<ISftpAcquisitionLogQueries>();

            // Census acquisition types to process
            var censusTypes = new[] { SftpAcquisitionType.CernerCensus };

            var allLogs = new List<SftpAcquisitionLog>();

            foreach (var acquisitionType in censusTypes)
            {
                // Get pending logs for this acquisition type
                var pendingLogs = await logQueries.GetPendingLogsAsync(
                    acquisitionType,
                    settings.Value.MaxConcurrency * 2,
                    context.CancellationToken);

                // Also get failed logs eligible for retry
                var retryLogs = await logQueries.GetFailedLogsForRetryAsync(
                    acquisitionType,
                    settings.Value.MaxRetryAttempts,
                    settings.Value.MaxConcurrency,
                    context.CancellationToken);

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

            // Determine processing mode based on settings and backlog size
            var useParallel = settings.Value.EnableParallelProcessing
                              && allLogs.Count > settings.Value.ParallelProcessingThreshold;

            if (useParallel)
            {
                logger.LogInformation(
                    "Processing {Count} SFTP acquisition logs in PARALLEL (threshold: {Threshold}, max concurrency: {MaxConcurrency})",
                    allLogs.Count, settings.Value.ParallelProcessingThreshold, settings.Value.MaxConcurrency);

                var options = new ParallelOptions
                {
                    MaxDegreeOfParallelism = settings.Value.MaxConcurrency,
                    CancellationToken = context.CancellationToken
                };

                await Parallel.ForEachAsync(allLogs, options, async (log, ct) =>
                {
                    // Each parallel task needs its own scope for DbContext thread safety
                    using var innerScope = serviceScopeFactory.CreateScope();
                    await ProcessLogAsync(log, innerScope.ServiceProvider, ct);
                });
            }
            else
            {
                logger.LogInformation(
                    "Processing {Count} SFTP acquisition logs SEQUENTIALLY (threshold: {Threshold}, parallel enabled: {ParallelEnabled})",
                    allLogs.Count, settings.Value.ParallelProcessingThreshold, settings.Value.EnableParallelProcessing);

                // Resolve services once for sequential processing
                var logManager = scope.ServiceProvider.GetRequiredService<ISftpAcquisitionLogManager>();
                var sftpConfigQueries = scope.ServiceProvider.GetRequiredService<ISftpConfigurationQueries>();
                var processorFactory = scope.ServiceProvider.GetRequiredService<ISftpAcquisitionProcessorFactory>();

                foreach (var log in allLogs)
                {
                    context.CancellationToken.ThrowIfCancellationRequested();
                    await ProcessLogAsync(log, logManager, sftpConfigQueries, processorFactory, context.CancellationToken);
                }
            }

            logger.LogInformation("SftpAcquisitionProcessingJob completed processing");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error in SftpAcquisitionProcessingJob execution");
            throw;
        }
    }

    private async Task ProcessLogAsync(SftpAcquisitionLog log, IServiceProvider serviceProvider, CancellationToken cancellationToken)
    {
        // Resolve services from the provided scope (used for parallel processing where each task has its own scope)
        var logManager = serviceProvider.GetRequiredService<ISftpAcquisitionLogManager>();
        var sftpConfigQueries = serviceProvider.GetRequiredService<ISftpConfigurationQueries>();
        var processorFactory = serviceProvider.GetRequiredService<ISftpAcquisitionProcessorFactory>();

        await ProcessLogAsync(log, logManager, sftpConfigQueries, processorFactory, cancellationToken);
    }

    private async Task ProcessLogAsync(
        SftpAcquisitionLog log,
        ISftpAcquisitionLogManager logManager,
        ISftpConfigurationQueries sftpConfigQueries,
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

        activity?.AddTag("log.id", log.ExternalId);
        activity?.AddTag(DiagnosticNames.FacilityId, log.FacilityId);

        // Try to claim the log (optimistic concurrency)
        var claimed = await logManager.TryClaimForProcessingAsync(log.Id, cancellationToken);
        if (!claimed)
        {
            logger.LogDebug("Log {LogId} already claimed by another worker", log.Id);
            return;
        }

        SftpBenchmarkCollector? benchmark = null;
        try
        {
            // Get SFTP configuration
            var sftpConfig = await sftpConfigQueries.GetByOrganizationIdAsync(log.FacilityId, cancellationToken);
            if (sftpConfig is null)
            {
                throw new InvalidOperationException($"No SFTP configuration found for facility {log.FacilityId}");
            }

            // Find the acquisition configuration for this log's type
            var acquisitionConfig = sftpConfig.AcquisitionConfigurations
                .FirstOrDefault(c => c.AcquisitionType == log.AcquisitionType);

            if (acquisitionConfig is null)
            {
                throw new InvalidOperationException(
                    $"No acquisition configuration found for type {log.AcquisitionType} in facility {log.FacilityId}");
            }

            // Initialize benchmarking if enabled for this facility
            benchmark = sftpConfig.EnableBenchmarking
                ? new SftpBenchmarkCollector(log.RetryAttempts ?? 0)
                : null;

            benchmark?.StartConnectionAndRetrieval();

            // Get the appropriate processor for this acquisition type
            var processor = processorFactory.GetProcessor(log.AcquisitionType);

            var processedFiles = await processor.ProcessAsync(log, sftpConfig, acquisitionConfig, cancellationToken);

            benchmark?.EndConnectionAndRetrieval();
            benchmark?.RecordSuccess(true);

            // Complete the log
            await logManager.CompleteAsync(log.Id, processedFiles, GetBenchmarks(benchmark), cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error processing SFTP acquisition log {LogId} for facility {FacilityId}",
                log.Id, log.FacilityId);

            // Check if max retries reached
            if ((log.RetryAttempts ?? 0) + 1 >= settings.Value.MaxRetryAttempts)
            {
                await logManager.SetMaxRetriesReachedAsync(log.Id, cancellationToken);
            }
            else
            {
                await logManager.FailAsync(log.Id, ex.Message, cancellationToken);
            }
        }
    }

    private static List<SftpAcquisitionBenchmark>? GetBenchmarks(SftpBenchmarkCollector? collector)
        => collector?.Build() is { } b ? [b] : null;
}
