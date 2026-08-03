using LantanaGroup.Link.DMRP.Data.Repository;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace LantanaGroup.Link.DMRP.Services
{
    public class DatabaseHealthCheck : IHealthCheck
    {
        protected readonly DmrpDbContext _dataContext;

        public DatabaseHealthCheck(DmrpDbContext dataContext)
        {
            _dataContext = dataContext;
        }

        public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
        {
            try
            {
                bool outcome = await _dataContext.Database.CanConnectAsync(cancellationToken);

                return outcome ? HealthCheckResult.Healthy() : HealthCheckResult.Unhealthy();
            }
            catch (Exception ex)
            {
                return HealthCheckResult.Unhealthy(exception: ex);
            }
        }
    }
}
