using Microsoft.Extensions.Options;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using LantanaGroup.Link.Report.Jobs.JobStoreFactories;
using Quartz;
using Quartz.Spi;
using Reddoxx.Quartz.MongoDbJobStore;
using Quartz.Simpl;
using Quartz.Impl;

namespace LantanaGroup.Link.Report.Application.Factory;

public class ReportMongoSchedulerFactory : ISchedulerFactory
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<ReportMongoSchedulerFactory> _logger;
    private IScheduler? _scheduler;
    private readonly SemaphoreSlim _lock = new SemaphoreSlim(1, 1);

    public ReportMongoSchedulerFactory(IServiceProvider serviceProvider, ILogger<ReportMongoSchedulerFactory> logger)
    {
        _serviceProvider = serviceProvider;
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

            _logger.LogInformation("Creating MongoDB scheduler...");

            var loggerFactory = _serviceProvider.GetRequiredService<ILoggerFactory>();
            var mongoOptions = _serviceProvider.GetRequiredService<IOptions<MongoConnection>>();

            // Register types for BSON serialization
            try
            {
                if (!MongoDB.Bson.Serialization.BsonClassMap.IsClassMapRegistered(typeof(LantanaGroup.Link.Shared.Application.Models.RetryEntity)))
                {
                    MongoDB.Bson.Serialization.BsonClassMap.RegisterClassMap<LantanaGroup.Link.Shared.Application.Models.RetryEntity>(cm =>
                    {
                        cm.AutoMap();
                        cm.SetIgnoreExtraElements(true);
                    });
                    _logger.LogInformation("Registered RetryEntity for BSON serialization");
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "RetryEntity may already be registered for BSON serialization");
            }

            var quartzFactory = new ReportQuartzMongoDbJobStoreFactory(mongoOptions);

            // Create the MongoDbJobStore
            var mongoJobStore = new MongoDbJobStore(
                loggerFactory,
                quartzFactory,
                _serviceProvider
            );

            //get prefix from configuration, default to "reportjobs" if not set
            var configuration = _serviceProvider.GetRequiredService<IConfiguration>();
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

            _logger.LogInformation("Scheduler Name: {SchedulerName}, Instance ID: {InstanceId}", schedulerName, schedulerInstanceId);

            var schedulerSignaler = new SchedulerSignalerImpl(loggerFactory);

            var loadHelper = new SimpleTypeLoadHelper();
            loadHelper.Initialize();

            await mongoJobStore.Initialize(loadHelper, schedulerSignaler, cancellationToken);

            _logger.LogInformation("MongoDB job store initialized successfully");

            DirectSchedulerFactory.Instance.CreateScheduler(
                schedulerName,
                schedulerInstanceId,
                threadPool,
                mongoJobStore
            );

            _scheduler = await DirectSchedulerFactory.Instance.GetScheduler(schedulerName, cancellationToken);

            _logger.LogInformation("Scheduler created successfully: {SchedulerName}", _scheduler.SchedulerName);

            return _scheduler;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create MongoDB scheduler");
            throw;
        }
        finally
        {
            _lock.Release();
        }
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