using System.Security.Claims;

namespace LantanaGroup.Link.MockDmrpApi.Application.Services;

/// <summary>
/// Error codes an unsuccessful token request can carry. Named to match the values the
/// contract's AuthErrorResponse declares.
/// </summary>
public enum AuthTokenError
{
    InvalidRequest,
    InvalidClient,
    UnsupportedGrantType
}

public class AuthTokenResult
{
    private AuthTokenResult()
    {
    }

    public bool Succeeded { get; private init; }

    public AuthTokenError? Error { get; private init; }

    public string? ErrorDescription { get; private init; }

    public string? AccessToken { get; private init; }

    public int ExpiresInSeconds { get; private init; }

    public DateTimeOffset IssuedAt { get; private init; }

    public string? Scope { get; private init; }

    public static AuthTokenResult Success(string accessToken, int expiresInSeconds, DateTimeOffset issuedAt, string? scope) =>
        new()
        {
            Succeeded = true,
            AccessToken = accessToken,
            ExpiresInSeconds = expiresInSeconds,
            IssuedAt = issuedAt,
            Scope = scope
        };

    public static AuthTokenResult Failure(AuthTokenError error, string? description = null) =>
        new()
        {
            Succeeded = false,
            Error = error,
            ErrorDescription = description
        };
}

/// <summary>
/// Issues and validates the bearer tokens that guard the reporting plan query.
/// </summary>
/// <remarks>
/// Tokens are genuine signed JSON Web Tokens carrying standard claims and a real
/// expiry, so a caller's acquire-then-use and refresh-on-expiry paths are exercised
/// rather than trivially satisfied. They are signed symmetrically (HS512) and there is
/// no discovery document or key set, so a caller cannot validate a token the way it
/// would validate one from a real authorization server.
/// </remarks>
public interface IAuthTokenService
{
    AuthTokenResult Issue(string? grantType, string? clientId, string? clientSecret, string? scope);

    /// <summary>
    /// Validates an <c>Authorization</c> header value. Returns false -- never throws --
    /// for a missing, malformed, expired, wrongly-signed or otherwise unusable token.
    /// </summary>
    bool TryValidate(string? authorizationHeaderValue, out ClaimsPrincipal? principal);
}
