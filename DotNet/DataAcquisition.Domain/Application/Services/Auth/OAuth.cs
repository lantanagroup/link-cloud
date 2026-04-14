using DataAcquisition.Domain.Application.Models;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Services.Interfaces;
using LantanaGroup.Link.DataAcquisition.Domain.Settings;
using LantanaGroup.Link.Shared.Application.Interfaces;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using Microsoft.Extensions.Logging;
using System.Net.Http.Headers;

namespace LantanaGroup.Link.DataAcquisition.Domain.Application.Services.Auth;

public class OAuth : IAuth
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<OAuth> _logger;
    private readonly ICacheService _cacheService;

    public OAuth(
        HttpClient httpClient,
        ILogger<OAuth> logger,
        ICacheService cacheService)
    {
        _httpClient = httpClient;
        _logger = logger;
        _cacheService = cacheService;
    }

    public async Task<(bool isQueryParam, object authHeaderValue)> SetAuthentication(string facilityId, AuthenticationConfigurationModel authSettings)
    {
        var cachedToken = _cacheService.Get<string>(facilityId);

        if (!string.IsNullOrWhiteSpace(cachedToken))
            return (false, new AuthenticationHeaderValue(DataAcquisitionConstants.Auth.Bearer, cachedToken));

        try
        {
            var credentials = Convert.ToBase64String(
                System.Text.Encoding.UTF8.GetBytes($"{authSettings.ClientId}:{authSettings.ClientSecret}"));

            var request = new HttpRequestMessage(HttpMethod.Post, authSettings.TokenUrl);
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic", credentials);

            var parameters = new Dictionary<string, string>
            {
                { "grant_type", "client_credentials" }
            };

            if (!string.IsNullOrWhiteSpace(authSettings.Scope))
                parameters["scope"] = authSettings.Scope;

            request.Content = new FormUrlEncodedContent(parameters);

            var responseMessage = await _httpClient.SendAsync(request);

            var responseBody = await responseMessage.Content.ReadAsStringAsync();
            var responseJson = System.Text.Json.JsonDocument.Parse(responseBody);

            if (responseJson != null)
            {
                var expirationInSeconds = responseJson.RootElement.GetProperty("expires_in").GetInt32();
                var accessToken = responseJson.RootElement.GetProperty("access_token").GetString();

                if (!string.IsNullOrWhiteSpace(accessToken))
                {
                    _cacheService.Set(facilityId, accessToken, TimeSpan.FromSeconds(expirationInSeconds), ExpirationType.Absolute);
                    return (false, new AuthenticationHeaderValue(DataAcquisitionConstants.Auth.Bearer, accessToken));
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error acquiring OAuth access token for facility {FacilityId}", facilityId);
        }

        return (false, null);
    }
}
