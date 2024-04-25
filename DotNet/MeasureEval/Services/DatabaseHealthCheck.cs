using LantanaGroup.Link.MeasureEval.Repository;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace LantanaGroup.Link.MeasureEval.Services
{
    public class DatabaseHealthCheck : IHealthCheck
    {
        private readonly IMeasureDefinitionRepo _datastore;

        public DatabaseHealthCheck(IMeasureDefinitionRepo datastore)
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
