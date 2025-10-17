using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Quartz;
using Quartz.Impl;
using Quartz.Spi;
using Reddoxx.Quartz.MongoDbJobStore;
using System.Collections.Specialized;
using System.Collections.Generic;

namespace LantanaGroup.Link.Report.Application.Factory
{
    public class CustomMongoSchedulerFactory : ISchedulerFactory
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly NameValueCollection _props;
        private IScheduler? _scheduler;

        public CustomMongoSchedulerFactory(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
            var config = serviceProvider.GetRequiredService<IConfiguration>();
            _props = new NameValueCollection
            {
                ["quartz.scheduler.instanceName"] = "ReportScheduler",
                ["quartz.scheduler.instanceId"] = "AUTO",
                ["quartz.jobStore.mongoUrl"] = config.GetConnectionString("MongoDbQuartz"),
                ["quartz.jobStore.collectionPrefix"] = "ReportJobs",
                ["quartz.jobStore.clustered"] = "true",
                ["quartz.jobStore.lockingManagerType"] = "Reddoxx.MongoDB.Quartz.Locking.DistributedLocksQuartzLockingManager, Reddoxx.MongoDB.Quartz",
                ["quartz.jobStore.redlock.connectionString"] = config.GetConnectionString("Redis"),
                ["quartz.jobStore.redlock.password"] = config["Redis:Password"],
                ["quartz.jobStore.redlock.database"] = "2",
                ["quartz.threadPool.type"] = "Quartz.Simpl.SimpleThreadPool, Quartz",
                ["quartz.threadPool.threadCount"] = "5",
                ["quartz.serializer.type"] = "json"
            };
        }

        public async Task<IScheduler> GetScheduler(CancellationToken cancellationToken = default)
        {
            if (_scheduler != null)
                return _scheduler;

            var loggerFactory = _serviceProvider.GetRequiredService<ILoggerFactory>();
            // TODO: Fix: IQuartzMongoDbJobStoreFactory registration
            // var mongoFactory = _serviceProvider.GetRequiredService<IQuartzMongoDbJobStoreFactory>();
            // var mongoJobStore = new MongoDbJobStore(loggerFactory, mongoFactory, _serviceProvider);

            var factory = new StdSchedulerFactory(_props);
            _scheduler = await factory.GetScheduler(cancellationToken);
            // TODO: Set JobStore if possible
            return _scheduler;
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
}
