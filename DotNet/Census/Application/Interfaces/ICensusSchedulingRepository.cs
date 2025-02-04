using Census.Domain.Entities;
using Quartz;

namespace LantanaGroup.Link.Census.Application.Interfaces;

public interface ICensusSchedulingRepository : IDisposable
{
    Task AddJobForFacility(CensusConfigEntity censusConfig, IScheduler scheduler);

    Task DeleteJobsForFacility(String facilityId, IScheduler scheduler);

    Task UpdateJobsForFacility(CensusConfigEntity config, IScheduler scheduler);

    Task RescheduleJob(string scheduledTrigger, JobKey jobKey, IScheduler scheduler);

    void CreateJobAndTrigger(CensusConfigEntity facility, IScheduler scheduler);

    IJobDetail CreateJob(CensusConfigEntity facility);

    ITrigger CreateTrigger(string ScheduledTrigger, JobKey jobKey);
}
