using LantanaGroup.Link.LinkAdmin.BFF.Application.Clients;
using LantanaGroup.Link.LinkAdmin.BFF.Infrastructure.Logging;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace LantanaGroup.Link.LinkAdmin.BFF.Infrastructure.Health
{
    public class SubmissionServiceHealthCheck : IHealthCheck
    {
        private readonly ILogger<SubmissionServiceHealthCheck> _logger;
        private readonly SubmissionService _submissionService;

        public SubmissionServiceHealthCheck(ILogger<SubmissionServiceHealthCheck> logger, SubmissionService submissionService)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _submissionService = submissionService ?? throw new ArgumentNullException(nameof(submissionService));
        }

        public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
        {
            try
            {
                // make a request to the submission service health check
                var response = await _submissionService.ServiceHealthCheck(cancellationToken);

                if (response.IsSuccessStatusCode)
                {
                    return HealthCheckResult.Healthy();
                }
                else
                {
                    return new HealthCheckResult(HealthStatus.Unhealthy, description: "Submission service is not healthy");
                }

            }
            catch (HttpRequestException ex)
            {
                _logger.LogLinkServiceRequestException("Submission", ex.Message);
                return new HealthCheckResult(HealthStatus.Unhealthy, description: "HTTP request error.");
            }
            catch (Exception ex)
            {
                _logger.LogLinkServiceRequestException("Submission", ex.Message);
                return new HealthCheckResult(HealthStatus.Unhealthy, description: "Failed to determine health status of the Submission service.");
            }
        }
    }
}
