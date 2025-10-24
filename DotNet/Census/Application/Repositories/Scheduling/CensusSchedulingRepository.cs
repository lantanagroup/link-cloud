using Census.Domain.Entities;
using LantanaGroup.Link.Census.Application.Interfaces;
using LantanaGroup.Link.Census.Application.Jobs;
using LantanaGroup.Link.Census.Application.Settings;
using LantanaGroup.Link.Shared.Application.Models;
using Quartz;
using Quartz.Impl.Matchers;

namespace LantanaGroup.Link.Census.Application.Repositories.Scheduling;

public class CensusSchedulingRepository : ICensusSchedulingRepository
{
    public async Task AddJobForFacility(CensusConfigEntity censusConfig, IScheduler scheduler)
    {
        await DeleteJobsForFacility(censusConfig.FacilityID, scheduler);

        await CreateJobAndTrigger(censusConfig, scheduler);
    }

    public IJobDetail CreateJob(CensusConfigEntity facility)
    {
        JobDataMap jobDataMap = new JobDataMap();

        jobDataMap.Put(CensusConstants.Scheduler.Facility, facility);

        jobDataMap.Put(CensusConstants.Scheduler.ReportType, KafkaTopic.PatientCensusScheduled.ToString());

        string jobName = $"{facility.FacilityID}-{KafkaTopic.PatientCensusScheduled.ToString()}";

        return JobBuilder
            .Create(typeof(SchedulePatientListRetrieval))
            .StoreDurably()
            .WithIdentity(jobName, KafkaTopic.PatientCensusScheduled.ToString())
            .WithDescription($"{jobName}-{KafkaTopic.PatientCensusScheduled.ToString()}")
            .UsingJobData(jobDataMap)
            .Build();
    }

    public async Task CreateJobAndTrigger(CensusConfigEntity facility, IScheduler scheduler)
    {
        IJobDetail job = CreateJob(facility);

        await scheduler.AddJob(job, true);

        ITrigger trigger = CreateTrigger(facility.ScheduledTrigger, job.Key);
        await scheduler.ScheduleJob(trigger);
    }

    public ITrigger CreateTrigger(string ScheduledTrigger, JobKey jobKey)
    {
        JobDataMap jobDataMap = new JobDataMap();

        jobDataMap.Put(CensusConstants.Scheduler.JobTrigger, ScheduledTrigger);

        return TriggerBuilder
            .Create()
            .ForJob(jobKey)
            .WithIdentity(Guid.NewGuid().ToString(), jobKey.Group)
            .WithCronSchedule(ScheduledTrigger)
            .WithDescription(ScheduledTrigger)
            .UsingJobData(jobDataMap)
            .Build();
    }

    public async Task DeleteJobsForFacility(string facilityId, IScheduler scheduler)
    {
        string jobKeyName = $"{facilityId}-{KafkaTopic.PatientCensusScheduled}";
        var groupMatcher = GroupMatcher<JobKey>.GroupContains(KafkaTopic.PatientCensusScheduled.ToString());
        var jobkeys = await scheduler.GetJobKeys(groupMatcher);
        if (jobkeys == null || jobkeys.Count == 0)
        {
            return;
        }
        JobKey jobKey = jobkeys?.FirstOrDefault(key => key.Name == jobKeyName);

        if (jobKey == null)
        {
            return;
        }

        // Unschedule all triggers
        IReadOnlyCollection<ITrigger> triggers = await scheduler.GetTriggersOfJob(jobKey);
        foreach (ITrigger trigger in triggers)
        {
            await scheduler.UnscheduleJob(trigger.Key);
        }

        // Now delete the job itself to ensure fresh data on recreate
        await scheduler.DeleteJob(jobKey);
    }

    public void Dispose()
    {
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

    public async Task UpdateJobsForFacility(CensusConfigEntity config, IScheduler scheduler)
    {
        await DeleteJobsForFacility(config.FacilityID, scheduler);

        // Always recreate with current config
        await CreateJobAndTrigger(config, scheduler);
    }
}
