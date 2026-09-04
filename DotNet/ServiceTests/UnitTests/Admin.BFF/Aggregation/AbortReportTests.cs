using LantanaGroup.Link.LinkAdmin.BFF.Application.Clients;
using LantanaGroup.Link.LinkAdmin.BFF.Application.Models.Configuration;
using LantanaGroup.Link.LinkAdmin.BFF.Presentation.Endpoints.Aggregation.Handlers.Report;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using LantanaGroup.Link.Shared.Application.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using System.Net;
using System.Text;
using Task = System.Threading.Tasks.Task;

namespace UnitTests.Admin.BFF.Aggregation;

[Trait("Category", "UnitTests")]
public class AbortReportTests
{
    private const string ReportId = "11111111-1111-1111-1111-111111111111";

    private readonly ILoggerFactory _loggerFactory = NullLoggerFactory.Instance;
    private readonly IOptions<ServiceRegistry> _serviceRegistry;
    private readonly IOptions<AuthenticationSchemaConfig> _authConfig;
    private readonly Mock<IServiceScopeFactory> _mockScopeFactory;

    public AbortReportTests()
    {
        _serviceRegistry = Options.Create(new ServiceRegistry
        {
            ReportServiceUrl = "http://report/",
            DataAcquisitionServiceUrl = "http://da/"
        });
        _authConfig = Options.Create(new AuthenticationSchemaConfig { EnableAnonymousAccess = true });
        _mockScopeFactory = new Mock<IServiceScopeFactory>();
    }

    private (ReportService report, DataAcquisitionService da) BuildServices(MockHttpMessageHandler handler)
    {
        var report = new ReportService(
            new Mock<ILogger<ReportService>>().Object,
            new HttpClient(handler) { BaseAddress = new Uri("http://report/") },
            _serviceRegistry,
            _authConfig,
            _mockScopeFactory.Object);
        var da = new DataAcquisitionService(
            new Mock<ILogger<DataAcquisitionService>>().Object,
            new HttpClient(handler) { BaseAddress = new Uri("http://da/") },
            _serviceRegistry,
            _authConfig,
            _mockScopeFactory.Object);
        return (report, da);
    }

    private static DefaultHttpContext BuildHttpContext() => new();

    private static async Task<int> ExecuteResultAsync(IResult result)
    {
        var services = new ServiceCollection();
        services.AddProblemDetails();
        services.AddLogging();
        var provider = services.BuildServiceProvider();
        var httpContext = new DefaultHttpContext
        {
            RequestServices = provider,
            Response = { Body = new MemoryStream() }
        };
        await result.ExecuteAsync(httpContext);
        return httpContext.Response.StatusCode;
    }

    private static HttpResponseMessage OkJson(string json) =>
        new(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

    private static HttpResponseMessage NoContent() => new(HttpStatusCode.NoContent);

    private static HttpResponseMessage Accepted() => new(HttpStatusCode.Accepted);

    [Fact]
    public async Task Handle_InProgressNew_AbortsAndSoftDeletes()
    {
        var abort = new InMemoryPipelineAbortRegistry();
        var cancelCalled = false;
        var deleteScheduleCalled = false;
        var deleteLogsCalled = false;

        var handler = new MockHttpMessageHandler(request =>
        {
            var path = request.RequestUri!.PathAndQuery;
            if (request.Method == HttpMethod.Get && path.Contains($"api/schedules/{ReportId}"))
                return OkJson("{\"status\":\"New\"}");
            if (request.Method == HttpMethod.Post && path.Contains("cancel-by-filter"))
            {
                cancelCalled = true;
                return Accepted();
            }
            if (request.Method == HttpMethod.Delete && path.Contains($"api/schedules/{ReportId}"))
            {
                deleteScheduleCalled = true;
                return NoContent();
            }
            if (request.Method == HttpMethod.Delete && path.Contains($"api/data/acquisition-logs/report/{ReportId}"))
            {
                deleteLogsCalled = true;
                return NoContent();
            }

            throw new InvalidOperationException($"Unexpected request: {request.Method} {request.RequestUri}");
        });

        var (report, da) = BuildServices(handler);
        var result = await AbortReport.Handle(_loggerFactory, BuildHttpContext(), report, da, abort, ReportId);

        Assert.Equal(StatusCodes.Status204NoContent, await ExecuteResultAsync(result));
        Assert.True(await abort.IsAbortedAsync(null, ReportId));
        Assert.True(cancelCalled);
        Assert.True(deleteScheduleCalled);
        Assert.True(deleteLogsCalled);
    }

    [Fact]
    public async Task Handle_EndOfPeriod_Aborts()
    {
        var abort = new InMemoryPipelineAbortRegistry();
        var handler = new MockHttpMessageHandler(request =>
        {
            var path = request.RequestUri!.PathAndQuery;
            if (request.Method == HttpMethod.Get && path.Contains($"api/schedules/{ReportId}"))
                return OkJson("{\"Status\":\"EndOfPeriod\"}");
            if (request.Method == HttpMethod.Post && path.Contains("cancel-by-filter"))
                return Accepted();
            if (request.Method == HttpMethod.Delete)
                return NoContent();
            throw new InvalidOperationException($"Unexpected request: {request.Method} {request.RequestUri}");
        });

        var (report, da) = BuildServices(handler);
        var result = await AbortReport.Handle(_loggerFactory, BuildHttpContext(), report, da, abort, ReportId);

        Assert.Equal(StatusCodes.Status204NoContent, await ExecuteResultAsync(result));
        Assert.True(await abort.IsAbortedAsync(null, ReportId));
    }

    [Fact]
    public async Task Handle_Submitted_Returns409_AndDoesNotAbort()
    {
        var abort = new InMemoryPipelineAbortRegistry();
        var handler = new MockHttpMessageHandler(request =>
        {
            if (request.Method == HttpMethod.Get)
                return OkJson("{\"status\":\"Submitted\"}");
            throw new InvalidOperationException($"Unexpected request: {request.Method} {request.RequestUri}");
        });

        var (report, da) = BuildServices(handler);
        var result = await AbortReport.Handle(_loggerFactory, BuildHttpContext(), report, da, abort, ReportId);

        Assert.Equal(StatusCodes.Status409Conflict, await ExecuteResultAsync(result));
        Assert.False(await abort.IsAbortedAsync(null, ReportId));
    }

    [Fact]
    public async Task Handle_MissingReport_Returns404()
    {
        var abort = new InMemoryPipelineAbortRegistry();
        var handler = new MockHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.NotFound));
        var (report, da) = BuildServices(handler);
        var result = await AbortReport.Handle(_loggerFactory, BuildHttpContext(), report, da, abort, ReportId);

        Assert.Equal(StatusCodes.Status404NotFound, await ExecuteResultAsync(result));
        Assert.False(await abort.IsAbortedAsync(null, ReportId));
    }
}
