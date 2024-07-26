namespace LantanaGroup.Link.Census.Application.Services;

using global::Census.Domain.Entities;
using LantanaGroup.Link.Census.Application.Interfaces;
using LantanaGroup.Link.Census.Application.Jobs;
using LantanaGroup.Link.Shared.Application.Models;
using Quartz;
using Quartz.Spi;
using System.Threading;

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
        Scheduler = await _schedulerFactory.GetScheduler(cancellationToken);

        Scheduler.JobFactory = _jobFactory;

        using var configRepo = _serviceScopeFactory.CreateScope().ServiceProvider.GetRequiredService<ICensusConfigRepository>();

        List<CensusConfigEntity> facilities = (await configRepo.GetAllFacilities(cancellationToken)).ToList();

        using var censusSchedulingRepo = _serviceScopeFactory.CreateScope().ServiceProvider.GetRequiredService<ICensusSchedulingRepository>();

        foreach (CensusConfigEntity facility in facilities)
        {
            censusSchedulingRepo.CreateJobAndTrigger(facility, Scheduler);
        }

        await Scheduler.Start(cancellationToken);
        _logger.LogInformation("Scheduler started.");
    }

    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await Scheduler?.Shutdown(cancellationToken);
    }

}



