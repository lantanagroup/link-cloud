using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Api.Configuration;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Services;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Services.Sftp;
using Microsoft.Extensions.Logging;
using Moq;
using Task = System.Threading.Tasks.Task;

namespace UnitTests.DataAcquisition.Services.Sftp;

[Trait("Category", "UnitTests")]
public class SftpClientServiceTests
{
    private readonly Mock<ISftpCredentialService> _credentialServiceMock = new();
    private readonly SftpClientService _service;

    public SftpClientServiceTests()
    {
        _service = new SftpClientService(
            new Mock<ILogger<SftpClientService>>().Object,
            _credentialServiceMock.Object);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task OpenSessionAsync_SavedConfigurationWithoutUsableCredentials_ThrowsBeforeConnecting(bool credentialsStored)
    {
        // The host is unresolvable, so reaching the connect step would throw a SocketException instead
        var credentials = credentialsStored
            ? new SftpCredentialsModel { Username = "", Password = "password" }
            : null;
        _credentialServiceMock
            .Setup(c => c.GetCredentialsAsync("TestFacility", It.IsAny<CancellationToken>()))
            .ReturnsAsync(credentials);

        var config = new SftpConfigurationModel
        {
            OrganizationId = "TestFacility",
            Host = "sftp.invalid",
            Port = 22
        };

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.OpenSessionAsync(config, CancellationToken.None));
    }

    [Fact]
    public async Task OpenSessionAsync_AdHocWithoutCredentials_ThrowsWithoutReadingCredentialStore()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => _service.OpenSessionAsync("sftp.invalid", 22, null!, TimeSpan.FromSeconds(1), CancellationToken.None));

        _credentialServiceMock.VerifyNoOtherCalls();
    }
}
