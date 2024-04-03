using LantanaGroup.Link.Census.Application.Repositories;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace LantanaGroup.Link.Census.Application.HealthChecks
{
    public class DatabaseHealthCheck : IHealthCheck
    {
        private readonly CensusConfigMongoRepository _datastore;

        public DatabaseHealthCheck(CensusConfigMongoRepository datastore)
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
