using LantanaGroup.Link.Shared.Application.Models;
using Microsoft.Extensions.Logging;
using Quartz;
using Quartz.Impl;
using System.Collections.Specialized;

namespace LantanaGroup.Link.Shared.Application.Factories;

public class SqlPersistentScheduleFactory : ISchedulerFactory
{
    private readonly ILogger<SqlPersistentScheduleFactory> _logger;
    private readonly ServiceInformation _serviceInformation;
    private IScheduler? _scheduler;


    public SqlPersistentScheduleFactory(ILogger<SqlPersistentScheduleFactory> logger, ServiceInformation serviceInformation)
    {
        _logger = logger;
        _serviceInformation = serviceInformation;
    }


    public async Task<IScheduler> GetScheduler(CancellationToken cancellationToken = default)
    {
        if (_scheduler != null)
            return _scheduler;

        _logger.LogInformation("Creating persistence scheduler...");

        var quartzProps = new NameValueCollection
        {
            ["quartz.scheduler.instanceName"] = _serviceInformation.ServiceConfigName +  "Scheduler",
            ["quartz.scheduler.instanceId"] = "AUTO",
            ["quartz.jobStore.clustered"] = "true",
            ["quartz.jobStore.type"] = "Quartz.Impl.AdoJobStore.JobStoreTX, Quartz",
            ["quartz.jobStore.driverDelegateType"] = "Quartz.Impl.AdoJobStore.SqlServerDelegate, Quartz",
            ["quartz.jobStore.tablePrefix"] = "quartz.QRTZ_",
            ["quartz.jobStore.dataSource"] = "default",
            ["quartz.dataSource.default.connectionString"] = _serviceInformation.ConnectionString,
            ["quartz.dataSource.default.provider"] = "SqlServer",
            ["quartz.threadPool.type"] = "Quartz.Simpl.SimpleThreadPool, Quartz",
            ["quartz.threadPool.threadCount"] = "5",
            ["quartz.jobStore.useProperties"] = "false",
            ["quartz.serializer.type"] = "json"
        };

        var schedulerFactory = new StdSchedulerFactory(quartzProps);
        _scheduler = await schedulerFactory.GetScheduler(cancellationToken);

        _logger.LogInformation("persistence scheduler created: {SchedulerName}", _scheduler.SchedulerName);
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
