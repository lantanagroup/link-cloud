using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Jobs;
using LantanaGroup.Link.Shared.Settings;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Quartz;
using Quartz.Spi;

namespace LantanaGroup.Link.Shared.Application.Services;

public class RetryScheduleService : BackgroundService
{
    private readonly ILogger<RetryScheduleService> _logger;
    private readonly IJobFactory _jobFactory;
    private readonly ISchedulerFactory _schedulerFactory;

    public RetryScheduleService(
        ILogger<RetryScheduleService> logger,
        IJobFactory jobFactory,
        [FromKeyedServices(ConfigurationConstants.RunTimeConstants.RetrySchedulerKeyedSingleton)] ISchedulerFactory schedulerFactory)
    {
        _logger = logger;
        _jobFactory = jobFactory;
        _schedulerFactory = schedulerFactory;
    }

    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        var scheduler = await _schedulerFactory.GetScheduler(cancellationToken);
        scheduler.JobFactory = _jobFactory;

        await scheduler.Start(cancellationToken);
        _logger.LogInformation("RetryScheduleService started.");
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        var scheduler = await _schedulerFactory.GetScheduler(cancellationToken);
        await scheduler.Shutdown(cancellationToken);  // Ensure clean shutdown to persist state
        await base.StopAsync(cancellationToken);
    }

    public static async Task CreateJobAndTrigger(RetryModel model, IScheduler scheduler)
    {
        IJobDetail job = CreateJob(model);
        await scheduler.AddJob(job, true);  // 'true' replaces if exists

        ITrigger trigger = CreateTrigger(model, job.Key);
        await scheduler.ScheduleJob(trigger);
    }

    public static IJobDetail CreateJob(RetryModel model)
    {
        JobDataMap jobDataMap = new JobDataMap();
        jobDataMap.Put("RetryModel", model);

        return JobBuilder
            .Create(typeof(RetryJob))
            .StoreDurably(true)
            .WithIdentity(model.JobId)
            .WithDescription($"{model.FacilityId}-{model.Topic}")
            .UsingJobData(jobDataMap)
            .Build();
    }

    private static ITrigger CreateTrigger(RetryModel model, JobKey jobKey)
    {
        JobDataMap jobDataMap = new JobDataMap();
        jobDataMap.Put("RetryModel", model);

        var offset = DateBuilder.DateOf(model.ScheduledTrigger.Hour, model.ScheduledTrigger.Minute, model.ScheduledTrigger.Second);

        return TriggerBuilder
            .Create()
            .StartAt(offset)
            .ForJob(jobKey)
            .WithIdentity(Guid.NewGuid().ToString(), jobKey.Group)
            .WithDescription($"{model.Id}-{model.ScheduledTrigger}")  // Assuming Id still exists; remove if not
            .UsingJobData(jobDataMap)
            .Build();
    }

    public static async Task DeleteJob(RetryModel model, IScheduler scheduler)
    {
        JobKey jobKey = new JobKey(model.JobId);
        await scheduler.DeleteJob(jobKey);
    }

    public static async Task RescheduleJob(RetryModel model, IScheduler scheduler)
    {
        await DeleteJob(model, scheduler);
        await CreateJobAndTrigger(model, scheduler);
    }
}