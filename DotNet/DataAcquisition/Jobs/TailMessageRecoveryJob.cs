using Confluent.Kafka;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Managers;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Kafka;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Queries;
using LantanaGroup.Link.DataAcquisition.Domain.Settings;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Models.Kafka;
using LantanaGroup.Link.Shared.Application.Services.Security;
using Microsoft.Extensions.Options;
using Quartz;
using System.Diagnostics;
using System.Text;

namespace LantanaGroup.Link.DataAcquisition.Jobs;

/// <summary>
/// Infrequent, lightweight safety-net for tail completion.
/// Checks older groups in small batches and publishes missing
/// ResourceAcquired completion messages.
/// </summary>
[DisallowConcurrentExecution]
public class TailMessageRecoveryJob : IJob
{
    private readonly ILogger<TailMessageRecoveryJob> _logger;
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly IProducer<ResourceKey, ResourcesAcquired> _resourceAcquiredProducer;
    private readonly TailMessageRecoveryJobSettings _settings;

    public TailMessageRecoveryJob(
        ILogger<TailMessageRecoveryJob> logger,
        IServiceScopeFactory serviceScopeFactory,
        IProducer<ResourceKey, ResourcesAcquired> resourceAcquiredProducer,
        IOptions<TailMessageRecoveryJobSettings> settings)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _serviceScopeFactory = serviceScopeFactory ?? throw new ArgumentNullException(nameof(serviceScopeFactory));
        _resourceAcquiredProducer = resourceAcquiredProducer ?? throw new ArgumentNullException(nameof(resourceAcquiredProducer));
        _settings = settings?.Value ?? new TailMessageRecoveryJobSettings();
    }

    public async Task Execute(IJobExecutionContext context)
    {
        var stopwatch = Stopwatch.StartNew();
        var minAge = TimeSpan.FromMinutes(_settings.MinAgeMinutes);

        using var scope = _serviceScopeFactory.CreateScope();
        var dataAcquisitionLogQueries = scope.ServiceProvider.GetRequiredService<IDataAcquisitionLogQueries>();
        var dataAcquisitionLogManager = scope.ServiceProvider.GetRequiredService<IDataAcquisitionLogManager>();

        _logger.LogInformation(
            "TailMessageRecoveryJob started. Looking for orphaned tail logs older than {MinAge}. Time budget: {TimeBudget} seconds.",
            minAge,
            _settings.TimeBudgetPerRunSeconds);
            
        try
        {
            var orphanedLogIds = await dataAcquisitionLogQueries.GetOrphanedTailLogIds(
                minAge,
                _settings.MaxGroupsPerRun,
                context.CancellationToken);

            int processed = 0;
            foreach (var logId in orphanedLogIds)
            {
                if (stopwatch.Elapsed.TotalSeconds >= _settings.TimeBudgetPerRunSeconds)
                {
                    _logger.LogDebug(
                        "TailMessageRecoveryJob time budget exhausted after {Elapsed:F1}s. Processed {Processed}/{Total} groups.",
                        stopwatch.Elapsed.TotalSeconds,
                        processed,
                        orphanedLogIds.Count);
                    break;
                }

                try
                {
                    var tailResult = await dataAcquisitionLogManager.TryCompleteTailAsync(logId, context.CancellationToken);
                    if (tailResult == null)
                    {
                        processed++;
                        continue;
                    }

                    var headers = new Headers
                    {
                        new Header(DataAcquisitionConstants.HeaderNames.CorrelationId,
                            Encoding.UTF8.GetBytes(tailResult.CorrelationId))
                    };

                    if (!string.IsNullOrEmpty(tailResult.TraceParentId))
                    {
                        headers.Add("traceparent", Encoding.UTF8.GetBytes(tailResult.TraceParentId));
                    }

                    await _resourceAcquiredProducer.ProduceAsync(
                        KafkaTopic.ResourcesAcquired.ToString(),
                        new Message<ResourceKey, ResourcesAcquired>
                        {
                            Key = new ResourceKey
                            {
                                FacilityId = tailResult.FacilityId,
                                PatientId = tailResult.PatientId
                            },
                            Headers = headers,
                            Value = tailResult.ResourcesAcquired
                        },
                        context.CancellationToken);

                    _logger.LogInformation(
                        "TailMessageRecoveryJob recovered tail for FacilityId={FacilityId}, PatientId={PatientId}",
                        tailResult.FacilityId.SanitizeForLog(),
                        tailResult.PatientId.SanitizeForLog());

                    processed++;
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "TailMessageRecoveryJob failed recovering orphaned tail for LogId {LogId}.", logId);
                    processed++;
                }
            }

            _resourceAcquiredProducer.Flush(context.CancellationToken);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("TailMessageRecoveryJob was cancelled.");
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "TailMessageRecoveryJob encountered an error.");
        }
    }
}
