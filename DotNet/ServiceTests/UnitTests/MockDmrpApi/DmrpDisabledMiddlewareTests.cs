using System.Net;
using System.Text.Json;
using FluentAssertions;
using LantanaGroup.Link.MockDmrpApi.Application.Middleware;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;

using Task = System.Threading.Tasks.Task;

namespace UnitTests.MockDmrpApi;

/// <summary>
/// Drives the availability gate through a real pipeline, so the assertions cover ordering
/// and the response body rather than only the decision.
/// </summary>
public class DmrpDisabledMiddlewareTests
{
    /// <summary>
    /// Builds a host whose only endpoint records whether the pipeline got past the gate.
    /// </summary>
    private static async Task<IHost> StartHostAsync(string environmentName, string? enabled)
    {
        var host = await new HostBuilder()
            .ConfigureWebHost(web =>
            {
                web.UseTestServer();
                web.UseEnvironment(environmentName);
                web.ConfigureAppConfiguration(config =>
                {
                    if (enabled is not null)
                    {
                        config.AddInMemoryCollection(new Dictionary<string, string?>
                        {
                            [DmrpAvailability.EnabledConfigurationKey] = enabled
                        });
                    }
                });
                web.ConfigureServices(services => services.AddProblemDetails());
                web.Configure(app =>
                {
                    app.UseDmrpAvailabilityGate();

                    // Stands in for everything downstream. Reaching it means the gate let
                    // the request through.
                    app.Run(async context =>
                    {
                        context.Response.StatusCode = StatusCodes.Status200OK;
                        await context.Response.WriteAsync($"reached:{context.Request.Path}");
                    });
                });
            })
            .StartAsync();

        return host;
    }

    [Fact]
    public async Task WhenEnabled_RequestsReachTheRestOfThePipeline()
    {
        using var host = await StartHostAsync("Development", "true");

        var response = await host.GetTestClient().GetAsync("/dmrp/mock/search");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync()).Should().Be("reached:/dmrp/mock/search");
    }

    [Fact]
    public async Task WhenDisabled_RequestsAreRefusedBeforeReachingThePipeline()
    {
        using var host = await StartHostAsync("Development", "false");

        var response = await host.GetTestClient().GetAsync("/dmrp/mock/search");

        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);

        // Nothing downstream ran -- the gate sits before routing precisely so that a
        // disabled deployment cannot reach anything added later.
        (await response.Content.ReadAsStringAsync()).Should().NotContain("reached:");
    }

    [Fact]
    public async Task WhenDisabled_TheResponseIsProblemDetailsCarryingATraceId()
    {
        using var host = await StartHostAsync("Development", "false");

        var response = await host.GetTestClient().GetAsync("/dmrp/mock/search");

        response.Content.Headers.ContentType?.MediaType.Should().Be("application/problem+json");

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = document.RootElement;

        root.GetProperty("status").GetInt32().Should().Be(503);
        root.GetProperty("title").GetString().Should().Contain("disabled");
        root.GetProperty("instance").GetString().Should().Be("/dmrp/mock/search");
        root.TryGetProperty("traceId", out var traceId).Should().BeTrue();
        traceId.GetString().Should().NotBeNullOrWhiteSpace();
    }

    [Theory]
    [InlineData("/health")]
    [InlineData("/api/mock-dmrp/info")]
    public async Task WhenDisabled_HealthAndInfoStillAnswer(string path)
    {
        // Health has to stay up or the container reports unhealthy and restarts, which
        // reads as an outage rather than a service that is deliberately dormant.
        using var host = await StartHostAsync("Development", "false");

        var response = await host.GetTestClient().GetAsync(path);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync()).Should().Be($"reached:{path}");
    }

    [Fact]
    public async Task InProduction_EvenAnExplicitEnabledTrueIsRefused()
    {
        // The one that guards production. A configuration source that outranks appsettings
        // -- App Configuration does -- cannot turn the mock on.
        using var host = await StartHostAsync("Production", "true");

        var response = await host.GetTestClient().GetAsync("/dmrp/mock/search");

        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        (await response.Content.ReadAsStringAsync()).Should().NotContain("reached:");
    }

    [Fact]
    public async Task InProduction_HealthStillAnswers()
    {
        using var host = await StartHostAsync("Production", "true");

        var response = await host.GetTestClient().GetAsync("/health");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task WithNoConfiguration_OutsideProductionTheServiceServes()
    {
        using var host = await StartHostAsync("Development", enabled: null);

        var response = await host.GetTestClient().GetAsync("/dmrp/mock/search");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
