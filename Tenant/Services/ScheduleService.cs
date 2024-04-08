
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

        private readonly ISchedulerFactory _schedulerFactory;
        private readonly Quartz.Spi.IJobFactory _jobFactory;

        private FacilityConfigurationService _facilityConfigurationService;

        private static Dictionary<string, Type> _topicJobs = new Dictionary<string, Type>();

        private readonly IServiceScopeFactory _scopeFactory;

        static ScheduleService()
        {
            _topicJobs.Add(KafkaTopic.ReportScheduled.ToString(), typeof(ReportScheduledJob));
            _topicJobs.Add(KafkaTopic.RetentionCheckScheduled.ToString(), typeof(RetentionCheckScheduledJob));
        }

        public ScheduleService(
           ISchedulerFactory schedulerFactory,
           IServiceScopeFactory serviceScopeFactory,
           IJobFactory jobFactory)
        {
            _schedulerFactory = schedulerFactory;
            _jobFactory = jobFactory;
        //   _facilityConfigurationService = facilityConfigurationService;
            this._scopeFactory = serviceScopeFactory;
        }

        public IScheduler Scheduler { get; set; }
       // static ConcurrentDictionary<string, JobKey> jobs = new ConcurrentDictionary<string, JobKey>();

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            Scheduler = await _schedulerFactory.GetScheduler(cancellationToken);

            Scheduler.JobFactory = _jobFactory;

            //List<FacilityConfigModel> facilities = _facilityConfigurationService.GetAllFacilities(cancellationToken).Result;

            using (var scope = _scopeFactory.CreateScope())
            {
                var _context = scope.ServiceProvider.GetRequiredService<FacilityDbContext>();
                var facilities = await _context.Facilities.ToListAsync();
                foreach (FacilityConfigModel facility in facilities)
                {
                    await AddJobsForFacility(facility, Scheduler);
                }
            }

            await Scheduler.Start(cancellationToken);
        }


        public async Task StopAsync(CancellationToken cancellationToken)
        {
            await Scheduler?.Shutdown(cancellationToken);
        }

        public static async Task AddJobsForFacility(FacilityConfigModel facility, IScheduler scheduler)
        {
            foreach (ScheduledTaskModel task in facility.ScheduledTasks)
            {

                foreach (ScheduledTaskModel.ReportTypeSchedule reportTypeSchedule in task.ReportTypeSchedules)
                {
                    createJobAndTrigger(facility, task.KafkaTopic.ToString(), reportTypeSchedule, scheduler);
                }

            }
        }

        public static async Task DeleteJobsForFacility(String facilityId, List<ScheduledTaskModel> jobsToBeDeleted, IScheduler scheduler)
        {
            foreach (ScheduledTaskModel task in jobsToBeDeleted)
            {
                var groupMatcher = GroupMatcher<JobKey>.GroupContains(task.KafkaTopic);

                foreach (ScheduledTaskModel.ReportTypeSchedule reportTypeSchedule in task.ReportTypeSchedules)
                {
                    string jobKeyName = $"{facilityId}-{reportTypeSchedule.ReportType}";

                    JobKey jobKey = scheduler.GetJobKeys(groupMatcher).Result.FirstOrDefault(key => key.Name == jobKeyName);

                    IReadOnlyCollection<ITrigger> triggers = scheduler.GetTriggersOfJob(jobKey).Result;

                    foreach (ITrigger trigger in triggers)
                    {
                        TriggerKey oldTrigger = trigger.Key;

                        await scheduler.UnscheduleJob(oldTrigger);
                    }

                }
            }
        }

        public static async Task UpdateJobsForFacility(FacilityConfigModel newFacility, FacilityConfigModel existingFacility, IScheduler scheduler)
        {
            // delete jobs that are in existing facility but not in the new one

            List<ScheduledTaskModel> tasksToBeDeleted = existingFacility.ScheduledTasks.Where(existingScheduledTask => !newFacility.ScheduledTasks.Select(newScheduledTask => newScheduledTask.KafkaTopic).Contains(existingScheduledTask.KafkaTopic)).ToList();

            foreach (ScheduledTaskModel existingScheduledTask in existingFacility.ScheduledTasks)
            {
                ScheduledTaskModel taskModel = new ScheduledTaskModel();

                var newTask = newFacility.ScheduledTasks.FirstOrDefault(newScheduledTask => newScheduledTask.KafkaTopic == existingScheduledTask.KafkaTopic);

                if (newTask is not null)
                {
                    List<ScheduledTaskModel.ReportTypeSchedule> schedulesToBeDeleted = existingScheduledTask.ReportTypeSchedules.Where(existingReportTypeScheduled => !newTask.ReportTypeSchedules.Select(newReportTypeScheduled => newReportTypeScheduled.ReportType).Contains(existingReportTypeScheduled.ReportType)).ToList();

                    taskModel.KafkaTopic = existingScheduledTask.KafkaTopic;

                    taskModel.ReportTypeSchedules.AddRange(schedulesToBeDeleted ?? schedulesToBeDeleted);

                    tasksToBeDeleted.Add(taskModel);
                }

            }

            ScheduleService.DeleteJobsForFacility(newFacility.Id.ToString(), tasksToBeDeleted, scheduler);

            // update and add new jobs

            foreach (ScheduledTaskModel task in newFacility.ScheduledTasks)
            {
                var groupMatcher = GroupMatcher<JobKey>.GroupContains(task.KafkaTopic.ToString());

                foreach (ScheduledTaskModel.ReportTypeSchedule reportTypeSchedule in task.ReportTypeSchedules)
                {
                    string jobKeyName = $"{newFacility.Id}-{reportTypeSchedule.ReportType}";

                    JobKey jobKey = scheduler.GetJobKeys(groupMatcher).Result.FirstOrDefault(key => key.Name == jobKeyName);

                    if (jobKey is not null)
                    {
                        await RescheduleJob(reportTypeSchedule, jobKey, scheduler);
                    }
                    else
                    {
                        createJobAndTrigger(newFacility, task.KafkaTopic, reportTypeSchedule, scheduler);

                    }
                }
            }

        }

        public static async Task RescheduleJob(ScheduledTaskModel.ReportTypeSchedule task, JobKey jobKey, IScheduler scheduler)
        {
            IReadOnlyCollection<ITrigger> triggers = scheduler.GetTriggersOfJob(jobKey).Result;

            foreach (ITrigger trigger in triggers)
            {
                TriggerKey oldTrigger = trigger.Key;

                await scheduler.UnscheduleJob(oldTrigger);

            }
            foreach (string trigger in task.ScheduledTriggers)
            {
                ITrigger newTrigger = CreateTrigger(trigger, jobKey);

                await scheduler.ScheduleJob(newTrigger);
            }

        }


        public static async void createJobAndTrigger(FacilityConfigModel facility, string topic, ScheduledTaskModel.ReportTypeSchedule reportTypeSchedule, IScheduler scheduler)
        {
            _topicJobs.TryGetValue(topic, out Type jobType);

            IJobDetail job = CreateJob(jobType, facility, reportTypeSchedule.ReportType, topic);

            await scheduler.AddJob(job, true);

            foreach (string scheduledTrigger in reportTypeSchedule.ScheduledTriggers)
            {
                ITrigger trigger = CreateTrigger(scheduledTrigger, job.Key);
                await scheduler.ScheduleJob(trigger);
            }

        }

        public static IJobDetail CreateJob(Type jobType, FacilityConfigModel facility, string reportType, string topic)
        {
            JobDataMap jobDataMap = new JobDataMap();

            jobDataMap.Put(TenantConstants.Scheduler.Facility, facility);

            jobDataMap.Put(TenantConstants.Scheduler.ReportType, reportType);

            string jobName = $"{facility.Id}-{reportType}";

            return JobBuilder
                .Create(jobType)
                .StoreDurably()
                .WithIdentity(jobName, topic)
                .WithDescription($"{jobName}-{topic}")
                .UsingJobData(jobDataMap)
                .Build();
        }

        public static ITrigger CreateTrigger(string ScheduledTrigger, JobKey jobKey)
        {
            JobDataMap jobDataMap = new JobDataMap();

            jobDataMap.Put(TenantConstants.Scheduler.JobTrigger, ScheduledTrigger);

            return TriggerBuilder
                .Create()
                .ForJob(jobKey)
                .WithIdentity(Guid.NewGuid().ToString(), jobKey.Group)
                .WithCronSchedule(ScheduledTrigger)
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
