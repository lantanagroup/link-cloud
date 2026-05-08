using LantanaGroup.Link.Account.Persistence;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace LantanaGroup.Link.Account.Infrastructure.Health
{
    public class DatabaseHealthCheck : IHealthCheck
    {
        protected readonly AccountDbContext _dbContext;

        public DatabaseHealthCheck(AccountDbContext dbContext)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        }

        public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
        {
            try
            {
                bool outcome = await _dbContext.Database.CanConnectAsync(cancellationToken);

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
