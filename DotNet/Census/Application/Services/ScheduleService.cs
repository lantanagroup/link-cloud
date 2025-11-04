using global::Census.Domain.Entities;
using LantanaGroup.Link.Census.Application.Interfaces;
using LantanaGroup.Link.Census.Application.Jobs;
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

            foreach (CensusConfigEntity facility in facilities)
            {
                try
                {
                    await censusSchedulingRepo.UpdateJobsForFacility(facility, Scheduler);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Something went wrong scheduling a Census job for facility: {FacilityId}.", facility.FacilityID);
                }
            }

            // Handle removed facilities: clean up orphan jobs
            var groupMatcher = GroupMatcher<JobKey>.GroupContains(KafkaTopic.PatientCensusScheduled.ToString());
            var allJobKeys = await Scheduler.GetJobKeys(groupMatcher);
            foreach (var jobKey in allJobKeys)
            {
                // Extract facilityId from job name (format: "{facilityId}-{KafkaTopic.PatientCensusScheduled}")
                var parts = jobKey.Name.Split('-');
                if (parts.Length < 2) continue; // Invalid name, skip
                string facilityId = parts[0];

                if (!facilities.Any(f => f.FacilityID == facilityId))
                {
                    try
                    {
                        await censusSchedulingRepo.DeleteJobsForFacility(facilityId, Scheduler);
                        _logger.LogInformation("Cleaned up orphan job for removed facility: {FacilityId}.", facilityId);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to clean up orphan job for facility: {FacilityId}.", facilityId);
                    }
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



