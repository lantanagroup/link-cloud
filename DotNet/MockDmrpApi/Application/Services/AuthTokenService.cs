using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using LantanaGroup.Link.MockDmrpApi.Settings;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace LantanaGroup.Link.MockDmrpApi.Application.Services;

public class AuthTokenService : IAuthTokenService
{
    private const string ClientCredentialsGrant = "client_credentials";
    private const string ScopeClaim = "scope";
    private const string BearerPrefix = "Bearer ";

    /// <summary>HS512 signs with a 512-bit key, so anything shorter is rejected outright.</summary>
    private const int MinimumSigningKeyBytes = 64;

    private readonly DmrpApiSettings _settings;
    private readonly SymmetricSecurityKey _signingKey;
    private readonly JwtSecurityTokenHandler _tokenHandler = new();

    public AuthTokenService(IOptions<DmrpApiSettings> settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        _settings = settings.Value;

        var keyBytes = Encoding.UTF8.GetBytes(_settings.SigningKey ?? string.Empty);
        if (keyBytes.Length < MinimumSigningKeyBytes)
        {
            // Fail at startup rather than on the first token request, where it would
            // surface as an opaque 500 from an endpoint that looks unrelated.
            throw new InvalidOperationException(
                $"{DmrpApiSettings.ConfigSectionName}:{nameof(DmrpApiSettings.SigningKey)} must be at least " +
                $"{MinimumSigningKeyBytes} bytes to sign with HMAC-SHA512; it is {keyBytes.Length}.");
        }

        _signingKey = new SymmetricSecurityKey(keyBytes);
    }

    public AuthTokenResult Issue(string? grantType, string? clientId, string? clientSecret, string? scope)
    {
        if (!string.Equals(grantType, ClientCredentialsGrant, StringComparison.Ordinal))
        {
            return AuthTokenResult.Failure(
                AuthTokenError.UnsupportedGrantType,
                $"Only the {ClientCredentialsGrant} grant is supported.");
        }

        if (string.IsNullOrEmpty(clientId) || string.IsNullOrEmpty(clientSecret))
        {
            return AuthTokenResult.Failure(AuthTokenError.InvalidRequest, "client_id and client_secret are required.");
        }

        if (!MatchesConfigured(clientId, _settings.AuthClientId) ||
            !MatchesConfigured(clientSecret, _settings.AuthClientSecret))
        {
            return AuthTokenResult.Failure(AuthTokenError.InvalidClient);
        }

        var issuedAt = DateTimeOffset.UtcNow;
        var expires = issuedAt.AddSeconds(_settings.TokenLifetimeSeconds);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, clientId),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new(ScopeClaim, scope ?? string.Empty)
        };

        var token = new JwtSecurityToken(
            issuer: _settings.Issuer,
            audience: _settings.Audience,
            claims: claims,
            notBefore: issuedAt.UtcDateTime,
            expires: expires.UtcDateTime,
            signingCredentials: new SigningCredentials(_signingKey, SecurityAlgorithms.HmacSha512));

        return AuthTokenResult.Success(
            _tokenHandler.WriteToken(token),
            _settings.TokenLifetimeSeconds,
            issuedAt,
            scope);
    }

    public bool TryValidate(string? authorizationHeaderValue, out ClaimsPrincipal? principal)
    {
        principal = null;

        if (string.IsNullOrWhiteSpace(authorizationHeaderValue))
        {
            return false;
        }

        if (!authorizationHeaderValue.StartsWith(BearerPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var token = authorizationHeaderValue[BearerPrefix.Length..].Trim();
        if (string.IsNullOrEmpty(token))
        {
            return false;
        }

        var parameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = _settings.Issuer,
            ValidateAudience = true,
            ValidAudience = _settings.Audience,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = _signingKey,
            ValidAlgorithms = [SecurityAlgorithms.HmacSha512],
            ClockSkew = TimeSpan.Zero
        };

        try
        {
            principal = _tokenHandler.ValidateToken(token, parameters, out _);
            return true;
        }
        catch (Exception)
        {
            // Every rejection reason -- malformed, expired, wrong signature, wrong issuer
            // or audience -- is the same answer to the caller, and none of them should
            // propagate as a fault.
            principal = null;
            return false;
        }
    }

    private static bool MatchesConfigured(string supplied, string? configured)
    {
        if (string.IsNullOrEmpty(configured))
        {
            return false;
        }

        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(supplied),
            Encoding.UTF8.GetBytes(configured));
    }
}
