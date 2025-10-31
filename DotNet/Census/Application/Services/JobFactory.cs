using Quartz;
using Quartz.Spi;

namespace LantanaGroup.Link.Census.Application.Services;

public class JobFactory : IJobFactory
{
    private readonly IServiceScopeFactory _scopeFactory;

    public JobFactory(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    public IJob NewJob(TriggerFiredBundle bundle, IScheduler scheduler)
    {
        var scope = _scopeFactory.CreateScope();
        var job = scope.ServiceProvider.GetRequiredService(bundle.JobDetail.JobType) as IJob;

        // Important: Dispose scope when job completes
        // Quartz doesn't do this automatically
        if (job is IDisposable disposableJob)
        {
            // Wrap job to dispose scope
            return new ScopedJobWrapper(job, scope);
        }

        return job;
    }

    public void ReturnJob(IJob job)
    {
        (job as IDisposable)?.Dispose();
    }
}

// Helper to dispose scope after job
internal class ScopedJobWrapper : IJob, IDisposable
{
    private readonly IJob _innerJob;
    private readonly IServiceScope _scope;

    public ScopedJobWrapper(IJob innerJob, IServiceScope scope)
    {
        _innerJob = innerJob;
        _scope = scope;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        try
        {
            await _innerJob.Execute(context);
        }
        finally
        {
            _scope.Dispose();
        }
    }

    public void Dispose()
    {
        (_innerJob as IDisposable)?.Dispose();
    }
}
