using DataAcquisition.Domain.Application.Models;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Services.Auth;
using LantanaGroup.Link.DataAcquisition.Domain.Settings;
using LantanaGroup.Link.Shared.Application.Interfaces;
using LantanaGroup.Link.Shared.Application.Interfaces.Services;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using LantanaGroup.Link.Shared.Application.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Moq;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using UnitTests.Admin.BFF.Aggregation;
using Task = System.Threading.Tasks.Task;

namespace UnitTests.DataAcquisition.Auth;

[Trait("Category", "UnitTests")]
public class EpicAuthTests
{
    private const string FacilityId = "test-facility";
    private const string KeySecretName = "test-facility-pem";
    private const string VendorSecretName = "epic-signing-key";
    private const string ClientIdSecretName = "secret/clientid";
    private const string ResolvedClientId = "epic-client-12345";
    private const string TokenUrl = "https://epic.example/oauth2/token";

    private readonly Mock<ILogger<EpicAuth>> _mockLogger;
    private readonly Mock<ICacheService> _mockCacheService;
    private readonly Mock<ISecretManager> _mockSecretManager;
    private readonly Mock<ITenantApiService> _mockTenantApiService;
    private readonly string _testPrivateKeyPem;
    private readonly RSA _testPublicKey;

    public EpicAuthTests()
    {
        using var rsa = RSA.Create(2048);
        _testPrivateKeyPem = rsa.ExportRSAPrivateKeyPem();
        _testPublicKey = RSA.Create();
        _testPublicKey.ImportSubjectPublicKeyInfo(rsa.ExportSubjectPublicKeyInfo(), out _);

        _mockLogger = new Mock<ILogger<EpicAuth>>();

        _mockCacheService = new Mock<ICacheService>();
        _mockCacheService.Setup(x => x.GetAsync<string>(FacilityId, It.IsAny<CancellationToken>())).ReturnsAsync((string)null!);

        _mockSecretManager = new Mock<ISecretManager>();
        _mockSecretManager
            .Setup(x => x.GetSecretAsync(KeySecretName, CancellationToken.None))
            .ReturnsAsync(_testPrivateKeyPem);
        _mockSecretManager
            .Setup(x => x.GetSecretAsync(VendorSecretName, CancellationToken.None))
            .ReturnsAsync(_testPrivateKeyPem);
        _mockSecretManager
            .Setup(x => x.GetSecretAsync(ClientIdSecretName, CancellationToken.None))
            .ReturnsAsync(ResolvedClientId);

        _mockTenantApiService = new Mock<ITenantApiService>();
        _mockTenantApiService
            .Setup(x => x.GetVendorSigningKeySecretId(FacilityId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);
    }

    private EpicAuth BuildSut(
        HttpMessageHandler handler,
        PemKeySource keySource = PemKeySource.SecretManager) =>
        new(
            new HttpClient(handler),
            _mockLogger.Object,
            _mockCacheService.Object,
            _mockSecretManager.Object,
            Options.Create(new DataSourceAuthSettings { KeySource = keySource }),
            _mockTenantApiService.Object);

    private AuthenticationConfigurationModel BuildAuthSettings(
        string? clientId = ClientIdSecretName,
        string? tokenUrl = TokenUrl,
        string? key = null) => new()
        {
            AuthType = "Epic",
            ClientId = clientId,
            TokenUrl = tokenUrl,
            Key = key
        };

    private static string BuildTokenResponse(string accessToken = "epic-access-token", int expiresIn = 3600) =>
        JsonSerializer.Serialize(new { access_token = accessToken, expires_in = expiresIn });

    private static HttpResponseMessage OkJsonResponse(string body) =>
        new(HttpStatusCode.OK)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        };

    [Fact]
    public async Task SetAuthentication_ReturnsCachedToken_WhenTokenExistsInCache()
    {
        const string cachedToken = "cached-token";
        _mockCacheService.Setup(x => x.GetAsync<string>(FacilityId, It.IsAny<CancellationToken>())).ReturnsAsync(cachedToken);

        var sut = BuildSut(new MockHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)));

        var (isQueryParam, authHeaderValue) = await sut.SetAuthentication(FacilityId, BuildAuthSettings());

        Assert.False(isQueryParam);
        var header = Assert.IsType<AuthenticationHeaderValue>(authHeaderValue);
        Assert.Equal("Bearer", header.Scheme);
        Assert.Equal(cachedToken, header.Parameter);
        _mockSecretManager.Verify(
            x => x.GetSecretAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task SetAuthentication_FetchesAndCachesNewToken_WhenNoCachedToken()
    {
        var responseBody = BuildTokenResponse("new-token", expiresIn: 1800);
        var sut = BuildSut(new MockHttpMessageHandler(_ => OkJsonResponse(responseBody)));

        var (isQueryParam, authHeaderValue) = await sut.SetAuthentication(FacilityId, BuildAuthSettings());

        Assert.False(isQueryParam);
        var header = Assert.IsType<AuthenticationHeaderValue>(authHeaderValue);
        Assert.Equal(DataAcquisitionConstants.Auth.Bearer, header.Scheme);
        Assert.Equal("new-token", header.Parameter);
        _mockCacheService.Verify(
            x => x.SetAsync(FacilityId, "new-token", TimeSpan.FromSeconds(1800), ExpirationType.Absolute, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task SetAuthentication_ResolvesKeyAndClientId_FromSecretManager()
    {
        var sut = BuildSut(new MockHttpMessageHandler(_ => OkJsonResponse(BuildTokenResponse())));

        await sut.SetAuthentication(FacilityId, BuildAuthSettings());

        _mockSecretManager.Verify(x => x.GetSecretAsync(KeySecretName, CancellationToken.None), Times.Once);
        _mockSecretManager.Verify(x => x.GetSecretAsync(ClientIdSecretName, CancellationToken.None), Times.Once);
    }

    [Fact]
    public async Task SetAuthentication_SendsSignedJwtAsClientAssertion()
    {
        HttpRequestMessage? captured = null;
        var sut = BuildSut(new MockHttpMessageHandler(req =>
        {
            captured = req;
            return OkJsonResponse(BuildTokenResponse());
        }));

        await sut.SetAuthentication(FacilityId, BuildAuthSettings());

        Assert.NotNull(captured);
        Assert.Equal(TokenUrl, captured!.RequestUri!.ToString());
        var formContent = await captured.Content!.ReadAsStringAsync();
        Assert.Contains("grant_type=client_credentials", formContent);
        Assert.Contains("client_assertion_type=urn:ietf:params:oauth:client-assertion-type:jwt-bearer", formContent);

        var jwt = ExtractFormValue(formContent, "client_assertion");
        Assert.False(string.IsNullOrEmpty(jwt));

        var handler = new JsonWebTokenHandler();
        var validationResult = await handler.ValidateTokenAsync(jwt!, new TokenValidationParameters
        {
            IssuerSigningKey = new RsaSecurityKey(_testPublicKey),
            ValidIssuer = ResolvedClientId,
            ValidAudience = TokenUrl,
            ValidateLifetime = true,
            ValidAlgorithms = [SecurityAlgorithms.RsaSha256]
        });

        Assert.True(validationResult.IsValid, validationResult.Exception?.Message);
        var token = (JsonWebToken)validationResult.SecurityToken;
        Assert.Equal(ResolvedClientId, token.Subject);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task SetAuthentication_FallsBackToPemSuffix_WhenVendorSigningKeySecretIdIsNullOrWhitespace(string? vendorSecretName)
    {
        _mockTenantApiService
            .Setup(x => x.GetVendorSigningKeySecretId(FacilityId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(vendorSecretName);
        var sut = BuildSut(new MockHttpMessageHandler(_ => OkJsonResponse(BuildTokenResponse())));

        await sut.SetAuthentication(FacilityId, BuildAuthSettings());

        _mockSecretManager.Verify(
            x => x.GetSecretAsync($"{FacilityId}-pem", CancellationToken.None), Times.Once);
    }

    [Fact]
    public async Task SetAuthentication_UsesVendorSigningKeySecretId_WhenTenantServiceProvidesOne()
    {
        _mockTenantApiService
            .Setup(x => x.GetVendorSigningKeySecretId(FacilityId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(VendorSecretName);
        var sut = BuildSut(new MockHttpMessageHandler(_ => OkJsonResponse(BuildTokenResponse())));

        await sut.SetAuthentication(FacilityId, BuildAuthSettings());

        _mockSecretManager.Verify(
            x => x.GetSecretAsync(VendorSecretName, CancellationToken.None), Times.Once);
        _mockSecretManager.Verify(
            x => x.GetSecretAsync(KeySecretName, It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SetAuthentication_PropagatesException_WhenTenantApiServiceThrows()
    {
        _mockTenantApiService
            .Setup(x => x.GetVendorSigningKeySecretId(FacilityId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Tenant service unavailable."));
        var sut = BuildSut(new MockHttpMessageHandler(_ => OkJsonResponse(BuildTokenResponse())));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            sut.SetAuthentication(FacilityId, BuildAuthSettings()));

        _mockSecretManager.Verify(
            x => x.GetSecretAsync(KeySecretName, It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SetAuthentication_DoesNotQueryTenantService_WhenKeySourceIsDatabase()
    {
        var sut = BuildSut(
            new MockHttpMessageHandler(_ => OkJsonResponse(BuildTokenResponse())),
            PemKeySource.Database);

        await sut.SetAuthentication(FacilityId, BuildAuthSettings(key: _testPrivateKeyPem));

        _mockTenantApiService.Verify(
            x => x.GetVendorSigningKeySecretId(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task SetAuthentication_ThrowsArgumentException_WhenFacilityIdIsNullOrWhitespace(string? facilityId)
    {
        var sut = BuildSut(new MockHttpMessageHandler(_ => OkJsonResponse(BuildTokenResponse())));

        await Assert.ThrowsAsync<ArgumentException>(() =>
            sut.SetAuthentication(facilityId!, BuildAuthSettings()));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task SetAuthentication_ThrowsArgumentException_WhenClientIdIsNullOrWhitespace(string? clientId)
    {
        var sut = BuildSut(new MockHttpMessageHandler(_ => OkJsonResponse(BuildTokenResponse())));

        await Assert.ThrowsAsync<ArgumentException>(() =>
            sut.SetAuthentication(FacilityId, BuildAuthSettings(clientId: clientId)));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task SetAuthentication_ThrowsInvalidOperationException_WhenSecretManagerReturnsNullOrWhitespaceForKey(string? resolvedKey)
    {
        _mockSecretManager
            .Setup(x => x.GetSecretAsync(KeySecretName, CancellationToken.None))
            .ReturnsAsync(resolvedKey);
        var sut = BuildSut(new MockHttpMessageHandler(_ => OkJsonResponse(BuildTokenResponse())));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            sut.SetAuthentication(FacilityId, BuildAuthSettings()));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task SetAuthentication_ThrowsInvalidOperationException_WhenSecretManagerReturnsNullOrWhitespaceForClientId(string? resolvedClientId)
    {
        _mockSecretManager
            .Setup(x => x.GetSecretAsync(ClientIdSecretName, CancellationToken.None))
            .ReturnsAsync(resolvedClientId);
        var sut = BuildSut(new MockHttpMessageHandler(_ => OkJsonResponse(BuildTokenResponse())));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            sut.SetAuthentication(FacilityId, BuildAuthSettings()));
    }

    [Fact]
    public async Task SetAuthentication_PropagatesException_WhenSecretManagerThrows()
    {
        _mockSecretManager
            .Setup(x => x.GetSecretAsync(KeySecretName, CancellationToken.None))
            .ThrowsAsync(new InvalidOperationException("Secret store unavailable."));
        var sut = BuildSut(new MockHttpMessageHandler(_ => OkJsonResponse(BuildTokenResponse())));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            sut.SetAuthentication(FacilityId, BuildAuthSettings()));
    }

    [Fact]
    public async Task SetAuthentication_ReturnsNull_AndLogsError_WhenHttpClientThrows()
    {
        var sut = BuildSut(new MockHttpMessageHandler(_ => throw new HttpRequestException("Connection refused")));

        var (isQueryParam, authHeaderValue) = await sut.SetAuthentication(FacilityId, BuildAuthSettings());

        Assert.False(isQueryParam);
        Assert.Null(authHeaderValue);
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task SetAuthentication_UsesAuthSettingsKey_WhenKeySourceIsDatabase()
    {
        var sut = BuildSut(
            new MockHttpMessageHandler(_ => OkJsonResponse(BuildTokenResponse())),
            PemKeySource.Database);

        await sut.SetAuthentication(
            FacilityId,
            BuildAuthSettings(key: _testPrivateKeyPem));

        _mockSecretManager.Verify(
            x => x.GetSecretAsync(KeySecretName, It.IsAny<CancellationToken>()),
            Times.Never);
        _mockSecretManager.Verify(
            x => x.GetSecretAsync(ClientIdSecretName, CancellationToken.None),
            Times.Once);
    }

    [Fact]
    public async Task SetAuthentication_SignsJwtWithDatabaseKey_WhenKeySourceIsDatabase()
    {
        HttpRequestMessage? captured = null;
        var sut = BuildSut(
            new MockHttpMessageHandler(req =>
            {
                captured = req;
                return OkJsonResponse(BuildTokenResponse());
            }),
            PemKeySource.Database);

        await sut.SetAuthentication(
            FacilityId,
            BuildAuthSettings(key: _testPrivateKeyPem));

        Assert.NotNull(captured);
        var formContent = await captured!.Content!.ReadAsStringAsync();
        var jwt = ExtractFormValue(formContent, "client_assertion");
        Assert.False(string.IsNullOrEmpty(jwt));

        var handler = new JsonWebTokenHandler();
        var validationResult = await handler.ValidateTokenAsync(jwt!, new TokenValidationParameters
        {
            IssuerSigningKey = new RsaSecurityKey(_testPublicKey),
            ValidIssuer = ResolvedClientId,
            ValidAudience = TokenUrl,
            ValidateLifetime = true,
            ValidAlgorithms = [SecurityAlgorithms.RsaSha256]
        });

        Assert.True(validationResult.IsValid, validationResult.Exception?.Message);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task SetAuthentication_ThrowsInvalidOperationException_WhenKeySourceIsDatabaseAndKeyIsNullOrWhitespace(string? key)
    {
        var sut = BuildSut(
            new MockHttpMessageHandler(_ => OkJsonResponse(BuildTokenResponse())),
            PemKeySource.Database);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            sut.SetAuthentication(FacilityId, BuildAuthSettings(key: key)));

        _mockSecretManager.Verify(
            x => x.GetSecretAsync(KeySecretName, It.IsAny<CancellationToken>()),
            Times.Never);
    }

    private static string? ExtractFormValue(string formContent, string key)
    {
        foreach (var pair in formContent.Split('&'))
        {
            var idx = pair.IndexOf('=');
            if (idx < 0) continue;
            if (pair[..idx] == key)
            {
                return Uri.UnescapeDataString(pair[(idx + 1)..]);
            }
        }
        return null;
    }
}
