using LantanaGroup.Link.Shared.Application.Models.Configs;
using Microsoft.Extensions.Logging;
using System.Net;

namespace LantanaGroup.Link.Shared.Application.Services;

public class TenantApiService : ITenantApiService
{
    private readonly ILogger<TenantApiService> _logger;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ServiceRegistry _serviceRegistry;

    public TenantApiService(ILogger<TenantApiService> logger, IHttpClientFactory httpClientFactory, ServiceRegistry serviceRegistry)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
        _serviceRegistry = serviceRegistry ?? throw new ArgumentNullException(nameof(serviceRegistry));
    }

    public async Task<bool> CheckFacilityExists(string facilityId, CancellationToken cancellationToken = default)
    {
        if (_serviceRegistry.TenantService == null)
            throw new Exception("Tenant Service configuration is missing.");

        if (!_serviceRegistry.TenantService.CheckIfTenantExists)
            return true;

        var httpClient = _httpClientFactory.CreateClient();
        var endpoint = $"{_serviceRegistry.TenantService.TenantServiceUrl.TrimEnd('/')}/{_serviceRegistry.TenantService.GetTenantRelativeEndpoint.TrimStart('/').TrimEnd('/')}/{facilityId.TrimStart('/').TrimEnd('/')}";
        _logger.LogInformation("Tenant Base Endpoint: {0}", _serviceRegistry.TenantService.TenantServiceUrl);
        _logger.LogInformation("Tenant Relative Endpoint: {0}", _serviceRegistry.TenantService.GetTenantRelativeEndpoint);
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
