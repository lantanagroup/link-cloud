using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace LantanaGroup.Link.LinkAdmin.BFF.Infrastructure.Health
{
    public class CacheHealthCheck : IHealthCheck
    {
        private readonly IDistributedCache _cache;

        public CacheHealthCheck(IDistributedCache cache)
        {
            _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        }

        public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
        {
            try
            {
                var outcome = await _cache.GetAsync("healthcheck", cancellationToken);                
                return HealthCheckResult.Healthy();                
            }
            catch (Exception)
            {
                return HealthCheckResult.Unhealthy(description: "Failed to connect to cache");
            }
        }
    }
}
