using LantanaGroup.Link.DataAcquisition.Application.Models;
using LantanaGroup.Link.DataAcquisition.Domain.Models;
using LantanaGroup.Link.DataAcquisition.Services.Interfaces;
using LantanaGroup.Link.Shared.Application.Extensions.Caching;
using LantanaGroup.Link.Shared.Application.Interfaces;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using Microsoft.AspNetCore.Authentication.BearerToken;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Security;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using System.Text.RegularExpressions;

namespace LantanaGroup.Link.DataAcquisition.Application.Services.Auth;

public class EpicAuth : IAuth
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<EpicAuth> _logger;
    private readonly ICacheService _cacheService;

    public EpicAuth(
        HttpClient httpClient, 
        ILogger<EpicAuth> logger,
        ICacheService cacheService
        )
    {
        _httpClient = httpClient;
        _logger = logger;
        _cacheService = cacheService;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="httpClient"></param>
    /// <param name="authSettings"></param>
    /// <exception cref="NotImplementedException"></exception>
    public async Task<(bool isQueryParam, object authHeaderValue)> SetAuthentication(string facilityId, AuthenticationConfiguration authSettings)
    {
        var cachedToken = _cacheService.Get<string>(facilityId);

        if(!string.IsNullOrWhiteSpace(cachedToken))
            return (false, new AuthenticationHeaderValue("Bearer", cachedToken));

        string jwt = GetJwt(authSettings);

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
                    _cacheService.Set(facilityId, accessToken, TimeSpan.FromSeconds(expirationInSeconds), ExpirationType.Absolute);

                    _logger.LogInformation($"Bearer Information Acquired.");
                    return (false, new AuthenticationHeaderValue("Bearer", accessToken));
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error Acquiring Access Token Encountered", ex);
        }

        return (false, null);
    }

    private string Sanitize(string input)
    {
        var sanitizedInput = Regex.Replace(input, @"\t|\n|\r", string.Empty, RegexOptions.Compiled).Trim();
        return sanitizedInput;
    }

    private string GetJwt(AuthenticationConfiguration authSettings)
    {
        var key = authSettings.Key.Replace("\\r\\n\\t", "\r\n\t");

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

        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = authSettings.ClientId,
            Audience = authSettings.TokenUrl,
            IssuedAt = now,
            NotBefore = now,
            Expires = now.AddMinutes(4),
            AdditionalHeaderClaims = headers,
            Claims = new Dictionary<string, object> { { "jti", Guid.NewGuid().ToString() } },
            Subject = new ClaimsIdentity(new List<Claim> { new Claim("sub", authSettings.ClientId) }),
            SigningCredentials = signingCredentials
        };

        string token = handler.CreateToken(descriptor);
        return token;
    }
}
