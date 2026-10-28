using LantanaGroup.Link.Shared.Application.Interfaces;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace LantanaGroup.Link.Shared.Application.Health
{
    public class CacheHealthCheck : IHealthCheck
    {
        private static readonly TimeSpan HealthCheckTimeout = TimeSpan.FromSeconds(5);

        private readonly ICacheService _cacheService;

        public CacheHealthCheck(ICacheService cacheService)
        {
            _cacheService = cacheService ?? throw new ArgumentNullException(nameof(cacheService));
        }

        public async Task<HealthCheckResult> CheckHealthAsync(
            HealthCheckContext context,
            CancellationToken cancellationToken = default)
        {
            try
            {
                using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                timeoutCts.CancelAfter(HealthCheckTimeout);

                _ = await _cacheService.GetAsync<string>("healthcheck", timeoutCts.Token);
                return HealthCheckResult.Healthy();
            }
            catch (Exception ex)
            {
                return HealthCheckResult.Unhealthy("Failed to connect to cache", ex);
            }
        }
    }
}
