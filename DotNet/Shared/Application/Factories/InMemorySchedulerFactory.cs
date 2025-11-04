using Microsoft.Extensions.Logging;
using Quartz;
using Quartz.Impl;
using System.Collections.Specialized;
using System.Threading;
using System.Threading.Tasks;

namespace LantanaGroup.Link.Shared.Application.Factory; 
public class InMemorySchedulerFactory : ISchedulerFactory
{
    private readonly ILogger<InMemorySchedulerFactory> _logger;
    private IScheduler? _scheduler;

    public InMemorySchedulerFactory(ILogger<InMemorySchedulerFactory> logger)
    {
        _logger = logger;
    }

    public async Task<IScheduler> GetScheduler(CancellationToken cancellationToken = default)
    {
        if (_scheduler != null)
            return _scheduler;

        _logger.LogInformation("Creating in-memory scheduler...");

        var properties = new NameValueCollection
        {
            { "quartz.scheduler.instanceName", "InMemoryScheduler" }, // Consistent name for both services
            { "quartz.scheduler.instanceId", $"{Environment.MachineName}-{DateTime.UtcNow.Ticks}" }
        };
        var schedulerFactory = new StdSchedulerFactory(properties);
        _scheduler = await schedulerFactory.GetScheduler(cancellationToken);

        _logger.LogInformation("In-memory scheduler created: {SchedulerName}", _scheduler.SchedulerName);
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