using System.Net.Http.Headers;
using System.Text.Json;
using LantanaGroup.Link.LinkAdmin.BFF.Application.Models.Health;
using LantanaGroup.Link.LinkAdmin.BFF.Infrastructure.Logging;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace LantanaGroup.Link.LinkAdmin.BFF.Application.Clients;

public class ValidationService
{
    //TODO: register once service registry has been updated to include validation service
    private readonly ILogger<ValidationService> _logger;
    private readonly HttpClient _client;
    private readonly IOptions<ServiceRegistry> _serviceRegistry;
    private const string HealthUp = "UP";


    public ValidationService(ILogger<ValidationService> logger, HttpClient client, IOptions<ServiceRegistry> serviceRegistry)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _serviceRegistry = serviceRegistry ?? throw new ArgumentNullException(nameof(serviceRegistry));

        InitHttpClient();
    }
    
    private void InitHttpClient()
    {
        //check if the service uri is set
        if (string.IsNullOrEmpty(_serviceRegistry.Value.ValidationServiceUrl))
        {
            _logger.LogGatewayServiceUriException("ValidationService", "Validation service uri is not set");
            throw new ArgumentNullException("Validation Service URL is missing.");
        }

        _client.BaseAddress = new Uri(_serviceRegistry.Value.ValidationServiceUrl);
        _client.DefaultRequestHeaders.Accept.Clear();
        _client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
    }

    public async Task<LinkServiceHealthReport> LinkServiceHealthCheck(CancellationToken cancellationToken)
    {
        // HTTP GET

        var report = new LinkServiceHealthReport
        {
            Service = "Validation"
        };

        try
        {
            var response = await _client.GetAsync($"health", cancellationToken);

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
                _logger.LogError(ex, "Failed to deserialize health response from Validation service");
                return new LinkServiceHealthReport { Service = "Validation", Status = HealthStatus.Unhealthy };
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
                    var key = component.Key == "db" ? "Database" : ToPascalCase(component.Key);

                    var componentStatus = component.Value?.Status?.ToUpperInvariant() == HealthUp
                        ? HealthStatus.Healthy
                        : HealthStatus.Unhealthy;

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
            _logger.LogError(ex, "Validation service health check failed");
            return new LinkServiceHealthReport { Service = "Validation", Status = HealthStatus.Unhealthy };
        }
    }

    private static string ToPascalCase(string input)
    {
        if (string.IsNullOrEmpty(input)) return input;
        return char.ToUpperInvariant(input[0]) + input.Substring(1);
    }
}