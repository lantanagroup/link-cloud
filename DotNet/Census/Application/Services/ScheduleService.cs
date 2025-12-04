using global::Census.Domain.Entities;
using LantanaGroup.Link.Census.Application.Interfaces;
using LantanaGroup.Link.Census.Application.Jobs;
using LantanaGroup.Link.Census.Application.Settings;
using LantanaGroup.Link.Census.Domain.Managers;
using LantanaGroup.Link.Shared.Application.Models;
using Quartz;
using Quartz.Impl.Matchers;
using Quartz.Spi;

namespace LantanaGroup.Link.Census.Application.Services;

public class ScheduleService : BackgroundService
{
    private readonly ILogger<ScheduleService> _logger;
    private readonly ISchedulerFactory _schedulerFactory;
    private readonly IJobFactory _jobFactory;
    private readonly IServiceScopeFactory _serviceScopeFactory;

    private static Dictionary<string, Type> _topicJobs = new Dictionary<string, Type>();


    static ScheduleService()
    {
        _topicJobs.Add(KafkaTopic.PatientCensusScheduled.ToString(), typeof(SchedulePatientListRetrieval));
    }

    public ScheduleService(
       ILogger<ScheduleService> logger,
       ISchedulerFactory schedulerFactory,
       IJobFactory jobFactory,
       IServiceScopeFactory serviceScopeFactory)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _schedulerFactory = schedulerFactory ?? throw new ArgumentNullException(nameof(schedulerFactory));
        _jobFactory = jobFactory ?? throw new ArgumentNullException(nameof(jobFactory));
        _serviceScopeFactory = serviceScopeFactory ?? throw new ArgumentNullException(nameof(serviceScopeFactory));
    }

    public IScheduler Scheduler { get; set; }

    public override async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            Scheduler = await _schedulerFactory.GetScheduler(cancellationToken);

            Scheduler.JobFactory = _jobFactory;

            var configRepo = _serviceScopeFactory.CreateScope().ServiceProvider.GetRequiredService<ICensusConfigManager>();

            List<CensusConfigEntity> facilities = (await configRepo.GetAllFacilities(cancellationToken)).ToList();

            using var censusSchedulingRepo = _serviceScopeFactory.CreateScope().ServiceProvider.GetRequiredService<ICensusSchedulingRepository>();

            // Handle removed facilities: clean up orphan jobs
            var groupMatcher = GroupMatcher<JobKey>.GroupContains(KafkaTopic.PatientCensusScheduled.ToString());
            var allJobKeys = await Scheduler.GetJobKeys(groupMatcher);

            foreach (var jobKey in allJobKeys)
            {
                //get facility id via job detail which contains the JobDataMap
                var jobDetail = await Scheduler.GetJobDetail(jobKey, cancellationToken);
                if (jobDetail == null)
                {
                    _logger.LogWarning("Job detail not found for job key: {JobKey}.", jobKey.Name);
                    continue;
                }

                var facilityId = ((CensusConfigEntity)jobDetail.JobDataMap.Get(CensusConstants.Scheduler.Facility))?.FacilityID;
                if (string.IsNullOrEmpty(facilityId))
                {
                    _logger.LogWarning("FacilityId not found in job data map for job: {JobKey}.", jobKey.Name);
                    continue;
                }

                if (!facilities.Any(f => f.FacilityID.Equals(facilityId, StringComparison.InvariantCultureIgnoreCase)))
                {
                    try
                    {
                        await censusSchedulingRepo.DeleteJobsForFacility(facilityId, Scheduler);
                        _logger.LogDebug("Cleaned up orphan job for removed facility: {FacilityId}.", facilityId);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to clean up orphan job for facility: {FacilityId}.", facilityId);
                    }
                }
            }

            //repull jobs (due to possible deletions above) and ID disabled facilities to remove their jobs
            allJobKeys = await Scheduler.GetJobKeys(groupMatcher);
            var invalidJobKeys = new List<JobKey>();
            foreach (var jobKey in allJobKeys)
            {
                var jobDetail = await Scheduler.GetJobDetail(jobKey, cancellationToken);
                if (jobDetail == null)
                {
                    _logger.LogWarning("Job detail not found for job key: {JobKey}.", jobKey.Name);
                    continue;
                }

                var facilityId = ((CensusConfigEntity)jobDetail.JobDataMap.Get(CensusConstants.Scheduler.Facility))?.FacilityID;
                if (string.IsNullOrEmpty(facilityId))
                {
                    _logger.LogWarning("FacilityId not found in job data map for job: {JobKey}.", jobKey.Name);
                    continue;
                }

                if (facilities.Any(y => y.FacilityID == facilityId && (y.Enabled ?? true) == false))
                {
                    invalidJobKeys.Add(jobKey);
                }
            }
            
            foreach (var jobKey in invalidJobKeys)
            {
                try
                {
                    var jobDetail = await Scheduler.GetJobDetail(jobKey, cancellationToken);
                    if (jobDetail == null)
                    {
                        _logger.LogWarning("Job detail not found for job key: {JobKey}.", jobKey.Name);
                        continue;
                    }

                    var facilityId = ((CensusConfigEntity)jobDetail.JobDataMap.Get(CensusConstants.Scheduler.Facility))?.FacilityID;
                    if (string.IsNullOrEmpty(facilityId))
                    {
                        _logger.LogWarning("FacilityId not found in job data map for job: {JobKey}.", jobKey.Name);
                        continue;
                    }

                    await censusSchedulingRepo.DeleteJobsForFacility(facilityId, Scheduler);
                    _logger.LogDebug("Removed disabled job for facility: {FacilityId}.", facilityId);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to remove disabled job for facility associated with job: {JobKey}.", jobKey.Name);
                }
            }

            foreach (CensusConfigEntity facility in facilities)
            {
                try
                {
                    _logger.LogDebug("Scheduling Census job for facility: {FacilityId}. enabled: {enabled}", facility.FacilityID, facility.Enabled);

                    if (facility.Enabled ?? true)
                    {
                        _logger.LogDebug("Adding/Updating Census job for facility: {FacilityId}.", facility.FacilityID);
                        await censusSchedulingRepo.UpdateJobsForFacility(facility, Scheduler);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Something went wrong scheduling a Census job for facility: {FacilityId}.", facility.FacilityID);
                }
            }

            await Scheduler.Start(cancellationToken);
            _logger.LogInformation("Scheduler started.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Something went wrong scheduling a Census job: {Message}.", ex.Message);
        }
    }

    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await Scheduler?.Shutdown(cancellationToken);
    }

}




