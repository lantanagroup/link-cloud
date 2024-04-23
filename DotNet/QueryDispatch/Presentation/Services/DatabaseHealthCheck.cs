using LantanaGroup.Link.QueryDispatch.Application.Interfaces;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace QueryDispatch.Presentation.Services
{
    public class DatabaseHealthCheck : IHealthCheck
    {
        private readonly IQueryDispatchConfigurationRepository _datastore;

        public DatabaseHealthCheck(IQueryDispatchConfigurationRepository datastore)
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
