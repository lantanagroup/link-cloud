using Quartz.Spi;
using Quartz;
using LantanaGroup.Link.Shared.Application.Models;

namespace LantanaGroup.Link.DataAcquisition.Jobs;
public class ScheduleService : IHostedService
{

    public const string MONTHLY = "Monthly";
    public const string WEEKLY = "Weekly";
    public const string DAILY = "Daily";

    private readonly ISchedulerFactory _schedulerFactory;
    private readonly Quartz.Spi.IJobFactory _jobFactory;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ScheduleService> _logger;

    public ScheduleService(
       ILogger<ScheduleService> logger,
       ISchedulerFactory schedulerFactory,
       IServiceScopeFactory serviceScopeFactory,
       IJobFactory jobFactory)
    {
        _schedulerFactory = schedulerFactory;
        _jobFactory = jobFactory;
        _scopeFactory = serviceScopeFactory;
        _logger = logger;
    }

    public IScheduler Scheduler { get; set; }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        Scheduler = await _schedulerFactory.GetScheduler(cancellationToken);

        Scheduler.JobFactory = _jobFactory;

        // adding 1 job to run
        JobDataMap jobDataMap = new JobDataMap();
        var job = JobBuilder
            .Create(typeof(AcquisitionProcessingJob))
            .StoreDurably()
            .WithIdentity("Acquisition Processing Job", nameof(KafkaTopic.ReadyToAcquire))
            .WithDescription("Acquisition Processing Job")
            .UsingJobData(jobDataMap)
            .Build();

        await Scheduler.AddJob(job, true);

        var trigger = TriggerBuilder
            .Create()
            .ForJob(job.Key)
            .WithIdentity("Acquisition Processing Trigger", job.Key.Group)
            .WithCronSchedule("0/30 * * * * ?") // every 30 seconds
            .WithDescription("Acquisition Processing Trigger")
            .Build();

        await Scheduler.ScheduleJob(trigger);

        await Scheduler.Start(cancellationToken);
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        await Scheduler?.Shutdown(cancellationToken);
    }
}

public class JobFactory : IJobFactory
{
    private readonly IServiceProvider _serviceProvider;

    public JobFactory(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public IJob NewJob(TriggerFiredBundle bundle, IScheduler scheduler)
    {
        return _serviceProvider.GetRequiredService(bundle.JobDetail.JobType) as IJob;
    }

    public void ReturnJob(IJob job)
    {
        // we let the DI container handler this
    }
}
