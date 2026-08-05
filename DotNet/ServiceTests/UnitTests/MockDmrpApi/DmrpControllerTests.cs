using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
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
/// Drives the controllers over real HTTP so the assertions cover routing, model binding,
/// status codes and serialization -- not just the method bodies.
/// </summary>
public class DmrpControllerTests : IAsyncLifetime
{
    private const string ClientId = "test-client";
    private const string ClientSecret = "test-client-secret";

    private readonly FakeEntryRepository _repository = new();
    private IHost _host = null!;
    private HttpClient _client = null!;

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
                    services.AddScoped<DmrpController>();
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
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        await _host.StopAsync();
        _host.Dispose();
    }

    private ReportingPlanEntryEntity Seed(
        string facilityId = "F1", string measure = "HOB", int month = 5, int year = 2026, string isReporting = "Y")
    {
        var entry = new ReportingPlanEntryEntity
        {
            Id = Guid.NewGuid().ToString(),
            FacilityId = facilityId,
            Measure = measure,
            ReportingMonth = month,
            ReportingYear = year,
            IsReporting = isReporting,
            CreateDate = DateTime.UtcNow
        };

        _repository.Seed(entry);
        return entry;
    }

    private async Task<string> AcquireTokenAsync()
    {
        var response = await _client.PostAsJsonAsync("/dmrp/mock/oauth2/token", new
        {
            grant_type = "client_credentials",
            client_id = ClientId,
            client_secret = ClientSecret,
            scope = "dmrp.read"
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var token = await response.Content.ReadFromJsonAsync<AuthTokenResponse>();
        return token!.Access_token;
    }

    // ------------------------------------------------------------- get by id

    [Fact]
    public async Task GetById_ReturnsTheEntry()
    {
        var seeded = Seed();

        var response = await _client.GetAsync($"/dmrp/mock/{seeded.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var entry = await response.Content.ReadFromJsonAsync<ReportingPlanEntry>();
        entry!.Id.Should().Be(seeded.Id);
        entry.Measure.Should().Be("HOB");
    }

    [Fact]
    public async Task GetById_WithNonGuid_Returns400InvalidIdFormat()
    {
        var response = await _client.GetAsync("/dmrp/mock/not-a-guid");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync()).Should().Contain("Invalid Id format");
    }

    [Fact]
    public async Task GetById_WhenAbsent_Returns404()
    {
        var response = await _client.GetAsync($"/dmrp/mock/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ThePrefixIsAppliedExactlyOnce()
    {
        // The spec's server URL carries the dmrp/mock path, so NSwag emits a class-level
        // [Route("dmrp/mock")] on the generated base -- and DmrpController declares the same
        // route itself. Attribute routing takes the most-derived declaration rather than
        // combining them, so the prefix is not doubled. If that ever changed, every route
        // would move to /dmrp/mock/dmrp/mock and this test is what would say so.
        (await _client.GetAsync("/dmrp/mock/search")).StatusCode
            .Should().Be(HttpStatusCode.NoContent, "the prefix is applied once");

        (await _client.GetAsync("/dmrp/mock/dmrp/mock/search")).StatusCode
            .Should().Be(HttpStatusCode.NotFound, "the prefix must not be applied twice");
    }

    [Fact]
    public async Task GetById_DoesNotShadowTheLiteralRoutes()
    {
        // /search and /reporting-plans must win over /{id}; otherwise they would be read
        // as identifiers and answered with "Invalid Id format".
        var search = await _client.GetAsync("/dmrp/mock/search");
        search.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var plan = await _client.GetAsync("/dmrp/mock/reporting-plans?facilityId=F1&reportingMonth=5&reportingYear=2026");
        plan.StatusCode.Should().Be(HttpStatusCode.Unauthorized, "it is gated on a token, not treated as an id");
    }

    // ------------------------------------------------------- facility + search

    [Fact]
    public async Task GetByFacility_ReturnsAPageAnd204WhenEmpty()
    {
        Seed();
        Seed(measure: "HTCDI");
        Seed(facilityId: "F2");

        var populated = await _client.GetAsync("/dmrp/mock/facilities/F1");
        populated.StatusCode.Should().Be(HttpStatusCode.OK);
        var page = await populated.Content.ReadFromJsonAsync<ReportingPlanEntryPage>();
        page!.Records.Should().HaveCount(2);
        page.Metadata.PageSize.Should().Be(10, "the generated default must survive the override");

        var empty = await _client.GetAsync("/dmrp/mock/facilities/NoSuchFacility");
        empty.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Search_WithNoFiltersSupplied_Works()
    {
        // Every filter is optional in the contract. This fails with 400 if the override
        // forgets to restate the generated string parameters as nullable.
        Seed();

        var response = await _client.GetAsync("/dmrp/mock/search");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var page = await response.Content.ReadFromJsonAsync<ReportingPlanEntryPage>();
        page!.Records.Should().ContainSingle();
    }

    [Fact]
    public async Task Search_FiltersAndSorts()
    {
        Seed(measure: "HOB");
        Seed(measure: "HTCDI");
        Seed(facilityId: "F2", measure: "HOB");

        var response = await _client.GetAsync(
            "/dmrp/mock/search?facilityId=F1&measure=HOB&sortBy=Measure&sortOrder=Ascending");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var page = await response.Content.ReadFromJsonAsync<ReportingPlanEntryPage>();
        page!.Records.Should().ContainSingle();
        page.Records.Single().Measure.Should().Be("HOB");
    }

    [Fact]
    public async Task Search_WithNoMatches_Returns204()
    {
        Seed();

        var response = await _client.GetAsync("/dmrp/mock/search?measure=NOPE");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Search_WithSortByOutsideTheAllowedSet_Returns400NotAServerError()
    {
        var response = await _client.GetAsync("/dmrp/mock/search?sortBy=DropTable");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ------------------------------------------------------------------ create

    [Fact]
    public async Task Create_Returns201WithALocationHeaderAndTheCreatedEntry()
    {
        var response = await _client.PostAsJsonAsync("/dmrp/mock", new
        {
            facilityId = "F1",
            measure = "HOB",
            reportingMonth = 5,
            reportingYear = 2026,
            isReporting = "Y"
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var created = await response.Content.ReadFromJsonAsync<ReportingPlanEntry>();
        created!.Id.Should().NotBeNullOrWhiteSpace();

        response.Headers.Location.Should().NotBeNull();
        response.Headers.Location!.ToString().Should().EndWith($"/dmrp/mock/{created.Id}");

        // The Location header must actually resolve.
        var followed = await _client.GetAsync(response.Headers.Location);
        followed.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Create_WithDuplicateNaturalKey_Returns409AndDoesNotPersist()
    {
        Seed();

        var response = await _client.PostAsJsonAsync("/dmrp/mock", new
        {
            facilityId = "F1",
            measure = "HOB",
            reportingMonth = 5,
            reportingYear = 2026,
            isReporting = "Y"
        });

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        _repository.Entries.Should().ContainSingle();
    }

    [Fact]
    public async Task Create_WithMonthOutsideTheContractRange_Returns400()
    {
        // Range validation comes from the spec via generated data annotations.
        var response = await _client.PostAsJsonAsync("/dmrp/mock", new
        {
            facilityId = "F1",
            measure = "HOB",
            reportingMonth = 13,
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

        var response = await _client.PutAsJsonAsync($"/dmrp/mock/{seeded.Id}", new
        {
            id = seeded.Id,
            facilityId = "F9",
            measure = "HTCDI",
            reportingMonth = 11,
            reportingYear = 2027,
            isReporting = "N"
        });

        response.StatusCode.Should().Be(HttpStatusCode.Accepted);
        var updated = await response.Content.ReadFromJsonAsync<ReportingPlanEntry>();
        updated!.FacilityId.Should().Be("F9");
        updated.Measure.Should().Be("HTCDI");
    }

    [Fact]
    public async Task Update_OnAMissingEntry_Returns404AndCreatesNothing()
    {
        var absentId = Guid.NewGuid().ToString();

        var response = await _client.PutAsJsonAsync($"/dmrp/mock/{absentId}", new
        {
            id = absentId,
            facilityId = "F1",
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

        var response = await _client.PutAsJsonAsync($"/dmrp/mock/{seeded.Id}", new
        {
            id = Guid.NewGuid().ToString(),
            facilityId = "F1",
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
        var response = await _client.PutAsJsonAsync("/dmrp/mock/not-a-guid", new
        {
            id = "not-a-guid",
            facilityId = "F1",
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

        (await _client.DeleteAsync($"/dmrp/mock/{seeded.Id}")).StatusCode
            .Should().Be(HttpStatusCode.NoContent);
        (await _client.DeleteAsync($"/dmrp/mock/{seeded.Id}")).StatusCode
            .Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DeleteByFacility_IsIdempotent()
    {
        Seed();
        Seed(measure: "HTCDI");
        Seed(facilityId: "F2");

        (await _client.DeleteAsync("/dmrp/mock/facilities/F1")).StatusCode
            .Should().Be(HttpStatusCode.NoContent);
        _repository.Entries.Should().ContainSingle();

        // Succeeds again even though there is nothing left to remove.
        (await _client.DeleteAsync("/dmrp/mock/facilities/F1")).StatusCode
            .Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task DeleteAll_EmptiesTheStore()
    {
        Seed();
        Seed(measure: "HTCDI");
        Seed(facilityId: "F2");

        var response = await _client.DeleteAsync("/dmrp/mock");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        _repository.Entries.Should().BeEmpty();
    }

    // -------------------------------------------------------- token + the plan

    [Fact]
    public async Task IssueToken_ReturnsABearerToken()
    {
        var response = await _client.PostAsJsonAsync("/dmrp/mock/oauth2/token", new
        {
            grant_type = "client_credentials",
            client_id = ClientId,
            client_secret = ClientSecret,
            scope = "dmrp.read"
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var token = await response.Content.ReadFromJsonAsync<AuthTokenResponse>();
        token!.Access_token.Should().NotBeNullOrWhiteSpace();
        token.Token_type.Should().Be(AuthTokenResponseToken_type.Bearer);
        token.Expires_in.Should().Be(3600);
    }

    [Fact]
    public async Task IssueToken_WithWrongSecret_Returns401WithAnOAuthErrorBody()
    {
        var response = await _client.PostAsJsonAsync("/dmrp/mock/oauth2/token", new
        {
            grant_type = "client_credentials",
            client_id = ClientId,
            client_secret = "wrong",
            scope = (string?)null
        });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        // OAuth error shape, not problem details -- this operation stands in for an
        // authorization server and callers parse the documented codes.
        var payload = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(payload);
        document.RootElement.GetProperty("error").GetString().Should().Be("invalid_client");
    }

    [Fact]
    public async Task GetReportingPlan_WithoutAToken_Returns401()
    {
        var response = await _client.GetAsync(
            "/dmrp/mock/reporting-plans?facilityId=F1&reportingMonth=5&reportingYear=2026");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetReportingPlan_WithAToken_ListsOnlyEnrolledMeasures()
    {
        Seed(measure: "HOB");
        Seed(measure: "HTCDI", isReporting: "N");
        var token = await AcquireTokenAsync();

        using var request = new HttpRequestMessage(
            HttpMethod.Get, "/dmrp/mock/reporting-plans?facilityId=F1&reportingMonth=5&reportingYear=2026");
        request.Headers.Add("Authorization", $"Bearer {token}");

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var plan = await response.Content.ReadFromJsonAsync<ReportingPlanResponse>();
        plan!.Measures.Should().ContainSingle();
        plan.Measures.Single().Measure.Should().Be("HOB");
        plan.Measures.Should().NotContain(m => m.Measure == "HTCDI");
    }

    [Fact]
    public async Task GetReportingPlan_ForAFacilityEnrolledInNothing_Returns200WithAnEmptyArray()
    {
        // Not 204 and not 404. An empty plan is a meaningful answer, and this is the one
        // place the contract deliberately departs from the empty-list convention used by
        // search and get-by-facility.
        var token = await AcquireTokenAsync();

        using var request = new HttpRequestMessage(
            HttpMethod.Get, "/dmrp/mock/reporting-plans?facilityId=Nobody&reportingMonth=5&reportingYear=2026");
        request.Headers.Add("Authorization", $"Bearer {token}");

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var plan = await response.Content.ReadFromJsonAsync<ReportingPlanResponse>();
        plan!.Measures.Should().NotBeNull();
        plan.Measures.Should().BeEmpty();
    }

    // ------------------------------------------------ NHSN Auth simulation route

    [Fact]
    public async Task AuthTestAlias_IssuesAnInterchangeableToken()
    {
        var aliasResponse = await _client.GetAsync(
            $"/dmrp/mock/auth-test?clientId={ClientId}&clientSecret={ClientSecret}&scope=dmrp.read");

        aliasResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var aliasToken = await aliasResponse.Content.ReadFromJsonAsync<AuthTokenResponse>();
        aliasToken!.Token_type.Should().Be(AuthTokenResponseToken_type.Bearer);
        aliasToken.Expires_in.Should().Be(3600);
        aliasToken.Scope.Should().Be("dmrp.read");

        // The token the alias issues must be accepted by the contract endpoint.
        using var request = new HttpRequestMessage(
            HttpMethod.Get, "/dmrp/mock/reporting-plans?facilityId=F1&reportingMonth=5&reportingYear=2026");
        request.Headers.Add("Authorization", $"Bearer {aliasToken.Access_token}");

        (await _client.SendAsync(request)).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task AuthTest_RejectsABadSecretTheSameWayAsTheTokenEndpoint()
    {
        var response = await _client.GetAsync(
            $"/dmrp/mock/auth-test?clientId={ClientId}&clientSecret=wrong");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await response.Content.ReadAsStringAsync()).Should().Contain("invalid_client");
    }

}
