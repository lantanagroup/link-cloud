using Quartz.Spi;
using Quartz;
using MediatR;
using LantanaGroup.Link.Report.Application.MeasureReportSchedule.Queries;
using LantanaGroup.Link.Report.Entities;
using LantanaGroup.Link.Report.Settings;
using LantanaGroup.Link.Report.Jobs;

namespace LantanaGroup.Link.Report.Services
{
    public class MeasureReportScheduleService : BackgroundService
    {
        private readonly ILogger<MeasureReportScheduleService> _logger;
        private readonly IJobFactory _jobFactory;
        private readonly ISchedulerFactory _schedulerFactory;
        private readonly IMediator _mediator;

        public IScheduler Scheduler { get; set; } = default!;

        public MeasureReportScheduleService(ILogger<MeasureReportScheduleService> logger, IJobFactory jobFactory, ISchedulerFactory schedulerFactory, IMediator mediator)
        {
            _logger = logger;
            _jobFactory = jobFactory;
            _schedulerFactory = schedulerFactory;
            _mediator = mediator;
        }


        protected override async Task ExecuteAsync(CancellationToken cancellationToken)
        {
            Scheduler = await _schedulerFactory.GetScheduler(cancellationToken);
            Scheduler.JobFactory = _jobFactory;

            // find all reports that have not been submitted yet
            var reportSchedules = await _mediator.Send(new GetMeasureReportSchedulesByIsSubmittedQuery { IsSubmitted = false });

            foreach (var reportSchedule in reportSchedules)
            {
                try
                {
                    await CreateJobAndTrigger(reportSchedule, Scheduler);
                }
                catch (Exception ex)
                {
                    _logger.LogError($"Could not schedule {reportSchedule.Id}: {ex.Message}");
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


        public static async Task CreateJobAndTrigger(MeasureReportScheduleModel reportSchedule, IScheduler scheduler)
        {
            IJobDetail job = CreateJob(reportSchedule);
            
            await scheduler.AddJob(job, true);

            ITrigger trigger = CreateTrigger(reportSchedule, job.Key);
            await scheduler.ScheduleJob(trigger);
        }


        public static IJobDetail CreateJob(MeasureReportScheduleModel reportSchedule)
        {
            JobDataMap jobDataMap = new JobDataMap();

            jobDataMap.Put(ReportConstants.MeasureReportSubmissionScheduler.ReportScheduleModel, reportSchedule);

            return JobBuilder
                .Create(typeof(GenerateDataAcquisitionRequestsForPatientsToQuery))
                .StoreDurably()
                .WithIdentity(reportSchedule.Id, ReportConstants.MeasureReportSubmissionScheduler.Group)
                .WithDescription($"{reportSchedule.Id}-{ReportConstants.MeasureReportSubmissionScheduler.Group}")
                .UsingJobData(jobDataMap)
                .Build();
        }

        private static ITrigger CreateTrigger(MeasureReportScheduleModel reportSchedule, JobKey jobKey)
        {
            JobDataMap jobDataMap = new JobDataMap();

            jobDataMap.Put(ReportConstants.MeasureReportSubmissionScheduler.ReportScheduleModel, reportSchedule);

            return TriggerBuilder
                .Create()
                .ForJob(jobKey)
                .WithIdentity(Guid.NewGuid().ToString(), jobKey.Group)
                .WithCronSchedule(reportSchedule.ScheduledTrigger)
                .WithDescription($"{reportSchedule.Id}-{reportSchedule.ScheduledTrigger}")
                .UsingJobData(jobDataMap)
                .Build();
        }


        public static async Task DeleteJob(MeasureReportScheduleModel reportSchedule, IScheduler scheduler)
        {
            JobKey jobKey = new JobKey(reportSchedule.Id, ReportConstants.MeasureReportSubmissionScheduler.Group);
            await scheduler.DeleteJob(jobKey);
        }

        public static async Task RescheduleJob(MeasureReportScheduleModel reportSchedule, IScheduler scheduler)
        {
            await DeleteJob(reportSchedule, scheduler);
            await CreateJobAndTrigger(reportSchedule, scheduler);
        }
    }
}
