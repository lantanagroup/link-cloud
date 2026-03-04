using Quartz;

namespace LantanaGroup.Link.Shared.Application.Utilities;

public interface IQuartzJobHelper
{
    Task ScheduleJob<TJob>(IDictionary<string, object> jobData, DateTimeOffset startAt, string identity, string group, string? description = null, CancellationToken ct = default)
        where TJob : IJob;

    Task DeleteJob(string identity, string group, CancellationToken ct = default);

    Task RescheduleJob<TJob>(string identity, IDictionary<string, object> jobData, DateTimeOffset newStartAt, string group, string? description = null, CancellationToken ct = default)
        where TJob : IJob;
}

public class QuartzJobHelper : IQuartzJobHelper
{
    private readonly ISchedulerFactory _schedulerFactory;

    public QuartzJobHelper(ISchedulerFactory schedulerFactory)
    {
        _schedulerFactory = schedulerFactory;
    }

    public async Task ScheduleJob<TJob>(IDictionary<string, object> jobData, DateTimeOffset startAt, string identity, string group, string? description = null, CancellationToken ct = default)
        where TJob : IJob
    {
        var scheduler = await _schedulerFactory.GetScheduler(ct);

        var job = JobBuilder.Create<TJob>()
            .WithIdentity(identity, group)
            .WithDescription(description ?? identity)
            .UsingJobData(new JobDataMap(jobData))
            .StoreDurably()
            .RequestRecovery(true)
            .Build();

        var trigger = TriggerBuilder.Create()
            .ForJob(job.Key)
            .StartAt(startAt)
            .WithIdentity($"{identity}-Trigger", group)
            .Build();

        await scheduler.ScheduleJob(job, trigger, ct);
    }

    public async Task DeleteJob(string identity, string group, CancellationToken ct = default)
    {
        var scheduler = await _schedulerFactory.GetScheduler(ct);
        var jobKey = new JobKey(identity, group);
        await scheduler.DeleteJob(jobKey, ct);
    }

    public async Task RescheduleJob<TJob>(string identity, IDictionary<string, object> jobData, DateTimeOffset newStartAt, string group = "ReportJobs", string? description = null, CancellationToken ct = default)
        where TJob : IJob
    {
        await DeleteJob(identity, group, ct);
        await ScheduleJob<TJob>(jobData, newStartAt, identity, group, description, ct);
    }
}