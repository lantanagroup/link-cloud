using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Services.Security;
using LantanaGroup.Link.Shared.Domain.Repositories.Interfaces;
using LantanaGroup.Link.Shared.Jobs;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Quartz;
using Quartz.Spi;
using Task = System.Threading.Tasks.Task;

namespace LantanaGroup.Link.Shared.Application.Services
{
    public class RetryScheduleService : BackgroundService
    {
        private readonly ILogger<RetryScheduleService> _logger;
        private readonly IJobFactory _jobFactory;
        private readonly ISchedulerFactory _schedulerFactory;

        private readonly IServiceScopeFactory _serviceScopeFactory;

        public RetryScheduleService(ILogger<RetryScheduleService> logger, IJobFactory jobFactory, ISchedulerFactory schedulerFactory, IServiceScopeFactory serviceScopeFactory)
        {
            _logger = logger;
            _jobFactory = jobFactory;
            _schedulerFactory = schedulerFactory;
            _serviceScopeFactory = serviceScopeFactory;
        }

        protected override async Task ExecuteAsync(CancellationToken cancellationToken)
        {
            using var scope = _serviceScopeFactory.CreateScope();
            var retryRepository = scope.ServiceProvider.GetRequiredService<IBaseEntityRepository<RetryEntity>>();

            var scheduler = await _schedulerFactory.GetScheduler(cancellationToken);
            scheduler.JobFactory = _jobFactory;

            var retries = await retryRepository.GetAllAsync(cancellationToken);

            foreach (var retry in retries)
            {
                try
                {
                    await CreateJobAndTrigger(retry, scheduler);
                }
                catch (Exception ex)
                {
                    _logger.LogError($"Could not schedule {retry.Id.Sanitize()}: {ex.Message}");
                }
            }

            await scheduler.Start(cancellationToken);
            _logger.LogInformation("RetryScheduleService started.");
        }

        public override async Task StopAsync(CancellationToken cancellationToken)
        {
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

            jobDataMap.Put("RetryEntity", entity);

            return JobBuilder
                .Create(typeof(RetryJob))
                .StoreDurably(true)
                .WithIdentity(entity.JobId)
                .WithDescription($"{entity.FacilityId}-{entity.Topic}")
                .UsingJobData(jobDataMap)
                .Build();
        }

        private static ITrigger CreateTrigger(RetryEntity entity, JobKey jobKey)
        {
            JobDataMap jobDataMap = new JobDataMap();

            jobDataMap.Put("RetryEntity", entity);

            var offset = DateBuilder.DateOf(entity.ScheduledTrigger.Hour, entity.ScheduledTrigger.Minute, entity.ScheduledTrigger.Second);

            return TriggerBuilder
                .Create()                
                .StartAt(offset)
                .ForJob(jobKey)
                .WithIdentity(Guid.NewGuid().ToString(), jobKey.Group)
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
