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

            var configRepo = _serviceScopeFactory.CreateScope().ServiceProvider
                .GetRequiredService<ICensusConfigManager>();
            var facilities = (await configRepo.GetAllFacilities(cancellationToken)).ToList();
            var enabledFacilityIds = new HashSet<string>(
                facilities.Where(f => f.Enabled ?? true).Select(f => f.FacilityID),
                StringComparer.InvariantCultureIgnoreCase
            );

            using var censusSchedulingRepo = _serviceScopeFactory.CreateScope().ServiceProvider
                .GetRequiredService<ICensusSchedulingRepository>();

            // Get all existing jobs and their facility IDs
            var groupMatcher = GroupMatcher<JobKey>.GroupContains(KafkaTopic.PatientCensusScheduled.ToString());
            var allJobKeys = await Scheduler.GetJobKeys(groupMatcher);
            
            // Process jobs concurrently with controlled parallelism
            var maxDegreeOfParallelism = 20; // Tune based on your database/scheduler capacity
            var semaphore = new SemaphoreSlim(maxDegreeOfParallelism);

            var facilityIdTasks = allJobKeys.Select(async jobKey =>
            {
                await semaphore.WaitAsync(cancellationToken);
                try
                {
                    var facilityId = await GetFacilityIdFromJob(jobKey, cancellationToken);
                    if (facilityId != null && !enabledFacilityIds.Contains(facilityId))
                    {
                        return facilityId;
                    }
                    return null;
                }
                finally
                {
                    semaphore.Release();
                }
            }).ToList();

            var results = await Task.WhenAll(facilityIdTasks);
            var facilityIdsToDelete = results
                .Where(id => id != null)
                .ToHashSet(StringComparer.InvariantCultureIgnoreCase);

            // Clean up jobs for removed or disabled facilities
            foreach (var facilityId in facilityIdsToDelete)
            {
                try
                {
                    await censusSchedulingRepo.DeleteJobsForFacility(facilityId, Scheduler);
                    _logger.LogDebug("Cleaned up jobs for facility: {FacilityId}.", facilityId);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to clean up jobs for facility: {FacilityId}.", facilityId);
                }
            }

            // Schedule jobs for enabled facilities
            foreach (var facility in facilities.Where(f => f.Enabled ?? true))
            {
                try
                {
                    _logger.LogDebug("Adding/Updating Census job for facility: {FacilityId}.", facility.FacilityID);
                    await censusSchedulingRepo.UpdateJobsForFacility(facility, Scheduler);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to schedule Census job for facility: {FacilityId}.", facility.FacilityID);
                }
            }

            await Scheduler.Start(cancellationToken);
            _logger.LogInformation("Scheduler started.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start scheduler: {Message}.", ex.Message);
        }
    }

    private async Task<string?> GetFacilityIdFromJob(JobKey jobKey, CancellationToken cancellationToken)
    {
        var jobDetail = await Scheduler.GetJobDetail(jobKey, cancellationToken);
        if (jobDetail == null)
        {
            _logger.LogWarning("Job detail not found for job key: {JobKey}.", jobKey.Name);
            return null;
        }

        var facilityId = ((CensusConfigEntity)jobDetail.JobDataMap.Get(CensusConstants.Scheduler.Facility))?.FacilityID;
        if (string.IsNullOrEmpty(facilityId))
        {
            _logger.LogWarning("FacilityId not found in job data map for job: {JobKey}.", jobKey.Name);
            return null;
        }

        return facilityId;
    }

    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await Scheduler?.Shutdown(cancellationToken);
    }

}




