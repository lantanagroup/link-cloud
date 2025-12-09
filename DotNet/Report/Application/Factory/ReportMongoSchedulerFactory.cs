using LantanaGroup.Link.Report.Domain;
using LantanaGroup.Link.Report.Jobs.JobStoreFactories;
using Quartz;
using Quartz.Impl;
using Quartz.Simpl;
using Quartz.Spi;
using Reddoxx.Quartz.MongoDbJobStore;

namespace LantanaGroup.Link.Report.Application.Factory;

public class ReportMongoSchedulerFactory : ISchedulerFactory
{
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly ILogger<ReportMongoSchedulerFactory> _logger;
    private IScheduler? _scheduler;
    private readonly SemaphoreSlim _lock = new SemaphoreSlim(1, 1);

    public ReportMongoSchedulerFactory(IServiceScopeFactory serviceScopeFactory, ILogger<ReportMongoSchedulerFactory> logger)
    {
        _serviceScopeFactory = serviceScopeFactory;
        _logger = logger;
    }

    public async Task<IScheduler> GetScheduler(CancellationToken cancellationToken = default)
    {
        if (_scheduler != null)
            return _scheduler;

        await _lock.WaitAsync(cancellationToken);

        try
        {
            if (_scheduler != null)
                return _scheduler;

            using var scope = _serviceScopeFactory.CreateScope();

            var loggerFactory = scope.ServiceProvider.GetRequiredService<ILoggerFactory>();
            var context = scope.ServiceProvider.GetRequiredService<MongoDbContext>();

            var quartzFactory = new ReportQuartzMongoDbJobStoreFactory(context);

            // Create the MongoDbJobStore
            var mongoJobStore = new MongoDbJobStore(
                loggerFactory,
                quartzFactory,
                scope.ServiceProvider
            );

            //get prefix from configuration, default to "reportjobs" if not set
            var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();
            var mongoCollectionPrefix = configuration.GetValue<string>("QuartzMongoCollectionPrefix") ?? "reportjobs";

            // Set properties
            mongoJobStore.CollectionPrefix = mongoCollectionPrefix;
            mongoJobStore.Clustered = true;
            mongoJobStore.ClusterCheckinInterval = TimeSpan.FromMilliseconds(7500);
            mongoJobStore.ClusterCheckinMisfireThreshold = TimeSpan.FromMilliseconds(7500);

            // Create thread pool
            var threadPool = new DefaultThreadPool();
            threadPool.MaxConcurrency = configuration.GetValue<int?>("QuartzMongoClusterMaxConcurrency") ?? 5;
            threadPool.Initialize();

            var schedulerName = "ReportScheduler";
            var schedulerInstanceId = Environment.MachineName + "-" + DateTime.UtcNow.Ticks;

            mongoJobStore.InstanceName = schedulerName;
            mongoJobStore.InstanceId = schedulerInstanceId;

            var schedulerSignaler = new SchedulerSignalerImpl(loggerFactory);

            var loadHelper = new SimpleTypeLoadHelper();
            loadHelper.Initialize();

            await mongoJobStore.Initialize(loadHelper, schedulerSignaler, cancellationToken);

            DirectSchedulerFactory.Instance.CreateScheduler(
                schedulerName,
                schedulerInstanceId,
                threadPool,
                mongoJobStore
            );

            _scheduler = await DirectSchedulerFactory.Instance.GetScheduler(schedulerName, cancellationToken);

            return _scheduler;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create MongoDB scheduler");
        }
        finally
        {
            _lock.Release();
        }

        return null;
    }

    public async Task<IReadOnlyList<IScheduler>> GetAllSchedulers(CancellationToken cancellationToken = default)
    {
        return new List<IScheduler> { await GetScheduler(cancellationToken) };
    }

    public async Task<IScheduler> GetScheduler(string schedulerName, CancellationToken cancellationToken = default)
    {
        var scheduler = await GetScheduler(cancellationToken);
        if (scheduler.SchedulerName == schedulerName)
            return scheduler;
        throw new ArgumentException($"Scheduler with name {schedulerName} not found.");
    }
}

// Simple implementation of ISchedulerSignaler
internal class SchedulerSignalerImpl : ISchedulerSignaler
{
    private readonly ILogger _logger;

    public SchedulerSignalerImpl(ILoggerFactory loggerFactory)
    {
        _logger = loggerFactory.CreateLogger<SchedulerSignalerImpl>();
    }

    public Task NotifyTriggerListenersMisfired(ITrigger trigger, CancellationToken cancellationToken = default)
    {
        _logger.LogWarning("Trigger misfired: {TriggerKey}", trigger.Key);
        return Task.CompletedTask;
    }

    public Task NotifySchedulerListenersFinalized(ITrigger trigger, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public Task NotifySchedulerListenersJobDeleted(JobKey jobKey, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public void SignalSchedulingChange(DateTimeOffset? candidateNewNextFireTime, CancellationToken cancellationToken = default)
    {
        // Signal that scheduling has changed
    }

    public Task NotifySchedulerListenersError(string message, SchedulerException jpe, CancellationToken cancellationToken = default)
    {
        _logger.LogError(jpe, "Scheduler error: {Message}", message);
        return Task.CompletedTask;
    }
}