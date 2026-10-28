using LantanaGroup.Link.LinkAdmin.BFF.Application.Clients;
using LantanaGroup.Link.LinkAdmin.BFF.Infrastructure.Logging;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace LantanaGroup.Link.LinkAdmin.BFF.Infrastructure.Health
{
    public class NormalizationServiceHealthCheck : IHealthCheck
    {
        private readonly ILogger<NormalizationServiceHealthCheck> _logger;
        private readonly NormalizationService _normalizationService;

        public NormalizationServiceHealthCheck(ILogger<NormalizationServiceHealthCheck> logger, NormalizationService normalizationService)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _normalizationService = normalizationService ?? throw new ArgumentNullException(nameof(normalizationService));
        }

        public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
        {
            try
            {
                // make a request to the normalization service health check
                var response = await _normalizationService.ServiceHealthCheck(cancellationToken);

                if (response.IsSuccessStatusCode)
                {
                    return HealthCheckResult.Healthy();
                }
                else
                {
                    return new HealthCheckResult(HealthStatus.Unhealthy, description: "Normalization service is not healthy");
                }

            }
            catch (HttpRequestException ex)
            {
                _logger.LogLinkServiceRequestException("Normalization", ex.Message);
                return new HealthCheckResult(HealthStatus.Unhealthy, description: "HTTP request error.");
            }
            catch (Exception ex)
            {
                _logger.LogLinkServiceRequestException("Normalization", ex.Message);
                return new HealthCheckResult(HealthStatus.Unhealthy, description: "Failed to determine health status of the Normalization service.");
            }
        }
    }
}
