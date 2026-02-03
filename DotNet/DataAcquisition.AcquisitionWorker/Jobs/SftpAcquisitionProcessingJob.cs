using LantanaGroup.Link.DataAcquisition.Domain.Application.Managers;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Queries;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Services;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Services.Sftp;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Entities;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models.Enums;
using LantanaGroup.Link.DataAcquisition.Domain.Settings;
using Microsoft.Extensions.Options;
using Quartz;

namespace LantanaGroup.Link.DataAcquisition.AcquisitionWorker.Jobs;

[DisallowConcurrentExecution]
public class SftpAcquisitionProcessingJob : IJob
{
    private readonly ILogger<SftpAcquisitionProcessingJob> _logger;
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly IOptions<SftpAcquisitionSettings> _settings;

    public SftpAcquisitionProcessingJob(
        ILogger<SftpAcquisitionProcessingJob> logger,
        IServiceScopeFactory serviceScopeFactory,
        IOptions<SftpAcquisitionSettings> settings)
    {
        _logger = logger;
        _serviceScopeFactory = serviceScopeFactory;
        _settings = settings;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        _logger.LogDebug("SftpAcquisitionProcessingJob starting execution");

        try
        {
            using var scope = _serviceScopeFactory.CreateScope();
            var logQueries = scope.ServiceProvider.GetRequiredService<ISftpAcquisitionLogQueries>();

            // Census acquisition types to process
            var censusTypes = new[] { SftpAcquisitionType.CernerCensus, SftpAcquisitionType.Census };

            var allLogs = new List<SftpAcquisitionLog>();

            foreach (var acquisitionType in censusTypes)
            {
                // Get pending logs for this acquisition type
                var pendingLogs = await logQueries.GetPendingLogsAsync(
                    acquisitionType,
                    _settings.Value.MaxConcurrency * 2,
                    context.CancellationToken);

                // Also get failed logs eligible for retry
                var retryLogs = await logQueries.GetFailedLogsForRetryAsync(
                    acquisitionType,
                    _settings.Value.MaxRetryAttempts,
                    _settings.Value.MaxConcurrency,
                    context.CancellationToken);

                allLogs.AddRange(pendingLogs);
                allLogs.AddRange(retryLogs);
            }

            // Order by scheduled date, then by ID for consistent processing
            allLogs = allLogs
                .OrderBy(x => x.ScheduledDate ?? DateTime.MinValue)
                .ThenBy(x => x.Id)
                .ToList();

            if (allLogs.Count == 0)
            {
                _logger.LogDebug("No pending SFTP acquisition logs to process");
                return;
            }

            // Determine processing mode based on settings and backlog size
            var useParallel = _settings.Value.EnableParallelProcessing
                              && allLogs.Count > _settings.Value.ParallelProcessingThreshold;

            if (useParallel)
            {
                _logger.LogInformation(
                    "Processing {Count} SFTP acquisition logs in PARALLEL (threshold: {Threshold}, max concurrency: {MaxConcurrency})",
                    allLogs.Count, _settings.Value.ParallelProcessingThreshold, _settings.Value.MaxConcurrency);

                var options = new ParallelOptions
                {
                    MaxDegreeOfParallelism = _settings.Value.MaxConcurrency,
                    CancellationToken = context.CancellationToken
                };

                await Parallel.ForEachAsync(allLogs, options, async (log, ct) =>
                {
                    // Each parallel task needs its own scope for DbContext thread safety
                    using var innerScope = _serviceScopeFactory.CreateScope();
                    await ProcessLogAsync(log, innerScope.ServiceProvider, ct);
                });
            }
            else
            {
                _logger.LogInformation(
                    "Processing {Count} SFTP acquisition logs SEQUENTIALLY (threshold: {Threshold}, parallel enabled: {ParallelEnabled})",
                    allLogs.Count, _settings.Value.ParallelProcessingThreshold, _settings.Value.EnableParallelProcessing);

                foreach (var log in allLogs)
                {
                    context.CancellationToken.ThrowIfCancellationRequested();
                    await ProcessLogAsync(log, scope.ServiceProvider, context.CancellationToken);
                }
            }

            _logger.LogInformation("SftpAcquisitionProcessingJob completed processing");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in SftpAcquisitionProcessingJob execution");
            throw;
        }
    }

    private async Task ProcessLogAsync(
        SftpAcquisitionLog log,
        IServiceProvider serviceProvider,
        CancellationToken cancellationToken)
    {
        var logManager = serviceProvider.GetRequiredService<ISftpAcquisitionLogManager>();
        var sftpConfigQueries = serviceProvider.GetRequiredService<ISftpConfigurationQueries>();
        var processorFactory = serviceProvider.GetRequiredService<ISftpAcquisitionProcessorFactory>();

        // Try to claim the log (optimistic concurrency)
        var claimed = await logManager.TryClaimForProcessingAsync(log.Id, cancellationToken);
        if (!claimed)
        {
            _logger.LogDebug("Log {LogId} already claimed by another worker", log.Id);
            return;
        }

        SftpBenchmarkCollector? benchmark = null;
        var processedFiles = new List<string>();

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

            // Process the log - need to convert model back to entity for processor
            var sftpConfigEntity = new SftpConfiguration
            {
                Id = sftpConfig.Id,
                OrganizationId = sftpConfig.OrganizationId,
                Host = sftpConfig.Host,
                Port = sftpConfig.Port,
                RemoteDirectory = sftpConfig.RemoteDirectory,
                Timeout = sftpConfig.Timeout,
                RemoveAfterProcessing = sftpConfig.RemoveAfterProcessing,
                AuthenticationProtocol = sftpConfig.AuthenticationProtocol,
                EnableBenchmarking = sftpConfig.EnableBenchmarking,
                AcquisitionConfigurations = sftpConfig.AcquisitionConfigurations
            };

            processedFiles = await processor.ProcessAsync(log, sftpConfigEntity, acquisitionConfig, cancellationToken);

            benchmark?.EndConnectionAndRetrieval();
            benchmark?.RecordSuccess(true);

            // Complete the log
            await logManager.CompleteAsync(log.Id, processedFiles, GetBenchmarks(benchmark), cancellationToken);
        }
        catch (Exception ex)
        {
            benchmark?.RecordSuccess(false);
            _logger.LogError(ex, "Error processing SFTP acquisition log {LogId} for facility {FacilityId}",
                log.Id, log.FacilityId);

            // Check if max retries reached
            if ((log.RetryAttempts ?? 0) + 1 >= _settings.Value.MaxRetryAttempts)
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
