using Census.Domain.Entities;
using LantanaGroup.Link.Census.Models;
using Quartz;

namespace LantanaGroup.Link.Census.Application.Interfaces;

public interface ICensusSchedulingRepository : IDisposable
{
    Task AddJobForFacility(CensusConfigModel censusConfig, IScheduler scheduler);

    Task DeleteJobsForFacility(string facilityId, IScheduler scheduler);

    Task UpdateJobsForFacility(CensusConfigModel config, IScheduler scheduler);

    Task RescheduleJob(string scheduledTrigger, JobKey jobKey, IScheduler scheduler);

    void CreateJobAndTrigger(CensusConfigModel facility, IScheduler scheduler);

    IJobDetail CreateJob(CensusConfigModel facility);

    ITrigger CreateTrigger(string ScheduledTrigger, JobKey jobKey);
}
