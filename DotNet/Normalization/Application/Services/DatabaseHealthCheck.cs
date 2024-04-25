using LantanaGroup.Link.Normalization.Domain.Entities;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace LantanaGroup.Link.Normalization.Application.Services
{
    public class DatabaseHealthCheck : IHealthCheck
    {
        protected readonly NormalizationDbContext _dataContext;

        public DatabaseHealthCheck(NormalizationDbContext dataContext)
        {
            _dataContext = dataContext ?? throw new ArgumentNullException(nameof(dataContext));
        }

        public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context,
            CancellationToken cancellationToken = default)
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
