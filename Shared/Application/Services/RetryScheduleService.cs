using LantanaGroup.Link.Report.Application.Models;
using LantanaGroup.Link.Shared.Jobs;
using MediatR;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Quartz;
using Quartz.Spi;

namespace LantanaGroup.Link.Shared.Application.Services
{
    public class RetryScheduleService : BackgroundService
    {
        private readonly ILogger<RetryScheduleService> _logger;
        private readonly IJobFactory _jobFactory;
        private readonly ISchedulerFactory _schedulerFactory;
        private readonly IMediator _mediator;

        public IScheduler Scheduler { get; set; } = default!;

        public RetryScheduleService(ILogger<RetryScheduleService> logger, IJobFactory jobFactory, ISchedulerFactory schedulerFactory, IMediator mediator)
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



            await Scheduler.Start(cancellationToken);
            _logger.LogInformation("RetryScheduleService started.");
        }

        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            await Scheduler.Shutdown(cancellationToken);
            await base.StopAsync(cancellationToken);
        }


        public static async Task CreateJobAndTrigger(RetryEntity entity, IScheduler scheduler)
        {
            IJobDetail job = CreateJob(entity);

            await scheduler.AddJob(job, true);

            ITrigger trigger = CreateTrigger(entity, job.Key);
            await scheduler.ScheduleJob(trigger);
        }


        public static IJobDetail CreateJob(RetryEntity entity)
        {
            JobDataMap jobDataMap = new JobDataMap();

            jobDataMap.Put("RetryService", entity);

            return JobBuilder
                .Create(typeof(RetryJob))
                .StoreDurably()
                .WithIdentity(entity.JobId)
                .WithDescription($"{entity.FacilityId}-{entity.Topic}")
                .UsingJobData(jobDataMap)
                .Build();
        }

        private static ITrigger CreateTrigger(RetryEntity entity, JobKey jobKey)
        {
            JobDataMap jobDataMap = new JobDataMap();

            jobDataMap.Put("RetryService", entity);

            return TriggerBuilder
                .Create()
                .ForJob(jobKey)
                .WithIdentity(Guid.NewGuid().ToString(), jobKey.Group)
                .WithCronSchedule(entity.ScheduledTrigger)
                .WithDescription($"{entity.Id}-{entity.ScheduledTrigger}")
                .UsingJobData(jobDataMap)
                .Build();
        }


        public static async Task DeleteJob(RetryEntity entity, IScheduler scheduler)
        {
            JobKey jobKey = new JobKey(entity.JobId);
            await scheduler.DeleteJob(jobKey);
        }

        public static async Task RescheduleJob(RetryEntity entity, IScheduler scheduler)
        {
            await DeleteJob(entity, scheduler);
            await CreateJobAndTrigger(entity, scheduler);
        }
    }
}
