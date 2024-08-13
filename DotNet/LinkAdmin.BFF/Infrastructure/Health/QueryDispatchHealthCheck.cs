using LantanaGroup.Link.LinkAdmin.BFF.Application.Clients;
using LantanaGroup.Link.LinkAdmin.BFF.Infrastructure.Logging;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace LantanaGroup.Link.LinkAdmin.BFF.Infrastructure.Health
{
    public class QueryDispatchHealthCheck : IHealthCheck
    {
        private readonly ILogger<QueryDispatchHealthCheck> _logger;
        private readonly QueryDispatchService _queryDispatchService;

        public QueryDispatchHealthCheck(ILogger<QueryDispatchHealthCheck> logger, QueryDispatchService queryDispatchService)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _queryDispatchService = queryDispatchService ?? throw new ArgumentNullException(nameof(queryDispatchService));
        }

        public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
        {
            try
            {
                // make a request to the query dispatch service health check
                var response = await _queryDispatchService.ServiceHealthCheck(cancellationToken);

                if (response.IsSuccessStatusCode)
                {
                    return HealthCheckResult.Healthy();
                }
                else
                {
                    return new HealthCheckResult(HealthStatus.Unhealthy, description: "Query dispatch service is not healthy");
                }

            }
            catch (HttpRequestException ex)
            {
                _logger.LogLinkServiceRequestException("Query Dispatch", ex.Message);
                return new HealthCheckResult(HealthStatus.Unhealthy, description: "HTTP request error.");
            }
            catch (Exception ex)
            {
                _logger.LogLinkServiceRequestException("Query Dispatch", ex.Message);
                return new HealthCheckResult(HealthStatus.Unhealthy, description: "Failed to determine health status of the Query Dispatch service.");
            }
        }
    }
}
