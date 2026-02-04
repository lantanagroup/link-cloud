using LantanaGroup.Link.Shared.Application.Models;
using Quartz;

namespace LantanaGroup.Link.DataAcquisition.Jobs;
public class AcquisitionProcessingScheduleService : IHostedService
{

    public const string MONTHLY = "Monthly";
    public const string WEEKLY = "Weekly";
    public const string DAILY = "Daily";

    private readonly ISchedulerFactory _schedulerFactory;

    public AcquisitionProcessingScheduleService(ISchedulerFactory schedulerFactory)
    {
        _schedulerFactory = schedulerFactory;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var scheduler = await _schedulerFactory.GetScheduler(cancellationToken);

        var job = JobBuilder
            .Create(typeof(AcquisitionProcessingJob))
            .StoreDurably()
            .WithIdentity("Acquisition Processing Job", nameof(KafkaTopic.ReadyToAcquire))
            .WithDescription("Acquisition Processing Job")
            .Build();

        await scheduler.AddJob(job, true);

        var trigger = TriggerBuilder
            .Create()
            .ForJob(job.Key)
            .WithIdentity("Acquisition Processing Trigger", job.Key.Group)
            .WithCronSchedule("0/30 * * * * ?") // every 30 seconds
            .WithDescription("Acquisition Processing Trigger")
            .Build();

        var existingTrigger = await scheduler.GetTrigger(trigger.Key, cancellationToken);

        if (existingTrigger != null)
        {
            // Trigger exists, reschedule it with the new definition
            await scheduler.RescheduleJob(trigger.Key, trigger, cancellationToken);
        }
        else
        {
            // Trigger does not exist, schedule it
            await scheduler.ScheduleJob(job, trigger, cancellationToken);
        }

        await scheduler.Start(cancellationToken);
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
