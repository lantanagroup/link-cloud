
using LantanaGroup.Link.Normalization.Application.Settings;
using System.Net;

namespace LantanaGroup.Link.Normalization.Application.Services;

public class TenantApiService : ITenantApiService
{
    private readonly ILogger<TenantApiService> _logger;
    private readonly HttpClient _httpClient;
    private readonly TenantApiSettings _settings;

    public TenantApiService(ILogger<TenantApiService> logger, HttpClient httpClient, TenantApiSettings settings)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
    }

    public async Task<bool> CheckFacilityExists(string facilityId, CancellationToken cancellationToken = default)
    {
        var endpoint = $"{_settings.TenantServiceBaseEndpoint.TrimEnd('/')}/{_settings.GetTenantRelativeEndpoint.TrimEnd('/')}?facilityId={facilityId}";
        var response = await _httpClient.GetAsync(endpoint, cancellationToken);

        if(response.IsSuccessStatusCode)
        {
            return true;
        }
        else if(response.StatusCode == HttpStatusCode.NotFound)
        {
            return false;
        }
        else
        {
            var message = $"Error checking if facility ({facilityId}) exists in Tenant Service. Status Code: {response.StatusCode}";
            _logger.LogError(message);
            throw new Exception(message);
        }
    }
}
