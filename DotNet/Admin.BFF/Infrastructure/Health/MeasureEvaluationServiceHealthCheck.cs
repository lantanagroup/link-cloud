using LantanaGroup.Link.LinkAdmin.BFF.Application.Clients;
using LantanaGroup.Link.LinkAdmin.BFF.Infrastructure.Logging;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace LantanaGroup.Link.LinkAdmin.BFF.Infrastructure.Health
{
    public class MeasureEvaluationServiceHealthCheck : IHealthCheck
    {
        private readonly ILogger<MeasureEvaluationServiceHealthCheck> _logger;
        private readonly MeasureEvalService _measureEvalService;

        public MeasureEvaluationServiceHealthCheck(ILogger<MeasureEvaluationServiceHealthCheck> logger, MeasureEvalService measureEvalService)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _measureEvalService = measureEvalService ?? throw new ArgumentNullException(nameof(measureEvalService));
        }

        public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
        {
            try
            {
                // make a request to the measure evaluation service health check
                var response = await _measureEvalService.ServiceHealthCheck(cancellationToken);

                if (response.IsSuccessStatusCode)
                {
                    return HealthCheckResult.Healthy();
                }
                else
                {
                    return new HealthCheckResult(HealthStatus.Unhealthy, description: "Measure evaluation service is not healthy");
                }

            }
            catch (HttpRequestException ex)
            {
                _logger.LogLinkServiceRequestException("Measure Evaluation", ex.Message);
                return new HealthCheckResult(HealthStatus.Unhealthy, description: "HTTP request error.");
            }
            catch (Exception ex)
            {
                _logger.LogLinkServiceRequestException("Measure Evaluation", ex.Message);
                return new HealthCheckResult(HealthStatus.Unhealthy, description: "Failed to determine health status of the Measure Evaluation service.");
            }
        }
    }
}
