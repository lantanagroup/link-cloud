using LantanaGroup.Link.LinkAdmin.BFF.Application.Clients;
using LantanaGroup.Link.LinkAdmin.BFF.Infrastructure.Logging;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace LantanaGroup.Link.LinkAdmin.BFF.Infrastructure.Health
{
    public class NotificationServiceHealthCheck : IHealthCheck
    {
        private readonly ILogger<NotificationServiceHealthCheck> _logger;
        private readonly NotificationService _notificationService;

        public NotificationServiceHealthCheck(ILogger<NotificationServiceHealthCheck> logger, NotificationService notificationService)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _notificationService = notificationService ?? throw new ArgumentNullException(nameof(notificationService));
        }

        public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
        {
            try
            {
                // make a request to the notification service health check
                var response = await _notificationService.ServiceHealthCheck(cancellationToken);

                if (response.IsSuccessStatusCode)
                {
                    return HealthCheckResult.Healthy();
                }
                else
                {
                    return new HealthCheckResult(HealthStatus.Unhealthy, description: "Notification service is not healthy");
                }

            }
            catch (HttpRequestException ex)
            {
                _logger.LogLinkServiceRequestException("Notification", ex.Message);
                return new HealthCheckResult(HealthStatus.Unhealthy, description: "HTTP request error.");
            }
            catch (Exception ex)
            {
                _logger.LogLinkServiceRequestException("Notification", ex.Message);
                return new HealthCheckResult(HealthStatus.Unhealthy, description: "Failed to determine health status of the Notification service.");
            }
        }
    }
}
