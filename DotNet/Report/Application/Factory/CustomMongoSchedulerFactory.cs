using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using Quartz;
using Quartz.Spi;
using Reddoxx.Quartz.MongoDbJobStore;
using System.Collections.Specialized;
using System.Collections.Generic;
using System;
using Quartz.Simpl;
using Quartz.Impl;
using MongoDB.Driver;
using System.Reflection;

namespace LantanaGroup.Link.Report.Application.Factory
{
    public class CustomMongoSchedulerFactory : ISchedulerFactory
    {
        private readonly IServiceProvider _serviceProvider;
        private IScheduler? _scheduler;

        public CustomMongoSchedulerFactory(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        public async Task<IScheduler> GetScheduler(CancellationToken cancellationToken = default)
        {
            if (_scheduler != null)
                return _scheduler;

            var loggerFactory = _serviceProvider.GetRequiredService<ILoggerFactory>();
            var config = _serviceProvider.GetRequiredService<IConfiguration>();
            var mongoOptions = _serviceProvider.GetRequiredService<IOptions<MongoConnection>>().Value;

            // Use reflection to instantiate the Reddoxx factory
            var reddoxxFactoryType = Type.GetType("Reddoxx.Quartz.MongoDbJobStore.Database.QuartzMongoDbJobStoreFactory, Reddoxx.Quartz.MongoDbJobStore");
            if (reddoxxFactoryType == null)
                throw new InvalidOperationException("Could not find Reddoxx.Quartz.MongoDbJobStore.Database.QuartzMongoDbJobStoreFactory type.");

            var mongoClient = new MongoClient(mongoOptions.ConnectionString);
            var mongoDatabase = mongoClient.GetDatabase(mongoOptions.DatabaseName);
            var factoryInstance = Activator.CreateInstance(reddoxxFactoryType, mongoDatabase);
            if (factoryInstance == null)
                throw new InvalidOperationException("Could not instantiate Reddoxx MongoDbJobStoreFactory.");

            // Cast to the expected interface using reflection
            var mongoJobStore = new MongoDbJobStore(
                loggerFactory,
                (Reddoxx.Quartz.MongoDbJobStore.Database.IQuartzMongoDbJobStoreFactory)factoryInstance,
                _serviceProvider
            );

            var threadPool = new Quartz.Simpl.DefaultThreadPool();
            threadPool.Initialize();

            var schedulerName = "ReportScheduler";
            var schedulerInstanceId = "AUTO";

            DirectSchedulerFactory.Instance.CreateScheduler(
                schedulerName,
                schedulerInstanceId,
                threadPool,
                mongoJobStore
            );

            _scheduler = await DirectSchedulerFactory.Instance.GetScheduler(schedulerName, cancellationToken);
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
