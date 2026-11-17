using System.Net;
using FluentAssertions;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Interfaces;
using LantanaGroup.Link.DataAcquisition.Infrastructure;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Moq;
using Task = System.Threading.Tasks.Task;

namespace ServiceTests.UnitTests.DataAcquisition.Infrastructure;

/// <summary>
/// Runs the rewrite rule, routing and the metrics middleware together through the extension Program.cs uses,
/// so the assertions cover the hand-off between them rather than each piece alone.
/// </summary>
[Trait("Category", "UnitTests")]
public class LegacyRoutePrefixPipelineTests
{
    private const string RouteTemplate = "/api/data-acquisition/{facilityId}/query-plans";

    private readonly Mock<IDataAcquisitionServiceMetrics> _metrics = new();

    [Fact]
    public async Task Request_LegacyPrefix_ReachesCanonicalEndpoint()
    {
        using var host = await StartHostAsync();

        var response = await host.GetTestClient().GetAsync("/api/data/fac-1/query-plans?type=Initial");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync()).Should().Be("GET|fac-1|?type=Initial");
    }

    [Fact]
    public async Task Request_LegacyPrefix_CountedOnceWithRouteTemplate()
    {
        using var host = await StartHostAsync();

        await host.GetTestClient().GetAsync("/api/data/fac-1/query-plans");

        _metrics.Verify(m => m.IncrementPathRewriteCounter(RouteTemplate, HttpMethods.Get), Times.Once);
    }

    [Fact]
    public async Task Request_LegacyPrefixPost_CountedWithPostMethod()
    {
        using var host = await StartHostAsync();

        var response = await host.GetTestClient().PostAsync("/api/data/fac-1/query-plans", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync()).Should().Be("POST|fac-1|");
        _metrics.Verify(m => m.IncrementPathRewriteCounter(RouteTemplate, HttpMethods.Post), Times.Once);
    }

    [Fact]
    public async Task Request_CanonicalPrefix_ReachesEndpointWithoutCounting()
    {
        using var host = await StartHostAsync();

        var response = await host.GetTestClient().GetAsync("/api/data-acquisition/fac-1/query-plans");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync()).Should().Be("GET|fac-1|");
        _metrics.Verify(m => m.IncrementPathRewriteCounter(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task Request_BothPrefixes_ReturnSameResponse()
    {
        using var host = await StartHostAsync();
        var client = host.GetTestClient();

        var legacy = await client.GetAsync("/api/data/fac-1/query-plans?type=Initial");
        var canonical = await client.GetAsync("/api/data-acquisition/fac-1/query-plans?type=Initial");

        legacy.StatusCode.Should().Be(canonical.StatusCode);
        (await legacy.Content.ReadAsStringAsync()).Should().Be(await canonical.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Request_LegacyPrefixUnknownPath_ReturnsNotFoundAndCountsUnmatched()
    {
        using var host = await StartHostAsync();

        var response = await host.GetTestClient().GetAsync("/api/data/no-such-thing");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        _metrics.Verify(m => m.IncrementPathRewriteCounter("unmatched", HttpMethods.Get), Times.Once);
    }

    [Fact]
    public async Task Request_PathOutsideLegacyPrefix_IsNotRewritten()
    {
        using var host = await StartHostAsync();

        var response = await host.GetTestClient().GetAsync("/health");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync()).Should().Be("healthy");
        _metrics.Verify(m => m.IncrementPathRewriteCounter(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    private async Task<IHost> StartHostAsync()
    {
        var host = await new HostBuilder()
            .ConfigureWebHost(web =>
            {
                web.UseTestServer();
                web.ConfigureServices(services =>
                {
                    services.AddRouting();
                    services.AddSingleton(_metrics.Object);
                });
                web.Configure(app =>
                {
                    app.UseRoutingWithLegacyRoutePrefix();
                    app.UseEndpoints(endpoints =>
                    {
                        // Echo the method, route value and query string, so a test can see the request
                        // arrived intact on the canonical route.
                        endpoints.MapMethods(RouteTemplate,
                                             [HttpMethods.Get, HttpMethods.Post],
                                             (string facilityId, HttpContext context) =>
                                                 $"{context.Request.Method}|{facilityId}|{context.Request.QueryString}");
                        endpoints.MapGet("/health", () => "healthy");
                    });
                });
            })
            .StartAsync();

        return host;
    }
}
