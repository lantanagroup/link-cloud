using LantanaGroup.Link.Account.Repositories;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace LantanaGroup.Link.Account.Infrastructure.Health
{
    public class DatabaseHealthCheck : IHealthCheck
    {
        protected readonly DataContext _dataContext;

        public DatabaseHealthCheck(DataContext dataContext)
        {            
            _dataContext = dataContext ?? throw new ArgumentNullException(nameof(dataContext));
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
                return HealthCheckResult.Unhealthy(exception: ex);
            }
        }
    }
}
