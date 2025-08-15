
using LantanaGroup.Link.Shared.Application.Models;
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

        private readonly ISchedulerFactory _schedulerFactory;
        private readonly IJobFactory _jobFactory;
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

            using (var scope = _scopeFactory.CreateScope())
            {
                var _context = scope.ServiceProvider.GetRequiredService<TenantDbContext>();
                var facilities = await _context.Facilities.ToListAsync();
                foreach (Facility facility in facilities)
                {
                    if(String.IsNullOrEmpty(facility.TimeZone))
                    {
                        _logger.LogError($"Facility {facility.Id} does not have a timezone set. Skipping scheduled jobs for this facility.");
                        continue;
                    }
                    await AddJobsForFacility(facility, Scheduler);
                }
            }

            await Scheduler.Start(cancellationToken);
        }


        public async Task StopAsync(CancellationToken cancellationToken)
        {
            await Scheduler?.Shutdown(cancellationToken);
        }

        public static async Task AddJobsForFacility(Facility facility, IScheduler scheduler)
        {
            // create a job and trigger for monthly reports
            if (facility.ScheduledReports.Monthly.Length > 0)
            {
               await createJobAndTrigger(facility, MONTHLY, scheduler);
            }

            //create a job and trigger for weekly reports
            if (facility.ScheduledReports.Weekly.Length > 0)
            {
                await createJobAndTrigger(facility, WEEKLY, scheduler);
            }

            // create a job and trigger for daily reports  
            if (facility.ScheduledReports.Daily.Length > 0)
            {
                await createJobAndTrigger(facility, DAILY, scheduler);
            }

        }

        public static async Task DeleteJobsForFacility(String facilityId, IScheduler scheduler, List<string>? frequencies = null)
        {
            frequencies ??= [ MONTHLY, WEEKLY, DAILY ];

            if (frequencies != null)
            {
                foreach (String frequency in frequencies)
                {
                    var groupMatcher = GroupMatcher<JobKey>.GroupContains(nameof(KafkaTopic.ReportScheduled));

                    string jobKeyName = $"{facilityId}-{frequency}";

                    JobKey jobKey = new JobKey(jobKeyName)
                    {
                        Group = nameof(KafkaTopic.ReportScheduled)
                    };

                    IJobDetail job = await scheduler.GetJobDetail(jobKey);

                    if (job != null)
                    {
                        await scheduler.DeleteJob(job.Key);
                    }
                }
            }
        }

        public static async Task DeleteJob(string facilityId, IScheduler scheduler)
        {
            JobKey jobKey = new JobKey(facilityId)
            {
                Group = nameof(KafkaTopic.ReportScheduled)
            };

            IJobDetail job = await scheduler.GetJobDetail(jobKey);

            if (job != null)
            {
                await scheduler.DeleteJob(job.Key);
            }
        }



        public static async Task UpdateJobsForFacility(Facility updatedFacility, Facility existingFacility, IScheduler scheduler)
        {
            // delete jobs that are in existing facility but not in the new one
            List<string> frequencies = [];

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

            // delete jobs that are in existing facility but not in the updated one

            if (frequencies.Count() > 0)
            {
                await DeleteJobsForFacility(updatedFacility.Id.ToString(), scheduler, frequencies);
            }

            // recreate jobs for updated facility for all frequencies

            if (frequencies.Contains(MONTHLY) && updatedFacility.ScheduledReports.Monthly.Length > 0)
            {
                await createJobAndTrigger(updatedFacility, MONTHLY, scheduler);
            }

            if (frequencies.Contains(WEEKLY) && updatedFacility.ScheduledReports.Weekly.Length > 0)
            {
                await createJobAndTrigger(updatedFacility, WEEKLY, scheduler);
            }

            if (frequencies.Contains(DAILY) && updatedFacility.ScheduledReports.Daily.Length > 0)
            {
                await createJobAndTrigger(updatedFacility, DAILY, scheduler);
            }

        }

        public static async Task createJobAndTrigger(Facility facility, string frequency, IScheduler scheduler)
        {

            IJobDetail job = CreateJob(facility, frequency);

            await scheduler.AddJob(job, true);

            ITrigger trigger = CreateTrigger(facility, frequency, job.Key);

            await scheduler.ScheduleJob(trigger);

        }

        public static IJobDetail CreateJob(Facility facility, string frequency)
        {
            JobDataMap jobDataMap = new JobDataMap();

            jobDataMap.Put(TenantConstants.Scheduler.Facility, facility);

            jobDataMap.Put(TenantConstants.Scheduler.Frequency, frequency);

            string jobName = $"{facility.Id}-{frequency}";

            return JobBuilder
                .Create(typeof(ReportScheduledJob))
                .StoreDurably()
                .WithIdentity(jobName, nameof(KafkaTopic.ReportScheduled))
                .WithDescription($"{jobName}")
                .UsingJobData(jobDataMap)
                .Build();
        }

        public static ITrigger CreateTrigger(Facility facility, string frequency, JobKey jobKey)
        {
            JobDataMap jobDataMap = new JobDataMap();
            string ScheduledTrigger = "";

            // determine the chron trigger based on frequency
            switch (frequency)
            {
                case MONTHLY:
                    ScheduledTrigger = "0 0 0 1 * ? *";
                    //ScheduledTrigger = "0 11 14 * * ? *";
                    break;
                case WEEKLY:
                    ScheduledTrigger = "0 0 0 ? * 1 *";
                    //ScheduledTrigger = "0 40 15 * * ? *";
                    break;
                case DAILY:
                     ScheduledTrigger = "0 0 0 * * ? *";
                    //ScheduledTrigger = "0 40 15 * * ? *";
                    break;
            }       


            // set the chron trigger based on timezone
            TimeZoneInfo timeZone = TimeZoneInfo.FindSystemTimeZoneById(facility.TimeZone);

            jobDataMap.Put(TenantConstants.Scheduler.JobTrigger, ScheduledTrigger);
 
            return TriggerBuilder
                .Create()
                .ForJob(jobKey)
                .WithIdentity(Guid.NewGuid().ToString(), jobKey.Group)
                .WithCronSchedule(ScheduledTrigger, x => x.InTimeZone(timeZone))
                .WithDescription(ScheduledTrigger)
                .UsingJobData(jobDataMap)
                .Build();
        }
     
        public static void GetAllJobs(IScheduler scheduler)
        {
            var jobGroups = scheduler.GetJobGroupNames().Result;

            foreach (string group in jobGroups)
            {
                var groupMatcher = Quartz.Impl.Matchers.GroupMatcher<JobKey>.GroupContains(group);
                var jobKeys = scheduler.GetJobKeys(groupMatcher).Result;
                foreach (JobKey jobKey in jobKeys)
                {
                    IJobDetail detail = scheduler.GetJobDetail(jobKey).Result;
                    IReadOnlyCollection<ITrigger> triggers = scheduler.GetTriggersOfJob(jobKey).Result;
                    foreach (ITrigger trigger in triggers)
                    {
                        Console.WriteLine(group);
                        Console.WriteLine(jobKey.Name);
                        Console.WriteLine(detail.Description);
                        Console.WriteLine(trigger.Key.Name);
                        Console.WriteLine(trigger.Key.Group);
                        Console.WriteLine(trigger.GetType().Name);
                        Console.WriteLine(scheduler.GetTriggerState(trigger.Key));
                        DateTimeOffset? nextFireTime = trigger.GetNextFireTimeUtc();
                        if (nextFireTime.HasValue)
                        {
                            Console.WriteLine(nextFireTime.Value.LocalDateTime.ToString());
                        }

                        DateTimeOffset? previousFireTime = trigger.GetPreviousFireTimeUtc();
                        if (previousFireTime.HasValue)
                        {
                            Console.WriteLine(previousFireTime.Value.LocalDateTime.ToString());
                        }
                    }
                }
            }
        }

    }

}
