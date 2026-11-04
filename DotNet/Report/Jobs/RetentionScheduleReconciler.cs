using LantanaGroup.Link.Report.Application.Options;
using LantanaGroup.Link.Report.Settings;
using LantanaGroup.Link.Shared.Application.Extensions.Quartz;
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

        // A missed firing runs once on recovery and reports the recovery instant, not the missed cron time.
        // See dev-docs/retention-schedule.md.
        var definition = new CronJobDefinition
        {
            JobKey = jobKey,
            JobType = typeof(RetentionCheckJob),
            Cron = settings.ResolvedCron,
            TimeZone = TimeZoneInfo.Utc,
            RequestRecovery = true,
            Description = "Report Retention Check to purge expired reports"
        };

        var result = await CronJobReconciler.EnsureAsync(scheduler, definition, cancellationToken);
        var nextFire = result.NextFireTimeUtc?.ToString("o") ?? "unknown";
        var timeZone = TimeZoneInfo.Utc.Id;

        switch (result.Outcome)
        {
            case CronJobReconcileOutcome.Created:
                _logger.LogInformation("Scheduled retention nightly job ({Cron}). {nextFire} {TimeZone}",
                    result.Cron.SanitizeForLog(),
                    nextFire,
                    timeZone);
                break;
            case CronJobReconcileOutcome.TriggerAdded:
                _logger.LogInformation(
                    "Retention nightly job was stored without its trigger; added it ({Cron}). {nextFire} {TimeZone}",
                    result.Cron.SanitizeForLog(),
                    nextFire,
                    timeZone);
                break;
            case CronJobReconcileOutcome.Rescheduled:
                _logger.LogInformation(
                    "Rescheduled Retention Nightly job: {OldCron} -> {NewCron}. " +
                    "Next {nextFire} {TimeZone}",
                    result.PreviousCron.SanitizeForLog(),
                    result.Cron.SanitizeForLog(),
                    nextFire,
                    timeZone);
                break;
            default:
                _logger.LogInformation("Retention nightly job is already scheduled ({Cron}). {nextFire} {TimeZone}",
                    result.Cron.SanitizeForLog(),
                    nextFire,
                    timeZone);
                break;
        }
    }
}
