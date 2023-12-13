namespace Census.Services;

using Census.Domain.Entities;
using Census.Jobs;
using Census.Repositories;
using LantanaGroup.Link.Census.Application.Interfaces;
using LantanaGroup.Link.Shared.Application.Models;
using Quartz;
using Quartz.Spi;
using System.Threading;

public class ScheduleService : BackgroundService
{
    private readonly ILogger<ScheduleService> _logger;
    private readonly ISchedulerFactory _schedulerFactory;
    private readonly IJobFactory _jobFactory;
    private readonly CensusConfigMongoRepository _censusConfigRepo;
    private readonly ICensusSchedulingRepository _censusSchedulingRepo;

    private static Dictionary<string, Type> _topicJobs = new Dictionary<string, Type>();


    static ScheduleService()
    {
        _topicJobs.Add(KafkaTopic.PatientCensusScheduled.ToString(), typeof(SchedulePatientListRetrieval));
    }

    public ScheduleService(
        ILogger<ScheduleService> logger,
       ISchedulerFactory schedulerFactory,
       CensusConfigMongoRepository censusConfigRepo,
       IJobFactory jobFactory,
       ICensusSchedulingRepository censusSchedulingRepo)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _schedulerFactory = schedulerFactory ?? throw new ArgumentNullException(nameof(schedulerFactory));
        _jobFactory = jobFactory ?? throw new ArgumentNullException(nameof(jobFactory));
        _censusConfigRepo = censusConfigRepo ?? throw new ArgumentNullException(nameof(censusConfigRepo));
        _censusSchedulingRepo = censusSchedulingRepo ?? throw new ArgumentNullException(nameof(censusSchedulingRepo));
    }

    public IScheduler Scheduler { get; set; }
    // static ConcurrentDictionary<string, JobKey> jobs = new ConcurrentDictionary<string, JobKey>();

    public override async Task StartAsync(CancellationToken cancellationToken)
    {
        Scheduler = await _schedulerFactory.GetScheduler(cancellationToken);

        Scheduler.JobFactory = _jobFactory;

        List<CensusConfigEntity> facilities = await _censusConfigRepo.GetAllFacilities(cancellationToken);

        foreach (CensusConfigEntity facility in facilities)
        {
            _censusSchedulingRepo.CreateJobAndTrigger(facility, Scheduler);
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



