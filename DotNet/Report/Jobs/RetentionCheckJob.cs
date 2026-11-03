using System.Diagnostics;
using System.Globalization;
using LantanaGroup.Link.Report.Application.Interfaces;
using LantanaGroup.Link.Report.Domain.Models;
using LantanaGroup.Link.Shared.Application.Models;
using Quartz;

namespace LantanaGroup.Link.Report.Jobs;

/// <summary>
/// The daily retention job. It runs once across all Report instances and hands each firing to
/// <see cref="IRetentionCheckWorker"/>, which does the work.
/// </summary>
/// <remarks>
/// A failed check is not refired; the next day's firing runs it again. A firing missed while every instance
/// was down runs once at recovery and reports the recovery instant as its scheduled fire time, so
/// <see cref="RetentionCheckRun.ScheduledFireTimeUtc"/> is not always the cron time and must not be used to
/// compute retention ages. See dev-docs/retention-schedule.md.
/// </remarks>
[DisallowConcurrentExecution]
public class RetentionCheckJob : IJob
{
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly ILogger<RetentionCheckJob> _logger;

    /// <summary>
    /// Creates the job. Quartz's job factory builds it from the container.
    /// </summary>
    public RetentionCheckJob(IServiceScopeFactory serviceScopeFactory, ILogger<RetentionCheckJob> logger)
    {
        _serviceScopeFactory = serviceScopeFactory;
        _logger = logger;
    }

    /// <summary>
    /// Runs the retention check for this firing.
    /// </summary>
    public async Task Execute(IJobExecutionContext context)
    {
        using var activity = ServiceActivitySource.Instance?
            .StartActivity($"{nameof(RetentionCheckJob)}.{nameof(Execute)}");

        var run = new RetentionCheckRun
        {
            ScheduledFireTimeUtc = ResolveScheduledFireTime(context),
            IsRecovery = context.Recovering,
            FireInstanceId = context.FireInstanceId
        };

        _logger.LogInformation(
            "Retention check job started for the firing due at {ScheduledFireTimeUtc:O} " +
            "(recovery {IsRecovery}, fire instance {FireInstanceId}).",
            run.ScheduledFireTimeUtc,
            run.IsRecovery,
            run.FireInstanceId);

        var stopwatch = Stopwatch.StartNew();

        try
        {
            using var scope = _serviceScopeFactory.CreateScope();
            var retentionCheck = scope.ServiceProvider.GetRequiredService<IRetentionCheckWorker>();

            await retentionCheck.RunAsync(run, context.CancellationToken);
        }
        catch (OperationCanceledException) when (context.CancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            activity?.AddException(ex);

            _logger.LogError(ex,
                "Retention check job failed for the firing due at {ScheduledFireTimeUtc:O} " +
                "(fire instance {FireInstanceId}); it will run again at the next scheduled firing.",
                run.ScheduledFireTimeUtc,
                run.FireInstanceId);

            throw new JobExecutionException(ex, refireImmediately: false);
        }

        _logger.LogInformation(
            "Retention check job finished for the firing due at {ScheduledFireTimeUtc:O} in {ElapsedMilliseconds} ms.",
            run.ScheduledFireTimeUtc,
            stopwatch.ElapsedMilliseconds);
    }

    private static DateTimeOffset ResolveScheduledFireTime(IJobExecutionContext context)
    {
        var scheduledUtc = context.ScheduledFireTimeUtc ?? context.FireTimeUtc;

        if (!context.Recovering)
        {
            return scheduledUtc;
        }

        // Quartz's cluster recovery writes only the key FailedJobOriginalTriggerFiretime 
        // to the job data map, so we have to parse it back to a DateTimeOffset.
        var dataMap = context.MergedJobDataMap;

        if (!dataMap.TryGetString(SchedulerConstants.FailedJobOriginalTriggerFiretime, out var original))
        {
            return scheduledUtc;
        }

        var parsed = DateTimeOffset.TryParse(original,
                                             CultureInfo.InvariantCulture,
                                             DateTimeStyles.AssumeUniversal,
                                             out var originalUtc);

        return parsed ? originalUtc : scheduledUtc;
    }
}
