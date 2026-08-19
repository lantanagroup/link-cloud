using DataAcquisition.Domain.Application.Models;
using LantanaGroup.Link.DataAcquisition.Domain.Settings;
using LantanaGroup.Link.Shared.Application.Interfaces;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using System.Text.RegularExpressions;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Services.Interfaces;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Security;
using LantanaGroup.Link.Shared.Application.Interfaces.Services;

namespace LantanaGroup.Link.DataAcquisition.Domain.Application.Services.Auth;

public class EpicAuth : IAuth
{
    private const string PemSuffix = "-pem";

    private readonly HttpClient _httpClient;
    private readonly ILogger<EpicAuth> _logger;
    private readonly ICacheService _cacheService;
    private readonly ISecretManager _secretManager;
    private readonly IOptions<DataSourceAuthSettings> _dataSourceAuthSettings;
    public EpicAuth(
        HttpClient httpClient,
        ILogger<EpicAuth> logger,
        ICacheService cacheService,
        ISecretManager secretManager,
        IOptions<DataSourceAuthSettings> dataSourceAuthSettings
        )
    {
        _httpClient = httpClient;
        _logger = logger;
        _cacheService = cacheService;
        _secretManager = secretManager;
        _dataSourceAuthSettings = dataSourceAuthSettings;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="httpClient"></param>
    /// <param name="authSettings"></param>
    /// <exception cref="NotImplementedException"></exception>
    public async Task<(bool isQueryParam, object authHeaderValue)> SetAuthentication(string facilityId, AuthenticationConfigurationModel authSettings, CancellationToken cancellationToken = default)
    {
        var cachedToken = await _cacheService.GetAsync<string>(facilityId, cancellationToken);

        if (!string.IsNullOrWhiteSpace(cachedToken))
            return (false, new AuthenticationHeaderValue("Bearer", cachedToken));

        var jwt = await GetJwt(facilityId, authSettings);

        try
        {
            _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/x-www-form-urlencoded"));
            var responseMessage = await _httpClient
                .PostAsync($"{authSettings.TokenUrl}",
                new StringContent($"grant_type=client_credentials&client_assertion_type=urn:ietf:params:oauth:client-assertion-type:jwt-bearer&client_assertion={jwt}",
                Encoding.UTF8,
                "application/x-www-form-urlencoded"));
            var responseBody = await responseMessage.Content.ReadAsStringAsync();
            var responseJson = System.Text.Json.JsonDocument.Parse(responseBody);

            if (responseJson != null)
            {
                var expirationInSeconds = responseJson.RootElement.GetProperty("expires_in").GetInt32();
                var accessToken = Sanitize(responseJson.RootElement.GetProperty("access_token").GetString());
                if (!string.IsNullOrWhiteSpace(accessToken))
                {
                    await _cacheService.SetAsync(facilityId, accessToken, TimeSpan.FromSeconds(expirationInSeconds), ExpirationType.Absolute, cancellationToken);
                    return (false, new AuthenticationHeaderValue(DataAcquisitionConstants.Auth.Bearer, accessToken));
                }
            }
        }
        catch (Exception ex) when (ex is not ArgumentException && ex is not InvalidOperationException)
        {
            _logger.LogError(ex, "Error Acquiring Access Token Encountered");
        }

        return (false, null);
    }

    private string Sanitize(string input)
    {
        var sanitizedInput = Regex.Replace(input, @"\t|\n|\r", string.Empty, RegexOptions.Compiled).Trim();
        return sanitizedInput;
    }

    private async Task<string> GetJwt(string facilityId, AuthenticationConfigurationModel authSettings)
    {
        if (string.IsNullOrWhiteSpace(facilityId))
            throw new ArgumentException("A facilityId must be provided for Epic authentication.");

        var resolvedPem = await ResolvePem(facilityId, authSettings);
        var key = resolvedPem.Replace("\\r\\n\\t", "\r\n\t");

        var handler = new JsonWebTokenHandler();
        var now = DateTime.UtcNow;
        RsaSecurityKey rsaKey;

        Dictionary<string, object> headers = new Dictionary<string, object>();
        headers.Add("typ", "JWT");

        using (var stream = new StringReader(key))
        {
            var reader = new Org.BouncyCastle.OpenSsl.PemReader(stream);
            var keyPair = reader.ReadObject() as AsymmetricCipherKeyPair;
            var privateRsaParams = keyPair.Private as RsaPrivateCrtKeyParameters;
            var rsaParams = DotNetUtilities.ToRSAParameters(privateRsaParams);
            rsaKey = new RsaSecurityKey(rsaParams);
        }

        var signingCredentials = new SigningCredentials(rsaKey, SecurityAlgorithms.RsaSha256);

        if (string.IsNullOrWhiteSpace(authSettings.ClientId))
                throw new ArgumentException("A secret name for ClientId must be provided for Epic authentication.");

        var clientId = await _secretManager.GetSecretAsync(authSettings.ClientId, CancellationToken.None);

        if (string.IsNullOrWhiteSpace(clientId))
            throw new InvalidOperationException($"No value found in secret manager for ClientId");

        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = clientId,
            Audience = authSettings.TokenUrl,
            IssuedAt = now,
            NotBefore = now,
            Expires = now.AddMinutes(4),
            AdditionalHeaderClaims = headers,
            Claims = new Dictionary<string, object> { { "jti", Guid.NewGuid().ToString() } },
            Subject = new ClaimsIdentity(new List<Claim> { new Claim("sub", clientId) }),
            SigningCredentials = signingCredentials
        };

        string token = handler.CreateToken(descriptor);
        return token;
    }

    private async Task<string> ResolvePem(string facilityId, AuthenticationConfigurationModel authSettings)
    {
        var keySource = _dataSourceAuthSettings.Value.KeySource;

        if (keySource == PemKeySource.Database)
        {
            if (string.IsNullOrWhiteSpace(authSettings.Key))
                throw new InvalidOperationException(
                    $"No PEM found on the authentication configuration for facility '{facilityId}' (KeySource=Database).");

            return authSettings.Key;
        }

        var pemName = $"{facilityId}{PemSuffix}";
        var resolvedPem = await _secretManager.GetSecretAsync(pemName, CancellationToken.None);

        if (string.IsNullOrWhiteSpace(resolvedPem))
            throw new InvalidOperationException(
                $"No PEM found in secret manager for facility '{facilityId}' (expected secret '{pemName}').");

        return resolvedPem;
    }
}
