using DataAcquisition.Domain.Application.Models;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Services.Interfaces;
using LantanaGroup.Link.DataAcquisition.Domain.Settings;
using LantanaGroup.Link.Shared.Application.Interfaces;
using LantanaGroup.Link.Shared.Application.Interfaces.Services;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using LantanaGroup.Link.Shared.Application.Services;
using LantanaGroup.Link.Shared.Application.Services.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace LantanaGroup.Link.DataAcquisition.Domain.Application.Services.Auth;

public class EpicAuth : IAuth
{
    private const string PemSuffix = "-pem";

    private readonly HttpClient _httpClient;
    private readonly ILogger<EpicAuth> _logger;
    private readonly ICacheService _cacheService;
    private readonly ISecretManager _secretManager;
    private readonly IOptions<DataSourceAuthSettings> _dataSourceAuthSettings;
    private readonly ITenantApiService _tenantApiService;
    public EpicAuth(
        HttpClient httpClient,
        ILogger<EpicAuth> logger,
        ICacheService cacheService,
        ISecretManager secretManager,
        IOptions<DataSourceAuthSettings> dataSourceAuthSettings,
        ITenantApiService tenantApiService
        )
    {
        _httpClient = httpClient;
        _logger = logger;
        _cacheService = cacheService;
        _secretManager = secretManager;
        _dataSourceAuthSettings = dataSourceAuthSettings;
        _tenantApiService = tenantApiService;
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

        var jwt = await GetJwt(facilityId, authSettings, cancellationToken);

        try
        {
            var responseMessage = await _httpClient
                .PostAsync($"{authSettings.TokenUrl}",
                new StringContent($"grant_type=client_credentials&client_assertion_type=urn:ietf:params:oauth:client-assertion-type:jwt-bearer&client_assertion={jwt}",
                Encoding.UTF8,
                "application/x-www-form-urlencoded"));
            var responseBody = await responseMessage.Content.ReadAsStringAsync();

            if (!responseMessage.IsSuccessStatusCode)
            {
                _logger.LogError(
                    "Token endpoint returned {StatusCode} acquiring an access token for facility {FacilityId}. Response: {Response}",
                    (int)responseMessage.StatusCode, facilityId.SanitizeForLog(), Truncate(responseBody).SanitizeForLog());
                return (false, null);
            }

            using var responseJson = System.Text.Json.JsonDocument.Parse(responseBody);

            string? accessToken = null;
            if (responseJson.RootElement.TryGetProperty("access_token", out var accessTokenElement))
            {
                accessToken = Sanitize(accessTokenElement.GetString());
            }
            if (string.IsNullOrEmpty(accessToken))
            {
                _logger.LogError(
                    "Token endpoint response for facility {FacilityId} did not contain an access token.",
                    facilityId.SanitizeForLog());
                return (false, null);
            }

            int expirationInSeconds = 300;
            if (responseJson.RootElement.TryGetProperty("expires_in", out var expiresInElement))
            {
                expirationInSeconds = expiresInElement.GetInt32();
            }

            await _cacheService.SetAsync(facilityId, accessToken, TimeSpan.FromSeconds(expirationInSeconds), ExpirationType.Absolute, cancellationToken);
            return (false, new AuthenticationHeaderValue(DataAcquisitionConstants.Auth.Bearer, accessToken));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error Acquiring Access Token Encountered");
        }

        return (false, null);
    }

    private string Sanitize(string? input)
    {
        if (string.IsNullOrEmpty(input))
            return "";
        var sanitizedInput = Regex.Replace(input, @"\t|\n|\r", string.Empty, RegexOptions.Compiled).Trim();
        return sanitizedInput;
    }

    private string Truncate(string? input, int maxLength = 500)
    {
        if (string.IsNullOrEmpty(input))
            return "";
        return input.Length <= maxLength ? input : input[..maxLength] + "...";
    }

    private async Task<string> GetJwt(string facilityId, AuthenticationConfigurationModel authSettings, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(facilityId))
            throw new ArgumentException("A facilityId must be provided for Epic authentication.");

        var resolvedPem = await ResolvePem(facilityId, authSettings, cancellationToken);

        if (string.IsNullOrWhiteSpace(authSettings.ClientId))
                throw new ArgumentException("A secret name for ClientId must be provided for Epic authentication.");
        var clientId = await _secretManager.GetSecretAsync(authSettings.ClientId, CancellationToken.None);
        if (string.IsNullOrWhiteSpace(clientId))
            throw new InvalidOperationException($"No value found in secret manager for ClientId");

        var audience = authSettings.Audience ?? authSettings.TokenUrl;
        if (string.IsNullOrWhiteSpace(audience))
            throw new InvalidOperationException("An Audience or TokenUrl must be provided for Epic authentication.");

        using var ecdsa = TryGetECDsa(resolvedPem);
        if (ecdsa is not null)
        {
            var algorithm = GetECDsaAlgorithm(ecdsa);
            if (algorithm is not null)
                return GetToken(clientId, audience,
                    new SigningCredentials(new ECDsaSecurityKey(ecdsa) { CryptoProviderFactory = NonCachingCryptoProviderFactory() }, algorithm));
        }

        using var rsa = TryGetRSA(resolvedPem);
        if (rsa is not null)
            return GetToken(clientId, audience,
                new SigningCredentials(new RsaSecurityKey(rsa) { CryptoProviderFactory = NonCachingCryptoProviderFactory() }, SecurityAlgorithms.RsaSha256));

        throw new InvalidOperationException("PEM uses unsupported algorithm.");
    }

    /// <summary>
    /// The default <see cref="CryptoProviderFactory"/> is a static singleton that caches signature providers by a
    /// key derived from the key material, so a provider built here would outlive the ECDsa/RSA instance disposed
    /// above and throw ObjectDisposedException when the cached provider was reused on a later call.
    /// </summary>
    private static CryptoProviderFactory NonCachingCryptoProviderFactory() => new() { CacheSignatureProviders = false };

    private async Task<string> ResolvePem(string facilityId, AuthenticationConfigurationModel authSettings, CancellationToken cancellationToken)
    {
        var keySource = _dataSourceAuthSettings.Value.KeySource;

        if (keySource == PemKeySource.Database)
        {
            if (string.IsNullOrWhiteSpace(authSettings.Key))
                throw new InvalidOperationException(
                    $"No PEM found on the authentication configuration for facility '{facilityId}' (KeySource=Database).");

            return authSettings.Key;
        }

        //char array so it can be cleared from memory asap
        var vendorSecretName = (await _tenantApiService.GetVendorSigningKeySecretId(facilityId, cancellationToken))?.ToCharArray();

        var pemName = vendorSecretName == null || vendorSecretName.Length == 0 || Array.TrueForAll(vendorSecretName, char.IsWhiteSpace)
            ? $"{facilityId}{PemSuffix}".ToCharArray()
            : vendorSecretName;

        var resolvedPem = await _secretManager.GetSecretAsync(new string(pemName) ?? "", CancellationToken.None);
        if(pemName != null)
        {
            Array.Clear(pemName);
        }

        if (string.IsNullOrWhiteSpace(resolvedPem))
            throw new InvalidOperationException(
                $"Authentication configuration is incomplete or the signing key could not be resolved.");

        return resolvedPem;
    }

    private static ECDsa? TryGetECDsa(string pem)
    {
        var ecdsa = ECDsa.Create();
        try
        {
            ecdsa.ImportFromPem(pem);
        }
        catch (Exception)
        {
            ecdsa.Dispose();
            return null;
        }

        return ecdsa;
    }

    private static string? GetECDsaAlgorithm(ECDsa ecdsa) => ecdsa.KeySize switch
        {
            256 => SecurityAlgorithms.EcdsaSha256,
            384 => SecurityAlgorithms.EcdsaSha384,
            521 => SecurityAlgorithms.EcdsaSha512,
            _ => null
        };

    private static RSA? TryGetRSA(string pem)
    {
        var rsa = RSA.Create();
        try
        {
            rsa.ImportFromPem(pem);
        }
        catch (Exception)
        {
            rsa.Dispose();
            return null;
        }

        return rsa;
    }

    private string GetToken(string clientId, string audience, SigningCredentials credentials)
    {
        DateTime now = DateTime.Now;
        SecurityTokenDescriptor tokenDescriptor = new()
        {
            AdditionalHeaderClaims = new Dictionary<string, object>
            {
                { JwtRegisteredClaimNames.Typ, "JWT" }
            },
            Issuer = clientId,
            Subject = new([
                new Claim(JwtRegisteredClaimNames.Sub, clientId)
            ]),
            Audience = audience,
            IssuedAt = now,
            NotBefore = now,
            Expires = now.AddMinutes(4.0),
            Claims = new Dictionary<string, object>
            {
                { JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString() }
            },
            SigningCredentials = credentials
        };
        JsonWebTokenHandler tokenHandler = new();
        return tokenHandler.CreateToken(tokenDescriptor);
    }
}
