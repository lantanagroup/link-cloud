using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
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
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xunit;

using Task = System.Threading.Tasks.Task;
using Claim = System.Security.Claims.Claim;

namespace UnitTests.MockDmrpApi;

/// <summary>
/// Drives the support surface over real HTTP, behind authentication.
/// </summary>
/// <remarks>
/// Unlike the contract endpoints, these are guarded by Link's own scheme, so the host wires
/// authentication and authorization middleware and every request must carry a Link
/// credential. The handler below authenticates only when one is present, which is what makes
/// the 401 assertions meaningful -- a handler that always succeeds could not tell a guarded
/// endpoint from an open one.
/// </remarks>
public class MockControllerTests : IAsyncLifetime
{
    private const string ClientId = "mock-test-client";
    private const string ClientSecret = "mock-test-client-secret";

    private readonly FakeEntryRepository _repository = new();
    private IHost _host = null!;

    /// <summary>Carries a Link credential on every request.</summary>
    private HttpClient _client = null!;

    /// <summary>Carries none, for asserting that the surface is actually guarded.</summary>
    private HttpClient _anonymousClient = null!;

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
                    services.AddSingleton<IResponseDelayService, ResponseDelayService>();

                    services
                        .AddAuthentication(LinkCredentialAuthHandler.SchemeName)
                        .AddScheme<AuthenticationSchemeOptions, LinkCredentialAuthHandler>(
                            LinkCredentialAuthHandler.SchemeName, _ => { });

                    // The real policy comes from AddLinkBearerServiceAuthentication, which
                    // stands up a JWT bearer handler against an authority. Standing that up
                    // here would make a unit test depend on network metadata, so the policy
                    // is reduced to its authentication requirement -- which is exactly the
                    // part these tests are about.
                    services.AddAuthorization(options =>
                        options.AddPolicy(PolicyNames.IsLinkAdmin, p => p.RequireAuthenticatedUser()));

                    services.AddControllers()
                            .AddApplicationPart(typeof(MockController).Assembly);
                });
                web.Configure(app =>
                {
                    app.UseRouting();
                    app.UseAuthentication();
                    app.UseAuthorization();
                    app.UseEndpoints(endpoints => endpoints.MapControllers());
                });
            })
            .StartAsync();

        _anonymousClient = _host.GetTestClient();

        _client = _host.GetTestClient();
        _client.DefaultRequestHeaders.Add(LinkCredentialAuthHandler.HeaderName, "link-admin");
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        _anonymousClient.Dispose();
        await _host.StopAsync();
        _host.Dispose();
    }

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

    private static object MonthlyBody(
        string facilityId = "F1", string measure = "HOB", int? month = 5, int year = 2026, string isReporting = "Y") =>
        new
        {
            facilityId,
            component = "MSC",
            measure,
            reportingMonth = month,
            reportingYear = year,
            isReporting
        };

    // ---------------------------------------------------------------- guarded

    [Theory]
    [InlineData("GET", "/mock/search")]
    [InlineData("GET", "/mock/facilities/F1")]
    [InlineData("GET", "/mock/11111111-1111-1111-1111-111111111111")]
    [InlineData("POST", "/mock")]
    [InlineData("POST", "/mock/oauth2/token")]
    [InlineData("PUT", "/mock/11111111-1111-1111-1111-111111111111")]
    [InlineData("GET", "/mock/delay")]
    [InlineData("PUT", "/mock/delay")]
    [InlineData("DELETE", "/mock/delay")]
    [InlineData("DELETE", "/mock")]
    [InlineData("DELETE", "/mock/11111111-1111-1111-1111-111111111111")]
    [InlineData("DELETE", "/mock/facilities/F1")]
    public async Task EverySupportEndpointRequiresALinkCredential(string method, string url)
    {
        // The surface can seed, mutate and wipe the store, so no operation on it may be
        // reachable anonymously. Enumerated rather than spot-checked because a new endpoint
        // that forgets the class-level policy would otherwise ship unnoticed.
        using var request = new HttpRequestMessage(new HttpMethod(method), url);
        if (method is "POST" or "PUT")
        {
            request.Content = JsonContent.Create(MonthlyBody());
        }

        var response = await _anonymousClient.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized, "{0} {1} must be guarded", method, url);
    }

    [Fact]
    public async Task TheContractEndpointsAreNotGuardedByLinksScheme()
    {
        // They opt out with [AllowAnonymous] and validate the third party's token instead.
        // A 401 here would be right for the wrong reason, so the body is what is checked:
        // the OAuth-shaped rejection is the contract endpoint's, not the middleware's.
        var response = await _anonymousClient.GetAsync("/msc?nhsnorgid=F1&year=2026&month=5");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        response.Headers.WwwAuthenticate.Should().BeEmpty(
            "the rejection comes from the controller, not from Link's authentication handler");
    }

    // -------------------------------------------------------------- get by id

    [Fact]
    public async Task GetById_ReturnsTheEntry()
    {
        var seeded = Seed();

        var response = await _client.GetAsync($"/mock/{seeded.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var entry = await response.Content.ReadFromJsonAsync<MockEntryModel>();
        entry!.Id.Should().Be(seeded.Id);
        entry.Component.Should().Be("MSC");
        entry.Measure.Should().Be("HOB");
    }

    [Fact]
    public async Task GetById_WithNonGuid_Returns400InvalidIdFormat()
    {
        var response = await _client.GetAsync("/mock/not-a-guid");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync()).Should().Contain("Invalid Id format");
    }

    [Fact]
    public async Task GetById_WhenAbsent_Returns404()
    {
        var response = await _client.GetAsync($"/mock/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task TheLiteralRoutesWinOverTheIdRoute()
    {
        // /search, /facilities and /oauth2 must not be read as identifiers, which is how
        // they would surface: an "Invalid Id format" for a perfectly good URL.
        (await _client.GetAsync("/mock/search")).StatusCode
            .Should().Be(HttpStatusCode.NoContent);

        var byFacility = await _client.GetAsync("/mock/facilities/F1");
        byFacility.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await byFacility.Content.ReadAsStringAsync()).Should().NotContain("Invalid Id format");
    }

    // ------------------------------------------------------- facility + search

    [Fact]
    public async Task GetByFacility_ReturnsAPageAnd404WhenTheFacilityHasNothing()
    {
        Seed();
        Seed(measure: "HTCDI");
        Seed(facilityId: "F2");

        var populated = await _client.GetAsync("/mock/facilities/F1");
        populated.StatusCode.Should().Be(HttpStatusCode.OK);
        var page = await populated.Content.ReadFromJsonAsync<MockEntryPage>();
        page!.Records.Should().HaveCount(2);
        page.Metadata.PageSize.Should().Be(10);

        // 404, not 204: the facility is named in the path, so an identifier that matches
        // nothing is an absent resource rather than an empty collection.
        var empty = await _client.GetAsync("/mock/facilities/NoSuchFacility");
        empty.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await empty.Content.ReadAsStringAsync()).Should().Contain("NoSuchFacility",
            "the detail names the facility it looked for");
    }

    [Fact]
    public async Task SearchStillAnswers204WhenNothingMatches()
    {
        // The distinction the by-facility 404 rests on. Search takes its filters as query
        // parameters, so no matches is an empty result set, not a missing resource. If these
        // two ever agreed, one of them would be wrong.
        Seed();

        var byFacility = await _client.GetAsync("/mock/facilities/NoSuchFacility");
        var bySearch = await _client.GetAsync("/mock/search?facilityId=NoSuchFacility");

        byFacility.StatusCode.Should().Be(HttpStatusCode.NotFound);
        bySearch.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task GetByFacility_ShowsEveryComponent()
    {
        // Seeding and inspection span both plans, so this must not be component-scoped.
        Seed(measure: "HOB");
        Seed(measure: "HAI", month: null, component: ReportingComponents.Ps);

        var response = await _client.GetAsync("/mock/facilities/F1");

        var page = await response.Content.ReadFromJsonAsync<MockEntryPage>();
        page!.Records.Should().HaveCount(2);
        page.Records.Select(r => r.Component).Should().BeEquivalentTo("MSC", "PS");
    }

    [Fact]
    public async Task Search_WithNoFiltersSupplied_Works()
    {
        Seed();

        var response = await _client.GetAsync("/mock/search");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var page = await response.Content.ReadFromJsonAsync<MockEntryPage>();
        page!.Records.Should().ContainSingle();
    }

    [Fact]
    public async Task Search_FiltersByComponent()
    {
        Seed(measure: "HOB");
        Seed(measure: "HAI", month: null, component: ReportingComponents.Ps);

        var response = await _client.GetAsync("/mock/search?component=PS");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var page = await response.Content.ReadFromJsonAsync<MockEntryPage>();
        page!.Records.Should().ContainSingle();
        page.Records.Single().Measure.Should().Be("HAI");
    }

    [Fact]
    public async Task Search_FiltersAndSorts()
    {
        Seed(measure: "HOB");
        Seed(measure: "HTCDI");
        Seed(facilityId: "F2", measure: "HOB");

        var response = await _client.GetAsync(
            "/mock/search?facilityId=F1&measure=HOB&sortBy=Measure&sortOrder=Ascending");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var page = await response.Content.ReadFromJsonAsync<MockEntryPage>();
        page!.Records.Should().ContainSingle();
        page.Records.Single().Measure.Should().Be("HOB");
    }

    [Fact]
    public async Task Search_WithNoMatches_Returns204()
    {
        Seed();

        var response = await _client.GetAsync("/mock/search?measure=NOPE");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Search_WithSortByOutsideTheAllowedSet_Returns400NotAServerError()
    {
        var response = await _client.GetAsync("/mock/search?sortBy=DropTable");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ------------------------------------------------------------------ create

    [Fact]
    public async Task Create_Returns201WithALocationHeaderAndTheCreatedEntry()
    {
        var response = await _client.PostAsJsonAsync("/mock", MonthlyBody());

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var created = await response.Content.ReadFromJsonAsync<MockEntryModel>();
        created!.Id.Should().NotBeNullOrWhiteSpace();

        response.Headers.Location.Should().NotBeNull();
        response.Headers.Location!.ToString().Should().EndWith($"/mock/{created.Id}");

        // The Location header must actually resolve.
        var followed = await _client.GetAsync(response.Headers.Location);
        followed.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Create_AnnualEntry_OmitsTheMonth()
    {
        var response = await _client.PostAsJsonAsync("/mock", new
        {
            facilityId = "F1",
            component = "PS",
            measure = "HAI",
            reportingYear = 2026,
            isReporting = "Y"
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await response.Content.ReadFromJsonAsync<MockEntryModel>();
        created!.ReportingMonth.Should().BeNull();
        created.Component.Should().Be("PS");
    }

    [Fact]
    public async Task Create_WithDuplicateNaturalKey_Returns409AndDoesNotPersist()
    {
        Seed();

        var response = await _client.PostAsJsonAsync("/mock", MonthlyBody());

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        _repository.Entries.Should().ContainSingle();
    }

    // ----------------------------------------------------------- trimming

    [Fact]
    public async Task Create_TrimsTheKeyFieldsBeforeStoringThem()
    {
        // The sanitizer keeps the space character, so without an explicit trim " HOB" would
        // be stored verbatim.
        var response = await _client.PostAsJsonAsync("/mock", new
        {
            facilityId = " 100 ",
            component = " MSC ",
            measure = " HOB ",
            reportingMonth = 5,
            reportingYear = 2026,
            isReporting = "Y"
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var created = await response.Content.ReadFromJsonAsync<MockEntryModel>();
        created!.Measure.Should().Be("HOB");
        created.FacilityId.Should().Be("100");
        created.Component.Should().Be("MSC");
    }

    [Fact]
    public async Task APaddedMeasure_CollidesWithItsTrimmedTwin()
    {
        // The payoff. Before trimming these were two distinct rows in the natural key, so a
        // plan seeded with the padded one silently omitted the measure a consumer looked for
        // -- no error anywhere, just a short plan. Now the second create is a visible 409.
        Seed(measure: "HOB");

        var response = await _client.PostAsJsonAsync("/mock", MonthlyBody(measure: " HOB "));

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        _repository.Entries.Should().ContainSingle();
    }

    [Fact]
    public async Task APaddedFilter_FindsTheTrimmedRow()
    {
        // Trimming writes without trimming lookups would move the problem rather than fix it.
        Seed(measure: "HOB");

        var response = await _client.GetAsync("/mock/search?measure=%20HOB%20");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var page = await response.Content.ReadFromJsonAsync<MockEntryPage>();
        page!.Records.Should().ContainSingle();
    }

    [Fact]
    public async Task APaddedFacility_FindsTheTrimmedRows()
    {
        Seed(facilityId: "100");

        var response = await _client.GetAsync("/mock/facilities/%20100%20");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // ------------------------------------------------------------- paging

    [Theory]
    [InlineData("pageSize=101")]
    [InlineData("pageSize=0")]
    [InlineData("pageSize=-1")]
    [InlineData("pageNumber=-1")]
    [InlineData("pageNumber=0")]
    public async Task Search_WithPagingOutsideTheAllowedRange_Returns400(string filter)
    {
        // Rejected at the boundary rather than clamped. A caller asking for 5,000 rows and
        // silently getting 100 has no way to tell that happened.
        Seed();

        var response = await _client.GetAsync($"/mock/search?{filter}");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Theory]
    [InlineData("pageSize=1")]
    [InlineData("pageSize=100")]
    [InlineData("pageNumber=1")]
    public async Task Search_AtThePagingBoundaries_IsAccepted(string filter)
    {
        Seed();

        var response = await _client.GetAsync($"/mock/search?{filter}");

        response.StatusCode.Should().NotBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetByFacility_WithPagingOutsideTheAllowedRange_Returns400()
    {
        Seed();

        var response = await _client.GetAsync("/mock/facilities/F1?pageSize=101");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Create_WithMonthOutsideTheRange_Returns400()
    {
        var response = await _client.PostAsJsonAsync("/mock", MonthlyBody(month: 13));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        _repository.Entries.Should().BeEmpty();
    }

    [Fact]
    public async Task Create_MonthlyEntryWithNoMonth_Returns400ExplainingTheCadence()
    {
        // The rule is conditional on the component, so it cannot be a data annotation. The
        // message has to name the cadence or the caller has no way to know what was wrong.
        var response = await _client.PostAsJsonAsync("/mock", MonthlyBody(month: null));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync()).Should().Contain("monthly");
        _repository.Entries.Should().BeEmpty();
    }

    [Fact]
    public async Task Create_AnnualEntryWithAMonth_Returns400ExplainingTheCadence()
    {
        var response = await _client.PostAsJsonAsync("/mock", new
        {
            facilityId = "F1",
            component = "PS",
            measure = "HAI",
            reportingMonth = 5,
            reportingYear = 2026,
            isReporting = "Y"
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync()).Should().Contain("annually");
        _repository.Entries.Should().BeEmpty();
    }

    [Fact]
    public async Task Create_WithAnUnrecognisedComponent_Returns400ListingTheKnownOnes()
    {
        var response = await _client.PostAsJsonAsync("/mock", new
        {
            facilityId = "F1",
            component = "XYZ",
            measure = "HOB",
            reportingMonth = 5,
            reportingYear = 2026,
            isReporting = "Y"
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("MSC").And.Contain("PS");
        _repository.Entries.Should().BeEmpty();
    }

    [Fact]
    public async Task Create_WithNoComponent_Returns400()
    {
        var response = await _client.PostAsJsonAsync("/mock", new
        {
            facilityId = "F1",
            measure = "HOB",
            reportingMonth = 5,
            reportingYear = 2026,
            isReporting = "Y"
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        _repository.Entries.Should().BeEmpty();
    }

    // ------------------------------------------------------------------ update

    [Fact]
    public async Task Update_Returns202WithTheUpdatedEntry()
    {
        var seeded = Seed();

        var response = await _client.PutAsJsonAsync($"/mock/{seeded.Id}", new
        {
            id = seeded.Id,
            facilityId = "F9",
            component = "MSC",
            measure = "HTCDI",
            reportingMonth = 11,
            reportingYear = 2027,
            isReporting = "N"
        });

        response.StatusCode.Should().Be(HttpStatusCode.Accepted);
        var updated = await response.Content.ReadFromJsonAsync<MockEntryModel>();
        updated!.FacilityId.Should().Be("F9");
        updated.Measure.Should().Be("HTCDI");
    }

    [Fact]
    public async Task Update_OnAMissingEntry_Returns404AndCreatesNothing()
    {
        var absentId = Guid.NewGuid().ToString();

        var response = await _client.PutAsJsonAsync($"/mock/{absentId}", new
        {
            id = absentId,
            facilityId = "F1",
            component = "MSC",
            measure = "HOB",
            reportingMonth = 5,
            reportingYear = 2026,
            isReporting = "Y"
        });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        _repository.Entries.Should().BeEmpty("update must never upsert");
    }

    [Fact]
    public async Task Update_WithMismatchedBodyId_Returns400()
    {
        var seeded = Seed();

        var response = await _client.PutAsJsonAsync($"/mock/{seeded.Id}", new
        {
            id = Guid.NewGuid().ToString(),
            facilityId = "F1",
            component = "MSC",
            measure = "HOB",
            reportingMonth = 5,
            reportingYear = 2026,
            isReporting = "Y"
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Update_WithNonGuidRouteId_Returns400InvalidIdFormat()
    {
        var response = await _client.PutAsJsonAsync("/mock/not-a-guid", new
        {
            id = "not-a-guid",
            facilityId = "F1",
            component = "MSC",
            measure = "HOB",
            reportingMonth = 5,
            reportingYear = 2026,
            isReporting = "Y"
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync()).Should().Contain("Invalid Id format");
    }

    // ----------------------------------------------------------------- deletes

    [Fact]
    public async Task Delete_RemovesTheEntryThen404sOnTheSecondAttempt()
    {
        var seeded = Seed();

        (await _client.DeleteAsync($"/mock/{seeded.Id}")).StatusCode
            .Should().Be(HttpStatusCode.NoContent);
        (await _client.DeleteAsync($"/mock/{seeded.Id}")).StatusCode
            .Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DeleteByFacility_IsIdempotent()
    {
        Seed();
        Seed(measure: "HTCDI");
        Seed(facilityId: "F2");

        (await _client.DeleteAsync("/mock/facilities/F1")).StatusCode
            .Should().Be(HttpStatusCode.NoContent);
        _repository.Entries.Should().ContainSingle();

        // Succeeds again even though there is nothing left to remove.
        (await _client.DeleteAsync("/mock/facilities/F1")).StatusCode
            .Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task DeleteAll_EmptiesTheStore()
    {
        Seed();
        Seed(measure: "HTCDI");
        Seed(facilityId: "F2");

        var response = await _client.DeleteAsync("/mock");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        _repository.Entries.Should().BeEmpty();
    }

    // ------------------------------------------------------------------- token

    [Fact]
    public async Task IssueToken_ReturnsABearerToken()
    {
        var response = await _client.PostAsJsonAsync("/mock/oauth2/token", new
        {
            grant_type = "client_credentials",
            client_id = ClientId,
            client_secret = ClientSecret,
            scope = "dmrp.read"
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var token = await response.Content.ReadFromJsonAsync<MockTokenResponse>();
        token!.Access_token.Should().NotBeNullOrWhiteSpace();
        token.Token_type.Should().Be("Bearer");
        token.Expires_in.Should().Be(3600);
        token.Scope.Should().Be("dmrp.read");
    }

    [Fact]
    public async Task IssueToken_WithWrongSecret_Returns401WithAnOAuthErrorBody()
    {
        var response = await _client.PostAsJsonAsync("/mock/oauth2/token", new
        {
            grant_type = "client_credentials",
            client_id = ClientId,
            client_secret = "wrong",
            scope = (string?)null
        });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        // OAuth error shape, not problem details -- this operation stands in for an
        // authorization server and callers parse the documented codes.
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        document.RootElement.GetProperty("error").GetString().Should().Be("invalid_client");
    }

    [Fact]
    public async Task IssueToken_WithAnUnsupportedGrant_Returns400WithTheOAuthCode()
    {
        // The grant is a string rather than an enum precisely so an unknown value reaches
        // the service and comes back as unsupported_grant_type. Bound as an enum it would
        // fail model binding and produce a generic validation error instead.
        var response = await _client.PostAsJsonAsync("/mock/oauth2/token", new
        {
            grant_type = "password",
            client_id = ClientId,
            client_secret = ClientSecret,
            scope = (string?)null
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        document.RootElement.GetProperty("error").GetString().Should().Be("unsupported_grant_type");
    }

    [Fact]
    public async Task IssueToken_IssuesACredentialTheContractEndpointsAccept()
    {
        // The whole point of hosting the token endpoint here: a caller acquires a
        // third-party token through Link's authenticated surface, then uses it against the
        // endpoints that impersonate the third party. If the two ever diverged, seeding
        // would still work and every contract call would 401.
        Seed(measure: "HOB");

        var tokenResponse = await _client.PostAsJsonAsync("/mock/oauth2/token", new
        {
            grant_type = "client_credentials",
            client_id = ClientId,
            client_secret = ClientSecret,
            scope = "dmrp.read"
        });
        var token = await tokenResponse.Content.ReadFromJsonAsync<MockTokenResponse>();

        using var request = new HttpRequestMessage(
            HttpMethod.Get, "/msc?nhsnorgid=F1&year=2026&month=5");
        request.Headers.Add("Authorization", $"Bearer {token!.Access_token}");

        // Sent without a Link credential, proving the third-party token is what carries it.
        var response = await _anonymousClient.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    /// <summary>
    /// Authenticates a request only when it carries the test credential header, so both the
    /// authorized and the unauthorized paths are genuinely exercised.
    /// </summary>
    public sealed class LinkCredentialAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
    {
        public const string SchemeName = "LinkTest";
        public const string HeaderName = "X-Link-Test-User";

        public LinkCredentialAuthHandler(
            IOptionsMonitor<AuthenticationSchemeOptions> options,
            ILoggerFactory logger,
            UrlEncoder encoder)
            : base(options, logger, encoder)
        {
        }

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (!Request.Headers.TryGetValue(HeaderName, out var user) || string.IsNullOrWhiteSpace(user))
            {
                return Task.FromResult(AuthenticateResult.NoResult());
            }

            var identity = new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, user!)], SchemeName);
            var ticket = new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName);
            return Task.FromResult(AuthenticateResult.Success(ticket));
        }
    }
}
