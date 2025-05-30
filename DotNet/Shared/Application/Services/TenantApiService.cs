using LantanaGroup.Link.Shared.Application.Extensions.Security;
using LantanaGroup.Link.Shared.Application.Interfaces.Services.Security.Token;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using LantanaGroup.Link.Shared.Application.Models.Tenant;
using LantanaGroup.Link.Shared.Application.Services.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;

namespace LantanaGroup.Link.Shared.Application.Services;

public class TenantApiService : ITenantApiService
{
    private readonly ILogger<TenantApiService> _logger;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IOptions<ServiceRegistry> _serviceRegistry;
    private readonly IOptions<LinkTokenServiceSettings> _linkTokenServiceConfig;
    private readonly IOptions<BackendAuthenticationServiceExtension.LinkBearerServiceOptions> _linkBearerServiceOptions;
    private readonly ICreateSystemToken _createSystemToken;

    public TenantApiService(ILogger<TenantApiService> logger, IHttpClientFactory httpClientFactory, IOptions<ServiceRegistry> serviceRegistry, IOptions<LinkTokenServiceSettings> linkTokenServiceConfig, ICreateSystemToken createSystemToken, IOptions<BackendAuthenticationServiceExtension.LinkBearerServiceOptions> linkBearerServiceOptions)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
        _serviceRegistry = serviceRegistry ?? throw new ArgumentNullException(nameof(serviceRegistry));
        _linkTokenServiceConfig = linkTokenServiceConfig ?? throw new ArgumentNullException(nameof(linkTokenServiceConfig));
        _createSystemToken = createSystemToken ?? throw new ArgumentNullException(nameof(createSystemToken));
        _linkBearerServiceOptions = linkBearerServiceOptions ?? throw new ArgumentNullException(nameof(linkBearerServiceOptions));
    }

    public async Task<bool> CheckFacilityExists(string facilityId, CancellationToken cancellationToken = default)
    {
        string sanitizedFacilityId = HtmlInputSanitizer.SanitizeAndRemove(facilityId);

        if (_serviceRegistry.Value.TenantService == null)
            throw new Exception("Tenant Service configuration is missing.");

        if (!_serviceRegistry.Value.TenantService.CheckIfTenantExists)
            return true;

       var tenantServiceUrl = _serviceRegistry.Value.TenantServiceApiUrl;

        if (string.IsNullOrWhiteSpace(tenantServiceUrl))
            throw new Exception("Tenant Service URL is missing.");

        var httpClient = _httpClientFactory.CreateClient();

        var endpoint = new Uri(tenantServiceUrl.TrimEnd('/') + $"/{_serviceRegistry.Value.TenantService.GetTenantRelativeEndpoint.Trim('/')}/{sanitizedFacilityId}").ToString();


        _logger.LogInformation("Tenant Base Endpoint: {0}", tenantServiceUrl);
        _logger.LogInformation("Tenant Relative Endpoint: {0}", _serviceRegistry.Value.TenantService.GetTenantRelativeEndpoint);
        _logger.LogInformation("Checking if facility ({1}) exists in Tenant Service. Endpoint: {2}", sanitizedFacilityId, endpoint);

        //TODO: add method to get key that includes looking at redis for future use case
        if (!_linkBearerServiceOptions.Value.AllowAnonymous && _linkTokenServiceConfig.Value.SigningKey is null)
            throw new Exception("Link Token Service Signing Key is missing.");

        //get link token
        if (!_linkBearerServiceOptions.Value.AllowAnonymous)
        {
            var token = await _createSystemToken.ExecuteAsync(_linkTokenServiceConfig.Value.SigningKey, 2);
            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        var response = await httpClient.GetAsync(endpoint, cancellationToken);

        if (response.IsSuccessStatusCode)
        {
            return true;
        }
        
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return false;
        }

        var message = $"Error checking if facility ({sanitizedFacilityId}) exists in Tenant Service. Status Code: {response.StatusCode}";
        _logger.LogError(message);
        throw new Exception(message);
    }

    public async Task<FacilityConfig> GetFacilityConfig(string facilityId, CancellationToken cancellationToken = default)
    {
        string sanitizedFacilityId = HtmlInputSanitizer.SanitizeAndRemove(facilityId);

        if (_serviceRegistry.Value.TenantService == null)
            throw new Exception("Tenant Service configuration is missing.");

        var tenantServiceUrl = _serviceRegistry.Value.TenantServiceApiUrl;

        if (string.IsNullOrWhiteSpace(tenantServiceUrl))
            throw new Exception("Tenant Service URL is missing.");

        var httpClient = _httpClientFactory.CreateClient();

        var endpoint = new Uri(tenantServiceUrl.TrimEnd('/') + $"/{_serviceRegistry.Value.TenantService.GetTenantRelativeEndpoint.Trim('/')}/{sanitizedFacilityId}").ToString();

        //TODO: add method to get key that includes looking at redis for future use case
        if (!_linkBearerServiceOptions.Value.AllowAnonymous && _linkTokenServiceConfig.Value.SigningKey is null)
            throw new Exception("Link Token Service Signing Key is missing.");

        //get link token
        if (!_linkBearerServiceOptions.Value.AllowAnonymous)
        {
            var token = await _createSystemToken.ExecuteAsync(_linkTokenServiceConfig.Value.SigningKey, 2);
            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        var response = await httpClient.GetAsync(endpoint, cancellationToken);

        if (response.IsSuccessStatusCode)
        {
            var result = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<FacilityConfig>(result);
        }

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            throw new InvalidOperationException($"No Faciity Config found for ({sanitizedFacilityId}). Status Code: {response.StatusCode}");
        }

        var message = $"Error checking if facility ({sanitizedFacilityId}) exists in Tenant Service. Status Code: {response.StatusCode}";
        _logger.LogError(message);
        throw new Exception(message);
    }
}
