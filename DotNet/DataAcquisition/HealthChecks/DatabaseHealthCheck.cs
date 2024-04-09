using LantanaGroup.Link.DataAcquisition.Application.Repositories;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace LantanaGroup.Link.DataAcquisition.HealthChecks
{
    public class DatabaseHealthCheck : IHealthCheck
    {
        private readonly DataAcqTenantConfigMongoRepo _datastore;

        public DatabaseHealthCheck(DataAcqTenantConfigMongoRepo datastore)
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
