using LantanaGroup.Link.Report.Application.Options;
using LantanaGroup.Link.Report.Settings;
using LantanaGroup.Link.Shared.Application.Services.Security;
using Microsoft.Extensions.Options;
using Quartz;

namespace LantanaGroup.Link.Report.Jobs;

/// <summary>
/// Keeps one durable, recoverable <see cref="RetentionCheckJob"/> with one UTC cron trigger in the clustered
/// Quartz store, or none when the schedule is disabled.
/// </summary>
public class RetentionScheduleReconciler : IRetentionScheduleReconciler
{
    private readonly ISchedulerFactory _schedulerFactory;
    private readonly IOptions<RetentionScheduleSettings> _settings;
    private readonly ILogger<RetentionScheduleReconciler> _logger;

    /// <summary>
    /// Creates the reconciler.
    /// </summary>
    public RetentionScheduleReconciler(ISchedulerFactory schedulerFactory,
                                       IOptions<RetentionScheduleSettings> settings,
                                       ILogger<RetentionScheduleReconciler> logger)
    {
        _schedulerFactory = schedulerFactory;
        _settings = settings;
        _logger = logger;
    }

    /// <summary>
    /// Creates, reschedules or removes the retention job to match Retention:Schedule.
    /// </summary>
    public async Task ReconcileAsync(CancellationToken cancellationToken)
    {
        var settings = _settings.Value;
        var scheduler = await _schedulerFactory.GetScheduler(cancellationToken);
        var jobKey = new JobKey(ReportConstants.RetentionScheduler.JobName, ReportConstants.RetentionScheduler.Group);

        if (!settings.Enabled)
        {
            if (await scheduler.DeleteJob(jobKey, cancellationToken))
            {
                _logger.LogInformation("Retention schedule is disabled; removed the retention job and its trigger.");
            }

            return;
        }

        if (!settings.CronIsValid)
        {
            _logger.LogError(
                "Retention:Schedule:Cron '{Cron}' is not a valid Quartz cron expression or can never fire; " +
                "using the default {DefaultCron}.",
                settings.Cron.SanitizeForLog(),
                RetentionScheduleSettings.DefaultCron);
        }

        var cron = new CronExpression(settings.ResolvedCron).CronExpressionString;

        if (!await scheduler.CheckExists(jobKey, cancellationToken))
        {
            var job = JobBuilder.Create<RetentionCheckJob>()
                .WithIdentity(jobKey)
                .WithDescription("Report Retention Check to purge expired reports")
                .RequestRecovery()
                .StoreDurably()
                .Build();

            try
            {
                var trigger = BuildTrigger(jobKey, cron) as ICronTrigger;
                
                if (trigger is null)
                {
                    throw new InvalidOperationException("Failed to build the retention job trigger.");
                }

                await scheduler.ScheduleJob(job, trigger, cancellationToken);

                _logger.LogInformation("Scheduled retention nightly job ({Cron}). {nextFire} {TimeZone}",
                    cron.SanitizeForLog(), 
                    trigger.GetNextFireTimeUtc()?.ToString("o") ?? "unknown", 
                    trigger.TimeZone.Id.SanitizeForLog());
                return;
            }
            catch (ObjectAlreadyExistsException ex)
            {
                // Another cluster node's ReconcileAsync won the race between our CheckExists
                // and this ScheduleJob. Fall through to the verify/reschedule logic below instead
                // of crashing the loser out of the hosted service's StartAsync.
                _logger.LogDebug(ex, "Retention Nightly job was scheduled by another node; verifying its trigger.");
            }
        }

        var existing = (await scheduler.GetTriggersOfJob(jobKey, cancellationToken))
            .OfType<ICronTrigger>()
            .FirstOrDefault();

        // ScheduleJob stores the job and its trigger in one transaction, so a job without its trigger
        // means the store was edited by hand. Say so rather than repair it.
        if (existing is null)
        {
            _logger.LogWarning(
                "The retention nightly job is stored without a cron trigger, so it will never fire. " +
                "Delete the job and restart Report to recreate both.");
            return;
        }

        if (existing.CronExpressionString == cron && Equals(existing.TimeZone, TimeZoneInfo.Utc))
        {
            _logger.LogInformation("Retention nightly job is already scheduled ({Cron}). {nextFire} {TimeZone}",
                cron.SanitizeForLog(),
                existing.GetNextFireTimeUtc()?.ToString("o") ?? "unknown",
                existing.TimeZone.Id.SanitizeForLog());
            return;
        }

        var replacement = BuildTrigger(jobKey, cron) as ICronTrigger;
        if (replacement is null)
        {
            throw new InvalidOperationException("Failed to build the retention job trigger.");
        }

        await scheduler.RescheduleJob(existing.Key, replacement, cancellationToken);

        _logger.LogInformation(
            "Rescheduled Retention Nightly job: {OldCron} -> {NewCron}. " +
            "Next {nextFire} {TimeZone}",
            existing.CronExpressionString.SanitizeForLog(),
            cron.SanitizeForLog(),
            replacement.GetNextFireTimeUtc()?.ToString("o") ?? "unknown",
            replacement.TimeZone.Id.SanitizeForLog());
    }

    private static ITrigger BuildTrigger(JobKey jobKey, string cron) =>
        TriggerBuilder.Create()
            .ForJob(jobKey)
            .WithIdentity(jobKey.Name, jobKey.Group)
            .WithDescription(cron)
            .WithCronSchedule(cron, x => x
                .InTimeZone(TimeZoneInfo.Utc)
                // A fire missed while the service was down runs once on recovery, rather than being
                // dropped or run once per missed night. The run reports the recovery instant, not the
                // missed cron time. See dev-docs/retention-schedule.md.
                .WithMisfireHandlingInstructionFireAndProceed())
            .Build();
}
