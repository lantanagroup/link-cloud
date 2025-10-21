using LantanaGroup.Link.Report.Domain;
using LantanaGroup.Link.Report.Entities;
using LantanaGroup.Link.Report.Jobs;
using LantanaGroup.Link.Report.Settings;
using LantanaGroup.Link.Shared.Application.Models;
using Quartz;
using Quartz.Spi;

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
            Scheduler = await _schedulerFactory.GetScheduler(cancellationToken);
            Scheduler.JobFactory = _jobFactory;

            // find all reports that have not been submitted yet
            var reportSchedules =
                await _database.ReportScheduledRepository.FindAsync(s => !s.EndOfReportPeriodJobHasRun && s.Frequency != Frequency.Adhoc, cancellationToken);

            foreach (var reportSchedule in reportSchedules)
            {
                try
                {
                    await CreateJobAndTrigger(reportSchedule, Scheduler);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Could not schedule {ReportScheduleId}", reportSchedule.Id);
                }
            }

            await Scheduler.Start(cancellationToken);
            _logger.LogInformation("MeasureReportScheduleService started.");
        }

        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            await Scheduler.Shutdown(cancellationToken);
            await base.StopAsync(cancellationToken);
        }


        public static async Task CreateJobAndTrigger(ReportScheduleModel reportSchedule, IScheduler scheduler)
        {
            IJobDetail job = CreateJob(reportSchedule);
            
            await scheduler.AddJob(job, true);

            ITrigger trigger = CreateTrigger(reportSchedule, job.Key);
            await scheduler.ScheduleJob(trigger);
        }


        public static IJobDetail CreateJob(ReportScheduleModel reportSchedule)
        {
            JobDataMap jobDataMap = new JobDataMap();

            jobDataMap.Put(ReportConstants.MeasureReportSubmissionScheduler.ReportScheduleModel, reportSchedule);

            return JobBuilder
                .Create(typeof(EndOfReportPeriodJob))
                .StoreDurably()
                .WithIdentity(reportSchedule.Id, ReportConstants.MeasureReportSubmissionScheduler.Group)
                .WithDescription($"{reportSchedule.Id}-{ReportConstants.MeasureReportSubmissionScheduler.Group}")
                .UsingJobData(jobDataMap)
                .Build();
        }

        private static ITrigger CreateTrigger(ReportScheduleModel reportSchedule, JobKey jobKey)
        {
            JobDataMap jobDataMap = new JobDataMap();

            jobDataMap.Put(ReportConstants.MeasureReportSubmissionScheduler.ReportScheduleModel, reportSchedule);

            var offset = new DateTimeOffset(
             reportSchedule.ReportEndDate.Year,
             reportSchedule.ReportEndDate.Month,
             reportSchedule.ReportEndDate.Day,
             reportSchedule.ReportEndDate.Hour,
             reportSchedule.ReportEndDate.Minute,
             reportSchedule.ReportEndDate.Second,
             TimeSpan.Zero // Adjust this to the correct UTC offset, if needed
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
