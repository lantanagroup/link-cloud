using System.Collections.Concurrent;
using System.Net;
using System.Text.Json;
using Automation.UI.Services;
using FluentAssertions;
using LantanaGroup.Link.Sdk.ApiClient;
using LantanaGroup.Link.Sdk.Clients;
using LantanaGroup.Link.Sdk.DependencyInjection;
using LantanaGroup.Link.Shared.Application.Extensions.Security;
using LantanaGroup.Link.Shared.Application.Interfaces.Services.Security.Token;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using LantanaGroup.Link.Shared.Application.Models.Integration.Census;
using Link.UI.Models;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xunit;

namespace Link.UI.Tests;

/// <summary>
/// LinkSDK clients routed through Admin.BFF: same relative paths, one identity per call.
/// A small Kestrel host stands in for Admin.BFF and records each request's headers.
/// </summary>
public sealed class AdminBffServiceCallTests : IAsyncLifetime
{
    private WebApplication _bff = null!;
    private string _bffUrl = null!;
    private readonly ConcurrentQueue<Captured> _requests = new();
    private readonly CountingTokenService _tokens = new();

    private sealed record Captured(string Path, string? Cookie, string? Authorization, string? InitiatedBy, string? InitiatedByName);

    private sealed class CountingTokenService : ICreateSystemToken
    {
        public int Calls;
        public Task<string> ExecuteAsync(string key, int timespan)
        {
            Interlocked.Increment(ref Calls);
            return Task.FromResult("system-token");
        }
    }

    public async Task InitializeAsync()
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.UseKestrel(o => o.Listen(IPAddress.Loopback, 0));
        builder.Logging.ClearProviders();
        _bff = builder.Build();
        _bff.Run(async context =>
        {
            string? H(string name) => context.Request.Headers.TryGetValue(name, out var v) ? v.ToString() : null;
            _requests.Enqueue(new Captured(context.Request.Path.Value!, H("Cookie"), H("Authorization"),
                H(LinkAuditHeaders.InitiatedBy), H(LinkAuditHeaders.InitiatedByName)));
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync("{}");
        });
        await _bff.StartAsync();
        _bffUrl = _bff.Urls.First();
    }

    public async Task DisposeAsync() => await _bff.DisposeAsync();

    private ServiceProvider Services(HttpContext? httpContext, bool allowAnonymous = false)
    {
        var services = new ServiceCollection();
        services.AddSingleton<IOptions<ServiceRegistry>>(Options.Create(new ServiceRegistry
        {
            AdminBffServiceUrl = _bffUrl,
            CensusServiceUrl = "http://census.invalid",
            TenantService = new TenantServiceRegistration { TenantServiceUrl = "http://tenant.invalid" }
        }));
        services.AddSingleton(Options.Create(new BackendAuthenticationServiceExtension.LinkBearerServiceOptions { AllowAnonymous = allowAnonymous }));
        services.AddSingleton(Options.Create(new LinkTokenServiceSettings { SigningKey = "test-signing-key" }));
        services.AddSingleton<ICreateSystemToken>(_tokens);
        services.AddSingleton<IHttpContextAccessor>(new HttpContextAccessor { HttpContext = httpContext });
        services.AddLinkSdk();
        services.AddServiceCallsThroughAdminBff();
        return services.BuildServiceProvider();
    }

    private static HttpContext Request(string? cookie, string? authorization)
    {
        var context = new DefaultHttpContext();
        if (cookie != null) context.Request.Headers.Cookie = cookie;
        if (authorization != null) context.Request.Headers.Authorization = authorization;
        return context;
    }

    private Captured Single()
    {
        _requests.Should().ContainSingle();
        _requests.TryDequeue(out var c).Should().BeTrue();
        return c!;
    }

    [Fact]
    public async Task Interactive_call_forwards_the_users_session_and_mints_no_system_token()
    {
        using var sp = Services(Request("link_cookie=abc", "Bearer user-token"));
        var census = sp.GetRequiredService<ICensusServiceClient>();

        var response = await census.GetCensusConfigAsync("fac-1");

        response.StatusCode.Should().Be(200);
        var call = Single();
        call.Path.Should().Be("/api/census/config/fac-1");
        call.Cookie.Should().Be("link_cookie=abc");
        call.Authorization.Should().Be("Bearer user-token");
        call.InitiatedBy.Should().BeNull();
        _tokens.Calls.Should().Be(0);
    }

    [Fact]
    public async Task Interactive_call_without_a_session_sends_nothing_rather_than_the_system_token()
    {
        using var sp = Services(Request(null, null));

        await sp.GetRequiredService<ICensusServiceClient>().GetCensusConfigAsync("fac-1");

        var call = Single();
        call.Authorization.Should().BeNull();
        call.Cookie.Should().BeNull();
        _tokens.Calls.Should().Be(0);
    }

    [Fact]
    public async Task Background_run_uses_the_system_token_and_names_the_starting_user_even_while_a_request_is_open()
    {
        using var sp = Services(Request("link_cookie=abc", "Bearer user-token"));
        var initiator = AutomationRunInitiator.FromUser(new AdminBffUser { IsAuthenticated = true, Email = "nick@example.org", UserName = "Nick Montalto" });

        using (BackgroundServiceCallScope.Begin(initiator))
            await sp.GetRequiredService<ICensusServiceClient>().GetCensusConfigAsync("fac-1");

        var call = Single();
        call.Authorization.Should().Be("Bearer system-token");
        call.Cookie.Should().BeNull();
        Uri.UnescapeDataString(call.InitiatedBy!).Should().Be("nick@example.org");
        Uri.UnescapeDataString(call.InitiatedByName!).Should().Be("Nick Montalto");
        _tokens.Calls.Should().Be(1);
        BackgroundServiceCallScope.Current.Should().BeNull();
    }

    [Fact]
    public async Task Hosted_work_without_a_request_or_scope_names_the_system()
    {
        using var sp = Services(httpContext: null);

        await sp.GetRequiredService<ICensusServiceClient>().GetCensusConfigAsync("fac-1");

        var call = Single();
        call.Authorization.Should().Be("Bearer system-token");
        Uri.UnescapeDataString(call.InitiatedBy!).Should().Be(AutomationRunInitiator.System.Id);
    }

    [Fact]
    public async Task Anonymous_mode_sends_the_initiator_without_a_token()
    {
        using var sp = Services(httpContext: null, allowAnonymous: true);
        using (BackgroundServiceCallScope.Begin(new AutomationRunInitiator("u1", null)))
            await sp.GetRequiredService<ICensusServiceClient>().GetCensusConfigAsync("fac-1");

        var call = Single();
        call.Authorization.Should().BeNull();
        call.InitiatedBy.Should().Be("u1");
        call.InitiatedByName.Should().BeNull();
        _tokens.Calls.Should().Be(0);
    }

    [Fact]
    public async Task Audit_header_values_drop_control_characters()
    {
        using var sp = Services(httpContext: null);
        using (BackgroundServiceCallScope.Begin(new AutomationRunInitiator("evil\r\nX-Injected: 1", "Name\nTwo")))
            await sp.GetRequiredService<ICensusServiceClient>().GetCensusConfigAsync("fac-1");

        var call = Single();
        Uri.UnescapeDataString(call.InitiatedBy!).Should().Be("evilX-Injected: 1");
        Uri.UnescapeDataString(call.InitiatedByName!).Should().Be("NameTwo");
    }

    [Theory]
    [InlineData("facility", "/api/Facility/fac-1")]
    [InlineData("data-acquisition", "/api/data-acquisition/facilities/fac-1/fhir-authentication-configuration")]
    [InlineData("terminology", "/api/terminology/codes")]
    [InlineData("report", "/api/schedules/rep-1/summary")]
    [InlineData("submission", "/api/Submission/fac-1/rep-1")]
    public async Task Clients_keep_their_relative_paths_under_the_bff_api_root(string client, string expectedPath)
    {
        using var sp = Services(Request("c=1", null));
        switch (client)
        {
            case "facility": await sp.GetRequiredService<IFacilityServiceClient>().GetAsync("fac-1"); break;
            case "data-acquisition": await sp.GetRequiredService<IDataAcquisitionServiceClient>().GetFhirAuthenticationConfigurationAsync("fac-1"); break;
            case "terminology": await sp.GetRequiredService<ITerminologyServiceClient>().SearchCodesAsync(); break;
            case "report": await sp.GetRequiredService<IReportServiceClient>().GetReportSummaryAsync("rep-1"); break;
            case "submission": await sp.GetRequiredService<ISubmissionServiceClient>().DownloadSubmissionAsync("fac-1", "rep-1"); break;
        }

        Single().Path.Should().Be(expectedPath);
    }

    [Fact]
    public void Registration_moves_the_service_clients_and_leaves_the_rest()
    {
        var services = new ServiceCollection();
        services.AddLinkSdk();
        services.AddServiceCallsThroughAdminBff();

        foreach (var moved in new[]
                 {
                     typeof(ICensusServiceClient), typeof(IReportServiceClient), typeof(IDataAcquisitionServiceClient),
                     typeof(IFacilityServiceClient), typeof(INormalizationServiceClient), typeof(IQueryDispatchServiceClient),
                     typeof(IMeasureEvalServiceClient), typeof(IValidationServiceClient), typeof(IDmrpServiceClient),
                     typeof(ISubmissionServiceClient), typeof(ITerminologyServiceClient)
                 })
        {
            services.Where(d => d.ServiceType == moved).Should().ContainSingle()
                .Which.ImplementationFactory.Should().NotBeNull(moved.Name + " should be built for Admin.BFF");
        }

        foreach (var kept in new[] { typeof(IAdminBffIntegrationClient), typeof(IAccountServiceClient), typeof(IAuditServiceClient), typeof(INotificationServiceClient) })
            services.Single(d => d.ServiceType == kept).ImplementationType.Should().NotBeNull(kept.Name + " keeps its own URL");
    }

    [Fact]
    public void A_credential_is_one_identity_and_never_prints_secrets()
    {
        LinkCallCredential.ForwardUser(" ", null).Should().BeSameAs(LinkCallCredential.None);
        var forwarded = LinkCallCredential.ForwardUser("session=secret", "Bearer secret");
        forwarded.InitiatedById.Should().BeNull();
        forwarded.ToString().Should().NotContain("secret");
        var system = LinkCallCredential.SystemOnBehalfOf("u1", "User");
        system.Cookie.Should().BeNull();
        system.Authorization.Should().BeNull();
        FluentActions.Invoking(() => LinkCallCredential.SystemOnBehalfOf("", null)).Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Initiator_falls_back_to_the_system_when_nobody_is_signed_in()
    {
        AutomationRunInitiator.FromUser(null).Should().Be(AutomationRunInitiator.System);
        AutomationRunInitiator.FromUser(new AdminBffUser { IsAuthenticated = false, Email = "x@y" }).Should().Be(AutomationRunInitiator.System);
        AutomationRunInitiator.FromUser(new AdminBffUser { IsAuthenticated = true, UserName = "Pat" }).Should().Be(new AutomationRunInitiator("Pat", "Pat"));
    }

    [Fact]
    public void Admin_bff_config_routes_terminology_and_data_acquisition()
    {
        var dir = AppContext.BaseDirectory;
        while (dir != null && !Directory.Exists(Path.Combine(dir, "DotNet", "Admin.BFF")))
            dir = Path.GetDirectoryName(dir);
        dir.Should().NotBeNull();
        var bff = Path.Combine(dir!, "DotNet", "Admin.BFF");

        foreach (var file in new[] { "appsettings.json", "appsettings.Development.json" })
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(bff, file)));
            var proxy = doc.RootElement.GetProperty("ReverseProxy");
            var paths = proxy.GetProperty("Routes").EnumerateObject()
                .ToDictionary(r => r.Value.GetProperty("Match").GetProperty("Path").GetString()!, r => r.Value);
            paths.Should().ContainKey("api/terminology/{**catch-all}");
            paths["api/terminology/{**catch-all}"].GetProperty("ClusterId").GetString().Should().Be("TerminologyService");
            paths["api/terminology/{**catch-all}"].GetProperty("AuthorizationPolicy").GetString().Should().Be("AuthenticatedUser");
            paths.Should().ContainKey("api/data-acquisition/{**catch-all}");
            paths["api/data-acquisition/{**catch-all}"].GetProperty("AuthorizationPolicy").GetString().Should().Be("AuthenticatedUser");
            proxy.GetProperty("Clusters").TryGetProperty("TerminologyService", out _).Should().BeTrue();
        }

        File.ReadAllText(Path.Combine(bff, "Infrastructure", "Filters", "YarpConfigFilter.cs"))
            .Should().Contain("\"TerminologyService\" => _serviceRegistry.TerminologyServiceUrl");
    }
}
