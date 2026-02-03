using LantanaGroup.Link.DataAcquisition.AcquisitionWorker.Jobs;
using LantanaGroup.Link.DataAcquisition.Domain.Settings;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Quartz;
using Quartz.Spi;

namespace LantanaGroup.Link.DataAcquisition.AcquisitionWorker.Services;

public class SftpAcquisitionScheduleService : BackgroundService
{
    private readonly ILogger<SftpAcquisitionScheduleService> _logger;
    private readonly ISchedulerFactory _schedulerFactory;
    private readonly IJobFactory _jobFactory;
    private readonly IOptions<SftpAcquisitionSettings> _settings;
    private IScheduler? _scheduler;

    public SftpAcquisitionScheduleService(
        ILogger<SftpAcquisitionScheduleService> logger,
        ISchedulerFactory schedulerFactory,
        IJobFactory jobFactory,
        IOptions<SftpAcquisitionSettings> settings)
    {
        _logger = logger;
        _schedulerFactory = schedulerFactory;
        _jobFactory = jobFactory;
        _settings = settings;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _scheduler = await _schedulerFactory.GetScheduler(stoppingToken);
        _scheduler.JobFactory = _jobFactory;

        var job = JobBuilder.Create<SftpAcquisitionProcessingJob>()
            .WithIdentity("SftpAcquisitionProcessingJob", "DataAcquisition")
            .StoreDurably(true)
            .RequestRecovery(true)
            .WithDescription("Processes pending SFTP census acquisition logs")
            .Build();

        var trigger = TriggerBuilder.Create()
            .WithIdentity("SftpAcquisitionProcessingTrigger", "DataAcquisition")
            .StartNow()
            .WithSimpleSchedule(x => x
                .WithIntervalInSeconds(_settings.Value.JobIntervalSeconds)
                .RepeatForever())
            .WithDescription("Triggers SFTP acquisition processing job")
            .Build();

        var exists = await _scheduler.CheckExists(job.Key, stoppingToken);
        if (!exists)
        {
            await _scheduler.ScheduleJob(job, trigger, stoppingToken);
        }
        else
        {
            await _scheduler.AddJob(job, true, stoppingToken);
            await _scheduler.RescheduleJob(trigger.Key, trigger, stoppingToken);
        }

        _logger.LogInformation("Scheduled SftpAcquisitionProcessingJob with interval of {Interval} seconds",
            _settings.Value.JobIntervalSeconds);

        await _scheduler.Start(stoppingToken);
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_scheduler != null)
        {
            await _scheduler.Shutdown(cancellationToken);
            _logger.LogInformation("SftpAcquisitionScheduleService stopped");
        }
        await base.StopAsync(cancellationToken);
    }
}
