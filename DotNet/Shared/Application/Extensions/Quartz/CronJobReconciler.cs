using Quartz;

namespace LantanaGroup.Link.Shared.Application.Extensions.Quartz;

/// <summary>
/// Keeps one durable job with one cron trigger in a Quartz store in line with its definition. Safe to run on
/// every cluster instance at every startup.
/// </summary>
/// <remarks>
/// Quartz keeps stored jobs and triggers across restarts, so this only changes the store when it differs from
/// the definition: the job is missing, its trigger is missing, or the trigger's cron or time zone changed.
/// </remarks>
public static class CronJobReconciler
{
    /// <summary>
    /// True when <paramref name="cron"/> is a valid Quartz cron expression that can still fire in
    /// <paramref name="timeZone"/>. A cron limited to a past year is valid but can never fire, and Quartz
    /// refuses to schedule it.
    /// </summary>
    public static bool CanFire(string? cron, TimeZoneInfo timeZone)
    {
        ArgumentNullException.ThrowIfNull(timeZone);

        if (string.IsNullOrWhiteSpace(cron) || !CronExpression.IsValidExpression(cron))
        {
            return false;
        }

        var expression = new CronExpression(cron)
        {
            TimeZone = timeZone
        };

        return expression.GetNextValidTimeAfter(DateTimeOffset.UtcNow) is not null;
    }

    /// <summary>
    /// Stores the job and trigger if missing, adds a missing trigger, and reschedules the trigger when its cron
    /// or time zone differs from <paramref name="definition"/>.
    /// </summary>
    public static async Task<CronJobReconcileResult> EnsureAsync(IScheduler scheduler,
                                                                 CronJobDefinition definition,
                                                                 CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(scheduler);
        ArgumentNullException.ThrowIfNull(definition);

        // Quartz stores cron expressions uppercased, so compare against its form, not the configured one.
        var cron = new CronExpression(definition.Cron).CronExpressionString;

        if (!await scheduler.CheckExists(definition.JobKey, cancellationToken))
        {
            var trigger = BuildTrigger(definition, cron);

            try
            {
                await scheduler.ScheduleJob(BuildJob(definition), trigger, cancellationToken);

                return Result(CronJobReconcileOutcome.Created, cron, trigger.GetNextFireTimeUtc());
            }
            catch (ObjectAlreadyExistsException)
            {
                // Another instance stored it between CheckExists and ScheduleJob; verify what it stored.
            }
        }

        var existing = (await scheduler.GetTriggersOfJob(definition.JobKey, cancellationToken))
            .OfType<ICronTrigger>()
            .FirstOrDefault();

        if (existing is null)
        {
            return await AddTriggerAsync(scheduler, definition, cron, cancellationToken);
        }

        if (existing.CronExpressionString == cron && existing.TimeZone.Id == definition.TimeZone.Id)
        {
            return Result(CronJobReconcileOutcome.Unchanged, cron, existing.GetNextFireTimeUtc());
        }

        var nextFire = await scheduler.RescheduleJob(existing.Key, BuildTrigger(definition, cron), cancellationToken);

        return new CronJobReconcileResult
        {
            Outcome = CronJobReconcileOutcome.Rescheduled,
            Cron = cron,
            PreviousCron = existing.CronExpressionString,
            NextFireTimeUtc = nextFire
        };
    }

    private static async Task<CronJobReconcileResult> AddTriggerAsync(IScheduler scheduler,
                                                                      CronJobDefinition definition,
                                                                      string cron,
                                                                      CancellationToken cancellationToken)
    {
        try
        {
            var nextFire = await scheduler.ScheduleJob(BuildTrigger(definition, cron), cancellationToken);

            return Result(CronJobReconcileOutcome.TriggerAdded, cron, nextFire);
        }
        catch (ObjectAlreadyExistsException)
        {
            // Another instance added it first.
            var trigger = await scheduler.GetTrigger(TriggerKeyOf(definition), cancellationToken);

            return Result(CronJobReconcileOutcome.Unchanged, cron, trigger?.GetNextFireTimeUtc());
        }
    }

    private static CronJobReconcileResult Result(CronJobReconcileOutcome outcome,
                                                 string cron,
                                                 DateTimeOffset? nextFireTimeUtc) =>
        new()
        {
            Outcome = outcome,
            Cron = cron,
            NextFireTimeUtc = nextFireTimeUtc
        };

    private static TriggerKey TriggerKeyOf(CronJobDefinition definition) =>
        new(definition.JobKey.Name, definition.JobKey.Group);

    private static IJobDetail BuildJob(CronJobDefinition definition)
    {
        var builder = JobBuilder.Create(definition.JobType)
            .WithIdentity(definition.JobKey)
            .StoreDurably()
            .RequestRecovery(definition.RequestRecovery);

        if (definition.Description is not null)
        {
            builder = builder.WithDescription(definition.Description);
        }

        foreach (var (key, value) in definition.JobData ?? new Dictionary<string, string>())
        {
            builder = builder.UsingJobData(key, value);
        }

        return builder.Build();
    }

    private static ITrigger BuildTrigger(CronJobDefinition definition, string cron) =>
        TriggerBuilder.Create()
            .ForJob(definition.JobKey)
            .WithIdentity(TriggerKeyOf(definition))
            .WithDescription(cron)
            .WithCronSchedule(cron, x => x
                .InTimeZone(definition.TimeZone)
                // A firing missed while every instance was down runs once on recovery, rather than being
                // dropped or run once per missed interval. The run reports the recovery instant as its
                // scheduled time, not the missed cron time.
                .WithMisfireHandlingInstructionFireAndProceed())
            .Build();
}
