using LantanaGroup.Link.DataAcquisition.Controllers;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Results;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Link.Authorization.Policies;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Moq;
using System.Net;
using System.Text.Json.Nodes;
using Xunit;
using Task = System.Threading.Tasks.Task;

namespace IntegrationTests.DataAcquisition.Controllers;

/// <summary>
/// The configuration-free "$validate" route sits directly beside the pre-existing
/// "{facilityId}/$validate" route on the same controller. These tests boot the real MVC routing
/// stack to prove the two templates resolve to the right actions and neither shadows the other.
/// </summary>
[Trait("Category", "IntegrationTests")]
public class ConnectionValidationRoutingTests : IClassFixture<ConnectionValidationRoutingFactory>
{
    private readonly ConnectionValidationRoutingFactory _factory;

    public ConnectionValidationRoutingTests(ConnectionValidationRoutingFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task ValidateRoute_WithoutFacilityId_RoutesToFhirServerConnectionAction()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/data/connectionValidation/$validate?fhirServerUrl=http://fhir.test/r4");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var payload = JsonNode.Parse(body)!.AsObject();
        Assert.True(payload["isConnected"]!.GetValue<bool>());

        _factory.FhirServerConnectionService.Verify(
            x => x.ValidateConnection(It.Is<ValidateFhirServerConnectionRequest>(r => r.FhirServerUrl == "http://fhir.test/r4"), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ValidateRoute_MissingFhirServerUrl_ReturnsBadRequest()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/data/connectionValidation/$validate");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ValidateRoute_WithFacilityId_StillRoutesToFacilityAction()
    {
        var client = _factory.CreateClient();

        // No query parameters, so the facility action's own validator should reject it with a 400.
        // What matters here is that the request reaches that action at all rather than 404ing or
        // being captured by the new configuration-free route. The "Patient" wording is unique to the
        // facility validator - the configuration-free action only ever complains about the server URL.
        var response = await client.GetAsync("/api/data/connectionValidation/test-facility/$validate");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("Patient", body);
        Assert.DoesNotContain("FHIR server URL", body);
    }
}

public sealed class ConnectionValidationRoutingFactory : WebApplicationFactory<ConnectionValidationRoutingFactory.ApiTestMarker>
{
    public Mock<IValidateFhirServerConnectionService> FhirServerConnectionService { get; } = new();
    public Mock<IValidateFacilityConnectionService> FacilityConnectionService { get; } = new();

    protected override IHost CreateHost(IHostBuilder builder)
    {
        builder.UseContentRoot(AppContext.BaseDirectory);
        return base.CreateHost(builder);
    }

    protected override IHostBuilder CreateHostBuilder()
    {
        FhirServerConnectionService
            .Setup(x => x.ValidateConnection(It.IsAny<ValidateFhirServerConnectionRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FhirServerConnectionResult(true));

        return Host.CreateDefaultBuilder()
            .ConfigureWebHostDefaults(webBuilder =>
            {
                webBuilder
                    .UseTestServer()
                    .ConfigureServices(services =>
                    {
                        services.AddSingleton(FhirServerConnectionService.Object);
                        services.AddSingleton(FacilityConnectionService.Object);

                        services
                            .AddControllers()
                            .AddApplicationPart(typeof(ConnectionValidationController).Assembly);

                        // The controller carries [Authorize(Policy = IsLinkAdmin)]. These tests are about
                        // routing, so the policy is registered as an always-satisfied one rather than
                        // standing up real authentication.
                        services.AddAuthorization(options =>
                            options.AddPolicy(PolicyNames.IsLinkAdmin, policy => policy.RequireAssertion(_ => true)));
                        services.AddProblemDetails();
                    })
                    .Configure(app =>
                    {
                        app.UseRouting();
                        app.UseAuthorization();
                        app.UseEndpoints(endpoints => endpoints.MapControllers());
                    });
            });
    }

    public sealed class ApiTestMarker { }
}
