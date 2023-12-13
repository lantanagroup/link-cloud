using LantanaGroup.Link.Audit.Application.Interfaces;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace LantanaGroup.Link.Audit.Infrastructure.Health
{  
    public class DatabaseHealthCheck : IHealthCheck
    {
        private readonly IAuditRepository _datastore;

        public DatabaseHealthCheck(IAuditRepository datastore)
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
                return HealthCheckResult.Unhealthy(exception:  ex);
            }           
        }
    }
}
