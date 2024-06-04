using LantanaGroup.Link.Submission.Persistence;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace LantanaGroup.Link.Submission.Infrastructure
{
    public class SubmissionHealthCheck : IHealthCheck
    {
        protected readonly TenantSubmissionDbContext _dataContext;

        public SubmissionHealthCheck(TenantSubmissionDbContext dataContext)
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
                return HealthCheckResult.Unhealthy(exception: ex);
            }
        }
    }
}
