using LantanaGroup.Link.Report.Domain;
using LantanaGroup.Link.Report.Entities;
using LantanaGroup.Link.Report.Jobs;
using LantanaGroup.Link.Report.Settings;
using LantanaGroup.Link.Shared.Application.Models;
using Quartz;
using Quartz.Spi;
using System.Text.Json;

namespace LantanaGroup.Link.Report.Services;

public class MeasureReportScheduleService : BackgroundService
{
    private readonly ILogger<MeasureReportScheduleService> _logger;
    private readonly IJobFactory _jobFactory;
    private readonly ISchedulerFactory _schedulerFactory;
    private readonly IServiceScopeFactory _serviceScopeFactory;

    public IScheduler Scheduler { get; set; } = default!;

    public MeasureReportScheduleService(
        ILogger<MeasureReportScheduleService> logger,
        IJobFactory jobFactory,
        [FromKeyedServices("MongoScheduler")] ISchedulerFactory schedulerFactory,
        IServiceScopeFactory serviceScopeFactory)
    {
        _logger = logger;
        _jobFactory = jobFactory;
        _schedulerFactory = schedulerFactory;
        _serviceScopeFactory = serviceScopeFactory;

    }

    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        Scheduler = await _schedulerFactory.GetScheduler(cancellationToken);
        Scheduler.JobFactory = _jobFactory;

		// find all reports that have not been submitted yet
        var database = _serviceScopeFactory.CreateScope().ServiceProvider.GetRequiredService<IDatabase>();
        var reportSchedules = await database.ReportScheduledRepository.FindAsync(s => !s.EndOfReportPeriodJobHasRun && s.Frequency != Frequency.Adhoc, cancellationToken);

        foreach (var reportSchedule in reportSchedules)
        {
            try
            {
                _logger.LogInformation("Scheduling job for ReportSchedule ID: {ScheduleId}, FacilityId: {FacilityId}, EndDate: {EndDate}",
                    reportSchedule.Id,
                    reportSchedule.FacilityId,
                    reportSchedule.ReportEndDate);

                await CreateJobAndTrigger(reportSchedule, Scheduler, _logger);

                _logger.LogInformation("Successfully scheduled job for ReportSchedule ID: {ScheduleId}", reportSchedule.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Could not schedule {ScheduleId}: {Message}", reportSchedule.Id, ex.Message);
            }
        }

        await Scheduler.Start(cancellationToken);
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await Scheduler.Shutdown(cancellationToken);
        _logger.LogInformation("MeasureReportScheduleService stopped.");
    }

    public static async Task CreateJobAndTrigger(ReportSchedule reportSchedule, IScheduler scheduler, ILogger? logger = null)
    {
        var job = CreateJob(reportSchedule);
        var trigger = CreateTrigger(reportSchedule, job.Key);

        var exists = await scheduler.CheckExists(job.Key);
        if (!exists)
			await scheduler.ScheduleJob(job, trigger);
        else
            await scheduler.ScheduleJob(trigger);
    }

    public static IJobDetail CreateJob(ReportSchedule reportSchedule)
    {
        JobDataMap jobDataMap = new JobDataMap();
        jobDataMap.Put("ReportScheduleId", reportSchedule.Id);
        jobDataMap.Put("FacilityId", reportSchedule.FacilityId);

        return JobBuilder
            .Create(typeof(EndOfReportPeriodJob))
            .StoreDurably(true)
            .RequestRecovery(true)
            .WithIdentity(reportSchedule.Id, ReportConstants.MeasureReportSubmissionScheduler.Group)
            .WithDescription($"{reportSchedule.Id}-{ReportConstants.MeasureReportSubmissionScheduler.Group}")
            .UsingJobData(jobDataMap)
            .Build();
    }

    private static ITrigger CreateTrigger(ReportSchedule reportSchedule, JobKey jobKey)
    {
        JobDataMap jobDataMap = new JobDataMap();
        string reportScheduleJson = JsonSerializer.Serialize(reportSchedule);
        jobDataMap.Put(ReportConstants.MeasureReportSubmissionScheduler.ReportScheduleModel, reportScheduleJson);

        var offset = new DateTimeOffset(
            reportSchedule.ReportEndDate.Year,
            reportSchedule.ReportEndDate.Month,
            reportSchedule.ReportEndDate.Day,
            reportSchedule.ReportEndDate.Hour,
            reportSchedule.ReportEndDate.Minute,
            reportSchedule.ReportEndDate.Second,
            TimeSpan.Zero
        );

        return TriggerBuilder
            .Create()
            .ForJob(jobKey)
            .StartAt(offset)
            .WithIdentity(Guid.NewGuid().ToString(), jobKey.Group)
            .WithDescription($"{reportSchedule.Id}-{reportSchedule.ReportEndDate}")
            .UsingJobData(jobDataMap)
            .Build();
    }

    public static async Task DeleteJob(ReportSchedule reportSchedule, IScheduler scheduler)
    {
        JobKey jobKey = new JobKey(reportSchedule.Id, ReportConstants.MeasureReportSubmissionScheduler.Group);
        await scheduler.DeleteJob(jobKey);
    }

    public static async Task RescheduleJob(ReportSchedule reportSchedule, IScheduler scheduler)
    {
        await DeleteJob(reportSchedule, scheduler);
        await CreateJobAndTrigger(reportSchedule, scheduler);
    }
}