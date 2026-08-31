using DataAcquisition.Domain.Application.Models;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Services.Interfaces;
using LantanaGroup.Link.DataAcquisition.Domain.Settings;
using LantanaGroup.Link.Shared.Application.Interfaces;
using LantanaGroup.Link.Shared.Application.Interfaces.Services;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using LantanaGroup.Link.Shared.Application.Services;
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

    private async Task<string> GetJwt(string facilityId, AuthenticationConfigurationModel authSettings, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(facilityId))
            throw new ArgumentException("A facilityId must be provided for Epic authentication.");

        var resolvedPem = await ResolvePem(facilityId, authSettings, cancellationToken);

        var signingCredentials =
            TryGetECDsaSigningCredentials(resolvedPem)
            ?? TryGetRSASigningCredentials(resolvedPem)
            ?? throw new InvalidOperationException("PEM uses unsupported algorithm.");

        if (string.IsNullOrWhiteSpace(authSettings.ClientId))
                throw new ArgumentException("A secret name for ClientId must be provided for Epic authentication.");
        var clientId = await _secretManager.GetSecretAsync(authSettings.ClientId, CancellationToken.None);
        if (string.IsNullOrWhiteSpace(clientId))
            throw new InvalidOperationException($"No value found in secret manager for ClientId");

        var audience = authSettings.Audience ?? authSettings.TokenUrl;
        if (string.IsNullOrWhiteSpace(audience))
            throw new InvalidOperationException("An Audience or TokenUrl must be provided for Epic authentication.");

        return GetToken(clientId, audience, signingCredentials);
    }

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

        var vendorSecretName = await _tenantApiService.GetVendorSigningKeySecretId(facilityId, cancellationToken);

        var pemName = string.IsNullOrWhiteSpace(vendorSecretName)
            ? $"{facilityId}{PemSuffix}"
            : vendorSecretName;

        var resolvedPem = await _secretManager.GetSecretAsync(pemName, CancellationToken.None);

        if (string.IsNullOrWhiteSpace(resolvedPem))
            throw new InvalidOperationException(
                $"No PEM found in secret manager for facility '{facilityId}' (expected secret '{pemName}').");

        return resolvedPem;
    }

    private SigningCredentials? TryGetECDsaSigningCredentials(string pem)
    {
        ECDsa ecdsa = ECDsa.Create();
        try
        {
            ecdsa.ImportFromPem(pem);
        }
        catch (Exception)
        {
            return null;
        }
        string? algorithm = ecdsa.KeySize switch
        {
            256 => SecurityAlgorithms.EcdsaSha256,
            384 => SecurityAlgorithms.EcdsaSha384,
            521 => SecurityAlgorithms.EcdsaSha512,
            _ => null
        };
        if (algorithm == null)
        {
            return null;
        }
        return new SigningCredentials(new ECDsaSecurityKey(ecdsa), algorithm);
    }

    private SigningCredentials? TryGetRSASigningCredentials(string pem)
    {
        RSA rsa = RSA.Create();
        try
        {
            rsa.ImportFromPem(pem);
        }
        catch (Exception)
        {
            return null;
        }
        return new SigningCredentials(new RsaSecurityKey(rsa), SecurityAlgorithms.RsaSha256);
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
