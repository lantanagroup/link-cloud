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

        var trigger = TriggerBuilder
            .Create()
            .ForJob(job.Key)
            .WithIdentity("Acquisition Processing Trigger", job.Key.Group)
            .WithCronSchedule("0/30 * * * * ?") // every 30 seconds
            .WithDescription("Acquisition Processing Trigger")
            .Build();

        var exists = await scheduler.CheckExists(job.Key);
        if (!exists)
            await scheduler.ScheduleJob(job, trigger);
        else
            await scheduler.ScheduleJob(trigger);

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
