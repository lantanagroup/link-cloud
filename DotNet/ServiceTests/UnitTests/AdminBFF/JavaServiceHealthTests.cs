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
    // DEGRADED is the custom Spring status the Java services return when a cache is unreachable but
    // the durable store behind it is fine; it must not collapse to Unhealthy. It serves 200, not 503.
    [InlineData(true, "DEGRADED", HttpStatusCode.OK, HealthStatus.Degraded)]
    [InlineData(false, "DEGRADED", HttpStatusCode.OK, HealthStatus.Degraded)]
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

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task HealthCheck_ReturnsUnhealthyForMalformedResponse(bool measureEval)
    {
        var report = await CheckHealth(measureEval, "not json");

        Assert.Equal(HealthStatus.Unhealthy, report.Status);
        Assert.Empty(report.Entries);
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

        return measureEval
            ? await new MeasureEvalService(NullLogger<MeasureEvalService>.Instance, client, registry)
                .LinkServiceHealthCheck(CancellationToken.None)
            : await new ValidationService(NullLogger<ValidationService>.Instance, client, registry)
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
}