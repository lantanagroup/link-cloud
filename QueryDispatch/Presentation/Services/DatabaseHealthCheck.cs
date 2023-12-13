using LantanaGroup.Link.QueryDispatch.Application.Interfaces;
using LantanaGroup.Link.QueryDispatch.Application.Models;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace QueryDispatch.Presentation.Services
{
    public class DatabaseHealthCheck : IHealthCheck
    {
        private readonly IBaseRepository<QueryDispatchConfiguration> _datastore;

        public DatabaseHealthCheck(IBaseRepository<QueryDispatchConfiguration> datastore)
        {
            _datastore = datastore ?? throw new ArgumentNullException(nameof(datastore));
        }

        public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
        {
            try
            {
                bool outcome = await _datastore.HealthCheck();

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
