using LantanaGroup.Link.DataAcquisition.Controllers;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Exceptions;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Results;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Services;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Moq.AutoMock;
using System.Net;
using Task = System.Threading.Tasks.Task;

namespace UnitTests.DataAcquisition.Controllers;

[Trait("Category", "UnitTests")]
public class ConnectionValidationControllerTests
{
    private const string FhirServerUrl = "http://fhir.test/r4";

    [Fact]
    public async Task ValidateFhirServerConnection_Connected_ReturnsOkWithResult()
    {
        var mocker = new AutoMocker();
        mocker.GetMock<IValidateFhirServerConnectionService>()
            .Setup(x => x.ValidateConnection(It.IsAny<ValidateFhirServerConnectionRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FhirServerConnectionResult(true));

        var controller = mocker.CreateInstance<ConnectionValidationController>();

        var result = await controller.ValidateFhirServerConnection(FhirServerUrl, CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var value = Assert.IsType<FhirServerConnectionResult>(okResult.Value);
        Assert.True(value.IsConnected);
        Assert.Null(value.ErrorMessage);
    }

    [Fact]
    public async Task ValidateFhirServerConnection_ReachableButNotFhir_ReturnsOkNotAnError()
    {
        var mocker = new AutoMocker();
        mocker.GetMock<IValidateFhirServerConnectionService>()
            .Setup(x => x.ValidateConnection(It.IsAny<ValidateFhirServerConnectionRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FhirServerConnectionResult(false, "Returned an OperationOutcome rather than a CapabilityStatement."));

        var controller = mocker.CreateInstance<ConnectionValidationController>();

        var result = await controller.ValidateFhirServerConnection(FhirServerUrl, CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var value = Assert.IsType<FhirServerConnectionResult>(okResult.Value);
        Assert.False(value.IsConnected);
        Assert.NotNull(value.ErrorMessage);
    }

    [Fact]
    public async Task ValidateFhirServerConnection_PassesSuppliedUrlToService()
    {
        var mocker = new AutoMocker();
        ValidateFhirServerConnectionRequest? captured = null;
        mocker.GetMock<IValidateFhirServerConnectionService>()
            .Setup(x => x.ValidateConnection(It.IsAny<ValidateFhirServerConnectionRequest>(), It.IsAny<CancellationToken>()))
            .Callback<ValidateFhirServerConnectionRequest, CancellationToken>((req, _) => captured = req)
            .ReturnsAsync(new FhirServerConnectionResult(true));

        var controller = mocker.CreateInstance<ConnectionValidationController>();

        await controller.ValidateFhirServerConnection(FhirServerUrl, CancellationToken.None);

        Assert.NotNull(captured);
        Assert.Equal(FhirServerUrl, captured!.FhirServerUrl);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not-a-url")]
    [InlineData("/relative/path")]
    [InlineData("file:///etc/passwd")]
    [InlineData("ftp://fhir.test/r4")]
    [InlineData("http://fhir.test/r4?foo=bar")]
    [InlineData("http://fhir.test/r4#fragment")]
    public async Task ValidateFhirServerConnection_InvalidUrl_ReturnsBadRequest(string fhirServerUrl)
    {
        var mocker = new AutoMocker();
        var controller = mocker.CreateInstance<ConnectionValidationController>();

        var result = await controller.ValidateFhirServerConnection(fhirServerUrl, CancellationToken.None);

        var objectResult = Assert.IsAssignableFrom<ObjectResult>(result.Result);
        Assert.Equal((int)HttpStatusCode.BadRequest, objectResult.StatusCode);

        mocker.GetMock<IValidateFhirServerConnectionService>()
            .Verify(x => x.ValidateConnection(It.IsAny<ValidateFhirServerConnectionRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ValidateFhirServerConnection_ConnectionFailed_ReturnsBadGateway()
    {
        var mocker = new AutoMocker();
        mocker.GetMock<IValidateFhirServerConnectionService>()
            .Setup(x => x.ValidateConnection(It.IsAny<ValidateFhirServerConnectionRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new FhirConnectionFailedException("Unable to connect to the FHIR server."));

        var controller = mocker.CreateInstance<ConnectionValidationController>();

        var result = await controller.ValidateFhirServerConnection(FhirServerUrl, CancellationToken.None);

        var objectResult = Assert.IsAssignableFrom<ObjectResult>(result.Result);
        Assert.Equal((int)HttpStatusCode.BadGateway, objectResult.StatusCode);
    }

    [Fact]
    public async Task ValidateFhirServerConnection_BadRequestExceptionFromService_ReturnsBadRequest()
    {
        var mocker = new AutoMocker();
        mocker.GetMock<IValidateFhirServerConnectionService>()
            .Setup(x => x.ValidateConnection(It.IsAny<ValidateFhirServerConnectionRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new BadRequestException("No FHIR server URL was provided. One is required to validate."));

        var controller = mocker.CreateInstance<ConnectionValidationController>();

        var result = await controller.ValidateFhirServerConnection(FhirServerUrl, CancellationToken.None);

        var objectResult = Assert.IsAssignableFrom<ObjectResult>(result.Result);
        Assert.Equal((int)HttpStatusCode.BadRequest, objectResult.StatusCode);
    }

    [Fact]
    public async Task ValidateFhirServerConnection_UnexpectedException_ReturnsInternalServerError()
    {
        var mocker = new AutoMocker();
        mocker.GetMock<IValidateFhirServerConnectionService>()
            .Setup(x => x.ValidateConnection(It.IsAny<ValidateFhirServerConnectionRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("boom"));

        var controller = mocker.CreateInstance<ConnectionValidationController>();

        var result = await controller.ValidateFhirServerConnection(FhirServerUrl, CancellationToken.None);

        var objectResult = Assert.IsAssignableFrom<ObjectResult>(result.Result);
        Assert.Equal((int)HttpStatusCode.InternalServerError, objectResult.StatusCode);
    }
}
