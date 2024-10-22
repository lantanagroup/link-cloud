using LantanaGroup.Link.LinkAdmin.BFF.Application.Clients;
using LantanaGroup.Link.LinkAdmin.BFF.Infrastructure.Logging;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace LantanaGroup.Link.LinkAdmin.BFF.Infrastructure.Health
{
    public class TenantServiceHealthCheck : IHealthCheck
    {
        private readonly ILogger<TenantServiceHealthCheck> _logger;
        private readonly TenantService _tenantService;

        public TenantServiceHealthCheck(ILogger<TenantServiceHealthCheck> logger, TenantService tenantService)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _tenantService = tenantService ?? throw new ArgumentNullException(nameof(tenantService));
        }

        public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
        {
            try
            {
                // make a request to the tenant service health check
                var response = await _tenantService.ServiceHealthCheck(cancellationToken);

                if (response.IsSuccessStatusCode)
                {
                    return HealthCheckResult.Healthy();
                }
                else
                {
                    return new HealthCheckResult(HealthStatus.Unhealthy, description: "Tenant service is not healthy");
                }

            }
            catch (HttpRequestException ex)
            {
                _logger.LogLinkServiceRequestException("Tenant", ex.Message);
                return new HealthCheckResult(HealthStatus.Unhealthy, description: "HTTP request error.");
            }
            catch (Exception ex)
            {
                _logger.LogLinkServiceRequestException("Tenant", ex.Message);
                return new HealthCheckResult(HealthStatus.Unhealthy, description: "Failed to determine health status of the Tenant service.");
            }
        }
    }
}
