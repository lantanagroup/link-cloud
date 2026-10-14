using LantanaGroup.Link.DataAcquisition.Controllers;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Api.Configuration;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Services.Sftp;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Validators;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Moq.Language.Flow;
using Task = System.Threading.Tasks.Task;

namespace UnitTests.DataAcquisition.Controllers;

[Trait("Category", "UnitTests")]
public class SftpConnectionTestControllerTests
{
    private const string TraceId = "test-trace-id";
    private const string Password = "Pa55-must-not-leak";

    private static readonly IServiceProvider MvcServices = BuildMvcServices();

    private readonly Mock<ISftpConnectionTestService> _serviceMock = new();
    private readonly SftpConnectionTestController _controller;

    public SftpConnectionTestControllerTests()
    {
        // The real validator, so the 400 tests prove the rules the endpoint enforces rather than a mock's
        var validator = new SftpTestConnectionRequestModelValidator(Options.Create(new SftpValidationSettings()));

        _controller = new SftpConnectionTestController(
            _serviceMock.Object,
            validator,
            new Mock<ILogger<SftpConnectionTestController>>().Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { RequestServices = MvcServices, TraceIdentifier = TraceId }
            },
            ProblemDetailsFactory = MvcServices.GetRequiredService<ProblemDetailsFactory>()
        };
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task TestConnection_ValidRequest_PassesRequestToServiceAndReturnsOk(bool includeFileContent)
    {
        var expected = new SftpTestConnectionResult { Success = true, Message = "Connected." };
        SetupService().ReturnsAsync(expected);
        using var cts = new CancellationTokenSource();

        var response = await _controller.TestConnection(ValidRequest(), includeFileContent, cts.Token);

        var ok = Assert.IsType<OkObjectResult>(response.Result);
        Assert.Same(expected, ok.Value);
        _serviceMock.Verify(s => s.TestSftpConnectionAsync(
            "sftp.example.com", 2222, "facility-user", Password, "/data", includeFileContent, cts.Token), Times.Once);
    }

    [Fact]
    public async Task TestConnection_ConnectionFails_StillReturnsOk()
    {
        // A server that refuses the connection is a test result, not a client or server error
        var failed = new SftpTestConnectionResult { Success = false, Message = "Authentication failed." };
        SetupService().ReturnsAsync(failed);

        var response = await _controller.TestConnection(ValidRequest());

        var ok = Assert.IsType<OkObjectResult>(response.Result);
        Assert.Same(failed, ok.Value);
    }

    [Theory]
    [MemberData(nameof(InvalidRequests))]
    public async Task TestConnection_InvalidRequest_ReturnsValidationProblemWithoutTesting(
        SftpTestConnectionRequestModel request, string invalidProperty)
    {
        var response = await _controller.TestConnection(request);

        var result = Assert.IsAssignableFrom<ObjectResult>(response.Result);
        Assert.Equal(StatusCodes.Status400BadRequest, result.StatusCode);
        var problem = Assert.IsType<ValidationProblemDetails>(result.Value);
        Assert.Contains(invalidProperty, problem.Errors.Keys);
        _serviceMock.Verify(s => s.TestSftpConnectionAsync(
            It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    public static TheoryData<SftpTestConnectionRequestModel, string> InvalidRequests => new()
    {
        { ValidRequest() with { HostName = "" }, nameof(SftpTestConnectionRequestModel.HostName) },
        { ValidRequest() with { HostName = "http://sftp.example.com" }, nameof(SftpTestConnectionRequestModel.HostName) },
        { ValidRequest() with { HostUrlPort = 0 }, nameof(SftpTestConnectionRequestModel.HostUrlPort) },
        { ValidRequest() with { HostUrlPort = 65536 }, nameof(SftpTestConnectionRequestModel.HostUrlPort) },
        { ValidRequest() with { Username = "" }, nameof(SftpTestConnectionRequestModel.Username) },
        { ValidRequest() with { Password = "" }, nameof(SftpTestConnectionRequestModel.Password) },
        { ValidRequest() with { ReportDirectory = "" }, nameof(SftpTestConnectionRequestModel.ReportDirectory) },
        { ValidRequest() with { ReportDirectory = "/data|reports" }, nameof(SftpTestConnectionRequestModel.ReportDirectory) }
    };

    [Fact]
    public async Task TestConnection_ServiceThrows_ReturnsProblemWithTraceIdAndNoExceptionDetail()
    {
        SetupService().ThrowsAsync(new InvalidOperationException($"internal detail {Password}"));

        var response = await _controller.TestConnection(ValidRequest());

        var result = Assert.IsAssignableFrom<ObjectResult>(response.Result);
        Assert.Equal(StatusCodes.Status500InternalServerError, result.StatusCode);
        var problem = Assert.IsType<ProblemDetails>(result.Value);
        Assert.Equal(StatusCodes.Status500InternalServerError, problem.Status);
        Assert.Contains(TraceId, problem.Detail);
        Assert.Equal(TraceId, problem.Extensions["traceId"]);
        Assert.DoesNotContain("internal detail", problem.Detail);
        Assert.DoesNotContain(Password, problem.Detail);
    }

    [Fact]
    public async Task TestConnection_Cancelled_Propagates()
    {
        SetupService().ThrowsAsync(new OperationCanceledException());

        await Assert.ThrowsAsync<OperationCanceledException>(() => _controller.TestConnection(ValidRequest()));
    }

    private static SftpTestConnectionRequestModel ValidRequest() => new()
    {
        HostName = "sftp.example.com",
        HostUrlPort = 2222,
        Username = "facility-user",
        Password = Password,
        ReportDirectory = "/data"
    };

    private ISetup<ISftpConnectionTestService, Task<SftpTestConnectionResult>> SetupService()
        => _serviceMock.Setup(s => s.TestSftpConnectionAsync(
            It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()));

    private static IServiceProvider BuildMvcServices()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddControllers();
        return services.BuildServiceProvider();
    }
}
