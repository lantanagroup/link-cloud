using Quartz.Spi;
using Quartz;
using Microsoft.Extensions.DependencyInjection;

namespace LantanaGroup.Link.DataAcquisition.Domain.Application.Factories;

public class JobFactory : IJobFactory
{
    private readonly IServiceProvider _serviceProvider;

    public JobFactory(IServiceProvider serviceProvider)
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
