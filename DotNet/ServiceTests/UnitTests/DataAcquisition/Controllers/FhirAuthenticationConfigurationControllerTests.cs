using Azure;
using LantanaGroup.Link.DataAcquisition.Controllers;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Api.Configuration;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Exceptions;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Services.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Task = System.Threading.Tasks.Task;

namespace UnitTests.DataAcquisition.Controllers;

[Trait("Category", "UnitTests")]
public class FhirAuthenticationConfigurationControllerTests
{
    private const string TraceId = "test-trace-id";
    private const string FacilityId = "test-facility";
    private const string SecretDetail = "vault-name-and-secret-must-not-leak";

    private static readonly IServiceProvider MvcServices = BuildMvcServices();

    private readonly Mock<IFhirAuthenticationConfigurationService> _serviceMock = new();
    private readonly FhirAuthenticationConfigurationController _controller;

    public FhirAuthenticationConfigurationControllerTests()
    {
        _controller = new FhirAuthenticationConfigurationController(
            _serviceMock.Object,
            new Mock<ILogger<FhirAuthenticationConfigurationController>>().Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { RequestServices = MvcServices, TraceIdentifier = TraceId }
            },
            ProblemDetailsFactory = MvcServices.GetRequiredService<ProblemDetailsFactory>()
        };
    }

    private static IServiceProvider BuildMvcServices()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddControllers();
        return services.BuildServiceProvider();
    }

    private static FhirAuthenticationConfigurationRequest CreateRequest() => new()
    {
        TokenUrl = "https://vendor.test/oauth2/token",
        ClientId = "link-client",
        ClientSecret = "s3cret",
        Scope = "system/*.read"
    };

    private static FhirAuthenticationConfigurationResponse CreateResponse() => new()
    {
        TokenUrl = "https://vendor.test/oauth2/token",
        ClientId = "link-client",
        Scope = "system/*.read",
        ClientSecretStored = true
    };

    private static ProblemDetails AssertProblem(IActionResult response, int expectedStatusCode)
    {
        var result = Assert.IsType<ObjectResult>(response);
        Assert.Equal(expectedStatusCode, result.StatusCode);
        var problem = Assert.IsType<ProblemDetails>(result.Value);
        Assert.Equal(expectedStatusCode, problem.Status);
        return problem;
    }

    // ---- GET ----

    [Fact]
    public async Task GetFhirAuthenticationConfiguration_ConfigurationExists_ReturnsOkWithTheConfiguration()
    {
        var expected = CreateResponse();
        _serviceMock
            .Setup(s => s.GetAsync(FacilityId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var response = await _controller.GetFhirAuthenticationConfiguration(FacilityId, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(response);
        Assert.Same(expected, ok.Value);
    }

    [Fact]
    public async Task GetFhirAuthenticationConfiguration_ForwardsTheCancellationToken()
    {
        using var cts = new CancellationTokenSource();
        _serviceMock
            .Setup(s => s.GetAsync(FacilityId, cts.Token))
            .ReturnsAsync(CreateResponse());

        await _controller.GetFhirAuthenticationConfiguration(FacilityId, cts.Token);

        _serviceMock.Verify(s => s.GetAsync(FacilityId, cts.Token), Times.Once);
    }

    [Fact]
    public async Task GetFhirAuthenticationConfiguration_NoConfiguration_ReturnsNotFound()
    {
        _serviceMock
            .Setup(s => s.GetAsync(FacilityId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((FhirAuthenticationConfigurationResponse?)null);

        var response = await _controller.GetFhirAuthenticationConfiguration(FacilityId, CancellationToken.None);

        var problem = AssertProblem(response, StatusCodes.Status404NotFound);
        Assert.Equal("Not Found", problem.Title);
    }

    /// <summary>
    /// The 404 detail is fixed text, so a caller cannot have its own input reflected back to it.
    /// </summary>
    [Fact]
    public async Task GetFhirAuthenticationConfiguration_NoConfiguration_DoesNotEchoTheFacilityId()
    {
        _serviceMock
            .Setup(s => s.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((FhirAuthenticationConfigurationResponse?)null);

        var response = await _controller.GetFhirAuthenticationConfiguration("reflect-me", CancellationToken.None);

        var problem = AssertProblem(response, StatusCodes.Status404NotFound);
        Assert.DoesNotContain("reflect-me", problem.Detail);
    }

    [Fact]
    public async Task GetFhirAuthenticationConfiguration_ServiceRejectsTheRequest_ReturnsBadRequest()
    {
        _serviceMock
            .Setup(s => s.GetAsync(FacilityId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new BadRequestException("FacilityId is required."));

        var response = await _controller.GetFhirAuthenticationConfiguration(FacilityId, CancellationToken.None);

        AssertProblem(response, StatusCodes.Status400BadRequest);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("<script>alert('x')</script>")]
    public async Task GetFhirAuthenticationConfiguration_FacilityIdSanitizesAwayToNothing_ReturnsBadRequest(
        string facilityId)
    {
        var response = await _controller.GetFhirAuthenticationConfiguration(facilityId, CancellationToken.None);

        AssertProblem(response, StatusCodes.Status400BadRequest);
        _serviceMock.Verify(s => s.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>
    /// Sanitising strips characters rather than failing, so "fac!ility@1" would become "facility1".
    /// On an endpoint that reads and overwrites credentials that is an identity change: the caller
    /// would be served a different facility than the one they named. It has to be a 400.
    /// </summary>
    [Theory]
    [InlineData("fac!ility@1")]
    [InlineData("facility/1")]
    [InlineData("facility 1<b>")]
    public async Task GetFhirAuthenticationConfiguration_FacilityIdThatSanitisingWouldChange_ReturnsBadRequest(
        string facilityId)
    {
        var response = await _controller.GetFhirAuthenticationConfiguration(facilityId, CancellationToken.None);

        AssertProblem(response, StatusCodes.Status400BadRequest);
        _serviceMock.Verify(s => s.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CreateOrUpdateFhirAuthenticationConfiguration_FacilityIdThatSanitisingWouldChange_ReturnsBadRequest()
    {
        var response = await _controller.CreateOrUpdateFhirAuthenticationConfiguration("fac!ility@1",
                                                                                       CreateRequest(),
                                                                                       CancellationToken.None);

        AssertProblem(response, StatusCodes.Status400BadRequest);
        _serviceMock.Verify(s => s.CreateOrUpdateAsync(It.IsAny<string>(),
                                                       It.IsAny<FhirAuthenticationConfigurationRequest>(),
                                                       It.IsAny<CancellationToken>()),
                            Times.Never);
    }

    /// <summary>
    /// A facility id that sanitising leaves alone is passed through untouched.
    /// </summary>
    [Fact]
    public async Task GetFhirAuthenticationConfiguration_AcceptableFacilityId_ReachesTheServiceUnchanged()
    {
        _serviceMock
            .Setup(s => s.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateResponse());

        await _controller.GetFhirAuthenticationConfiguration("Facility_01.a-B", CancellationToken.None);

        _serviceMock.Verify(s => s.GetAsync("Facility_01.a-B", It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// A cancelled request has no caller left to answer, so it must not be turned into a 500.
    /// </summary>
    [Fact]
    public async Task GetFhirAuthenticationConfiguration_RequestCancelled_RethrowsRatherThanReturningAProblem()
    {
        _serviceMock
            .Setup(s => s.GetAsync(FacilityId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new TaskCanceledException());

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => _controller.GetFhirAuthenticationConfiguration(FacilityId, CancellationToken.None));
    }

    [Fact]
    public async Task GetFhirAuthenticationConfiguration_SecretManagerFails_ReturnsServiceUnavailable()
    {
        _serviceMock
            .Setup(s => s.GetAsync(FacilityId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new RequestFailedException(403, SecretDetail));

        var response = await _controller.GetFhirAuthenticationConfiguration(FacilityId, CancellationToken.None);

        var problem = AssertProblem(response, StatusCodes.Status503ServiceUnavailable);
        Assert.Contains(TraceId, problem.Detail);
        Assert.DoesNotContain(SecretDetail, problem.Detail);
    }

    [Fact]
    public async Task GetFhirAuthenticationConfiguration_UnexpectedFailure_ReturnsProblemWithTraceIdAndNoMessage()
    {
        _serviceMock
            .Setup(s => s.GetAsync(FacilityId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException(SecretDetail));

        var response = await _controller.GetFhirAuthenticationConfiguration(FacilityId, CancellationToken.None);

        var problem = AssertProblem(response, StatusCodes.Status500InternalServerError);
        Assert.Contains(TraceId, problem.Detail);
        Assert.DoesNotContain(SecretDetail, problem.Detail);
    }

    // ---- PUT ----

    [Fact]
    public async Task CreateOrUpdateFhirAuthenticationConfiguration_ValidRequest_ReturnsAcceptedWithTheConfiguration()
    {
        var expected = CreateResponse();
        _serviceMock
            .Setup(s => s.CreateOrUpdateAsync(FacilityId,
                                              It.IsAny<FhirAuthenticationConfigurationRequest>(),
                                              It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var response = await _controller.CreateOrUpdateFhirAuthenticationConfiguration(FacilityId,
                                                                                       CreateRequest(),
                                                                                       CancellationToken.None);

        var accepted = Assert.IsType<AcceptedResult>(response);
        Assert.Equal(StatusCodes.Status202Accepted, accepted.StatusCode);
        Assert.Same(expected, accepted.Value);
    }

    [Fact]
    public async Task CreateOrUpdateFhirAuthenticationConfiguration_ForwardsTheRequestAndCancellationToken()
    {
        using var cts = new CancellationTokenSource();
        var request = CreateRequest();
        _serviceMock
            .Setup(s => s.CreateOrUpdateAsync(FacilityId, request, cts.Token))
            .ReturnsAsync(CreateResponse());

        await _controller.CreateOrUpdateFhirAuthenticationConfiguration(FacilityId, request, cts.Token);

        _serviceMock.Verify(s => s.CreateOrUpdateAsync(FacilityId, request, cts.Token), Times.Once);
    }

    [Fact]
    public async Task CreateOrUpdateFhirAuthenticationConfiguration_ServiceRejectsTheRequest_ReturnsBadRequest()
    {
        _serviceMock
            .Setup(s => s.CreateOrUpdateAsync(FacilityId,
                                              It.IsAny<FhirAuthenticationConfigurationRequest>(),
                                              It.IsAny<CancellationToken>()))
            .ThrowsAsync(new BadRequestException("ClientSecret is required."));

        var response = await _controller.CreateOrUpdateFhirAuthenticationConfiguration(FacilityId,
                                                                                       CreateRequest(),
                                                                                       CancellationToken.None);

        var problem = AssertProblem(response, StatusCodes.Status400BadRequest);
        Assert.Equal("Bad Request", problem.Title);
    }

    /// <summary>
    /// The facility has no FHIR query configuration to attach authentication to. That is the
    /// endpoint's 404, and it must not surface as a 500.
    /// </summary>
    [Fact]
    public async Task CreateOrUpdateFhirAuthenticationConfiguration_NoFhirQueryConfiguration_ReturnsNotFound()
    {
        _serviceMock
            .Setup(s => s.CreateOrUpdateAsync(FacilityId,
                                              It.IsAny<FhirAuthenticationConfigurationRequest>(),
                                              It.IsAny<CancellationToken>()))
            .ThrowsAsync(new NotFoundException("No configuration found for facilityId."));

        var response = await _controller.CreateOrUpdateFhirAuthenticationConfiguration(FacilityId,
                                                                                       CreateRequest(),
                                                                                       CancellationToken.None);

        var problem = AssertProblem(response, StatusCodes.Status404NotFound);
        Assert.Equal("Not Found", problem.Title);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("<script>alert('x')</script>")]
    public async Task CreateOrUpdateFhirAuthenticationConfiguration_FacilityIdSanitizesAwayToNothing_ReturnsBadRequest(
        string facilityId)
    {
        var response = await _controller.CreateOrUpdateFhirAuthenticationConfiguration(facilityId,
                                                                                       CreateRequest(),
                                                                                       CancellationToken.None);

        AssertProblem(response, StatusCodes.Status400BadRequest);
        _serviceMock.Verify(s => s.CreateOrUpdateAsync(It.IsAny<string>(),
                                                       It.IsAny<FhirAuthenticationConfigurationRequest>(),
                                                       It.IsAny<CancellationToken>()),
                            Times.Never);
    }

    [Fact]
    public async Task CreateOrUpdateFhirAuthenticationConfiguration_RequestCancelled_Rethrows()
    {
        _serviceMock
            .Setup(s => s.CreateOrUpdateAsync(FacilityId,
                                              It.IsAny<FhirAuthenticationConfigurationRequest>(),
                                              It.IsAny<CancellationToken>()))
            .ThrowsAsync(new TaskCanceledException());

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => _controller.CreateOrUpdateFhirAuthenticationConfiguration(FacilityId,
                                                                            CreateRequest(),
                                                                            CancellationToken.None));
    }

    [Fact]
    public async Task CreateOrUpdateFhirAuthenticationConfiguration_SecretManagerFails_ReturnsServiceUnavailable()
    {
        _serviceMock
            .Setup(s => s.CreateOrUpdateAsync(FacilityId,
                                              It.IsAny<FhirAuthenticationConfigurationRequest>(),
                                              It.IsAny<CancellationToken>()))
            .ThrowsAsync(new RequestFailedException(503, SecretDetail));

        var response = await _controller.CreateOrUpdateFhirAuthenticationConfiguration(FacilityId,
                                                                                       CreateRequest(),
                                                                                       CancellationToken.None);

        var problem = AssertProblem(response, StatusCodes.Status503ServiceUnavailable);
        Assert.Contains(TraceId, problem.Detail);
        Assert.DoesNotContain(SecretDetail, problem.Detail);
    }

    [Fact]
    public async Task CreateOrUpdateFhirAuthenticationConfiguration_UnexpectedFailure_WithholdsTheMessage()
    {
        _serviceMock
            .Setup(s => s.CreateOrUpdateAsync(FacilityId,
                                              It.IsAny<FhirAuthenticationConfigurationRequest>(),
                                              It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException(SecretDetail));

        var response = await _controller.CreateOrUpdateFhirAuthenticationConfiguration(FacilityId,
                                                                                       CreateRequest(),
                                                                                       CancellationToken.None);

        var problem = AssertProblem(response, StatusCodes.Status500InternalServerError);
        Assert.Contains(TraceId, problem.Detail);
        Assert.DoesNotContain(SecretDetail, problem.Detail);
    }

    /// <summary>
    /// The response model has no client secret property at all, so no status can leak one.
    /// </summary>
    [Fact]
    public async Task ResponseModel_ExposesNoClientSecret()
    {
        var properties = typeof(FhirAuthenticationConfigurationResponse)
            .GetProperties()
            .Select(p => p.Name)
            .ToList();

        Assert.DoesNotContain("ClientSecret", properties);
        await Task.CompletedTask;
    }
}
