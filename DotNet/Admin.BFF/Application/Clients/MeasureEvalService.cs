using LantanaGroup.Link.LinkAdmin.BFF.Infrastructure.Logging;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using Microsoft.Extensions.Options;
using System.Net.Http.Headers;
using LantanaGroup.Link.LinkAdmin.BFF.Application.Models.Health;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using System.Text.Json;

namespace LantanaGroup.Link.LinkAdmin.BFF.Application.Clients
{
    public class MeasureEvalService
    {
        private readonly ILogger<MeasureEvalService> _logger;
        private readonly HttpClient _client;
        private readonly IOptions<ServiceRegistry> _serviceRegistry;
        private const string HealthUp = "UP";
        private static readonly TimeSpan HealthCheckTimeout = TimeSpan.FromSeconds(5);

        public MeasureEvalService(ILogger<MeasureEvalService> logger, HttpClient client, IOptions<ServiceRegistry> serviceRegistry)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _client = client ?? throw new ArgumentNullException(nameof(client));
            _serviceRegistry = serviceRegistry ?? throw new ArgumentNullException(nameof(serviceRegistry));

            InitHttpClient();
        }

        public async Task<HttpResponseMessage> ServiceHealthCheck(CancellationToken cancellationToken)
        {
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(HealthCheckTimeout);

            // HTTP GET
            HttpResponseMessage response = await _client.GetAsync($"health", timeoutCts.Token);

            return response;
        }

        public async Task<LinkServiceHealthReport> LinkServiceHealthCheck(CancellationToken cancellationToken)
        {
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(HealthCheckTimeout);
            // HTTP GET

            var report = new LinkServiceHealthReport
            {
                Service = "Measure Evaluation"
            };

            try
            {
                var response = await _client.GetAsync($"health", timeoutCts.Token);

                var content = await response.Content.ReadAsStringAsync();

                HealthResponse? health = null;

                try
                {

                    health = JsonSerializer.Deserialize<HealthResponse>(content, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });
                }
                catch (JsonException ex)
                {
                    _logger.LogError(ex, "Failed to deserialize health response from Measure Evaluation service");
                    return new LinkServiceHealthReport { Service = "Measure Evaluation", Status = HealthStatus.Unhealthy };
                }

                var status = health?.Status?.Equals(HealthUp, StringComparison.OrdinalIgnoreCase) == true
                    ? HealthStatus.Healthy
                    : HealthStatus.Unhealthy;

                report.Status = status;

                // Populate Entries based on components
                if (health?.Components != null)
                {
                    foreach (var component in health.Components)
                    {
                        var componentStatus = component.Value?.Status?.ToUpperInvariant() == HealthUp
                            ? HealthStatus.Healthy
                            : HealthStatus.Unhealthy;

                        // MeasureEval's ResourceCacheHealthIndicator reports the combined Redis + ABS
                        // resource-cache status under the "resourceCache" component; map it to the
                        // "Cache" entry the UI's Cache column expects.
                        var key = component.Key.Equals("resourceCache", StringComparison.OrdinalIgnoreCase)
                            ? "Cache"
                            : ToPascalCase(component.Key);

                        report.Entries[key] = new LinkServiceHealthReportEntry
                        {
                            Status = componentStatus,
                            Duration = TimeSpan.Zero
                        };
                    }
                }

                return report;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Measure Evaluation service health check failed");
                return new LinkServiceHealthReport { Service = "Measure Evaluation", Status = HealthStatus.Unhealthy };
            }
        }

        private static string ToPascalCase(string input)
        {
            if (string.IsNullOrEmpty(input)) return input;
            return char.ToUpperInvariant(input[0]) + input.Substring(1);
        }

        private void InitHttpClient()
        {
            //check if the service uri is set
            if (string.IsNullOrEmpty(_serviceRegistry.Value.MeasureServiceUrl))
            {
                _logger.LogGatewayServiceUriException("MeasureEvaluation", "Measure Evaluation service uri is not set");
                throw new ArgumentNullException("Measure Evaluation Service URL is missing.");
            }

            _client.BaseAddress = new Uri(_serviceRegistry.Value.MeasureServiceUrl);
            _client.DefaultRequestHeaders.Accept.Clear();
            _client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        }
    }
}
