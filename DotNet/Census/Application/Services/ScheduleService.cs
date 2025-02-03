using global::Census.Domain.Entities;
using LantanaGroup.Link.Census.Application.Interfaces;
using LantanaGroup.Link.Census.Application.Jobs;
using LantanaGroup.Link.Census.Domain.Managers;
using LantanaGroup.Link.Shared.Application.Models;
using Quartz;
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
                    censusSchedulingRepo.CreateJobAndTrigger(facility, Scheduler);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Something went wrong scheduling a Census job for facility: {1}.", facility.FacilityID);
                }
            }

            await Scheduler.Start(cancellationToken);
            _logger.LogInformation("Scheduler started.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Something went wrong scheduling a Census job: {1}.", ex.Message);
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



