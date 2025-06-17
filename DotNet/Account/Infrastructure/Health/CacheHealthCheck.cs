using LantanaGroup.Link.Shared.Application.Interfaces;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace LantanaGroup.Link.Account.Infrastructure.Health;

public class CacheHealthCheck : IHealthCheck
{
    private readonly ICacheService _cacheService;
    
    public CacheHealthCheck(ICacheService cacheService)
    {
        _cacheService = cacheService ?? throw new ArgumentNullException(nameof(cacheService));
    }
    
    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = new CancellationToken())
    {
        try
        {
            var outcome = _cacheService.Get<string>("healthcheck");
            return Task.FromResult(HealthCheckResult.Healthy());
        }
        catch (Exception)
        {
            return Task.FromResult(HealthCheckResult.Unhealthy(description: "Failed to connect to cache"));
        }
    }
}