using System.IdentityModel.Tokens.Jwt;
using System.Text;
using FluentAssertions;
using LantanaGroup.Link.MockDmrpApi.Application.Services;
using LantanaGroup.Link.MockDmrpApi.Settings;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Xunit;

// ServiceTests globally imports Hl7.Fhir.Model, which has its own Claim type.
using Claim = System.Security.Claims.Claim;

namespace UnitTests.MockDmrpApi;

public class AuthTokenServiceTests
{
    private const string ClientId = "test-client";
    private const string ClientSecret = "test-client-secret";
    private const string SigningKey = "unit-test-signing-key-long-enough-for-hmac-sha512-which-needs-64-bytes";
    private const string Issuer = "link-mock-dmrp-tests";
    private const string Audience = "dmrp-api-tests";

    private static DmrpApiSettings Settings(Action<DmrpApiSettings>? customize = null)
    {
        var settings = new DmrpApiSettings
        {
            AuthClientId = ClientId,
            AuthClientSecret = ClientSecret,
            SigningKey = SigningKey,
            Issuer = Issuer,
            Audience = Audience,
            TokenLifetimeSeconds = 3600
        };

        customize?.Invoke(settings);
        return settings;
    }

    private static AuthTokenService CreateService(Action<DmrpApiSettings>? customize = null) =>
        new(Options.Create(Settings(customize)));

    private static AuthTokenResult IssueValid(AuthTokenService service, string? scope = "dmrp.read") =>
        service.Issue("client_credentials", ClientId, ClientSecret, scope);

    // ---------------------------------------------------------------- issuing

    [Fact]
    public void Issue_WithValidClientCredentials_ReturnsSignedJwtWithExpectedClaims()
    {
        var service = CreateService();

        var result = IssueValid(service);

        result.Succeeded.Should().BeTrue();
        result.AccessToken.Should().NotBeNullOrWhiteSpace();
        result.ExpiresInSeconds.Should().Be(3600);
        result.Scope.Should().Be("dmrp.read");

        // The point of this phase is that a real token is issued, not an opaque string.
        // Decode it and assert the claims a caller would actually read.
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(result.AccessToken);

        jwt.Issuer.Should().Be(Issuer);
        jwt.Audiences.Should().Contain(Audience);
        jwt.Subject.Should().Be(ClientId);
        jwt.Claims.Should().Contain(c => c.Type == "scope" && c.Value == "dmrp.read");
        jwt.Claims.Should().Contain(c => c.Type == JwtRegisteredClaimNames.Jti);
        jwt.Header.Alg.Should().Be(SecurityAlgorithms.HmacSha512);
        jwt.ValidTo.Should().BeAfter(DateTime.UtcNow);
    }

    [Fact]
    public void Issue_TwiceForSameClient_ProducesDistinctTokenIdentifiers()
    {
        var service = CreateService();
        var handler = new JwtSecurityTokenHandler();

        var first = handler.ReadJwtToken(IssueValid(service).AccessToken);
        var second = handler.ReadJwtToken(IssueValid(service).AccessToken);

        first.Claims.Single(c => c.Type == JwtRegisteredClaimNames.Jti).Value
            .Should().NotBe(second.Claims.Single(c => c.Type == JwtRegisteredClaimNames.Jti).Value);
    }

    [Fact]
    public void Issue_WithNullScope_SucceedsAndOmitsScopeValue()
    {
        var service = CreateService();

        var result = service.Issue("client_credentials", ClientId, ClientSecret, scope: null);

        result.Succeeded.Should().BeTrue();
        result.Scope.Should().BeNull();
    }

    [Theory]
    [InlineData("password")]
    [InlineData("authorization_code")]
    [InlineData("")]
    [InlineData(null)]
    public void Issue_WithUnsupportedGrantType_FailsAsUnsupportedGrantType(string? grantType)
    {
        var service = CreateService();

        var result = service.Issue(grantType, ClientId, ClientSecret, null);

        result.Succeeded.Should().BeFalse();
        result.Error.Should().Be(AuthTokenError.UnsupportedGrantType);
        result.AccessToken.Should().BeNull();
    }

    [Fact]
    public void Issue_WithWrongClientSecret_FailsAsInvalidClient()
    {
        var service = CreateService();

        var result = service.Issue("client_credentials", ClientId, "not-the-secret", null);

        result.Succeeded.Should().BeFalse();
        result.Error.Should().Be(AuthTokenError.InvalidClient);
        result.AccessToken.Should().BeNull();
    }

    [Fact]
    public void Issue_WithUnknownClientId_FailsAsInvalidClient()
    {
        var service = CreateService();

        var result = service.Issue("client_credentials", "someone-else", ClientSecret, null);

        result.Succeeded.Should().BeFalse();
        result.Error.Should().Be(AuthTokenError.InvalidClient);
    }

    [Theory]
    [InlineData(null, "secret")]
    [InlineData("client", null)]
    [InlineData("", "secret")]
    public void Issue_WithMissingCredentials_FailsAsInvalidRequest(string? clientId, string? clientSecret)
    {
        var service = CreateService();

        var result = service.Issue("client_credentials", clientId, clientSecret, null);

        result.Succeeded.Should().BeFalse();
        result.Error.Should().Be(AuthTokenError.InvalidRequest);
    }

    // ------------------------------------------------------------- validating

    [Fact]
    public void TryValidate_WithFreshlyIssuedToken_Succeeds()
    {
        var service = CreateService();
        var token = IssueValid(service).AccessToken;

        var valid = service.TryValidate($"Bearer {token}", out var principal);

        valid.Should().BeTrue();
        principal.Should().NotBeNull();
        principal!.FindFirst(JwtRegisteredClaimNames.Sub)?.Value.Should().Be(ClientId);
    }

    [Fact]
    public void TryValidate_IsCaseInsensitiveOnTheBearerScheme()
    {
        var service = CreateService();
        var token = IssueValid(service).AccessToken;

        service.TryValidate($"bearer {token}", out _).Should().BeTrue();
        service.TryValidate($"BEARER {token}", out _).Should().BeTrue();
    }

    [Fact]
    public void TryValidate_WithTokenSignedByADifferentKey_Fails()
    {
        // A token minted by an instance configured with another signing key. This is the
        // multi-replica hazard: every replica must share one key or tokens fail to cross.
        var otherInstance = CreateService(s =>
            s.SigningKey = "a-completely-different-signing-key-also-long-enough-for-hmac-sha512");
        var foreignToken = IssueValid(otherInstance).AccessToken;

        var service = CreateService();

        service.TryValidate($"Bearer {foreignToken}", out var principal).Should().BeFalse();
        principal.Should().BeNull();
    }

    [Fact]
    public void TryValidate_WithExpiredToken_Fails()
    {
        // Minted directly rather than through Issue(), which always dates tokens from now:
        // JwtSecurityToken refuses to construct with an expiry before its notBefore, so a
        // negative lifetime cannot produce one. Signed with the same key so that expiry is
        // the only reason validation can fail.
        var service = CreateService();
        var expiredToken = new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken(
            issuer: Issuer,
            audience: Audience,
            claims: [new Claim(JwtRegisteredClaimNames.Sub, ClientId)],
            notBefore: DateTime.UtcNow.AddHours(-2),
            expires: DateTime.UtcNow.AddHours(-1),
            signingCredentials: new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(SigningKey)),
                SecurityAlgorithms.HmacSha512)));

        service.TryValidate($"Bearer {expiredToken}", out var principal).Should().BeFalse();
        principal.Should().BeNull();
    }

    [Fact]
    public void TryValidate_WithTokenNotYetValid_Fails()
    {
        var service = CreateService();
        var futureToken = new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken(
            issuer: Issuer,
            audience: Audience,
            claims: [new Claim(JwtRegisteredClaimNames.Sub, ClientId)],
            notBefore: DateTime.UtcNow.AddHours(1),
            expires: DateTime.UtcNow.AddHours(2),
            signingCredentials: new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(SigningKey)),
                SecurityAlgorithms.HmacSha512)));

        service.TryValidate($"Bearer {futureToken}", out _).Should().BeFalse();
    }

    [Fact]
    public void TryValidate_WithTamperedPayload_Fails()
    {
        var service = CreateService();
        var token = IssueValid(service).AccessToken!;

        // Swap the payload segment for one claiming a different subject, leaving the
        // original signature in place.
        var segments = token.Split('.');
        var forgedPayload = Base64UrlEncoder.Encode(
            Encoding.UTF8.GetBytes($$"""{"sub":"attacker","iss":"{{Issuer}}","aud":"{{Audience}}"}"""));
        var tampered = $"{segments[0]}.{forgedPayload}.{segments[2]}";

        service.TryValidate($"Bearer {tampered}", out _).Should().BeFalse();
    }

    [Fact]
    public void TryValidate_WithWrongIssuerOrAudience_Fails()
    {
        var wrongIssuer = CreateService(s => s.Issuer = "somebody-else");
        var wrongAudience = CreateService(s => s.Audience = "another-api");
        var service = CreateService();

        service.TryValidate($"Bearer {IssueValid(wrongIssuer).AccessToken}", out _).Should().BeFalse();
        service.TryValidate($"Bearer {IssueValid(wrongAudience).AccessToken}", out _).Should().BeFalse();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Bearer")]
    [InlineData("Bearer ")]
    [InlineData("Bearer not-a-token")]
    [InlineData("Basic dXNlcjpwYXNz")]
    [InlineData("some-token-with-no-scheme")]
    [InlineData("Bearer a.b.c")]
    public void TryValidate_WithUnusableHeader_ReturnsFalseWithoutThrowing(string? header)
    {
        var service = CreateService();

        var act = () => service.TryValidate(header, out _);

        act.Should().NotThrow();
        service.TryValidate(header, out var principal).Should().BeFalse();
        principal.Should().BeNull();
    }

    // ----------------------------------------------------------- configuration

    [Theory]
    [InlineData("")]
    [InlineData("too-short")]
    [InlineData("sixty-three-bytes-is-still-one-byte-short-of-the-hs512-minimum")]
    public void Constructor_WithSigningKeyShorterThanHmacSha512Requires_Throws(string signingKey)
    {
        // Caught at construction so a misconfigured deployment fails at startup rather
        // than surfacing as an opaque 500 on the first token request.
        var act = () => CreateService(s => s.SigningKey = signingKey);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*SigningKey*");
    }

    [Fact]
    public void Constructor_WithSigningKeyOfExactlyTheMinimumLength_Succeeds()
    {
        var exactly64Bytes = new string('k', 64);

        var act = () => CreateService(s => s.SigningKey = exactly64Bytes);

        act.Should().NotThrow();
    }
}
