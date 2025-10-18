using Census.Domain.Entities;
using LantanaGroup.Link.Census.Application.Interfaces;
using LantanaGroup.Link.Census.Application.Jobs;
using LantanaGroup.Link.Census.Application.Settings;
using LantanaGroup.Link.Shared.Application.Models;
using Quartz;
using Quartz.Impl.Matchers;
using Quartz.Spi;

namespace LantanaGroup.Link.Census.Application.Repositories.Scheduling;

public class CensusSchedulingRepository : ICensusSchedulingRepository
{
    private readonly ILogger<CensusSchedulingRepository> _logger;
    private readonly IJobFactory _jobFactory;
    private readonly ISchedulerFactory _schedulerFactory;

    public CensusSchedulingRepository(
        ILogger<CensusSchedulingRepository> logger,
        IJobFactory jobFactory,
        [FromKeyedServices("InMemoryScheduler")] ISchedulerFactory schedulerFactory)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _jobFactory = jobFactory ?? throw new ArgumentNullException(nameof(jobFactory));
        _schedulerFactory = schedulerFactory ?? throw new ArgumentNullException(nameof(schedulerFactory));
    }

    public async Task AddJobForFacility(CensusConfigEntity censusConfig, IScheduler scheduler = null)
    {
        scheduler ??= await _schedulerFactory.GetScheduler();
        scheduler.JobFactory = _jobFactory;

        await DeleteJobsForFacility(censusConfig.FacilityID, scheduler);

        CreateJobAndTrigger(censusConfig, scheduler);
    }

    public IJobDetail CreateJob(CensusConfigEntity facility)
    {
        JobDataMap jobDataMap = new JobDataMap();
        jobDataMap.Put(CensusConstants.Scheduler.Facility, facility);
        jobDataMap.Put(CensusConstants.Scheduler.ReportType, KafkaTopic.PatientCensusScheduled.ToString());

        string jobName = $"{facility.FacilityID}-{KafkaTopic.PatientCensusScheduled.ToString()}";

        return JobBuilder
            .Create<SchedulePatientListRetrieval>()
            .StoreDurably()
            .WithIdentity(jobName, KafkaTopic.PatientCensusScheduled.ToString())
            .WithDescription($"{jobName}-{KafkaTopic.PatientCensusScheduled.ToString()}")
            .UsingJobData(jobDataMap)
            .Build();
    }

    public async void CreateJobAndTrigger(CensusConfigEntity facility, IScheduler scheduler)
    {
        IJobDetail job = CreateJob(facility);
        await scheduler.AddJob(job, true);

        ITrigger trigger = CreateTrigger(facility.ScheduledTrigger, job.Key);
        await scheduler.ScheduleJob(trigger);
    }

    public ITrigger CreateTrigger(string scheduledTrigger, JobKey jobKey)
    {
        JobDataMap jobDataMap = new JobDataMap();
        jobDataMap.Put(CensusConstants.Scheduler.JobTrigger, scheduledTrigger);

        return TriggerBuilder
            .Create()
            .ForJob(jobKey)
            .WithIdentity(Guid.NewGuid().ToString(), jobKey.Group)
            .WithCronSchedule(scheduledTrigger)
            .WithDescription(scheduledTrigger)
            .UsingJobData(jobDataMap)
            .Build();
    }

    public async Task DeleteJobsForFacility(string facilityId, IScheduler scheduler)
    {
        string jobKeyName = $"{facilityId}-{KafkaTopic.PatientCensusScheduled}";
        var groupMatcher = GroupMatcher<JobKey>.GroupContains(KafkaTopic.PatientCensusScheduled.ToString());
        var jobKeys = await scheduler.GetJobKeys(groupMatcher);
        if (jobKeys == null || !jobKeys.Any())
        {
            _logger.LogWarning("Could not find any job keys for {JobKeyName}", jobKeyName);
            return;
        }

        JobKey jobKey = jobKeys.FirstOrDefault(key => key.Name == jobKeyName);
        if (jobKey == null)
        {
            _logger.LogWarning("Could not find job key {JobKeyName}", jobKeyName);
            return;
        }

        await scheduler.DeleteJob(jobKey);
    }

    public void GetAllJobs(IScheduler scheduler)
    {
        var jobGroups = scheduler.GetJobGroupNames().Result;
        foreach (string group in jobGroups)
        {
            var groupMatcher = GroupMatcher<JobKey>.GroupContains(group);
            var jobKeys = scheduler.GetJobKeys(groupMatcher).Result;
            foreach (JobKey jobKey in jobKeys)
            {
                IJobDetail detail = scheduler.GetJobDetail(jobKey).Result;
                IReadOnlyCollection<ITrigger> triggers = scheduler.GetTriggersOfJob(jobKey).Result;
                foreach (ITrigger trigger in triggers)
                {
                    _logger.LogInformation("Group: {Group}, Job: {JobName}, Description: {Description}, Trigger: {TriggerName}, Group: {TriggerGroup}, Type: {TriggerType}, State: {TriggerState}, NextFire: {NextFireTime}, PreviousFire: {PreviousFireTime}",
                        group,
                        jobKey.Name,
                        detail.Description,
                        trigger.Key.Name,
                        trigger.Key.Group,
                        trigger.GetType().Name,
                        scheduler.GetTriggerState(trigger.Key).Result,
                        trigger.GetNextFireTimeUtc()?.LocalDateTime.ToString() ?? "N/A",
                        trigger.GetPreviousFireTimeUtc()?.LocalDateTime.ToString() ?? "N/A");
                }
            }
        }
    }

    public async Task RescheduleJob(string scheduledTrigger, JobKey jobKey, IScheduler scheduler)
    {
        IReadOnlyCollection<ITrigger> triggers = await scheduler.GetTriggersOfJob(jobKey);
        foreach (ITrigger trigger in triggers)
        {
            TriggerKey oldTrigger = trigger.Key;
            await scheduler.UnscheduleJob(oldTrigger);
        }

        ITrigger newTrigger = CreateTrigger(scheduledTrigger, jobKey);
        await scheduler.ScheduleJob(newTrigger);
    }

    public async Task UpdateJobsForFacility(CensusConfigEntity config, IScheduler scheduler = null)
    {
        scheduler ??= await _schedulerFactory.GetScheduler();
        scheduler.JobFactory = _jobFactory;

        await DeleteJobsForFacility(config.FacilityID, scheduler);

        var groupMatcher = GroupMatcher<JobKey>.GroupContains(KafkaTopic.PatientCensusScheduled.ToString());
        string jobKeyName = $"{config.FacilityID}-{KafkaTopic.PatientCensusScheduled}";
        JobKey jobKey = (await scheduler.GetJobKeys(groupMatcher)).FirstOrDefault(key => key.Name == jobKeyName);

        if (jobKey != null)
        {
            await RescheduleJob(config.ScheduledTrigger, jobKey, scheduler);
        }
        else
        {
            CreateJobAndTrigger(config, scheduler);
        }
    }

    public void Dispose()
    {
        // No resources to dispose
    }
}