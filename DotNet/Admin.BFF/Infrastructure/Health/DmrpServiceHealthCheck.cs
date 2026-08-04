using LantanaGroup.Link.LinkAdmin.BFF.Application.Clients;
using LantanaGroup.Link.LinkAdmin.BFF.Infrastructure.Logging;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace LantanaGroup.Link.LinkAdmin.BFF.Infrastructure.Health
{
    public class DmrpServiceHealthCheck : IHealthCheck
    {
        private readonly ILogger<DmrpServiceHealthCheck> _logger;
        private readonly DmrpService _dmrpService;

        public DmrpServiceHealthCheck(ILogger<DmrpServiceHealthCheck> logger, DmrpService dmrpService)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _dmrpService = dmrpService ?? throw new ArgumentNullException(nameof(dmrpService));
        }

        public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
        {
            try
            {
                // make a request to the DMRP service health check
                var response = await _dmrpService.ServiceHealthCheck(cancellationToken);

                if (response.IsSuccessStatusCode)
                {
                    return HealthCheckResult.Healthy();
                }
                else
                {
                    return new HealthCheckResult(HealthStatus.Unhealthy, description: "DMRP service is not healthy");
                }

            }
            catch (HttpRequestException ex)
            {
                _logger.LogLinkServiceRequestException("DMRP", ex.Message);
                return new HealthCheckResult(HealthStatus.Unhealthy, description: "HTTP request error.");
            }
            catch (Exception ex)
            {
                _logger.LogLinkServiceRequestException("DMRP", ex.Message);
                return new HealthCheckResult(HealthStatus.Unhealthy, description: "Failed to determine health status of the DMRP service.");
            }
        }
    }
}
