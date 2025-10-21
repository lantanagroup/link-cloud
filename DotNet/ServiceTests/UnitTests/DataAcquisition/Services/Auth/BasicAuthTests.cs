using DataAcquisition.Domain.Application.Models;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Services.Auth;
using LantanaGroup.Link.Shared.Application.Interfaces.Services;
using Moq;
using System.Net.Http.Headers;
using System.Text;
using Task = System.Threading.Tasks.Task;

namespace UnitTests.DataAcquisition.Auth;

[Trait("Category", "UnitTests")]
public class BasicAuthTests
{
    private const string FacilityId = "test-facility";
    private const string UserNameSecretKey = "secret/username";
    private const string PasswordSecretKey = "secret/password";
    private const string ResolvedUserName = "testuser";
    private const string ResolvedPassword = "testpass";

    private readonly Mock<ISecretManager> _mockSecretManager;

    public BasicAuthTests()
    {
        _mockSecretManager = new Mock<ISecretManager>();
        _mockSecretManager
            .Setup(x => x.GetSecretAsync(UserNameSecretKey, CancellationToken.None))
            .ReturnsAsync(ResolvedUserName);
        _mockSecretManager
            .Setup(x => x.GetSecretAsync(PasswordSecretKey, CancellationToken.None))
            .ReturnsAsync(ResolvedPassword);
    }

    private BasicAuth BuildSut() => new(_mockSecretManager.Object);

    private static AuthenticationConfigurationModel BuildAuthSettings(
        string? userName = UserNameSecretKey,
        string? password = PasswordSecretKey) => new()
        {
            AuthType = "Basic",
            UserName = userName,
            Password = password
        };

    [Fact]
    public async Task SetAuthentication_ReturnsIsQueryParamFalse_WhenCredentialsAreValid()
    {
        var sut = BuildSut();

        var (isQueryParam, _) = await sut.SetAuthentication(FacilityId, BuildAuthSettings());

        Assert.False(isQueryParam);
    }

    [Fact]
    public async Task SetAuthentication_ReturnsBasicScheme_WhenCredentialsAreValid()
    {
        var sut = BuildSut();

        var (_, authHeaderValue) = await sut.SetAuthentication(FacilityId, BuildAuthSettings());

        var header = Assert.IsType<AuthenticationHeaderValue>(authHeaderValue);
        Assert.Equal("basic", header.Scheme);
    }

    [Fact]
    public async Task SetAuthentication_ReturnsBase64EncodedCredentials_WhenCredentialsAreValid()
    {
        var sut = BuildSut();
        var expectedParameter = Convert.ToBase64String(
            Encoding.UTF8.GetBytes($"{ResolvedUserName}:{ResolvedPassword}".ToCharArray()));

        var (_, authHeaderValue) = await sut.SetAuthentication(FacilityId, BuildAuthSettings());

        var header = Assert.IsType<AuthenticationHeaderValue>(authHeaderValue);
        Assert.Equal(expectedParameter, header.Parameter);
    }

    [Fact]
    public async Task SetAuthentication_ResolvesCredentials_FromSecretManager()
    {
        var sut = BuildSut();

        await sut.SetAuthentication(FacilityId, BuildAuthSettings());

        _mockSecretManager.Verify(x => x.GetSecretAsync(UserNameSecretKey, CancellationToken.None), Times.Once);
        _mockSecretManager.Verify(x => x.GetSecretAsync(PasswordSecretKey, CancellationToken.None), Times.Once);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task SetAuthentication_ThrowsArgumentException_WhenUserNameIsNullOrWhitespace(string? userName)
    {
        var sut = BuildSut();

        await Assert.ThrowsAsync<ArgumentException>(() =>
            sut.SetAuthentication(FacilityId, BuildAuthSettings(userName: userName)));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task SetAuthentication_ThrowsArgumentException_WhenPasswordIsNullOrWhitespace(string? password)
    {
        var sut = BuildSut();

        await Assert.ThrowsAsync<ArgumentException>(() =>
            sut.SetAuthentication(FacilityId, BuildAuthSettings(password: password)));
    }

    [Fact]
    public async Task SetAuthentication_PropagatesException_WhenSecretManagerThrows()
    {
        _mockSecretManager
            .Setup(x => x.GetSecretAsync(UserNameSecretKey, CancellationToken.None))
            .ThrowsAsync(new InvalidOperationException("Secret store unavailable."));
        var sut = BuildSut();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            sut.SetAuthentication(FacilityId, BuildAuthSettings()));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task SetAuthentication_ThrowsInvalidOperationException_WhenSecretManagerReturnsNullOrWhitespaceForUserName(string? resolvedUserName)
    {
        _mockSecretManager
            .Setup(x => x.GetSecretAsync(UserNameSecretKey, CancellationToken.None))
            .ReturnsAsync(resolvedUserName);
        var sut = BuildSut();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            sut.SetAuthentication(FacilityId, BuildAuthSettings()));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task SetAuthentication_ThrowsInvalidOperationException_WhenSecretManagerReturnsNullOrWhitespaceForPassword(string? resolvedPassword)
    {
        _mockSecretManager
            .Setup(x => x.GetSecretAsync(PasswordSecretKey, CancellationToken.None))
            .ReturnsAsync(resolvedPassword);
        var sut = BuildSut();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            sut.SetAuthentication(FacilityId, BuildAuthSettings()));
    }
}
