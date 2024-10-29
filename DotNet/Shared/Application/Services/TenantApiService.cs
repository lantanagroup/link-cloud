using LantanaGroup.Link.Shared.Application.Extensions.Security;
using LantanaGroup.Link.Shared.Application.Interfaces.Services.Security.Token;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Net;
using System.Net.Http.Headers;
using LantanaGroup.Link.Shared.Application.Services.Security;

namespace LantanaGroup.Link.Shared.Application.Services;

public class TenantApiService : ITenantApiService
{
    private readonly ILogger<TenantApiService> _logger;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IOptions<ServiceRegistry> _serviceRegistry;
    private readonly IOptions<LinkTokenServiceSettings> _linkTokenServiceConfig;
    private readonly ICreateSystemToken _createSystemToken;
    private readonly IOptions<BackendAuthenticationServiceExtension.LinkBearerServiceOptions> _linkBearerServiceOptions;

    public TenantApiService(
        ILogger<TenantApiService> logger, 
        IHttpClientFactory httpClientFactory, 
        IOptions<ServiceRegistry> serviceRegistry, 
        IOptions<LinkTokenServiceSettings> linkTokenServiceConfig, 
        ICreateSystemToken createSystemToken, 
        IOptions<BackendAuthenticationServiceExtension.LinkBearerServiceOptions> linkBearerServiceOptions)
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

        var tenantServiceApiUrl = _serviceRegistry.Value.TenantService.TenantServiceApiUrl;

        if (string.IsNullOrWhiteSpace(tenantServiceApiUrl))
            throw new Exception("Tenant Service URL is missing.");

        var httpClient = _httpClientFactory.CreateClient();

        var baseUri = new Uri(tenantServiceApiUrl.TrimEnd('/'));
        var endpoint = new Uri(baseUri, $"{_serviceRegistry.Value.TenantService.GetTenantRelativeEndpoint.TrimStart('/')}/{sanitizedFacilityId}").ToString();
        _logger.LogInformation("Tenant Base Endpoint: {0}", tenantServiceApiUrl);
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
}
