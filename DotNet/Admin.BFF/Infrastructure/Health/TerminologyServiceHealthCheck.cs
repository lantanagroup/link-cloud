using LantanaGroup.Link.LinkAdmin.BFF.Application.Clients;
using LantanaGroup.Link.LinkAdmin.BFF.Infrastructure.Logging;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace LantanaGroup.Link.LinkAdmin.BFF.Infrastructure.Health
{
    public class TerminologyServiceHealthCheck : IHealthCheck
    {
        private readonly ILogger<TerminologyServiceHealthCheck> _logger;
        private readonly TerminologyService _terminologyService;

        public TerminologyServiceHealthCheck(ILogger<TerminologyServiceHealthCheck> logger, TerminologyService terminologyService)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _terminologyService = terminologyService ?? throw new ArgumentNullException(nameof(terminologyService));
        }

        public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
        {
            try
            {
                // make a request to the terminology service health check
                var response = await _terminologyService.ServiceHealthCheck(cancellationToken);

                if (response.IsSuccessStatusCode)
                {
                    return HealthCheckResult.Healthy();
                }
                else
                {
                    return new HealthCheckResult(HealthStatus.Unhealthy, description: "Terminology service is not healthy");
                }

            }
            catch (HttpRequestException ex)
            {
                _logger.LogLinkServiceRequestException("Terminology", ex.Message);
                return new HealthCheckResult(HealthStatus.Unhealthy, description: "HTTP request error.");
            }
            catch (Exception ex)
            {
                _logger.LogLinkServiceRequestException("Terminology", ex.Message);
                return new HealthCheckResult(HealthStatus.Unhealthy, description: "Failed to determine health status of the Terminology service.");
            }
        }
    }
}
