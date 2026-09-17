using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using LantanaGroup.Link.MockDmrpApi.Application.Models;
using LantanaGroup.Link.MockDmrpApi.Application.Services;
using LantanaGroup.Link.MockDmrpApi.Domain.Entities;
using LantanaGroup.Link.MockDmrpApi.Presentation.Controllers;
using LantanaGroup.Link.MockDmrpApi.Settings;
using LantanaGroup.Link.Shared.Domain.Repositories.Interfaces;
using Link.Authorization.Policies;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Xunit;

using Task = System.Threading.Tasks.Task;

namespace UnitTests.MockDmrpApi;

/// <summary>
/// Pins the shape of every problem response, on both surfaces.
/// </summary>
/// <remarks>
/// Following the pattern Terminology uses: a <c>CustomizeProblemDetails</c> hook supplies a
/// <c>traceId</c> and a fallback <c>detail</c>, and each controller passes a <c>title</c> and
/// <c>type</c> alongside its own detail.
/// <para>
/// Worth testing rather than assuming, because the failure is quiet. A <c>Problem()</c> call
/// that omits the title still produces a well-formed response — just a generic one, sitting
/// next to a specific message, with nothing for a caller to branch on but a status code that
/// several of these share.
/// </para>
/// </remarks>
public class ProblemDetailsShapeTests : IAsyncLifetime
{
    private readonly FakeEntryRepository _repository = new();
    private IHost _host = null!;
    private HttpClient _client = null!;
    private HttpClient _anonymousClient = null!;

    public async Task InitializeAsync()
    {
        var settings = new DmrpApiSettings
        {
            AuthClientId = "problem-test-client",
            AuthClientSecret = "problem-test-client-secret",
            SigningKey = "controller-test-signing-key-long-enough-for-hmac-sha512-which-needs-64",
            Issuer = "link-mock-dmrp-tests",
            Audience = "dmrp-api-tests",
            TokenLifetimeSeconds = 3600
        };

        _host = await new HostBuilder()
            .ConfigureWebHost(web =>
            {
                web.UseTestServer();
                web.ConfigureServices(services =>
                {
                    services.AddSingleton<IBaseEntityRepository<ReportingPlanEntryEntity>>(_repository);
                    services.AddSingleton<IOptions<DmrpApiSettings>>(Options.Create(settings));
                    services.AddScoped<IReportingPlanService, ReportingPlanService>();
                    services.AddSingleton<IAuthTokenService, AuthTokenService>();
                    services.AddSingleton<IResponseDelayService, ResponseDelayService>();

                    // The real customization is internal to the service project, so this host
                    // reproduces the two guarantees it makes rather than reaching for it: a
                    // traceId on every problem, and a detail where the framework leaves none.
                    services.AddProblemDetails(options =>
                    {
                        options.CustomizeProblemDetails = ctx =>
                        {
                            if (string.IsNullOrWhiteSpace(ctx.ProblemDetails.Detail))
                            {
                                ctx.ProblemDetails.Detail = "The request could not be completed.";
                            }

                            if (!ctx.ProblemDetails.Extensions.ContainsKey("traceId"))
                            {
                                ctx.ProblemDetails.Extensions.Add("traceId", ctx.HttpContext.TraceIdentifier);
                            }
                        };
                    });

                    services
                        .AddAuthentication(MockControllerTests.LinkCredentialAuthHandler.SchemeName)
                        .AddScheme<AuthenticationSchemeOptions, MockControllerTests.LinkCredentialAuthHandler>(
                            MockControllerTests.LinkCredentialAuthHandler.SchemeName, _ => { });
                    services.AddAuthorization(options =>
                        options.AddPolicy(PolicyNames.IsLinkAdmin, p => p.RequireAuthenticatedUser()));

                    services.AddControllers().AddApplicationPart(typeof(MockController).Assembly);
                });
                web.Configure(app =>
                {
                    app.UseStatusCodePages();
                    app.UseRouting();
                    app.UseAuthentication();
                    app.UseAuthorization();
                    app.UseEndpoints(endpoints => endpoints.MapControllers());
                });
            })
            .StartAsync();

        _anonymousClient = _host.GetTestClient();
        _client = _host.GetTestClient();
        _client.DefaultRequestHeaders.Add(
            MockControllerTests.LinkCredentialAuthHandler.HeaderName, "link-admin");
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        _anonymousClient.Dispose();
        await _host.StopAsync();
        _host.Dispose();
    }

    private ReportingPlanEntryEntity Seed(string measure = "HOB") =>
        SeedEntry(new ReportingPlanEntryEntity
        {
            Id = Guid.NewGuid().ToString(),
            FacilityId = "100",
            Component = ReportingComponents.Msc,
            Measure = measure,
            ReportingMonth = 2,
            ReportingYear = 2020,
            IsReporting = "Y",
            CreateDate = DateTime.UtcNow
        });

    private ReportingPlanEntryEntity SeedEntry(ReportingPlanEntryEntity entry)
    {
        _repository.Seed(entry);
        return entry;
    }

    private static object Body(string component = "MSC", string measure = "HOB", int? month = 2) =>
        new
        {
            facilityId = "100",
            component,
            measure,
            reportingMonth = month,
            reportingYear = 2020,
            isReporting = "Y"
        };

    private static async Task<JsonElement> ProblemAsync(HttpResponseMessage response)
    {
        var payload = await response.Content.ReadAsStringAsync();
        payload.Should().NotBeNullOrWhiteSpace("a problem response must have a body");
        return JsonDocument.Parse(payload).RootElement.Clone();
    }

    /// <summary>Every field the house style requires, present and non-empty.</summary>
    private static void ShouldBeAWellFormedProblem(JsonElement problem, int status, string title)
    {
        problem.GetProperty("status").GetInt32().Should().Be(status);
        problem.GetProperty("title").GetString().Should().Be(title);

        problem.GetProperty("type").GetString()
            .Should().NotBeNullOrWhiteSpace()
            .And.StartWith("https://", "the type is a resolvable reference, not a bare code");

        problem.GetProperty("detail").GetString()
            .Should().NotBeNullOrWhiteSpace("a status code alone does not say what went wrong");

        problem.GetProperty("traceId").GetString()
            .Should().NotBeNullOrWhiteSpace("a report of a failure must be traceable without a repro");
    }

    // ------------------------------------------------- the support surface

    [Fact]
    public async Task MalformedId_IsAWellFormedProblem()
    {
        var problem = await ProblemAsync(await _client.GetAsync("/api/mock-dmrp/entries/not-a-guid"));

        ShouldBeAWellFormedProblem(problem, 400, "Invalid Id");
        problem.GetProperty("detail").GetString().Should().Contain("Invalid Id format");
    }

    [Fact]
    public async Task AbsentEntry_NamesTheIdItLookedFor()
    {
        var id = "11111111-1111-1111-1111-111111111111";

        var problem = await ProblemAsync(await _client.GetAsync($"/api/mock-dmrp/entries/{id}"));

        ShouldBeAWellFormedProblem(problem, 404, "Entry Not Found");
        problem.GetProperty("detail").GetString().Should().Contain(id,
            "a 404 that does not say what was missing is indistinguishable from a bad route");
    }

    [Fact]
    public async Task UpdateAgainstAnAbsentEntry_SaysItNeverCreates()
    {
        var id = "11111111-1111-1111-1111-111111111111";

        var response = await _client.PutAsJsonAsync($"/api/mock-dmrp/entries/{id}", new
        {
            id,
            facilityId = "100",
            component = "MSC",
            measure = "HOB",
            reportingMonth = 2,
            reportingYear = 2020,
            isReporting = "Y"
        });

        var problem = await ProblemAsync(response);

        ShouldBeAWellFormedProblem(problem, 404, "Entry Not Found");
        problem.GetProperty("detail").GetString().Should().Contain("never creates",
            "a bare 404 from a PUT reads like a routing problem rather than a deliberate rule");
    }

    [Fact]
    public async Task DuplicateEntry_IsAWellFormedConflict()
    {
        Seed();

        var problem = await ProblemAsync(await _client.PostAsJsonAsync("/api/mock-dmrp/entries", Body()));

        ShouldBeAWellFormedProblem(problem, 409, "Duplicate Reporting Plan Entry");
    }

    [Fact]
    public async Task CadenceViolation_IsAWellFormedBadRequest()
    {
        var problem = await ProblemAsync(
            await _client.PostAsJsonAsync("/api/mock-dmrp/entries", Body(measure: "NOMONTH", month: null)));

        ShouldBeAWellFormedProblem(problem, 400, "Invalid Reporting Plan Entry");
        problem.GetProperty("detail").GetString().Should().Contain("monthly");
    }

    [Fact]
    public async Task UnknownComponent_IsAWellFormedBadRequest()
    {
        var problem = await ProblemAsync(
            await _client.PostAsJsonAsync("/api/mock-dmrp/entries", Body(component: "XYZ", measure: "BAD")));

        ShouldBeAWellFormedProblem(problem, 400, "Invalid Reporting Plan Entry");
    }

    [Fact]
    public async Task MismatchedBodyId_IsAWellFormedBadRequest()
    {
        var seeded = Seed();

        var response = await _client.PutAsJsonAsync($"/api/mock-dmrp/entries/{seeded.Id}", new
        {
            id = Guid.NewGuid().ToString(),
            facilityId = "100",
            component = "MSC",
            measure = "HOB",
            reportingMonth = 2,
            reportingYear = 2020,
            isReporting = "Y"
        });

        ShouldBeAWellFormedProblem(await ProblemAsync(response), 400, "Id Mismatch");
    }

    [Fact]
    public async Task AnOversizedDelay_IsTheFrameworksValidationProblem()
    {
        // Not the controller's "Invalid Delay". The [Range] annotation shares its bounds with
        // the service guard, so [ApiController] rejects the request before the action runs and
        // the controller's catch is unreachable over HTTP -- defence in depth for callers that
        // reach the service directly, not a response shape anyone will see.
        //
        // What matters here is that the customization still reaches a validation problem, so
        // it carries a traceId like every other error this service returns.
        var response = await _client.PutAsJsonAsync("/api/mock-dmrp/delay", new { milliseconds = 999_999 });

        var problem = await ProblemAsync(response);

        problem.GetProperty("status").GetInt32().Should().Be(400);
        problem.GetProperty("type").GetString().Should().StartWith("https://");
        problem.GetProperty("errors").GetProperty("Milliseconds").GetArrayLength()
            .Should().BeGreaterThan(0, "the offending field is named");
        problem.GetProperty("traceId").GetString().Should().NotBeNullOrWhiteSpace();
    }

    // ------------------------------------------------ the contract surface

    [Fact]
    public async Task AMissingThirdPartyToken_IsAWellFormedProblem()
    {
        // Not a bare Unauthorized(): a caller gets a traceId and a reason.
        var problem = await ProblemAsync(await _anonymousClient.GetAsync("/msc?nhsnorgid=100"));

        ShouldBeAWellFormedProblem(problem, 401, "Unauthorized");
    }

    [Fact]
    public async Task TheUnauthorizedDetail_DoesNotSayWhichCheckFailed()
    {
        // Missing, malformed and expired get one answer. Distinguishing them tells someone
        // probing the endpoint more than it tells a caller trying to fix their client.
        using var request = new HttpRequestMessage(HttpMethod.Get, "/msc?nhsnorgid=100");
        request.Headers.Add("Authorization", "Bearer not-a-token-this-service-issued");

        var withBadToken = await ProblemAsync(await _anonymousClient.SendAsync(request));
        var withNoToken = await ProblemAsync(await _anonymousClient.GetAsync("/msc?nhsnorgid=100"));

        withBadToken.GetProperty("detail").GetString()
            .Should().Be(withNoToken.GetProperty("detail").GetString());
    }

    [Fact]
    public async Task AMalformedPeriod_IsAWellFormedProblemNamingTheParameter()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/msc?nhsnorgid=100&month=May");
        request.Headers.Add("Authorization", $"Bearer {ThirdPartyToken()}");

        var problem = await ProblemAsync(await _anonymousClient.SendAsync(request));

        ShouldBeAWellFormedProblem(problem, 400, "Invalid Reporting Period");
        problem.GetProperty("detail").GetString().Should().Contain("month");
    }

    private string ThirdPartyToken()
    {
        var result = _host.Services.GetRequiredService<IAuthTokenService>()
            .Issue("client_credentials", "problem-test-client", "problem-test-client-secret", "dmrp.read");
        return result.AccessToken!;
    }

    // ---------------------------------------------------- the OAuth exception

    [Fact]
    public async Task TheTokenEndpointKeepsItsOAuthErrorShape()
    {
        // The one deliberate departure. This operation stands in for an authorization server,
        // and callers parse the documented OAuth codes -- turning it into problem details
        // would break every client library that expects them.
        var response = await _client.PostAsJsonAsync("/api/mock-dmrp/oauth2/token", new
        {
            grant_type = "client_credentials",
            client_id = "problem-test-client",
            client_secret = "wrong"
        });

        var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;

        body.GetProperty("error").GetString().Should().Be("invalid_client");
        body.TryGetProperty("traceId", out _).Should().BeFalse("this is not a problem-details body");
        body.TryGetProperty("title", out _).Should().BeFalse();
    }
}
