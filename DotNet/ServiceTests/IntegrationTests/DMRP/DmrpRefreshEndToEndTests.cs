using LantanaGroup.Link.DMRP.Api;
using LantanaGroup.Link.DMRP.Business;
using LantanaGroup.Link.DMRP.Config;
using LantanaGroup.Link.DMRP.Data.Entities;
using LantanaGroup.Link.MockDmrpApi.Application.Services;
using LantanaGroup.Link.MockDmrpApi.Domain.Entities;
using LantanaGroup.Link.MockDmrpApi.Presentation.Controllers;
using LantanaGroup.Link.MockDmrpApi.Settings;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Domain.Repositories.Implementations;
using LantanaGroup.Link.Shared.Domain.Repositories.Interceptors;
using LantanaGroup.Link.Shared.Domain.Repositories.Interfaces;
using LantanaGroup.Link.Tenant.Repository.Context;
using Link.Authorization.Policies;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using UnitTests.MockDmrpApi;
using Task = System.Threading.Tasks.Task;

// Both sides of this test define their own component constants, which is the point of the test:
// they are two systems that have to agree, not one system with a shared type.
using DmrpComponents = LantanaGroup.Link.MockDmrpApi.Domain.Entities.ReportingComponents;
using LinkComponents = LantanaGroup.Link.DMRP.Data.Entities.ReportingComponents;
using DmrpApiSettings = LantanaGroup.Link.MockDmrpApi.Settings.DmrpApiSettings;
using LinkDmrpApiSettings = LantanaGroup.Link.DMRP.Config.DmrpApiSettings;

namespace IntegrationTests.DMRP;

/// <summary>
/// Drives a refresh the whole way through: Link's client asks the real mock for a token, calls both
/// contract operations, and the sync writes what came back into Link's own database.
/// </summary>
/// <remarks>
/// Every other test on this path stubs one side of it. The client's own tests stub the transport, so
/// they prove what the client sends and never what the service accepts; the mock's tests drive it
/// over HTTP with a hand-written client, so they prove what it serves and never that Link can read
/// it. Two defects lived in the gap between those - Link posted a form to an endpoint that takes
/// JSON, and the endpoint required a Link token the client never sends - and neither suite could
/// have caught either, because neither ever had both halves in the same process.
/// <para>
/// So the mock here is the real one: its controllers, its token service, its validation. Only the
/// two databases are test doubles, and only because a database is not what this is about.
/// </para>
/// </remarks>
[Trait("Category", "IntegrationTests")]
public class DmrpRefreshEndToEndTests : IAsyncLifetime
{
    private const string FacilityId = "100";
    private const string ClientId = "link-cloud-dev";
    private const string ClientSecret = "end-to-end-client-secret";
    private const int Month = 10;
    private const int Year = 2026;

    private readonly FakeEntryRepository _dmrpEntries = new();
    private readonly SqliteConnection _linkConnection = new("Data Source=:memory:");

    private IHost _dmrp = null!;
    private HttpClient _dmrpClient = null!;

    public async Task InitializeAsync()
    {
        _linkConnection.Open();

        var settings = new DmrpApiSettings
        {
            AuthClientId = ClientId,
            AuthClientSecret = ClientSecret,
            SigningKey = "end-to-end-signing-key-long-enough-for-hmac-sha512-which-needs-64-bytes",
            Issuer = "link-mock-dmrp-e2e",
            Audience = "dmrp-api-e2e",
            TokenLifetimeSeconds = 3600
        };

        _dmrp = await new HostBuilder()
            .ConfigureWebHost(web =>
            {
                web.UseTestServer();
                web.ConfigureServices(services =>
                {
                    services.AddSingleton<IBaseEntityRepository<ReportingPlanEntryEntity>>(_dmrpEntries);
                    services.AddSingleton<IOptions<DmrpApiSettings>>(Options.Create(settings));
                    services.AddScoped<IReportingPlanService, ReportingPlanService>();
                    services.AddSingleton<IAuthTokenService, AuthTokenService>();
                    services.AddSingleton<IResponseDelayService, ResponseDelayService>();
                    services.AddLogging(logging => logging.SetMinimumLevel(LogLevel.Warning));

                    // The contract endpoints are anonymous, but [AllowAnonymous] is still
                    // authorization metadata: without the middleware present the pipeline refuses
                    // to serve them at all. The support surface's own policy is registered closed,
                    // because nothing in this test should be able to reach it.
                    services.AddAuthorization(options =>
                        options.AddPolicy(PolicyNames.IsLinkAdmin, p => p.RequireAssertion(_ => false)));

                    services.AddControllers().AddApplicationPart(typeof(DmrpController).Assembly);
                });

                // No authentication scheme: the contract endpoints carry no Link credential and
                // check the third party's token themselves, which is exactly how they are reached
                // in a deployed environment.
                web.Configure(app =>
                {
                    app.UseRouting();
                    app.UseAuthorization();
                    app.UseEndpoints(endpoints => endpoints.MapControllers());
                });
            })
            .StartAsync();

        _dmrpClient = _dmrp.GetTestClient();
    }

    public async Task DisposeAsync()
    {
        _dmrpClient.Dispose();
        await _dmrp.StopAsync();
        _dmrp.Dispose();
        _linkConnection.Dispose();
    }

    /// <summary>Hands Link's client a connection to the in-process mock rather than the network.</summary>
    private sealed class TestServerHttpClientFactory : IHttpClientFactory
    {
        private readonly IHost _host;

        public TestServerHttpClientFactory(IHost host) => _host = host;

        public HttpClient CreateClient(string name) => _host.GetTestClient();
    }

    private TenantDbContext CreateLinkContext()
    {
        var builder = new DbContextOptionsBuilder<TenantDbContext>();
        builder.UseSqlite(_linkConnection);
        builder.AddInterceptors(new UpdateBaseEntityInterceptor());

        var context = new TenantDbContext(builder.Options);
        context.Database.EnsureCreated();
        return context;
    }

    private DmrpReportingPlanSync CreateSync(TenantDbContext link)
    {
        var options = Options.Create(new DmrpSettings
        {
            Enabled = true,
            Api = new LinkDmrpApiSettings
            {
                // The test server ignores the host but still needs an absolute address.
                BaseUrl = "http://localhost/",
                TokenUrl = "http://localhost/api/mock-dmrp/oauth2/token",
                ClientId = ClientId,
                ClientSecret = ClientSecret
            }
        });

        var factory = new TestServerHttpClientFactory(_dmrp);

        var tokens = new DmrpApiTokenProvider(factory, options,
            NullLogger<DmrpApiTokenProvider>.Instance, TimeProvider.System);

        var client = new DmrpApiClient(factory, tokens, options, NullLogger<DmrpApiClient>.Instance);

        return new DmrpReportingPlanSync(client,
            new EntityRepository<FacilityReportingPlan, TenantDbContext>(link),
            new EntityRepository<MeasureMapping, TenantDbContext>(link),
            NullLogger<DmrpReportingPlanSync>.Instance);
    }

    private void SeedDmrp(string measure, string component = DmrpComponents.Msc) =>
        _dmrpEntries.Seed(new ReportingPlanEntryEntity
        {
            Id = Guid.NewGuid().ToString(),
            FacilityId = FacilityId,
            Component = component,
            Measure = measure,
            ReportingMonth = Month,
            ReportingYear = Year,
            IsReporting = "Y",
            CreateDate = DateTime.UtcNow
        });

    private async Task RemoveFromDmrpAsync(string measure)
    {
        foreach (var entry in _dmrpEntries.Entries.Where(e => e.Measure == measure).ToList())
        {
            await _dmrpEntries.DeleteAsync(entry, CancellationToken.None);
        }
    }

    [Fact]
    public async Task ARefreshCarriesBothComponentsFromDmrpIntoLink()
    {
        SeedDmrp("HOB");
        SeedDmrp("CAUTI");
        SeedDmrp("HAI", DmrpComponents.Ps);

        using var link = CreateLinkContext();

        link.MeasureMappings.Add(new MeasureMapping
        {
            Measure = "HOB",
            DQM = "NHSNdQMAcuteCareHospitalInitialPopulation",
            Frequency = Frequency.Monthly
        });

        await link.SaveChangesAsync();

        var result = await CreateSync(link).SyncAsync(FacilityId, Month, Year);

        // Three enrollments across two operations, one of them mapped.
        Assert.Equal(3, result.Recorded);
        Assert.Equal(2, result.Unmapped);

        var plans = await link.FacilityReportingPlans.OrderBy(p => p.Measure).ToListAsync();

        Assert.Equal(["CAUTI", "HAI", "HOB"], plans.Select(p => p.Measure));
        Assert.Equal(LinkComponents.Ps, plans.Single(p => p.Measure == "HAI").Component);
        Assert.NotNull(plans.Single(p => p.Measure == "HOB").MeasureMappingId);
        Assert.All(plans, p => Assert.Equal(Month, p.ReportingMonth));
    }

    [Fact]
    public async Task ASecondRefreshWithdrawsWhatDmrpStoppedReturning()
    {
        SeedDmrp("HOB");
        SeedDmrp("CAUTI");

        using var link = CreateLinkContext();

        await CreateSync(link).SyncAsync(FacilityId, Month, Year);

        // DMRP conveys a withdrawal only by no longer returning the measure, so CAUTI is removed
        // from what the service will serve.
        await RemoveFromDmrpAsync("CAUTI");

        var result = await CreateSync(link).SyncAsync(FacilityId, Month, Year);

        Assert.Equal(1, result.Withdrawn);

        link.ChangeTracker.Clear();

        Assert.False((await link.FacilityReportingPlans.SingleAsync(p => p.Measure == "CAUTI")).IsReporting);
        Assert.True((await link.FacilityReportingPlans.SingleAsync(p => p.Measure == "HOB")).IsReporting);
    }

    [Fact]
    public async Task ARefreshAgainstAFacilityDmrpKnowsNothingAboutWritesNothing()
    {
        using var link = CreateLinkContext();

        var result = await CreateSync(link).SyncAsync("no-such-facility", Month, Year);

        Assert.Equal(DmrpSyncResult.Nothing, result);
        Assert.Empty(await link.FacilityReportingPlans.ToListAsync());
    }

    [Fact]
    public async Task TheClientAndTheTokenEndpointAgreeOnTheWireFormat()
    {
        // The regression this whole class exists for. Link posted a form to an endpoint that binds
        // JSON, which answered 415 - invisible to both suites, because neither had the client and
        // the endpoint in the same process. A token being issued at all is the assertion.
        SeedDmrp("HOB");

        using var link = CreateLinkContext();

        var result = await CreateSync(link).SyncAsync(FacilityId, Month, Year);

        Assert.Equal(1, result.Recorded);
    }

    [Fact]
    public async Task TheWrongClientSecretIsReportedAsAFailedRefreshRatherThanAnEmptyPlan()
    {
        SeedDmrp("HOB");

        using var link = CreateLinkContext();

        var options = Options.Create(new DmrpSettings
        {
            Enabled = true,
            Api = new LinkDmrpApiSettings
            {
                BaseUrl = "http://localhost/",
                TokenUrl = "http://localhost/api/mock-dmrp/oauth2/token",
                ClientId = ClientId,
                ClientSecret = "not-the-configured-secret"
            }
        });

        var factory = new TestServerHttpClientFactory(_dmrp);

        var sync = new DmrpReportingPlanSync(
            new DmrpApiClient(factory,
                new DmrpApiTokenProvider(factory, options, NullLogger<DmrpApiTokenProvider>.Instance,
                    TimeProvider.System),
                options, NullLogger<DmrpApiClient>.Instance),
            new EntityRepository<FacilityReportingPlan, TenantDbContext>(link),
            new EntityRepository<MeasureMapping, TenantDbContext>(link),
            NullLogger<DmrpReportingPlanSync>.Instance);

        // A refused credential must not read as "the facility is enrolled in nothing", which is
        // what would quietly withdraw its whole plan.
        await Assert.ThrowsAsync<DmrpApiException>(() => sync.SyncAsync(FacilityId, Month, Year));

        Assert.Empty(await link.FacilityReportingPlans.ToListAsync());
    }
}
