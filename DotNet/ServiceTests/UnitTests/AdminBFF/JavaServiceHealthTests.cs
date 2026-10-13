using System.Net;
using System.Text.Json;
using LantanaGroup.Link.LinkAdmin.BFF.Application.Clients;
using LantanaGroup.Link.LinkAdmin.BFF.Application.Models.Health;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Task = System.Threading.Tasks.Task;

namespace UnitTests.AdminBFF;

[Trait("Category", "UnitTests")]
public class JavaServiceHealthTests
{
    [Theory]
    [InlineData(true, "UP", HttpStatusCode.OK, HealthStatus.Healthy)]
    [InlineData(false, "UP", HttpStatusCode.OK, HealthStatus.Healthy)]
    [InlineData(true, "DOWN", HttpStatusCode.ServiceUnavailable, HealthStatus.Unhealthy)]
    [InlineData(false, "DOWN", HttpStatusCode.ServiceUnavailable, HealthStatus.Unhealthy)]
    public async Task HealthCheck_PreservesAllDetailsInDescription(
        bool measureEval, string status, HttpStatusCode statusCode, HealthStatus expectedStatus)
    {
                var componentName = measureEval ? "Resource Cache" : "Redis";
        var payload = $$"""
            {
              "status": "{{status}}",
              "components": {
                                "{{componentName}}": {
                  "status": "{{status}}",
                  "details": {
                    "Redis": "Available",
                    "ABS": "Unavailable",
                    "error": "Connection failed",
                    "attempts": 2,
                    "reachable": false,
                    "nested": { "hosts": ["first", "second"] }
                  }
                }
              }
            }
            """;

        var report = await CheckHealth(measureEval, payload, statusCode);

        Assert.Equal(measureEval ? "Measure Evaluation" : "Validation", report.Service);
        Assert.Equal(expectedStatus, report.Status);
        var entry = Assert.Single(report.Entries);
        Assert.Equal(componentName, entry.Key);
        Assert.Equal(expectedStatus, entry.Value.Status);
        Assert.Equal(TimeSpan.Zero, entry.Value.Duration);
        using var description = JsonDocument.Parse(entry.Value.Description!);
        var details = description.RootElement;
        Assert.Equal(6, details.EnumerateObject().Count());
        Assert.Equal("Available", details.GetProperty("Redis").GetString());
        Assert.Equal("Unavailable", details.GetProperty("ABS").GetString());
        Assert.Equal("Connection failed", details.GetProperty("error").GetString());
        Assert.Equal(2, details.GetProperty("attempts").GetInt32());
        Assert.False(details.GetProperty("reachable").GetBoolean());
        Assert.Equal("second", details.GetProperty("nested").GetProperty("hosts")[1].GetString());
    }

    [Theory]
    [InlineData(true, "")]
    [InlineData(false, "")]
    [InlineData(true, ", \"details\": {}")]
    [InlineData(false, ", \"details\": {}")]
    [InlineData(true, ", \"details\": null")]
    [InlineData(false, ", \"details\": null")]
    public async Task HealthCheck_LeavesDescriptionNullWithoutDetails(bool measureEval, string details)
    {
        var payload = $$"""
            { "status": "UP", "components": { "kafka": { "status": "UP"{{details}} } } }
            """;

        var report = await CheckHealth(measureEval, payload);

        Assert.Equal(HealthStatus.Healthy, report.Status);
        Assert.Null(Assert.Single(report.Entries).Value.Description);
    }

    /// <summary>
    /// A body that is not a health report has to be reported as one entry rather than as none. The
    /// dashboard renders a row per entry and falls back to "No health check entries available" for
    /// an empty report, so returning nothing showed the service Unhealthy while explaining nothing.
    /// </summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task HealthCheck_ExplainsAMalformedResponse(bool measureEval)
    {
        var report = await CheckHealth(measureEval, "not json", HttpStatusCode.BadGateway);

        Assert.Equal(HealthStatus.Unhealthy, report.Status);
        var entry = Assert.Single(report.Entries);
        Assert.Equal(SpringServiceHealth.ReadEntry, entry.Key);
        Assert.Equal(HealthStatus.Unhealthy, entry.Value.Status);
        // The status code tells a 502 from a gateway apart from a service that answered late.
        Assert.Contains("502", entry.Value.Description!);
    }

    /// <summary>A parsed report with no components still has to render as something.</summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task HealthCheck_ExplainsAReportWithNoComponents(bool measureEval)
    {
        var report = await CheckHealth(measureEval, """{ "status": "UP" }""");

        // The status the service reported is kept; only the missing detail is filled in.
        Assert.Equal(HealthStatus.Healthy, report.Status);
        var entry = Assert.Single(report.Entries);
        Assert.Equal(SpringServiceHealth.ReadEntry, entry.Key);
        Assert.Contains("no health components", entry.Value.Description!);
    }

    /// <summary>
    /// The case that prompted this: MeasureEval answered /health correctly in 6.01s with Kafka down,
    /// the BFF abandoned it at 5s, and the dashboard lost the MongoDB and Resource Cache rows that
    /// explained the outage. The deadline now reports itself.
    /// </summary>
    [Fact]
    public async Task HealthCheck_ReportsItsOwnDeadlineRatherThanNothing()
    {
        using var client = new HttpClient(new NeverAnswersHandler()) { BaseAddress = new Uri("http://localhost/") };

        var report = await SpringServiceHealth.ReadAsync(client, "Measure Evaluation",
            TimeSpan.FromMilliseconds(100), NullLogger.Instance, CancellationToken.None);

        Assert.Equal(HealthStatus.Unhealthy, report.Status);
        var entry = Assert.Single(report.Entries);
        Assert.Equal(SpringServiceHealth.ReadEntry, entry.Key);
        Assert.Contains("did not answer", entry.Value.Description!);
        // Timed, so the dashboard can show where the budget went instead of 00:00:00.
        Assert.True(report.TotalDuration > TimeSpan.Zero, "the report must carry how long it waited");
    }

    /// <summary>
    /// A caller that gave up is not a service that failed. Reporting Unhealthy here would record a
    /// verdict this check never reached, for a dashboard request that is already gone.
    /// </summary>
    [Fact]
    public async Task HealthCheck_PropagatesCallerCancellation()
    {
        using var client = new HttpClient(new NeverAnswersHandler()) { BaseAddress = new Uri("http://localhost/") };
        using var abandoned = new CancellationTokenSource();
        await abandoned.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            SpringServiceHealth.ReadAsync(client, "Measure Evaluation", TimeSpan.FromSeconds(5),
                NullLogger.Instance, abandoned.Token));
    }

    private static async Task<LinkServiceHealthReport> CheckHealth(
        bool measureEval, string payload, HttpStatusCode statusCode = HttpStatusCode.OK)
    {
        using var client = new HttpClient(new HealthResponseHandler(payload, statusCode));
        var registry = Options.Create(new ServiceRegistry
        {
            MeasureServiceUrl = "http://localhost/",
            ValidationServiceUrl = "http://localhost/"
        });
        var healthCheckSettings = Options.Create(new ServiceHealthCheckSettings());

        return measureEval
            ? await new MeasureEvalService(NullLogger<MeasureEvalService>.Instance, client, registry, healthCheckSettings)
                .LinkServiceHealthCheck(CancellationToken.None)
            : await new ValidationService(NullLogger<ValidationService>.Instance, client, registry, healthCheckSettings)
                .LinkServiceHealthCheck(CancellationToken.None);
    }

    private sealed class HealthResponseHandler(string payload, HttpStatusCode statusCode) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Assert.Equal("/health", request.RequestUri!.AbsolutePath);
            Assert.True(cancellationToken.CanBeCanceled);
            return Task.FromResult(new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(payload)
            });
        }
    }

    /// <summary>Stands in for a service whose indicators outlast the deadline.</summary>
    private sealed class NeverAnswersHandler : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            await Task.Delay(Timeout.Infinite, cancellationToken);
            throw new InvalidOperationException("unreachable");
        }
    }
}