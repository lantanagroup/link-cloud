using LantanaGroup.Link.LinkAdmin.BFF.Application.Clients;
using LantanaGroup.Link.LinkAdmin.BFF.Infrastructure.Logging;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace LantanaGroup.Link.LinkAdmin.BFF.Infrastructure.Health
{
    public class DataAcquisitionHealthCheck : IHealthCheck
    {
        private readonly ILogger<DataAcquisitionHealthCheck> _logger;
        private readonly DataAcquisitionService _dataAcquisitionService;

        public DataAcquisitionHealthCheck(ILogger<DataAcquisitionHealthCheck> logger, DataAcquisitionService dataAcquisitionService)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _dataAcquisitionService = dataAcquisitionService ?? throw new ArgumentNullException(nameof(dataAcquisitionService));
        }

        public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
        {
            try
            {
                // make a request to the data acquisition service health check
                var response = await _dataAcquisitionService.ServiceHealthCheck(cancellationToken);

                if (response.IsSuccessStatusCode)
                {
                    return HealthCheckResult.Healthy();
                }
                else
                {
                    return new HealthCheckResult(HealthStatus.Unhealthy, description: "Data Acquisition service is not healthy");
                }

            }
            catch (HttpRequestException ex)
            {
                _logger.LogLinkServiceRequestException("Data Acquisition", ex.Message);
                return new HealthCheckResult(HealthStatus.Unhealthy, description: "HTTP request error.");
            }
            catch (Exception ex)
            {
                _logger.LogLinkServiceRequestException("Data Acquisition", ex.Message);
                return new HealthCheckResult(HealthStatus.Unhealthy, description: "Failed to determine health status of the Data Acquisition service.");
            }
        }
    }
}
