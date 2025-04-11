using System.Net.Http.Headers;
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
        if (string.IsNullOrEmpty(_serviceRegistry.Value.MeasureServiceUrl))
        {
            _logger.LogGatewayServiceUriException("ValidationService", "Validation service uri is not set");
            throw new ArgumentNullException("Validation Service URL is missing.");
        }

        _client.BaseAddress = new Uri(_serviceRegistry.Value.MeasureServiceUrl);
        _client.DefaultRequestHeaders.Accept.Clear();
        _client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
    }

    public async Task<LinkServiceHealthReport> LinkServiceHealthCheck(CancellationToken cancellationToken)
    {
        // HTTP GET
        try
        {
            var response = await _client.GetAsync($"api/validation/health", cancellationToken);
            var healthResult = await response.Content.ReadFromJsonAsync<LinkServiceHealthReport>(cancellationToken: cancellationToken);
            if (healthResult is not null) healthResult.Service = "Validation";

            return healthResult;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Validation service health check failed");
            return new LinkServiceHealthReport { Service = "Validation", Status = HealthStatus.Unhealthy };
        }
    }
}