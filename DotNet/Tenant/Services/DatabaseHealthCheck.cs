
using LantanaGroup.Link.Tenant.Repository.Context;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace LantanaGroup.Link.Tenant.Services
{
    public class DatabaseHealthCheck : IHealthCheck
    {
        protected readonly TenantDbContext _dataContext;

        public DatabaseHealthCheck(TenantDbContext dataContext)
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
                return HealthCheckResult.Unhealthy(exception: ex);
            }
        }
    }
}
