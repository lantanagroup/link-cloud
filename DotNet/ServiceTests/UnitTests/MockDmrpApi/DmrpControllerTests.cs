using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using LantanaGroup.Link.MockDmrpApi.Application.Services;
using LantanaGroup.Link.MockDmrpApi.Contracts.Generated;
using LantanaGroup.Link.MockDmrpApi.Domain.Entities;
using LantanaGroup.Link.MockDmrpApi.Presentation.Controllers;
using LantanaGroup.Link.MockDmrpApi.Settings;
using LantanaGroup.Link.Shared.Domain.Repositories.Interfaces;
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
/// Drives the two contract endpoints over real HTTP, so the assertions cover routing, model
/// binding, status codes and serialization -- not just the method bodies.
/// </summary>
/// <remarks>
/// Both endpoints are placeholders whose shape is expected to change when the published
/// contract arrives, so these tests assert <em>behaviour</em> -- which component is served,
/// which rows are excluded, how an empty plan reads, what an unauthenticated call gets -- and
/// avoid pinning field-level detail that the next contract is likely to move.
/// <para>
/// Link's authentication is deliberately absent from this host. These endpoints carry
/// <c>[AllowAnonymous]</c> and check the third party's token themselves, and a host with no
/// authentication middleware is the sharpest way to prove that: if the attribute were ever
/// dropped, the 401s below would come from the wrong system, and the token-bearing tests
/// would fail outright.
/// </para>
/// </remarks>
public class DmrpControllerTests : IAsyncLifetime
{
    private const string ClientId = "contract-test-client";
    private const string ClientSecret = "contract-test-client-secret";

    private readonly FakeEntryRepository _repository = new();
    private IHost _host = null!;
    private HttpClient _client = null!;
    private IAuthTokenService _tokens = null!;

    public async Task InitializeAsync()
    {
        var settings = new DmrpApiSettings
        {
            AuthClientId = ClientId,
            AuthClientSecret = ClientSecret,
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
                    services.AddControllers()
                            .AddApplicationPart(typeof(DmrpController).Assembly);
                });
                web.Configure(app =>
                {
                    app.UseRouting();
                    app.UseEndpoints(endpoints => endpoints.MapControllers());
                });
            })
            .StartAsync();

        _client = _host.GetTestClient();
        _tokens = _host.Services.GetRequiredService<IAuthTokenService>();
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        await _host.StopAsync();
        _host.Dispose();
    }

    /// <summary>Seeds a monthly (MSC) entry.</summary>
    private ReportingPlanEntryEntity Seed(
        string facilityId = "F1",
        string measure = "HOB",
        int? month = 5,
        int year = 2026,
        string isReporting = "Y",
        string component = ReportingComponents.Msc)
    {
        var entry = new ReportingPlanEntryEntity
        {
            Id = Guid.NewGuid().ToString(),
            FacilityId = facilityId,
            Component = component,
            Measure = measure,
            ReportingMonth = month,
            ReportingYear = year,
            IsReporting = isReporting,
            CreateDate = DateTime.UtcNow
        };

        _repository.Seed(entry);
        return entry;
    }

    /// <summary>Seeds an annual (PS) entry, which carries no month.</summary>
    private ReportingPlanEntryEntity SeedAnnual(
        string facilityId = "F1", string measure = "HAI", int year = 2026, string isReporting = "Y") =>
        Seed(facilityId, measure, month: null, year, isReporting, ReportingComponents.Ps);

    /// <summary>
    /// Mints a third-party token directly rather than through the support surface, so these
    /// tests do not depend on a host that has Link's authentication wired up.
    /// </summary>
    private string ThirdPartyToken()
    {
        var result = _tokens.Issue("client_credentials", ClientId, ClientSecret, "dmrp.read");
        result.Succeeded.Should().BeTrue();
        return result.AccessToken!;
    }

    private async Task<HttpResponseMessage> GetAuthorizedAsync(string url)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Add("Authorization", $"Bearer {ThirdPartyToken()}");
        return await _client.SendAsync(request);
    }

    // -------------------------------------------------------------- routing

    [Fact]
    public async Task TheContractEndpointsSitAtTheRootWithNoPrefix()
    {
        // The spec's server URL no longer carries a path, so NSwag emits no class-level
        // route and both operations land at the root. Repointing a consumer at the real
        // service is then a base-URL change and nothing else. If a prefix ever crept back
        // in -- via the spec's server URL or a stray [Route] -- this is what would say so.
        Seed();

        (await GetAuthorizedAsync("/msc?facilityId=F1&reportingMonth=5&reportingYear=2026"))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        (await GetAuthorizedAsync("/dmrp/mock/msc?facilityId=F1&reportingMonth=5&reportingYear=2026"))
            .StatusCode.Should().Be(HttpStatusCode.NotFound, "the old prefix must be gone");
    }

    [Fact]
    public async Task TheSupportSurfaceCannotBeServedWithoutAuthorizationMiddleware()
    {
        // This host wires no authorization middleware, and ASP.NET refuses outright to
        // serve an endpoint that carries authorization metadata without it -- so reaching
        // DELETE /mock throws rather than wiping the store.
        //
        // That refusal is the assertion. It holds only while MockController actually
        // carries [Authorize]; drop the attribute and this call quietly succeeds instead,
        // which is precisely the regression worth catching.
        var act = async () => await _client.DeleteAsync("/mock");

        (await act.Should().ThrowAsync<InvalidOperationException>())
            .WithMessage("*authorization metadata*");
        _repository.Entries.Should().BeEmpty();
    }

    // ------------------------------------------------------------------ /msc

    [Fact]
    public async Task GetMonthlyPlan_WithoutAToken_Returns401()
    {
        var response = await _client.GetAsync("/msc?facilityId=F1&reportingMonth=5&reportingYear=2026");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetMonthlyPlan_WithAnUnrelatedToken_Returns401()
    {
        // A token this service did not sign must not be accepted, or the endpoint would be
        // effectively anonymous to anyone who can send a plausible-looking bearer.
        Seed();

        using var request = new HttpRequestMessage(
            HttpMethod.Get, "/msc?facilityId=F1&reportingMonth=5&reportingYear=2026");
        request.Headers.Add("Authorization", "Bearer not-a-token-this-service-issued");

        (await _client.SendAsync(request)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetMonthlyPlan_ListsOnlyEnrolledMeasuresForThePeriod()
    {
        Seed(measure: "HOB");
        Seed(measure: "HTCDI", isReporting: "N");
        Seed(measure: "OTHER", month: 6);

        var response = await GetAuthorizedAsync("/msc?facilityId=F1&reportingMonth=5&reportingYear=2026");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var plan = await response.Content.ReadFromJsonAsync<ReportingPlanResponse>();
        plan!.FacilityId.Should().Be("F1");
        plan.ReportingMonth.Should().Be(5);
        plan.ReportingYear.Should().Be(2026);
        plan.Measures.Should().ContainSingle();
        plan.Measures.Single().Measure.Should().Be("HOB");
    }

    [Fact]
    public async Task GetMonthlyPlan_DoesNotServePatientSafetyEntries()
    {
        // The two endpoints share a table, so component isolation is the property that
        // keeps them from becoming one another. A patient-safety measure surfacing in the
        // medicine plan is silent -- the response still looks well formed.
        Seed(measure: "HOB");
        SeedAnnual(measure: "HAI");

        var response = await GetAuthorizedAsync("/msc?facilityId=F1&reportingMonth=5&reportingYear=2026");

        var plan = await response.Content.ReadFromJsonAsync<ReportingPlanResponse>();
        plan!.Measures.Should().ContainSingle();
        plan.Measures.Single().Measure.Should().Be("HOB");
    }

    [Fact]
    public async Task GetMonthlyPlan_ForAFacilityEnrolledInNothing_Returns200WithAnEmptyArray()
    {
        // Not 204 and not 404. An empty plan is a meaningful answer -- "enrolled in
        // nothing" -- and the caller iterates measures unconditionally.
        var response = await GetAuthorizedAsync("/msc?facilityId=Nobody&reportingMonth=5&reportingYear=2026");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var plan = await response.Content.ReadFromJsonAsync<ReportingPlanResponse>();
        plan!.Measures.Should().NotBeNull();
        plan.Measures.Should().BeEmpty();
    }

    [Fact]
    public async Task GetMonthlyPlan_WithoutTheRequiredQueryParameters_Returns400()
    {
        // The parameters are required in the contract, so the generated data annotations
        // reject the call before the token is even looked at.
        var response = await GetAuthorizedAsync("/msc?facilityId=F1");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ------------------------------------------------------------ /ps/annual

    [Fact]
    public async Task GetAnnualPlan_WithoutAToken_Returns401()
    {
        var response = await _client.GetAsync("/ps/annual?facilityId=F1&reportingYear=2026");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetAnnualPlan_ListsTheYearsMeasuresAndOmitsTheMonth()
    {
        SeedAnnual(measure: "HAI");
        SeedAnnual(measure: "SSI");
        SeedAnnual(measure: "OLD", year: 2025);

        var response = await GetAuthorizedAsync("/ps/annual?facilityId=F1&reportingYear=2026");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var plan = await response.Content.ReadFromJsonAsync<ReportingPlanResponse>();
        plan!.FacilityId.Should().Be("F1");
        plan.ReportingYear.Should().Be(2026);
        plan.ReportingMonth.Should().BeNull("an annual plan covers no particular month");
        plan.Measures.Select(m => m.Measure).Should().BeEquivalentTo("HAI", "SSI");
    }

    [Fact]
    public async Task GetAnnualPlan_DoesNotServeMedicineEntries()
    {
        // The annual query deliberately does not filter on month. Without the component
        // in the predicate that omission would pull in every monthly entry for the year.
        SeedAnnual(measure: "HAI");
        Seed(measure: "HOB");
        Seed(measure: "HTCDI", month: 6);

        var response = await GetAuthorizedAsync("/ps/annual?facilityId=F1&reportingYear=2026");

        var plan = await response.Content.ReadFromJsonAsync<ReportingPlanResponse>();
        plan!.Measures.Should().ContainSingle();
        plan.Measures.Single().Measure.Should().Be("HAI");
    }

    [Fact]
    public async Task GetAnnualPlan_ExcludesEntriesNotBeingReported()
    {
        SeedAnnual(measure: "HAI");
        SeedAnnual(measure: "SSI", isReporting: "N");

        var response = await GetAuthorizedAsync("/ps/annual?facilityId=F1&reportingYear=2026");

        var plan = await response.Content.ReadFromJsonAsync<ReportingPlanResponse>();
        plan!.Measures.Should().ContainSingle();
        plan.Measures.Single().Measure.Should().Be("HAI");
    }

    [Fact]
    public async Task GetAnnualPlan_ForAFacilityEnrolledInNothing_Returns200WithAnEmptyArray()
    {
        var response = await GetAuthorizedAsync("/ps/annual?facilityId=Nobody&reportingYear=2026");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var plan = await response.Content.ReadFromJsonAsync<ReportingPlanResponse>();
        plan!.Measures.Should().BeEmpty();
    }

    [Fact]
    public async Task GetAnnualPlan_DoesNotAcceptAReportingMonth()
    {
        // A month on an annual request would be quietly ignored by the handler. Proving it
        // is not bound at all keeps a caller from believing the plan was month-scoped.
        SeedAnnual(measure: "HAI");
        Seed(measure: "HOB");

        var response = await GetAuthorizedAsync("/ps/annual?facilityId=F1&reportingYear=2026&reportingMonth=5");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var plan = await response.Content.ReadFromJsonAsync<ReportingPlanResponse>();
        plan!.ReportingMonth.Should().BeNull();
        plan.Measures.Should().ContainSingle();
        plan.Measures.Single().Measure.Should().Be("HAI");
    }

    // -------------------------------------------------------- shared behaviour

    [Fact]
    public async Task BothEndpointsAcceptTheSameToken()
    {
        // One authorization server stands behind both, so a caller acquires a token once.
        Seed(measure: "HOB");
        SeedAnnual(measure: "HAI");

        var token = ThirdPartyToken();

        foreach (var url in new[]
                 {
                     "/msc?facilityId=F1&reportingMonth=5&reportingYear=2026",
                     "/ps/annual?facilityId=F1&reportingYear=2026"
                 })
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Add("Authorization", $"Bearer {token}");

            (await _client.SendAsync(request)).StatusCode
                .Should().Be(HttpStatusCode.OK, "{0} must accept the same token", url);
        }
    }

    [Fact]
    public async Task BothEndpointsScopeToTheRequestedFacility()
    {
        Seed(facilityId: "F2", measure: "HOB");
        SeedAnnual(facilityId: "F2", measure: "HAI");

        var monthly = await (await GetAuthorizedAsync("/msc?facilityId=F1&reportingMonth=5&reportingYear=2026"))
            .Content.ReadFromJsonAsync<ReportingPlanResponse>();
        var annual = await (await GetAuthorizedAsync("/ps/annual?facilityId=F1&reportingYear=2026"))
            .Content.ReadFromJsonAsync<ReportingPlanResponse>();

        monthly!.Measures.Should().BeEmpty();
        annual!.Measures.Should().BeEmpty();
    }
}
