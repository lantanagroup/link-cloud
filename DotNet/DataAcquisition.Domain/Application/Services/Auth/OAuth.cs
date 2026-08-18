using DataAcquisition.Domain.Application.Models;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Services.Interfaces;
using LantanaGroup.Link.DataAcquisition.Domain.Settings;
using LantanaGroup.Link.Shared.Application.Interfaces;
using LantanaGroup.Link.Shared.Application.Interfaces.Services;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using LantanaGroup.Link.Shared.Application.Services.Security;
using Microsoft.Extensions.Logging;
using System.Net.Http.Headers;

namespace LantanaGroup.Link.DataAcquisition.Domain.Application.Services.Auth;

public class OAuth : IAuth
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<OAuth> _logger;
    private readonly ICacheService _cacheService;
    private readonly ISecretManager _secretManager;

    public OAuth(
        HttpClient httpClient,
        ILogger<OAuth> logger,
        ICacheService cacheService,
        ISecretManager secretManager)
    {
        _httpClient = httpClient;
        _logger = logger;
        _cacheService = cacheService;
        _secretManager = secretManager;
    }

    public async Task<(bool isQueryParam, object authHeaderValue)> SetAuthentication(string facilityId, AuthenticationConfigurationModel authSettings)
    {
        var cachedToken = _cacheService.Get<string>(facilityId);

        if (!string.IsNullOrWhiteSpace(cachedToken))
            return (false, new AuthenticationHeaderValue(DataAcquisitionConstants.Auth.Bearer, cachedToken));

        try
        {
            if (string.IsNullOrWhiteSpace(authSettings.ClientId))
                throw new ArgumentException("A secret name for ClientId must be provided for OAuth authentication.");
            if (string.IsNullOrWhiteSpace(authSettings.ClientSecret))
                throw new ArgumentException("A secret name for ClientSecret must be provided for OAuth authentication.");

            var clientId = await _secretManager.GetSecretAsync(authSettings.ClientId, CancellationToken.None);
            var clientSecret = await _secretManager.GetSecretAsync(authSettings.ClientSecret, CancellationToken.None);

            if (string.IsNullOrWhiteSpace(clientId))
                throw new InvalidOperationException($"No value found in secret manager for ClientId");
            if (string.IsNullOrWhiteSpace(clientSecret))
                throw new InvalidOperationException($"No value found in secret manager for ClientSecret");

            var request = new HttpRequestMessage(HttpMethod.Post, authSettings.TokenUrl);
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic",
                Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes($"{clientId}:{clientSecret}")));

            var parameters = new Dictionary<string, string>
            {
                { "grant_type", "client_credentials" }
            };

            if (!string.IsNullOrWhiteSpace(authSettings.Scope))
                parameters["scope"] = authSettings.Scope;

            request.Content = new FormUrlEncodedContent(parameters);

            var responseMessage = await _httpClient.SendAsync(request);
            responseMessage.EnsureSuccessStatusCode();
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
        catch (Exception ex) when (ex is not ArgumentException && ex is not InvalidOperationException)
        {
            _logger.LogError(ex, "Error acquiring OAuth access token for facility {FacilityId}", facilityId.SanitizeForLog());
        }

        return (false, null);
    }
}
