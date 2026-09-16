using LantanaGroup.Link.LinkAdmin.BFF.Application.Clients;
using LantanaGroup.Link.LinkAdmin.BFF.Application.Models.Configuration;
using LantanaGroup.Link.LinkAdmin.BFF.Presentation.Endpoints.Aggregation.Handlers.Report;
using LantanaGroup.Link.Shared.Application.Interfaces;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using LantanaGroup.Link.Shared.Application.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using System.Net;
using Task = System.Threading.Tasks.Task;

namespace UnitTests.Admin.BFF.Aggregation;

[Trait("Category", "UnitTests")]
public class RestoreReportTests
{
    private const string ReportId = "11111111-1111-1111-1111-111111111111";

    private readonly ILoggerFactory _loggerFactory = NullLoggerFactory.Instance;
    private readonly IOptions<ServiceRegistry> _serviceRegistry;
    private readonly IOptions<AuthenticationSchemaConfig> _authConfig;
    private readonly Mock<IServiceScopeFactory> _mockScopeFactory;

    public RestoreReportTests()
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

    private static HttpResponseMessage NoContent() => new(HttpStatusCode.NoContent);

    [Fact]
    public async Task Handle_Success_ClearsAbortFlag()
    {
        var abort = new InMemoryPipelineAbortRegistry();
        await abort.AbortAsync(null, ReportId, TimeSpan.FromDays(14));

        var handler = new MockHttpMessageHandler(request =>
        {
            if (request.Method == HttpMethod.Patch)
                return NoContent();
            throw new InvalidOperationException($"Unexpected request: {request.Method} {request.RequestUri}");
        });

        var (report, da) = BuildServices(handler);
        var result = await RestoreReport.Handle(_loggerFactory, BuildHttpContext(), report, da, abort, ReportId);

        Assert.Equal(StatusCodes.Status204NoContent, await ExecuteResultAsync(result));
        Assert.False(await abort.IsAbortedAsync(null, ReportId));
    }

    [Fact]
    public async Task Handle_ClearFailure_RollsBackScheduleAndAcquisitionLogs()
    {
        var abort = new ThrowingClearRegistry();
        var scheduleRolledBack = false;
        var logsRolledBack = false;

        var handler = new MockHttpMessageHandler(request =>
        {
            var path = request.RequestUri!.PathAndQuery;
            if (request.Method == HttpMethod.Patch && path.Contains($"api/schedules/{ReportId}/restore"))
                return NoContent();
            if (request.Method == HttpMethod.Patch && path.Contains($"api/data/acquisition-logs/report/{ReportId}/restore"))
                return NoContent();
            if (request.Method == HttpMethod.Delete && path.Contains($"api/schedules/{ReportId}"))
            {
                scheduleRolledBack = true;
                return NoContent();
            }

            if (request.Method == HttpMethod.Delete && path.Contains($"api/data/acquisition-logs/report/{ReportId}"))
            {
                logsRolledBack = true;
                return NoContent();
            }

            throw new InvalidOperationException($"Unexpected request: {request.Method} {request.RequestUri}");
        });

        var (report, da) = BuildServices(handler);
        var result = await RestoreReport.Handle(_loggerFactory, BuildHttpContext(), report, da, abort, ReportId);

        Assert.Equal(StatusCodes.Status500InternalServerError, await ExecuteResultAsync(result));
        Assert.True(scheduleRolledBack);
        Assert.True(logsRolledBack);
    }

    private sealed class ThrowingClearRegistry : IPipelineAbortRegistry
    {
        public Task AbortAsync(string? facilityId, string? reportId, TimeSpan timeToLive, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<bool> IsAbortedAsync(string? facilityId, string? reportId, CancellationToken cancellationToken = default)
            => Task.FromResult(false);

        public Task ClearAsync(string? facilityId, string? reportId, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("redis down");
    }
}
