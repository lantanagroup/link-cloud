using LantanaGroup.Link.Report.Application.Interfaces;
using LantanaGroup.Link.Report.Domain;
using LantanaGroup.Link.Report.Entities;
using LantanaGroup.Link.Report.Jobs;
using LantanaGroup.Link.Report.Settings;
using LantanaGroup.Link.Shared.Application.Models;
using Quartz;
using Quartz.Spi;
using System.Text.Json;

namespace LantanaGroup.Link.Report.Services
{
    public class MeasureReportScheduleService : BackgroundService
    {
        private readonly ILogger<MeasureReportScheduleService> _logger;
        private readonly IJobFactory _jobFactory;
        private readonly ISchedulerFactory _schedulerFactory;
        private readonly IDatabase _database;

        public IScheduler Scheduler { get; set; } = default!;

        public MeasureReportScheduleService(ILogger<MeasureReportScheduleService> logger, IJobFactory jobFactory, ISchedulerFactory schedulerFactory, IDatabase database)
        {
            _logger = logger;
            _jobFactory = jobFactory;
            _schedulerFactory = schedulerFactory;
            _database = database;
        }

        protected override async Task ExecuteAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("MeasureReportScheduleService ExecuteAsync starting...");

            Scheduler = await _schedulerFactory.GetScheduler(cancellationToken);
            Scheduler.JobFactory = _jobFactory;

            _logger.LogInformation("Scheduler obtained: {SchedulerName}", Scheduler.SchedulerName);

            // find all reports that have not been submitted yet
            var reportSchedules =
                await _database.ReportScheduledRepository.FindAsync(s => !s.EndOfReportPeriodJobHasRun && s.Frequency != Frequency.Adhoc, cancellationToken);

            _logger.LogInformation("Found {Count} report schedules to process", reportSchedules.Count());

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

            // Log all scheduled jobs
            var allJobKeys = await Scheduler.GetJobKeys(Quartz.Impl.Matchers.GroupMatcher<JobKey>.AnyGroup(), cancellationToken);
            _logger.LogInformation("Total jobs in scheduler: {Count}", allJobKeys.Count);
            foreach (var jobKey in allJobKeys)
            {
                var jobDetail = await Scheduler.GetJobDetail(jobKey, cancellationToken);
                var triggers = await Scheduler.GetTriggersOfJob(jobKey, cancellationToken);
                _logger.LogInformation("Job: {JobKey}, Triggers: {TriggerCount}", jobKey, triggers.Count);
            }

            _logger.LogInformation("MeasureReportScheduleService started.");
        }

        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            await Scheduler.Shutdown(cancellationToken);
            await base.StopAsync(cancellationToken);
        }


        public static async Task CreateJobAndTrigger(ReportScheduleModel reportSchedule, IScheduler scheduler, ILogger? logger = null)
        {
            logger?.LogInformation("Creating job and trigger for schedule: {ScheduleId}", reportSchedule.Id);

            IJobDetail job = CreateJob(reportSchedule);
            ITrigger trigger = CreateTrigger(reportSchedule, job.Key);

            logger?.LogInformation("Job Key: {JobKey}, Group: {Group}", job.Key.Name, job.Key.Group);
            logger?.LogInformation("Trigger Key: {TriggerKey}, Start Time: {StartTime}", trigger.Key, trigger.StartTimeUtc);

            // Check if job already exists
            var existingJob = await scheduler.GetJobDetail(job.Key);

            if (existingJob == null)
            {
                logger?.LogInformation("Job doesn't exist, scheduling new job and trigger");
                // Job doesn't exist, schedule it with the trigger
                var scheduledTime = await scheduler.ScheduleJob(job, trigger);
                logger?.LogInformation("Job scheduled successfully. Next fire time: {NextFireTime}", scheduledTime);
            }
            else
            {
                logger?.LogInformation("Job already exists, just adding new trigger");
                // Job exists, just add the new trigger
                var scheduledTime = await scheduler.ScheduleJob(trigger);
                logger?.LogInformation("Trigger scheduled successfully. Next fire time: {NextFireTime}", scheduledTime);
            }

            // Verify it was added
            var verifyJob = await scheduler.GetJobDetail(job.Key);
            var verifyTriggers = await scheduler.GetTriggersOfJob(job.Key);
            logger?.LogInformation("Job verification - Exists: {Exists}, Trigger count: {TriggerCount}",
                verifyJob != null,
                verifyTriggers?.Count ?? 0);
        }


        public static IJobDetail CreateJob(ReportScheduleModel reportSchedule)
        {
            JobDataMap jobDataMap = new JobDataMap();

            // Store only the schedule ID, not the entire object
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

        private static ITrigger CreateTrigger(ReportScheduleModel reportSchedule, JobKey jobKey)
        {
            JobDataMap jobDataMap = new JobDataMap();

            // Serialize ReportScheduleModel to JSON for safe storage in JobDataMap
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


        public static async Task DeleteJob(ReportScheduleModel reportSchedule, IScheduler scheduler)
        {
            JobKey jobKey = new JobKey(reportSchedule.Id, ReportConstants.MeasureReportSubmissionScheduler.Group);
            await scheduler.DeleteJob(jobKey);
        }

        public static async Task RescheduleJob(ReportScheduleModel reportSchedule, IScheduler scheduler)
        {
            await DeleteJob(reportSchedule, scheduler);
            await CreateJobAndTrigger(reportSchedule, scheduler);
        }
    }
}