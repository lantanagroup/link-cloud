using LantanaGroup.Link.Shared.Application.Models.Configs;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Net;

namespace LantanaGroup.Link.Shared.Application.Services;

public class TenantApiService : ITenantApiService
{
    private readonly ILogger<TenantApiService> _logger;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IOptions<ServiceRegistry> _serviceRegistry;

    public TenantApiService(ILogger<TenantApiService> logger, IHttpClientFactory httpClientFactory, IOptions<ServiceRegistry> serviceRegistry)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
        _serviceRegistry = serviceRegistry ?? throw new ArgumentNullException(nameof(serviceRegistry));
    }

    public async Task<bool> CheckFacilityExists(string facilityId, CancellationToken cancellationToken = default)
    {
        if (_serviceRegistry.Value.TenantService == null)
            throw new Exception("Tenant Service configuration is missing.");

        if (!_serviceRegistry.Value.TenantService.CheckIfTenantExists)
            return true;

        var tenantServiceApiUrl = _serviceRegistry.Value.TenantService.TenantServiceApiUrl;

        if (string.IsNullOrWhiteSpace(tenantServiceApiUrl))
            throw new Exception("Tenant Service URL is missing.");

        var httpClient = _httpClientFactory.CreateClient();

        var endpoint = $"{tenantServiceApiUrl.TrimStart('/').TrimEnd('/')}/facility/{facilityId.TrimStart('/').TrimEnd('/')}";
        _logger.LogInformation("Tenant Base Endpoint: {0}", tenantServiceApiUrl);
        _logger.LogInformation("Tenant Relative Endpoint: {0}", _serviceRegistry.Value.TenantService.GetTenantRelativeEndpoint);
        _logger.LogInformation("Checking if facility ({1}) exists in Tenant Service. Endpoint: {2}", facilityId, endpoint);
        var response = await httpClient.GetAsync(endpoint, cancellationToken);

        if (response.IsSuccessStatusCode)
        {
            return true;
        }
        
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return false;
        }

        var message = $"Error checking if facility ({facilityId}) exists in Tenant Service. Status Code: {response.StatusCode}";
        _logger.LogError(message);
        throw new Exception(message);
    }
}
