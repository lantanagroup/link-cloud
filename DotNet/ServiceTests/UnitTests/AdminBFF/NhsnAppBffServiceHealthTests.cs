using System.Net;
using System.Text;
using LantanaGroup.Link.LinkAdmin.BFF.Application.Clients;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Task = System.Threading.Tasks.Task;

namespace UnitTests.AdminBFF;

/// <summary>
/// The Admin UI health page's NHSN App BFF row: its health report, database check included, read
/// through the Admin BFF.
/// </summary>
[Trait("Category", "UnitTests")]
public class NhsnAppBffServiceHealthTests
{
    private sealed class StubHandler(HttpStatusCode status, string body) : HttpMessageHandler
    {
        public Uri? RequestedUri { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestedUri = request.RequestUri;
            return Task.FromResult(new HttpResponseMessage(status)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            });
        }
    }

    private static NhsnAppBffService AClient(string? serviceUrl, HttpMessageHandler handler) =>
        new(NullLogger<NhsnAppBffService>.Instance, new HttpClient(handler),
            Options.Create(new ServiceRegistry { NhsnAppBffServiceUrl = serviceUrl }));

    [Fact]
    public async Task ReportsTheDatabaseEntry_UnderTheNhsnAppBffLabel()
    {
        var handler = new StubHandler(HttpStatusCode.OK, """
            {
              "status": "Healthy",
              "totalDuration": "00:00:00.0123456",
              "entries": {
                "Database (SQL Server)": { "status": "Healthy", "duration": "00:00:00.0100000" }
              }
            }
            """);

        var report = await AClient("http://nhsn-app-bff:8079", handler).LinkServiceHealthCheck(CancellationToken.None);

        Assert.Equal("http://nhsn-app-bff:8079/health", handler.RequestedUri!.ToString());
        Assert.Equal("NHSN App BFF", report.Service);
        Assert.Equal(HealthStatus.Healthy, report.Status);
        var entry = Assert.Single(report.Entries);
        Assert.Equal("Database (SQL Server)", entry.Key);
        Assert.Equal(HealthStatus.Healthy, entry.Value.Status);
    }

    [Fact]
    public async Task AnUnreachableDatabase_IsReportedUnhealthy()
    {
        var handler = new StubHandler(HttpStatusCode.ServiceUnavailable, """
            {
              "status": "Unhealthy",
              "totalDuration": "00:00:05",
              "entries": {
                "Database (SQL Server)": { "status": "Unhealthy", "duration": "00:00:05", "description": "Database connection failed." }
              }
            }
            """);

        var report = await AClient("http://nhsn-app-bff:8079/", handler).LinkServiceHealthCheck(CancellationToken.None);

        Assert.Equal(HealthStatus.Unhealthy, report.Status);
        Assert.Equal("Database connection failed.", report.Entries["Database (SQL Server)"].Description);
    }

    [Fact]
    public async Task AnUnreachableService_IsReportedUnhealthy()
    {
        var report = await new NhsnAppBffService(NullLogger<NhsnAppBffService>.Instance,
                new HttpClient(new ThrowingHandler()),
                Options.Create(new ServiceRegistry { NhsnAppBffServiceUrl = "http://nhsn-app-bff:8079" }))
            .LinkServiceHealthCheck(CancellationToken.None);

        Assert.Equal("NHSN App BFF", report.Service);
        Assert.Equal(HealthStatus.Unhealthy, report.Status);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public async Task AMissingUrl_IsReportedAsNotConfigured_WithoutACall(string? serviceUrl)
    {
        var handler = new StubHandler(HttpStatusCode.OK, "{}");

        var report = await AClient(serviceUrl, handler).LinkServiceHealthCheck(CancellationToken.None);

        Assert.Null(handler.RequestedUri);
        Assert.Equal("NHSN App BFF", report.Service);
        Assert.Equal(HealthStatus.Unhealthy, report.Status);
        Assert.Contains("NhsnAppBffServiceUrl", report.Entries["Configuration"].Description);
    }

    private sealed class ThrowingHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            throw new HttpRequestException("Connection refused");
    }
}
