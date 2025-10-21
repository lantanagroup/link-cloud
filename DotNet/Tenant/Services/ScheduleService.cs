using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Services.Security;
using LantanaGroup.Link.Tenant.Config;
using LantanaGroup.Link.Tenant.Entities;
using LantanaGroup.Link.Tenant.Jobs;
using LantanaGroup.Link.Tenant.Repository.Context;
using Microsoft.EntityFrameworkCore;
using Quartz;
using Quartz.Impl.Matchers;
using Quartz.Spi;

namespace LantanaGroup.Link.Tenant.Services
{
    public class ScheduleService : IHostedService
    {
        public const string MONTHLY = "Monthly";
        public const string WEEKLY = "Weekly";
        public const string DAILY = "Daily";

        private IScheduler _scheduler;

        private readonly ILogger<ScheduleService> _logger;
        private readonly ISchedulerFactory _schedulerFactory;
        private readonly IJobFactory _jobFactory;
        private readonly IServiceScopeFactory _scopeFactory;

        public ScheduleService(
            ILogger<ScheduleService> logger,
            ISchedulerFactory schedulerFactory,
            IServiceScopeFactory serviceScopeFactory,
            IJobFactory jobFactory)
        {
            _logger = logger;
            _schedulerFactory = schedulerFactory;
            _jobFactory = jobFactory;
            _scopeFactory = serviceScopeFactory;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            _scheduler = await _schedulerFactory.GetScheduler(cancellationToken);

            _scheduler.JobFactory = _jobFactory;

            using (var scope = _scopeFactory.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<TenantDbContext>();
                var facilities = await context.Facilities.ToListAsync(cancellationToken);
                foreach (Facility facility in facilities)
                {
                    if (string.IsNullOrEmpty(facility.TimeZone))
                    {
                        _logger.LogError("Facility {FacilityId} does not have a timezone set. Skipping scheduled jobs for this facility.", facility.FacilityId.SanitizeAndRemove());
                        continue;
                    }
                    await AddJobsForFacility(facility, cancellationToken);
                }
            }

            await _scheduler.Start(cancellationToken);
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            await _scheduler?.Shutdown(cancellationToken);
        }

        public async Task AddJobsForFacility(Facility facility, CancellationToken cancellationToken = default)
        {
            // Create a job and trigger for monthly reports
            if (facility.ScheduledReports.Monthly.Length > 0)
            {
                await CreateJobAndTrigger(facility, MONTHLY, cancellationToken);
            }

            // Create a job and trigger for weekly reports
            if (facility.ScheduledReports.Weekly.Length > 0)
            {
                await CreateJobAndTrigger(facility, WEEKLY, cancellationToken);
            }

            // Create a job and trigger for daily reports  
            if (facility.ScheduledReports.Daily.Length > 0)
            {
                await CreateJobAndTrigger(facility, DAILY, cancellationToken);
            }
        }

        public async Task DeleteJobsForFacility(string facilityId, List<string>? frequencies = null, CancellationToken cancellationToken = default)
        {
            frequencies ??= new List<string> { MONTHLY, WEEKLY, DAILY };

            var scheduler = await _schedulerFactory.GetScheduler(cancellationToken);

            foreach (string frequency in frequencies)
            {
                string jobKeyName = $"{facilityId}-{frequency}";
                JobKey jobKey = new JobKey(jobKeyName, nameof(KafkaTopic.ReportScheduled));

                IJobDetail job = await scheduler.GetJobDetail(jobKey, cancellationToken);

                if (job != null)
                {
                    await scheduler.DeleteJob(job.Key, cancellationToken);
                }
            }
        }

        public async Task DeleteJob(string facilityId, CancellationToken cancellationToken = default)
        {
            var scheduler = await _schedulerFactory.GetScheduler(cancellationToken);

            JobKey jobKey = new JobKey(facilityId, nameof(KafkaTopic.ReportScheduled));

            IJobDetail job = await scheduler.GetJobDetail(jobKey, cancellationToken);

            if (job != null)
            {
                await scheduler.DeleteJob(job.Key, cancellationToken);
            }
        }

        public async Task UpdateJobsForFacility(Facility updatedFacility, Facility existingFacility, CancellationToken cancellationToken = default)
        {
            List<string> frequencies = new List<string>();

            if (!updatedFacility.ScheduledReports.Monthly.Distinct().OrderBy(x => x).SequenceEqual(existingFacility.ScheduledReports.Monthly.Distinct().OrderBy(x => x)))
            {
                frequencies.Add(MONTHLY);
            }
            if (!updatedFacility.ScheduledReports.Weekly.Distinct().OrderBy(x => x).SequenceEqual(existingFacility.ScheduledReports.Weekly.Distinct().OrderBy(x => x)))
            {
                frequencies.Add(WEEKLY);
            }
            if (!updatedFacility.ScheduledReports.Daily.Distinct().OrderBy(x => x).SequenceEqual(existingFacility.ScheduledReports.Daily.Distinct().OrderBy(x => x)))
            {
                frequencies.Add(DAILY);
            }

            // Delete jobs that are in existing facility but not in the updated one
            if (frequencies.Count > 0)
            {
                await DeleteJobsForFacility(updatedFacility.FacilityId, frequencies, cancellationToken);
            }

            // Recreate jobs for updated facility for changed frequencies
            if (frequencies.Contains(MONTHLY) && updatedFacility.ScheduledReports.Monthly.Length > 0)
            {
                var scheduler = await _schedulerFactory.GetScheduler(cancellationToken);
                await CreateJobAndTrigger(updatedFacility, MONTHLY, cancellationToken);
            }

            if (frequencies.Contains(WEEKLY) && updatedFacility.ScheduledReports.Weekly.Length > 0)
            {
                var scheduler = await _schedulerFactory.GetScheduler(cancellationToken);
                await CreateJobAndTrigger(updatedFacility, WEEKLY, cancellationToken);
            }

            if (frequencies.Contains(DAILY) && updatedFacility.ScheduledReports.Daily.Length > 0)
            {
                var scheduler = await _schedulerFactory.GetScheduler(cancellationToken);
                await CreateJobAndTrigger(updatedFacility, DAILY, cancellationToken);
            }
        }

        private async Task CreateJobAndTrigger(Facility facility, string frequency, CancellationToken cancellationToken = default)
        {
            string jobName = $"{facility.FacilityId}-{frequency}";
            JobKey jobKey = new JobKey(jobName, nameof(KafkaTopic.ReportScheduled));

            IJobDetail? job = await _scheduler.GetJobDetail(jobKey, cancellationToken);

            if (job == null)
            {
                job = CreateJob(facility, frequency);
                await _scheduler.AddJob(job, true, cancellationToken);

            }

            var triggers = await _scheduler.GetTriggersOfJob(jobKey, cancellationToken);
            if (triggers == null || !triggers.Any())
            {
                var trigger = CreateTrigger(facility, frequency, job.Key);

                try
                {
                    await _scheduler.ScheduleJob(trigger, cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to schedule trigger for job {JobName} (Facility: {FacilityId}, Frequency: {Frequency})", jobName, facility.FacilityId, frequency);
                    throw;
                }
            }
        }

        private IJobDetail CreateJob(Facility facility, string frequency)
        {
            JobDataMap jobDataMap = new JobDataMap();

            jobDataMap.Put(TenantConstants.Scheduler.Facility, facility);
            jobDataMap.Put(TenantConstants.Scheduler.Frequency, frequency);

            string jobName = $"{facility.FacilityId}-{frequency}";

            return JobBuilder
                .Create(typeof(ReportScheduledJob))
                .StoreDurably()
                .WithIdentity(jobName, nameof(KafkaTopic.ReportScheduled))
                .WithDescription($"{jobName}")
                .UsingJobData(jobDataMap)
                .Build();
        }

        private ITrigger CreateTrigger(Facility facility, string frequency, JobKey jobKey)
        {
            JobDataMap jobDataMap = new JobDataMap();
            string scheduledTrigger = "";

            // Determine the cron trigger based on frequency
            switch (frequency)
            {
                case MONTHLY:
                    scheduledTrigger = "0 0 0 1 * ? *";
                    //scheduledTrigger = "0 11 14 * * ? *"; // Uncomment for testing
                    break;
                case WEEKLY:
                    scheduledTrigger = "0 0 0 ? * 1 *";
                    //scheduledTrigger = "0 40 15 * * ? *"; // Uncomment for testing
                    break;
                case DAILY:
                    scheduledTrigger = "0 0 0 * * ? *";
                    //scheduledTrigger = "0 40 15 * * ? *"; // Uncomment for testing
                    break;
            }

            // Set the cron trigger based on timezone
            TimeZoneInfo timeZone = TimeZoneInfo.FindSystemTimeZoneById(facility.TimeZone);

            jobDataMap.Put(TenantConstants.Scheduler.JobTrigger, scheduledTrigger);

            return TriggerBuilder
                .Create()
                .ForJob(jobKey)
                .WithIdentity(Guid.NewGuid().ToString(), jobKey.Group)
                .WithCronSchedule(scheduledTrigger, x => x.InTimeZone(timeZone))
                .WithDescription(scheduledTrigger)
                .UsingJobData(jobDataMap)
                .Build();
        }

        public async Task GetAllJobs(CancellationToken cancellationToken = default)
        {
            var scheduler = await _schedulerFactory.GetScheduler(cancellationToken);
            var jobGroups = await scheduler.GetJobGroupNames(cancellationToken);

            foreach (string group in jobGroups)
            {
                var groupMatcher = GroupMatcher<JobKey>.GroupContains(group);
                var jobKeys = await scheduler.GetJobKeys(groupMatcher, cancellationToken);
                foreach (JobKey jobKey in jobKeys)
                {
                    IJobDetail detail = await scheduler.GetJobDetail(jobKey, cancellationToken);
                    IReadOnlyCollection<ITrigger> triggers = await scheduler.GetTriggersOfJob(jobKey, cancellationToken);
                    foreach (ITrigger trigger in triggers)
                    {
                        _logger.LogInformation("Job details - Group: {Group}, JobName: {JobName}, Description: {Description}, TriggerName: {TriggerName}, TriggerGroup: {TriggerGroup}, TriggerType: {TriggerType}, State: {State}", 
                            group, jobKey.Name, detail.Description, trigger.Key.Name, trigger.Key.Group, trigger.GetType().Name, await scheduler.GetTriggerState(trigger.Key, cancellationToken));
                        DateTimeOffset? nextFireTime = trigger.GetNextFireTimeUtc();
                        if (nextFireTime.HasValue)
                        {
                            _logger.LogInformation("Next Fire Time: {NextFireTime}", nextFireTime.Value.LocalDateTime);
                        }
                        DateTimeOffset? previousFireTime = trigger.GetPreviousFireTimeUtc();
                        if (previousFireTime.HasValue)
                        {
                            _logger.LogInformation("Previous Fire Time: {PreviousFireTime}", previousFireTime.Value.LocalDateTime);
                        }
                    }
                }
            }
        }
    }
}