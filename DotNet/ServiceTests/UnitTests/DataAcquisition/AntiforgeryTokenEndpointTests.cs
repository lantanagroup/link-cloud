using System.Net;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Task = System.Threading.Tasks.Task;

namespace ServiceTests.UnitTests.DataAcquisition;

/// <summary>
/// The Docker and local hosts turn on Authentication:EnableAnonymousAccess, which registers no
/// authentication scheme. A parameterless RequireAuthorization() then fails inside the challenge
/// instead of reaching the handler.
/// </summary>
[Trait("Category", "UnitTests")]
public class AntiforgeryTokenEndpointTests
{
    [Fact]
    public async Task Returns_a_token_when_anonymous_access_does_not_challenge()
    {
        using var host = await StartHost(requireAuthorization: false);
        var response = await host.GetTestClient().GetAsync("/api/data-acquisition/antiforgery-token");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        document.RootElement.GetProperty("token").GetString().Should().NotBeNullOrWhiteSpace();
        document.RootElement.GetProperty("headerName").GetString().Should().Be("X-Link-AntiForgery");
    }

    [Fact]
    public async Task Returns_500_when_anonymous_access_still_requires_authorization()
    {
        using var host = await StartHost(requireAuthorization: true);
        var response = await host.GetTestClient().GetAsync("/api/data-acquisition/antiforgery-token");

        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
    }

    private static async Task<IHost> StartHost(bool requireAuthorization)
    {
        var host = new HostBuilder()
            .ConfigureWebHost(web =>
            {
                web.UseTestServer();
                web.UseEnvironment(Environments.Production);
                web.ConfigureServices(services =>
                {
                    services.AddRouting();
                    services.AddAuthorization();
                    services.AddAntiforgery(options => options.HeaderName = "X-Link-AntiForgery");
                });
                web.Configure(app =>
                {
                    app.UseExceptionHandler(handler => handler.Run(context =>
                    {
                        context.Response.StatusCode = StatusCodes.Status500InternalServerError;
                        return Task.CompletedTask;
                    }));
                    app.UseRouting();
                    app.UseAuthorization();
                    app.UseEndpoints(endpoints =>
                    {
                        var route = endpoints.MapGet("/api/data-acquisition/antiforgery-token",
                            (IAntiforgery antiforgery, HttpContext context) =>
                            {
                                var tokens = antiforgery.GetAndStoreTokens(context);
                                return Results.Ok(new { token = tokens.RequestToken, headerName = "X-Link-AntiForgery" });
                            });
                        if (requireAuthorization)
                            route.RequireAuthorization();
                    });
                });
            })
            .Build();
        await host.StartAsync();
        return host;
    }
}
