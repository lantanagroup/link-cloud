using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.DataAcquisition.Domain.Settings;
using Microsoft.Extensions.Options;
using Quartz;

namespace LantanaGroup.Link.DataAcquisition.Jobs;
public class AcquisitionProcessingScheduleService : IHostedService
{

    public const string MONTHLY = "Monthly";
    public const string WEEKLY = "Weekly";
    public const string DAILY = "Daily";

    private readonly ISchedulerFactory _schedulerFactory;
    private readonly IOptions<AcquisitionJobSettings> _settings;
    private readonly IOptions<TailMessageRecoveryJobSettings> _tailRecoverySettings;

    public AcquisitionProcessingScheduleService(
        ISchedulerFactory schedulerFactory,
        IOptions<AcquisitionJobSettings> settings,
        IOptions<TailMessageRecoveryJobSettings> tailRecoverySettings)
    {
        _schedulerFactory = schedulerFactory;
        _settings = settings;
        _tailRecoverySettings = tailRecoverySettings;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var scheduler = await _schedulerFactory.GetScheduler(cancellationToken);

        var acquisitionJob = JobBuilder
            .Create(typeof(AcquisitionProcessingJob))
            .StoreDurably()
            .WithIdentity("Acquisition Processing Job", nameof(KafkaTopic.ReadyToAcquire))
            .WithDescription("Acquisition Processing Job")
            .Build();

        var acquisitionTrigger = TriggerBuilder
            .Create()
            .ForJob(acquisitionJob.Key)
            .WithIdentity("Acquisition Processing Trigger", acquisitionJob.Key.Group)
            .WithCronSchedule(_settings.Value.CronSchedule) // every 30 seconds
            .WithDescription("Acquisition Processing Trigger")
            .Build();

        var tailRecoveryJob = JobBuilder
            .Create(typeof(TailMessageRecoveryJob))
            .StoreDurably()
            .WithIdentity("Tail Message Recovery Job", nameof(KafkaTopic.ResourcesAcquired))
            .WithDescription("Tail Message Recovery Job")
            .Build();

        var tailRecoveryTrigger = TriggerBuilder
            .Create()
            .ForJob(tailRecoveryJob.Key)
            .WithIdentity("Tail Message Recovery Trigger", tailRecoveryJob.Key.Group)
            .WithCronSchedule(_tailRecoverySettings.Value.CronSchedule) // default every 10 minutes
            .WithDescription("Tail Message Recovery Trigger")
            .Build();

        await EnsureJobAndTrigger(scheduler, acquisitionJob, acquisitionTrigger, cancellationToken);
        await EnsureJobAndTrigger(scheduler, tailRecoveryJob, tailRecoveryTrigger, cancellationToken);

        await scheduler.Start(cancellationToken);
    }

    private static async Task EnsureJobAndTrigger(
        IScheduler scheduler,
        IJobDetail job,
        ITrigger trigger,
        CancellationToken cancellationToken)
    {
        var existingTrigger = await scheduler.GetTrigger(trigger.Key, cancellationToken);

        if (existingTrigger != null)
        {
            // Ensure the job exists (add or replace)
            await scheduler.AddJob(job, true, true, cancellationToken);

            // Trigger exists, update it
            await scheduler.RescheduleJob(trigger.Key, trigger, cancellationToken);
        }
        else
        {
            // Trigger does not exist, schedule job and trigger
            await scheduler.ScheduleJob(job, trigger, cancellationToken);
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        var scheduler = await _schedulerFactory.GetScheduler(cancellationToken);

        if (scheduler != null)
        {
            await scheduler.Shutdown(cancellationToken);
        }
    }
}
