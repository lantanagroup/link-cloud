using LantanaGroup.Link.LinkAdmin.BFF.Application.Models.Health;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using System.Net.Http.Headers;

namespace LantanaGroup.Link.LinkAdmin.BFF.Application.Clients
{
    // Reads the NHSN App BFF's /health report, which includes its SQL Server database check.
    //
    // Unlike the core Link service clients this one does not throw when its URL is missing: the NHSN
    // App BFF is not deployed in every environment, and a throwing constructor would fail every
    // handler that injects it - the whole system health summary with it.
    public class NhsnAppBffService
    {
        public const string ServiceName = "NHSN App BFF";

        private readonly ILogger<NhsnAppBffService> _logger;
        private readonly HttpClient _client;
        private readonly bool _isConfigured;

        public NhsnAppBffService(ILogger<NhsnAppBffService> logger, HttpClient client, IOptions<ServiceRegistry> serviceRegistry)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _client = client ?? throw new ArgumentNullException(nameof(client));
            ArgumentNullException.ThrowIfNull(serviceRegistry);

            var serviceUrl = serviceRegistry.Value.NhsnAppBffServiceUrl;
            _isConfigured = !string.IsNullOrWhiteSpace(serviceUrl);
            if (_isConfigured)
            {
                _client.BaseAddress = new Uri(serviceUrl!.TrimEnd('/') + "/");
                _client.DefaultRequestHeaders.Accept.Clear();
                _client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            }
        }

        public async Task<LinkServiceHealthReport> LinkServiceHealthCheck(CancellationToken cancellationToken)
        {
            if (!_isConfigured)
            {
                return new LinkServiceHealthReport
                {
                    Service = ServiceName,
                    Status = HealthStatus.Unhealthy,
                    Entries =
                    {
                        ["Configuration"] = new LinkServiceHealthReportEntry
                        {
                            Status = HealthStatus.Unhealthy,
                            Description = "ServiceRegistry:NhsnAppBffServiceUrl is not configured."
                        }
                    }
                };
            }

            try
            {
                var response = await _client.GetAsync("health", cancellationToken);
                var healthResult = await response.Content.ReadFromJsonAsync<LinkServiceHealthReport>(cancellationToken: cancellationToken);

                if (healthResult is null)
                {
                    _logger.LogWarning("NHSN App BFF health check returned null or invalid response");
                    return new LinkServiceHealthReport { Service = ServiceName, Status = HealthStatus.Unhealthy };
                }

                healthResult.Service = ServiceName;
                return healthResult;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "NHSN App BFF health check failed");
                return new LinkServiceHealthReport { Service = ServiceName, Status = HealthStatus.Unhealthy };
            }
        }
    }
}
