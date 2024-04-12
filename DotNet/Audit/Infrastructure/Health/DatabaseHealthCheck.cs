using LantanaGroup.Link.Audit.Application.Interfaces;
using LantanaGroup.Link.Audit.Persistance;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace LantanaGroup.Link.Audit.Infrastructure.Health
{  
    public class DatabaseHealthCheck : IHealthCheck
    {
        protected readonly AuditDbContext _dataContext;

        public DatabaseHealthCheck(AuditDbContext dataContext)
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
                return HealthCheckResult.Unhealthy(exception:  ex);
            }           
        }
    }
}
