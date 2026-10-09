using Confluent.Kafka;
using FluentAssertions;
using LantanaGroup.Link.DMRP.Api;
using LantanaGroup.Link.DMRP.Business;
using LantanaGroup.Link.DMRP.Config;
using LantanaGroup.Link.DMRP.Data.Entities;
using LantanaGroup.Link.DMRP.Scheduling;
using LantanaGroup.Link.MockDmrpApi.Application.Services;
using LantanaGroup.Link.MockDmrpApi.Domain.Entities;
using LantanaGroup.Link.MockDmrpApi.Presentation.Controllers;
using LantanaGroup.Link.MockDmrpApi.Settings;
using LantanaGroup.Link.Shared.Application.Interfaces;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Models.Kafka;
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
using Moq;
using Quartz;
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
/// Drives a month-end fire of the nightly job the whole way through: Link's own scheduling job asks
/// the real mock DMRP for next month's plan, writes what comes back into Link's own database, and
/// announces the periods that start at the coming midnight over the (mocked) Kafka producer.
/// </summary>
/// <remarks>
/// This is <see cref="DmrpRefreshEndToEndTests"/>'s sibling one layer up: that class proves the sync
/// alone reaches a real mock DMRP; this one proves the whole nightly job - directory lookup, sync,
/// projection and production - does the same, with only the two databases and the Kafka producer as
/// test doubles.
/// </remarks>
[Trait("Category", "IntegrationTests")]
public class DmrpNightlyJobEndToEndTests : IAsyncLifetime
{
    private const string FacilityId = "100";
    private const string ClientId = "link-cloud-dev";
    private const string ClientSecret = "end-to-end-client-secret";
    private const int Month = 11;
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

    private readonly List<Message<string, object>> _produced = [];

    private DmrpNightlyJob CreateJob(TenantDbContext link, string clientSecret = ClientSecret)
    {
        var options = Options.Create(new DmrpSettings
        {
            Enabled = true,
            Api = new LinkDmrpApiSettings
            {
                BaseUrl = "http://localhost/",
                TokenUrl = "http://localhost/api/mock-dmrp/oauth2/token",
                ClientId = ClientId,
                ClientSecret = clientSecret
            }
        });
        var factory = new TestServerHttpClientFactory(_dmrp);
        var tokens = new DmrpApiTokenProvider(factory, options, NullLogger<DmrpApiTokenProvider>.Instance, TimeProvider.System);
        var client = new DmrpApiClient(factory, tokens, options, NullLogger<DmrpApiClient>.Instance);

        var plans = new EntityRepository<FacilityReportingPlan, TenantDbContext>(link);
        var mappings = new EntityRepository<MeasureMapping, TenantDbContext>(link);

        var services = new ServiceCollection();
        services.AddScoped<IFacilityDirectory>(_ => new FixedDirectory(FacilityId));
        services.AddScoped<IDmrpReportingPlanSync>(_ =>
            new DmrpReportingPlanSync(client, plans, mappings, NullLogger<DmrpReportingPlanSync>.Instance));
        services.AddScoped<IReportingPlanSource>(_ =>
            new DbBackedReportingPlanSource(NullLogger<DbBackedReportingPlanSource>.Instance, plans, mappings));
        services.AddScoped<IEntityRepository<FacilityReportingPlan>>(_ => plans);
        services.AddScoped<IReportingPlanScheduleProjector>(_ =>
            new ReportingPlanScheduleProjector(NullLogger<ReportingPlanScheduleProjector>.Instance));
        var provider = services.BuildServiceProvider();

        var producer = new Mock<IProducer<string, object>>();
        producer
            .Setup(p => p.ProduceAsync(It.IsAny<string>(), It.IsAny<Message<string, object>>(), It.IsAny<CancellationToken>()))
            // The job produces for its facilities concurrently, so this callback can run on several
            // threads at once. List<T> is not safe under that.
            .Callback<string, Message<string, object>, CancellationToken>((_, m, _) => { lock (_produced) { _produced.Add(m); } })
            .ReturnsAsync((DeliveryResult<string, object>)null!);
        var producerFactory = new Mock<IKafkaProducerFactory<string, object>>();
        producerFactory.Setup(f => f.CreateProducer(It.IsAny<ProducerConfig>(), null, null, true)).Returns(producer.Object);

        return new DmrpNightlyJob(provider.GetRequiredService<IServiceScopeFactory>(), producerFactory.Object,
            options, new Mock<IDmrpSchedulingMetrics>().Object, NullLogger<DmrpNightlyJob>.Instance);
    }

    private sealed class FixedDirectory(string facilityId) : IFacilityDirectory
    {
        public Task<IReadOnlyList<string>> GetTimeZonesAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<string>>(["UTC"]);

        public Task<IReadOnlyList<ScheduledFacility>> GetActiveInTimeZoneAsync(string timeZone, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<ScheduledFacility>>([new ScheduledFacility(facilityId, "UTC")]);

        // The interface now extends IFacilityTimeZoneSource; this test drives the job entirely
        // through GetActiveInTimeZoneAsync, so the single-facility lookup is never exercised here.
        public Task<string?> GetTimeZoneAsync(string facilityId, CancellationToken cancellationToken = default) =>
            Task.FromResult<string?>("UTC");
    }

    private static IJobExecutionContext LastNightOfOctober()
    {
        var detail = JobBuilder.Create<DmrpNightlyJob>().WithIdentity("UTC", "DmrpNightly")
            .UsingJobData(DmrpNightlyJob.TimeZoneKey, "UTC").Build();
        var context = new Mock<IJobExecutionContext>();
        context.SetupGet(c => c.JobDetail).Returns(detail);
        context.SetupGet(c => c.ScheduledFireTimeUtc).Returns(new DateTimeOffset(2026, 10, 31, 23, 59, 0, TimeSpan.Zero));
        context.SetupGet(c => c.FireTimeUtc).Returns(new DateTimeOffset(2026, 10, 31, 23, 59, 1, TimeSpan.Zero));
        context.SetupGet(c => c.CancellationToken).Returns(CancellationToken.None);
        return context.Object;
    }

    [Fact]
    public async Task A_month_end_fire_pulls_november_from_dmrp_and_announces_it()
    {
        await using var link = CreateLinkContext();
        link.Set<MeasureMapping>().Add(new MeasureMapping { Measure = "HTCDI", DQM = "NHSNdQMHTCDI", Frequency = Frequency.Monthly });
        link.Set<MeasureMapping>().Add(new MeasureMapping { Measure = "HOB", DQM = "NHSNdQMHOB", Frequency = Frequency.Daily });
        await link.SaveChangesAsync();

        SeedDmrp("HTCDI");
        SeedDmrp("HOB", DmrpComponents.Ps);

        await CreateJob(link).Execute(LastNightOfOctober());

        var rows = await link.Set<FacilityReportingPlan>().Where(p => p.FacilityId == FacilityId).ToListAsync();
        rows.Select(r => (r.Component, r.Measure, r.ReportingMonth, r.ReportingYear)).Should().BeEquivalentTo([
            (LinkComponents.Msc, "HTCDI", 11, 2026),
            (LinkComponents.Ps, "HOB", 11, 2026)
        ]);

        // 2026-11-01 is the first of the month: Daily and Monthly both start there.
        var values = _produced.Select(m => (ReportScheduledMessage)m.Value).ToList();
        values.Select(v => v.Frequency).Should().BeEquivalentTo(["Daily", "Monthly"]);
        values.Single(v => v.Frequency == "Monthly").ReportTypes.Should().BeEquivalentTo(["NHSNdQMHTCDI"]);
        values.Single(v => v.Frequency == "Daily").ReportTypes.Should().BeEquivalentTo(["NHSNdQMHOB"]);
        values.Should().OnlyContain(v => v.StartDate == new DateTime(2026, 11, 1, 0, 0, 0, DateTimeKind.Utc));
        _produced.Should().OnlyContain(m => m.Key == FacilityId);
    }

    [Fact]
    public async Task A_refused_credential_leaves_stored_rows_alone_and_still_announces_them()
    {
        await using var link = CreateLinkContext();
        var mapping = new MeasureMapping { Measure = "HTCDI", DQM = "NHSNdQMHTCDI", Frequency = Frequency.Monthly };
        link.Set<MeasureMapping>().Add(mapping);
        await link.SaveChangesAsync();
        link.Set<FacilityReportingPlan>().Add(new FacilityReportingPlan
        {
            FacilityId = FacilityId, Component = LinkComponents.Msc, Measure = "HTCDI",
            MeasureMappingId = mapping.Id, ReportingMonth = 11, ReportingYear = 2026, IsReporting = true
        });
        await link.SaveChangesAsync();

        // Wrong secret: the token endpoint refuses, the sync throws twice, the job carries on.
        await CreateJob(link, clientSecret: "wrong").Execute(LastNightOfOctober());

        _produced.Select(m => ((ReportScheduledMessage)m.Value).Frequency).Should().BeEquivalentTo(["Monthly"]);
    }
}
