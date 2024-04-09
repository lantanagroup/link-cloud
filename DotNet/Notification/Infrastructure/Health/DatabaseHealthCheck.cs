using LantanaGroup.Link.Notification.Persistence;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace LantanaGroup.Link.Notification.Infrastructure.Health
{
    public class DatabaseHealthCheck : IHealthCheck
    {
        protected readonly NotificationDbContext _dataContext;

        public DatabaseHealthCheck(NotificationDbContext dataContext)
        {            
            _dataContext = dataContext;
        }

        public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
        {
            try 
            {
                bool outcome = await _dataContext.Database.CanConnectAsync();

                if (outcome)
                {
                    return HealthCheckResult.Healthy();
                }
                else
                { 
                    return HealthCheckResult.Unhealthy();
                }
                
            }
            catch (Exception ex)
            {
                return HealthCheckResult.Unhealthy(exception:  ex);
            }           
        }
    }
}
