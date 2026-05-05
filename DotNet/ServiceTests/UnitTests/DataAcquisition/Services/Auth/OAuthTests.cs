using DataAcquisition.Domain.Application.Models;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Services.Auth;
using LantanaGroup.Link.DataAcquisition.Domain.Settings;
using LantanaGroup.Link.Shared.Application.Interfaces;
using LantanaGroup.Link.Shared.Application.Interfaces.Services;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using Microsoft.Extensions.Logging;
using Moq;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using UnitTests.Admin.BFF.Aggregation;
using Task = System.Threading.Tasks.Task;

namespace UnitTests.DataAcquisition.Auth;

[Trait("Category", "UnitTests")]
public class OAuthTests
{
    private const string FacilityId = "test-facility";
    private const string ClientId = "test-client-id";
    private const string ClientSecret = "test-client-secret";
    private const string TokenUrl = "https://auth.example.com/token";

    private readonly Mock<ILogger<OAuth>> _mockLogger;
    private readonly Mock<ICacheService> _mockCacheService;
    private readonly Mock<ISecretManager> _mockSecretManager;

    public OAuthTests()
    {
        _mockLogger = new Mock<ILogger<OAuth>>();
        _mockCacheService = new Mock<ICacheService>();
        _mockSecretManager = new Mock<ISecretManager>();
        _mockSecretManager
            .Setup(x => x.GetSecretAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string name, CancellationToken _) => name);
    }

    private OAuth BuildSut(HttpMessageHandler handler) =>
        new(new HttpClient(handler), _mockLogger.Object, _mockCacheService.Object, _mockSecretManager.Object);

    private static AuthenticationConfigurationModel BuildAuthSettings(string? scope = null) => new()
    {
        ClientId = ClientId,
        ClientSecret = ClientSecret,
        TokenUrl = TokenUrl,
        Scope = scope
    };

    private static string BuildTokenResponse(string accessToken = "test-access-token", int expiresIn = 3600) =>
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
        _mockCacheService.Setup(x => x.Get<string>(FacilityId)).Returns(cachedToken);

        var sut = BuildSut(new MockHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)));

        var (isQueryParam, authHeaderValue) = await sut.SetAuthentication(FacilityId, BuildAuthSettings());

        Assert.False(isQueryParam);
        var header = Assert.IsType<AuthenticationHeaderValue>(authHeaderValue);
        Assert.Equal(DataAcquisitionConstants.Auth.Bearer, header.Scheme);
        Assert.Equal(cachedToken, header.Parameter);
        _mockCacheService.Verify(
            x => x.Set(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<ExpirationType>()),
            Times.Never);
    }

    [Fact]
    public async Task SetAuthentication_FetchesAndCachesNewToken_WhenNoCachedToken()
    {
        _mockCacheService.Setup(x => x.Get<string>(FacilityId)).Returns((string)null!);

        var responseBody = BuildTokenResponse("new-token", expiresIn: 1800);
        var sut = BuildSut(new MockHttpMessageHandler(_ => OkJsonResponse(responseBody)));

        var (isQueryParam, authHeaderValue) = await sut.SetAuthentication(FacilityId, BuildAuthSettings());

        Assert.False(isQueryParam);
        var header = Assert.IsType<AuthenticationHeaderValue>(authHeaderValue);
        Assert.Equal(DataAcquisitionConstants.Auth.Bearer, header.Scheme);
        Assert.Equal("new-token", header.Parameter);
        _mockCacheService.Verify(
            x => x.Set(FacilityId, "new-token", TimeSpan.FromSeconds(1800), ExpirationType.Absolute),
            Times.Once);
    }

    [Fact]
    public async Task SetAuthentication_IncludesScopeInRequest_WhenScopeIsProvided()
    {
        _mockCacheService.Setup(x => x.Get<string>(FacilityId)).Returns((string)null!);

        HttpRequestMessage? captured = null;
        var sut = BuildSut(new MockHttpMessageHandler(req =>
        {
            captured = req;
            return OkJsonResponse(BuildTokenResponse());
        }));

        await sut.SetAuthentication(FacilityId, BuildAuthSettings(scope: "system/*.read"));

        Assert.NotNull(captured);
        var formContent = await captured!.Content!.ReadAsStringAsync();
        Assert.Contains("grant_type=client_credentials", formContent);
        Assert.Contains("scope=", formContent);
    }

    [Fact]
    public async Task SetAuthentication_OmitsScopeFromRequest_WhenScopeIsNotProvided()
    {
        _mockCacheService.Setup(x => x.Get<string>(FacilityId)).Returns((string)null!);

        HttpRequestMessage? captured = null;
        var sut = BuildSut(new MockHttpMessageHandler(req =>
        {
            captured = req;
            return OkJsonResponse(BuildTokenResponse());
        }));

        await sut.SetAuthentication(FacilityId, BuildAuthSettings(scope: null));

        Assert.NotNull(captured);
        var formContent = await captured!.Content!.ReadAsStringAsync();
        Assert.Contains("grant_type=client_credentials", formContent);
        Assert.DoesNotContain("scope=", formContent);
    }

    [Fact]
    public async Task SetAuthentication_SendsCorrectBasicAuthorizationHeader()
    {
        _mockCacheService.Setup(x => x.Get<string>(FacilityId)).Returns((string)null!);

        HttpRequestMessage? captured = null;
        var sut = BuildSut(new MockHttpMessageHandler(req =>
        {
            captured = req;
            return OkJsonResponse(BuildTokenResponse());
        }));

        await sut.SetAuthentication(FacilityId, BuildAuthSettings());

        Assert.NotNull(captured?.Headers.Authorization);
        Assert.Equal("Basic", captured!.Headers.Authorization!.Scheme);

        var expectedCredentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{ClientId}:{ClientSecret}"));
        Assert.Equal(expectedCredentials, captured.Headers.Authorization.Parameter);
    }

    [Fact]
    public async Task SetAuthentication_ReturnsNull_WhenAccessTokenIsEmptyInResponse()
    {
        _mockCacheService.Setup(x => x.Get<string>(FacilityId)).Returns((string)null!);

        var responseBody = BuildTokenResponse(accessToken: "");
        var sut = BuildSut(new MockHttpMessageHandler(_ => OkJsonResponse(responseBody)));

        var (isQueryParam, authHeaderValue) = await sut.SetAuthentication(FacilityId, BuildAuthSettings());

        Assert.False(isQueryParam);
        Assert.Null(authHeaderValue);
        _mockCacheService.Verify(
            x => x.Set(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<ExpirationType>()),
            Times.Never);
    }

    [Fact]
    public async Task SetAuthentication_ReturnsNull_AndLogsError_WhenHttpClientThrows()
    {
        _mockCacheService.Setup(x => x.Get<string>(FacilityId)).Returns((string)null!);

        var sut = BuildSut(new MockHttpMessageHandler(_ => throw new HttpRequestException("Connection refused")));

        var (isQueryParam, authHeaderValue) = await sut.SetAuthentication(FacilityId, BuildAuthSettings());

        Assert.False(isQueryParam);
        Assert.Null(authHeaderValue);
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, _) => v.ToString()!.Contains(FacilityId)),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }
}