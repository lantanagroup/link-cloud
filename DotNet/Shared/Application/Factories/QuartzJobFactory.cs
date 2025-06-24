using Microsoft.Extensions.DependencyInjection;
using Quartz.Spi;
using Quartz;

namespace LantanaGroup.Link.Shared.Application.Factories;
public class QuartzJobFactory : IJobFactory
{
    private readonly IServiceProvider _serviceProvider;

    public QuartzJobFactory(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public IJob NewJob(TriggerFiredBundle bundle, IScheduler scheduler)
    {
        return _serviceProvider.GetRequiredService(bundle.JobDetail.JobType) as IJob;
    }

    public void ReturnJob(IJob job)
    {
        // we let the DI container handler this
    }
}
