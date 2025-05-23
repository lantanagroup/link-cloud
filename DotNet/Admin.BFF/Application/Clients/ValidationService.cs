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
        try
        {
            var response = await _client.GetAsync($"health", cancellationToken);

            //TODO: update when further functionality within the java services have been added
            if (response.IsSuccessStatusCode)
            {
                return new LinkServiceHealthReport { Service = "Validation", Status = HealthStatus.Healthy };
            }
            else
            {
                return new LinkServiceHealthReport { Service = "Validation", Status = HealthStatus.Unhealthy };
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Validation service health check failed");
            return new LinkServiceHealthReport { Service = "Validation", Status = HealthStatus.Unhealthy };
        }
    }
}