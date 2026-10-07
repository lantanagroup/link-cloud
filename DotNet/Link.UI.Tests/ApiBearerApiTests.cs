using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Automation.UI.Models;
using Automation.UI.Models.ApiHealth;
using Automation.UI.Services;
using Automation.UI.Services.ApiHealth;
using Automation.UI.Services.Persistence;
using FluentAssertions;
using LantanaGroup.Link.Automation.Link.Models;
using Link.UI.Auth;
using Link.UI.Models;
using Link.UI.Services;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace Link.UI.Tests;

internal static class ApiTestDatabaseGuard
{
    [System.Runtime.CompilerServices.ModuleInitializer]
    internal static void KeepTestsOffSharedDatabases()
    {
        Environment.SetEnvironmentVariable("MongoDB__ConnectionString", "not-a-connection-string");
        Environment.SetEnvironmentVariable("MongoDB__DatabaseName", "link-ui-3i-api-tests");
    }
}

public class ApiBearerAudienceTests
{
    [Theory]
    [InlineData("api://link-ui-test", "api://link-ui-test")]
    [InlineData("api://link-ui-test", "link-ui-test")]
    [InlineData("link-ui-test/", "link-ui-test")]
    [InlineData("link-ui-test/", "api://link-ui-test")]
    public void Audience_accepts_the_configured_value_and_its_api_uri_pair(string configured, string accepted)
    {
        ApiBearerAuthentication.BuildValidAudiences(configured)
            .Should().Contain(accepted);
    }

    [Fact]
    public void Audience_pair_does_not_include_a_different_application()
    {
        ApiBearerAuthentication.BuildValidAudiences("api://link-ui-test")
            .Should().NotContain("api://other-app")
            .And.NotContain("other-app");
    }
}

[Collection("api-hosts")]
public sealed class ApiBearerEnabledTests : IClassFixture<BearerOnHost>
{
    private readonly BearerOnHost _host;

    public ApiBearerEnabledTests(BearerOnHost host) => _host = host;

    [Fact]
    public void Bearer_scheme_is_not_the_default_scheme()
    {
        var options = _host.Services.GetRequiredService<IOptions<AuthenticationOptions>>().Value;
        options.DefaultScheme.Should().BeNull();
        options.DefaultAuthenticateScheme.Should().BeNull();
        options.DefaultChallengeScheme.Should().BeNull();
        options.DefaultSignInScheme.Should().BeNull();
    }

    [Theory]
    [InlineData("/")]
    [InlineData("/Tenants")]
    [InlineData("/Home/overview")]
    [InlineData("/Home/overview/data")]
    [InlineData("/hubs/runs")]
    [InlineData("/hubs/cleanup")]
    public async Task Shell_redirects_to_sign_in_when_anonymous_mode_is_off(string path)
    {
        using var response = await _host.Client.GetAsync(path);

        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        response.Headers.Location.Should().NotBeNull();
        response.Headers.Location!.OriginalString.Should().Be("/api/login");
    }

    [Fact]
    public async Task Health_stays_open()
    {
        using var response = await _host.Client.GetAsync("/health");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Api_without_a_token_is_401_and_not_a_sign_in_redirect()
    {
        using var response = await _host.Client.GetAsync("/api/runs/metrics");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        response.Headers.Location.Should().BeNull();
    }

    [Fact]
    public async Task Malformed_token_is_401()
    {
        using var client = _host.CreatePlainClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "not-a-jwt");

        using var response = await client.GetAsync("/api/runs/metrics");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        response.Headers.Location.Should().BeNull();
    }

    [Fact]
    public async Task Wrong_audience_is_401()
    {
        using var client = _host.CreatePlainClient();
        _host.Authorize(client, "other-app");

        using var response = await client.GetAsync("/api/runs/metrics");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Theory]
    [InlineData("link-ui-test")]
    [InlineData("api://link-ui-test")]
    public async Task Valid_token_returns_the_metrics_list(string audience)
    {
        using var client = _host.CreatePlainClient();
        _host.Authorize(client, audience);

        using var response = await client.GetAsync("/api/runs/metrics");
        var body = await ApiJson.Read(response);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        body.GetProperty("records").GetArrayLength().Should().Be(1);
        body.GetProperty("records")[0].GetProperty("runId").GetGuid().Should().Be(ApiRunsFakes.RunId);
        body.GetProperty("metadata").GetProperty("totalCount").GetInt64().Should().Be(1);
    }

    [Fact]
    public async Task Pipeline_start_requires_a_bearer_token_and_does_not_redirect()
    {
        using var anonymous = await _host.Client.PostAsync("/api/api-health-runs/start-all-for-pipeline", null);
        anonymous.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        anonymous.Headers.Location.Should().BeNull();

        using var client = _host.CreatePlainClient();
        _host.Authorize(client, "api://link-ui-test");
        using var authorized = await client.PostAsync("/api/api-health-runs/start-all-for-pipeline", null);
        var body = await ApiJson.Read(authorized);

        authorized.StatusCode.Should().Be(HttpStatusCode.Created);
        body.GetProperty("scope").GetString().Should().Be("All");
        authorized.Headers.Location.Should().NotBeNull();
        authorized.Headers.Location!.OriginalString.Should().Contain("/api/api-health-runs/");
        authorized.Headers.Location.OriginalString.Should().Contain("/status");
    }

    [Fact]
    public async Task Ui_start_all_uses_antiforgery_and_not_the_bearer_scheme()
    {
        using var missing = await _host.Client.PostAsync("/api/api-health-runs/start-all", null);
        missing.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        using var client = _host.CreatePlainClient();
        _host.AddAntiforgery(client);
        using var started = await client.PostAsync("/api/api-health-runs/start-all", null);

        started.StatusCode.Should().Be(HttpStatusCode.Created);
    }
}

[Collection("api-hosts")]
public sealed class ApiBearerDisabledTests : IClassFixture<BearerOffHost>
{

    private readonly BearerOffHost _host;

    public ApiBearerDisabledTests(BearerOffHost host) => _host = host;

    [Theory]
    [InlineData("/")]
    [InlineData("/Tenants")]
    [InlineData("/Home/overview/data")]
    [InlineData("/hubs/runs")]
    public async Task Shell_stays_closed_when_anonymous_mode_is_off(string path)
    {
        using var response = await _host.Client.GetAsync(path);
        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
    }

    [Fact]
    public async Task Disabled_bearer_allows_the_runs_api_without_a_token()
    {
        using var response = await _host.Client.GetAsync($"/api/runs/{ApiRunsFakes.RunId}/status");
        var body = await ApiJson.Read(response);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        body.GetProperty("status").GetString().Should().Be("Succeeded");
        body.GetProperty("isTerminal").GetBoolean().Should().BeTrue();
        body.GetProperty("runName").GetString().Should().Be("Ad hoc");
        body.GetProperty("error").GetString().Should().BeNull();
    }

    [Fact]
    public async Task Start_binds_scenarioId_and_returns_the_accepted_shape()
    {
        using var missing = await _host.PostJson("/api/runs/start", new { });
        missing.Status.Should().Be(HttpStatusCode.BadRequest);
        missing.Body.GetProperty("error").GetString().Should().Be("scenarioId is required.");

        using var unknown = await _host.PostJson("/api/runs/start", new { scenarioId = Guid.NewGuid(), source = "contract" });
        unknown.Status.Should().Be(HttpStatusCode.NotFound);
        unknown.Body.GetProperty("error").GetString().Should().Contain("not found");

        using var invalid = await _host.PostJson("/api/runs/start", new { scenarioId = ApiRunsFakes.InvalidScenarioId, source = "contract" });
        invalid.Status.Should().Be(HttpStatusCode.BadRequest);
        invalid.Body.GetProperty("error").GetString().Should().Be("scenario cannot start");

        using var failed = await _host.PostJson("/api/runs/start", new { scenarioId = ApiRunsFakes.BoomScenarioId, source = "contract" });
        failed.Status.Should().Be(HttpStatusCode.InternalServerError);

        using var started = await _host.PostJson("/api/runs/start", new { scenarioId = ApiRunsFakes.ScenarioId, source = "contract" });
        started.Status.Should().Be(HttpStatusCode.Accepted);
        started.Body.GetProperty("runId").GetGuid().Should().Be(ApiRunsFakes.RunId);
        started.Body.GetProperty("scenarioId").GetGuid().Should().Be(ApiRunsFakes.ScenarioId);
        started.Body.GetProperty("scenarioName").GetString().Should().Be("Ad hoc report");
        started.Body.GetProperty("source").GetString().Should().Be("contract");
    }

    [Fact]
    public async Task Missing_run_status_and_metrics_use_the_controller_status_codes()
    {
        var missing = Guid.NewGuid();

        using var status = await _host.Client.GetAsync($"/api/runs/{missing}/status");
        status.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await ApiJson.Read(status)).GetProperty("error").GetString().Should().Contain(missing.ToString());

        using var metrics = await _host.Client.GetAsync($"/api/runs/{missing}/metrics");
        metrics.StatusCode.Should().Be(HttpStatusCode.NotFound);
        metrics.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
        (await ApiJson.Read(metrics)).GetProperty("title").GetString().Should().Be("Run not found");

        using var found = await _host.Client.GetAsync($"/api/runs/{ApiRunsFakes.RunId}/metrics");
        var body = await ApiJson.Read(found);
        found.StatusCode.Should().Be(HttpStatusCode.OK);
        body.GetProperty("runId").GetGuid().Should().Be(ApiRunsFakes.RunId);
        body.GetProperty("stagesUnavailable").GetBoolean().Should().BeTrue();
        body.GetProperty("runAvailable").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task Benchmarks_round_trip_the_document_shape()
    {
        using var list = await _host.Client.GetAsync("/api/runs/metrics/benchmarks");
        list.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ApiJson.Read(list)).GetProperty("metadata").GetProperty("pageNumber").GetInt32().Should().Be(1);

        using var missing = await _host.Client.GetAsync("/api/runs/metrics/benchmarks/missing");
        missing.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await ApiJson.Read(missing)).GetProperty("title").GetString().Should().Be("Benchmark not found");

        using var mismatch = await _host.PutJson("/api/runs/metrics/benchmarks/e2e", new { key = "other", regressionPercent = 5 });
        mismatch.Status.Should().Be(HttpStatusCode.BadRequest);
        mismatch.Body.GetProperty("title").GetString().Should().Be("Benchmark key mismatch");

        using var saved = await _host.PutJson("/api/runs/metrics/benchmarks/e2e", new { key = "e2e", regressionPercent = 0 });
        saved.Status.Should().Be(HttpStatusCode.Accepted);
        saved.Body.GetProperty("key").GetString().Should().Be("e2e");
        saved.Body.GetProperty("regressionPercent").GetDouble().Should().Be(10);

        using var loaded = await _host.Client.GetAsync("/api/runs/metrics/benchmarks/e2e");
        loaded.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ApiJson.Read(loaded)).GetProperty("regressionPercent").GetDouble().Should().Be(10);
    }

    [Fact]
    public async Task Live_patient_routes_keep_their_status_codes_and_json_names()
    {
        using var admitMissingToken = await _host.Client.PostAsJsonAsync(
            $"/api/runs/{ApiRunsFakes.RunId}/events/admit",
            new { patientId = "p1" });
        admitMissingToken.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        using var admitMissingPatient = await _host.PostJson($"/api/runs/{ApiRunsFakes.RunId}/events/admit", new { notes = "x" });
        admitMissingPatient.Status.Should().Be(HttpStatusCode.BadRequest);
        admitMissingPatient.Body.GetProperty("error").GetString().Should().Be("patientId is required.");

        using var admit = await _host.PostJson($"/api/runs/{ApiRunsFakes.RunId}/events/admit", new { patientId = "p1", source = "contract" });
        admit.Status.Should().Be(HttpStatusCode.OK);
        admit.Body.GetProperty("patientId").GetString().Should().Be("p1");
        admit.Body.GetProperty("eventType").GetString().Should().Be("Admit");
        admit.Body.GetProperty("source").GetString().Should().Be("contract");

        using var blocked = await _host.PostJson($"/api/runs/{ApiRunsFakes.RunId}/events/discharge", new { patientId = "blocked" });
        blocked.Status.Should().Be(HttpStatusCode.Conflict);
        blocked.Body.GetProperty("error").GetString().Should().Be("closed");

        using var unknown = await _host.PostJson($"/api/runs/{Guid.NewGuid()}/events/admit", new { patientId = "p1" });
        unknown.Status.Should().Be(HttpStatusCode.NotFound);

        using var events = await _host.Client.GetAsync($"/api/runs/{ApiRunsFakes.RunId}/events");
        events.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ApiJson.Read(events))[0].GetProperty("eventType").GetString().Should().Be("Admit");

        using var state = await _host.Client.GetAsync($"/api/runs/{ApiRunsFakes.RunId}/patient-state");
        var stateBody = await ApiJson.Read(state);
        state.StatusCode.Should().Be(HttpStatusCode.OK);
        stateBody.GetProperty("admitted")[0].GetString().Should().Be("p1");
        stateBody.GetProperty("expectedPopulation").GetArrayLength().Should().Be(1);
        stateBody.GetProperty("poolTotals").GetProperty("total").GetInt32().Should().Be(1);
        stateBody.GetProperty("acceptingInjections").GetBoolean().Should().BeTrue();

        using var generated = await _host.PostEmpty($"/api/runs/{ApiRunsFakes.RunId}/pool/generate");
        generated.Status.Should().Be(HttpStatusCode.OK);
        generated.Body.GetProperty("origin").GetString().Should().Be("Generated");
        generated.Body.GetProperty("patientId").GetString().Should().Be("generated-1");

        using var uploaded = await _host.PostText($"/api/runs/{ApiRunsFakes.RunId}/pool/upload", "uploaded-1");
        uploaded.Status.Should().Be(HttpStatusCode.OK);
        uploaded.Body.GetProperty("origin").GetString().Should().Be("Upload");
        uploaded.Body.GetProperty("patientId").GetString().Should().Be("uploaded-1");

        using var referenceMissing = await _host.PostJson($"/api/runs/{ApiRunsFakes.RunId}/pool/reference", new { });
        referenceMissing.Status.Should().Be(HttpStatusCode.BadRequest);

        using var referenced = await _host.PostJson($"/api/runs/{ApiRunsFakes.RunId}/pool/reference", new { patientId = "fhir-1" });
        referenced.Status.Should().Be(HttpStatusCode.OK);
        referenced.Body.GetProperty("origin").GetString().Should().Be("FhirId");
        referenced.Body.GetProperty("censusState").GetString().Should().Be("NotAdmitted");
    }

    [Fact]
    public async Task Api_health_status_shape_matches_the_pipeline_poll()
    {
        using var missingToken = await _host.Client.PostAsync("/api/api-health-runs/start-all", null);
        missingToken.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        using var started = await _host.PostEmpty("/api/api-health-runs/start-all");
        started.Status.Should().Be(HttpStatusCode.Created);
        started.Body.GetProperty("runId").GetGuid().Should().NotBeEmpty();
        started.Body.GetProperty("scope").GetString().Should().Be("All");

        using var pipeline = await _host.Client.PostAsync("/api/api-health-runs/start-all-for-pipeline", null);
        pipeline.StatusCode.Should().Be(HttpStatusCode.Created);

        using var status = await _host.Client.GetAsync($"/api/api-health-runs/{ApiRunsFakes.HealthRunId}/status");
        var body = await ApiJson.Read(status);
        status.StatusCode.Should().Be(HttpStatusCode.OK);
        body.GetProperty("status").GetString().Should().Be("Failed");
        body.GetProperty("isTerminal").GetBoolean().Should().BeTrue();
        body.GetProperty("totalEndpoints").GetInt32().Should().Be(3);
        body.GetProperty("passedEndpoints").GetInt32().Should().Be(1);
        body.GetProperty("failedEndpoints").GetInt32().Should().Be(1);
        body.GetProperty("skippedEndpoints").GetInt32().Should().Be(1);
        var failure = body.GetProperty("failedResults")[0];
        failure.GetProperty("endpointKey").GetString().Should().Be("report.get");
        failure.GetProperty("expectedStatusCode").GetInt32().Should().Be(200);
        failure.GetProperty("actualStatusCode").GetInt32().Should().Be(500);
        failure.GetProperty("errorMessage").GetString().Should().Be("nope");

        using var results = await _host.Client.GetAsync($"/api/api-health-runs/{ApiRunsFakes.HealthRunId}/results");
        var rows = await ApiJson.Read(results);
        results.StatusCode.Should().Be(HttpStatusCode.OK);
        rows.GetArrayLength().Should().Be(3);
        rows[0].GetProperty("serviceName").GetString().Should().Be("Account");

        using var unknown = await _host.Client.GetAsync($"/api/api-health-runs/{Guid.NewGuid()}/status");
        unknown.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}

[CollectionDefinition("api-hosts", DisableParallelization = true)]
public sealed class ApiHostCollection
{
}

public sealed class BearerOnHost() : ApiRunsHost(bearerEnabled: true, allowAnonymous: false, requireSession: true);

public sealed class BearerOffHost() : ApiRunsHost(bearerEnabled: false, allowAnonymous: false, requireSession: false);

public class ApiRunsHost : WebApplicationFactory<Program>
{
    private HttpClient? _client;

    public ApiRunsHost(bool bearerEnabled, bool allowAnonymous, bool requireSession)
    {
        Environment.SetEnvironmentVariable("Authentication__EnableAnonymousAccess", allowAnonymous ? "true" : "false");
        Environment.SetEnvironmentVariable("Authentication__RequireBffSession", requireSession ? "true" : "false");
        Environment.SetEnvironmentVariable("Authentication__ApiBearer__Enabled", bearerEnabled ? "true" : "false");
        Environment.SetEnvironmentVariable("Authentication__ApiBearer__Authority", TestIssuer);
        Environment.SetEnvironmentVariable("Authentication__ApiBearer__Audience", TestAudience);
        BearerEnabled = bearerEnabled;
        AllowAnonymous = allowAnonymous;
        RequireSession = requireSession;
        Runs = new ApiRunsFakes.RunManager();
        Scenarios = new ApiRunsFakes.ScenarioStore();
        Metrics = new ApiRunsFakes.MetricsStore();
        Benchmarks = new ApiRunsFakes.BenchmarkStore();
        Health = new ApiRunsFakes.HealthRuns();
    }

    public bool BearerEnabled { get; }
    public bool AllowAnonymous { get; }
    public bool RequireSession { get; }
    private ApiRunsFakes.RunManager Runs { get; }
    private ApiRunsFakes.ScenarioStore Scenarios { get; }
    private ApiRunsFakes.MetricsStore Metrics { get; }
    private ApiRunsFakes.BenchmarkStore Benchmarks { get; }
    private ApiRunsFakes.HealthRuns Health { get; }

    public const string TestIssuer = "https://link-ui-test.invalid/auth";
    public const string TestAudience = "api://link-ui-test";
    public const string TestKey = "link-ui-api-bearer-test-key-only";

    public HttpClient Client => _client ??= CreateReadyClient();

    public HttpClient CreatePlainClient() => CreateClient(NoRedirect);

    public void Authorize(HttpClient client, string audience)
    {
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Mint(audience));
    }

    public void AddAntiforgery(HttpClient client)
    {
        var http = new DefaultHttpContext { RequestServices = Services };
        var tokens = Services.GetRequiredService<IAntiforgery>().GetAndStoreTokens(http);
        var setCookie = http.Response.Headers.SetCookie.ToString();
        var pair = setCookie.Split(';')[0];
        client.DefaultRequestHeaders.Remove("RequestVerificationToken");
        client.DefaultRequestHeaders.TryAddWithoutValidation("RequestVerificationToken", tokens.RequestToken);
        client.DefaultRequestHeaders.TryAddWithoutValidation("Cookie", pair);
    }

    public async Task<ApiCall> PostJson(string path, object body)
    {
        using var client = CreatePlainClient();
        AddAntiforgery(client);
        using var response = await client.PostAsJsonAsync(path, body);
        return await ApiCall.From(response);
    }

    public async Task<ApiCall> PutJson(string path, object body)
    {
        using var client = CreatePlainClient();
        AddAntiforgery(client);
        using var response = await client.PutAsJsonAsync(path, body);
        return await ApiCall.From(response);
    }

    public async Task<ApiCall> PostEmpty(string path)
    {
        using var client = CreatePlainClient();
        AddAntiforgery(client);
        using var response = await client.PostAsync(path, null);
        return await ApiCall.From(response);
    }

    public async Task<ApiCall> PostText(string path, string body)
    {
        using var client = CreatePlainClient();
        AddAntiforgery(client);
        using var content = new StringContent(body, Encoding.UTF8, "text/plain");
        using var response = await client.PostAsync(path, content);
        return await ApiCall.From(response);
    }

    protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
    {
        builder.UseSetting(WebHostDefaults.ContentRootKey, FindContentRoot());
        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["MongoDB:ConnectionString"] = "not-a-connection-string",
                ["MongoDB:DatabaseName"] = "link-ui-3i-api-tests",
                ["Authentication:EnableAnonymousAccess"] = AllowAnonymous ? "true" : "false",
                ["Authentication:RequireBffSession"] = RequireSession ? "true" : "false",
                ["Authentication:ApiBearer:Enabled"] = BearerEnabled ? "true" : "false",
                ["Authentication:ApiBearer:Authority"] = TestIssuer,
                ["Authentication:ApiBearer:Audience"] = TestAudience,
                ["ExternalConfigurationSource"] = "",
                ["Telemetry:EnableTelemetry"] = "false"
            });
        });

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IAutomationRunManager>();
            services.AddSingleton<IAutomationRunManager>(Runs);
            services.RemoveAll<IScenarioStore>();
            services.AddSingleton<IScenarioStore>(Scenarios);
            services.RemoveAll<MetricsRunPresenter>();
            services.AddSingleton(new MetricsRunPresenter(Metrics, Runs, Scenarios, Benchmarks));
            services.RemoveAll<IApiHealthExecutionRunManager>();
            services.AddSingleton<IApiHealthExecutionRunManager>(Health);
            services.RemoveAll<IAdminBffUserService>();
            services.AddSingleton<IAdminBffUserService, SignedOutUser>();

            if (BearerEnabled)
            {
                services.PostConfigure<JwtBearerOptions>(ApiBearerAuthentication.SchemeName, options =>
                {
                    var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(TestKey));
                    options.RequireHttpsMetadata = false;
                    options.Authority = null!;
                    options.MetadataAddress = null!;
                    options.Configuration = new OpenIdConnectConfiguration
                    {
                        Issuer = TestIssuer,
                        SigningKeys = { key }
                    };
                    options.ConfigurationManager = new StaticConfigurationManager<OpenIdConnectConfiguration>(options.Configuration);
                    options.TokenValidationParameters.ValidateIssuerSigningKey = true;
                    options.TokenValidationParameters.IssuerSigningKey = key;
                    options.TokenValidationParameters.ValidIssuer = TestIssuer;
                    options.TokenValidationParameters.ValidateIssuer = true;
                    options.TokenValidationParameters.ValidateAudience = true;
                    options.TokenValidationParameters.ValidateLifetime = true;
                });
            }
        });
    }

    private HttpClient CreateReadyClient()
    {
        var client = CreatePlainClient();
        var status = Services.GetRequiredService<LinkAutomationEngineStatus>();
        if (status.Ready)
            throw new InvalidOperationException("The API test host started the run engine. " + status.Message);
        return client;
    }

    private string Mint(string audience)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(TestKey));
        var token = new JwtSecurityToken(
            issuer: TestIssuer,
            audience: audience,
            claims: [new Claim("sub", "api-contract-test")],
            notBefore: DateTime.UtcNow.AddMinutes(-1),
            expires: DateTime.UtcNow.AddMinutes(5),
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private static WebApplicationFactoryClientOptions NoRedirect => new()
    {
        AllowAutoRedirect = false
    };

    private static string FindContentRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var sibling = Path.Combine(dir.FullName, "Link.UI", "Link.UI.csproj");
            if (File.Exists(sibling))
                return Path.Combine(dir.FullName, "Link.UI");
            var nested = Path.Combine(dir.FullName, "DotNet", "Link.UI", "Link.UI.csproj");
            if (File.Exists(nested))
                return Path.Combine(dir.FullName, "DotNet", "Link.UI");
            dir = dir.Parent;
        }

        throw new InvalidOperationException("Link.UI content root was not found.");
    }

    private sealed class SignedOutUser : IAdminBffUserService
    {
        public Task<AdminBffUser?> GetCurrentUserAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<AdminBffUser?>(new AdminBffUser { IsAuthenticated = false });
    }
}

public sealed class ApiCall : IDisposable
{
    public required HttpStatusCode Status { get; init; }
    public required JsonElement Body { get; init; }

    public static async Task<ApiCall> From(HttpResponseMessage response) => new()
    {
        Status = response.StatusCode,
        Body = await ApiJson.Read(response)
    };

    public void Dispose()
    {
    }
}

internal static class ApiJson
{
    public static async Task<JsonElement> Read(HttpResponseMessage response)
    {
        var text = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(text);
        return document.RootElement.Clone();
    }
}

internal static class ApiRunsFakes
{
    public static readonly Guid ScenarioId = Guid.Parse("00000000-0000-0000-0000-000000000001");
    public static readonly Guid InvalidScenarioId = Guid.Parse("00000000-0000-0000-0000-0000000000b1");
    public static readonly Guid BoomScenarioId = Guid.Parse("00000000-0000-0000-0000-0000000000b0");
    public static readonly Guid RunId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    public static readonly Guid HealthRunId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    public sealed class RunManager : IAutomationRunManager
    {
        public Task<Guid> StartAsync(StartScenarioRequest request, CancellationToken cancellationToken = default)
        {
            if (request.ScenarioId == BoomScenarioId)
                throw new Exception("boom");
            if (request.ScenarioId == InvalidScenarioId)
                throw new InvalidOperationException("scenario cannot start");
            return Task.FromResult(RunId);
        }

        public Task<AutomationRunSummary?> GetRunAsync(Guid runId, CancellationToken cancellationToken = default)
        {
            if (runId != RunId)
                return Task.FromResult<AutomationRunSummary?>(null);
            return Task.FromResult<AutomationRunSummary?>(new AutomationRunSummary
            {
                RunId = RunId,
                RunName = "Ad hoc",
                Status = AutomationRunStatus.Succeeded,
                CreatedAt = DateTimeOffset.Parse("2026-10-07T15:00:00Z"),
                StartedAt = DateTimeOffset.Parse("2026-10-07T15:00:01Z"),
                FinishedAt = DateTimeOffset.Parse("2026-10-07T15:01:00Z"),
                Duration = "00:00:59"
            });
        }

        public Task<PatientStateEvent> InjectAdmitAsync(Guid runId, string? patientId, string source, string? notes = null, CancellationToken cancellationToken = default) =>
            Task.FromResult(Event(runId, patientId ?? "", PatientEventType.Admit, source, notes));

        public Task<PatientStateEvent> InjectDischargeAsync(Guid runId, string patientId, string source, string? notes = null, CancellationToken cancellationToken = default)
        {
            if (patientId == "blocked")
                throw new LiveInjectionException("closed", StatusCodes.Status409Conflict);
            return Task.FromResult(Event(runId, patientId, PatientEventType.Discharge, source, notes));
        }

        public Task<LivePatientPoolEntry> GenerateLivePoolPatientAsync(Guid runId, string source, CancellationToken cancellationToken = default) =>
            Task.FromResult(Entry("generated-1", LivePatientOrigin.Generated));

        public Task<LivePatientPoolEntry> UploadLivePoolPatientAsync(Guid runId, string content, string? fileName, string source, CancellationToken cancellationToken = default) =>
            Task.FromResult(Entry(content, LivePatientOrigin.Upload));

        public Task<LivePatientPoolEntry> ReferenceLivePoolPatientAsync(Guid runId, string patientId, string source, CancellationToken cancellationToken = default) =>
            Task.FromResult(Entry(patientId, LivePatientOrigin.FhirId));

        public Task<IReadOnlyList<PatientStateEvent>> GetLiveEventsAsync(Guid runId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<PatientStateEvent>>([Event(runId, "p1", PatientEventType.Admit, "API", null)]);

        public Task<LivePatientStateSnapshot> GetLivePatientStateAsync(Guid runId, CancellationToken cancellationToken = default) =>
            Task.FromResult(new LivePatientStateSnapshot
            {
                Admitted = ["p1"],
                DischargedDuringWindow = [],
                ExpectedPopulation = ["p1"],
                Pool = [Entry("p1", LivePatientOrigin.Cohort)],
                PoolTotals = new LivePatientPoolTotals { Total = 1, Admitted = 1 },
                AcceptingInjections = true,
                WindowStartUtc = DateTimeOffset.Parse("2026-10-07T15:00:00Z")
            });

        public Task<bool> CancelRunAsync(Guid runId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<AutomationRunIndexViewModel> GetRunsPageAsync(int pageNumber = 1, int pageSize = 20, string? sortBy = null, bool sortDescending = true, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<AutomationRunSummary?> GetRunForDisplayAsync(Guid runId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<bool> DeleteRunAsync(Guid runId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<LantanaGroup.Link.Automation.Link.Helpers.PipelineSummarySnapshotBuilder.PipelineSummarySnapshot?> GetPipelineSnapshotAsync(Guid runId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<LantanaGroup.Automation.Generation.GenerationManifestSnapshot?> GetGenerationManifestAsync(Guid runId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<LantanaGroup.Automation.Generation.AbsUploadSnapshot?> GetAbsUploadSnapshotAsync(Guid runId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<RunDashboardStats> GetDashboardStatsAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();

        private static PatientStateEvent Event(Guid runId, string patientId, PatientEventType type, string? source, string? notes) => new()
        {
            EventId = Guid.Parse("33333333-3333-3333-3333-333333333333"),
            RunId = runId,
            PatientId = patientId,
            EventType = type,
            TimestampUtc = DateTimeOffset.Parse("2026-10-07T15:02:00Z"),
            Source = source,
            Notes = notes
        };

        private static LivePatientPoolEntry Entry(string patientId, LivePatientOrigin origin) => new()
        {
            PatientId = patientId,
            Origin = origin,
            CensusState = LivePatientCensusState.NotAdmitted
        };
    }

    public sealed class ScenarioStore : IScenarioStore
    {
        public Task<TestScenarioDefinition?> GetByIdAsync(Guid id, CancellationToken ct = default)
        {
            if (id == ScenarioId)
                return Task.FromResult<TestScenarioDefinition?>(new TestScenarioDefinition { Id = id, Name = "Ad hoc report" });
            if (id == InvalidScenarioId || id == BoomScenarioId)
                return Task.FromResult<TestScenarioDefinition?>(new TestScenarioDefinition { Id = id, Name = id.ToString() });
            return Task.FromResult<TestScenarioDefinition?>(null);
        }

        public Task<List<TestScenarioDefinition>> GetAllAsync(CancellationToken ct = default) =>
            Task.FromResult(new List<TestScenarioDefinition>());

        public Task UpsertAsync(TestScenarioDefinition scenario, CancellationToken ct = default) => throw new NotSupportedException();
        public Task DeleteAsync(Guid id, CancellationToken ct = default) => throw new NotSupportedException();
    }

    public sealed class MetricsStore : IRunMetricsStore
    {
        public Task<(IReadOnlyList<AutomationRunMetricsDocument> Records, long TotalCount)> ListPageAsync(int pageNumber, int pageSize, Guid? scenarioId = null, CancellationToken cancellationToken = default) =>
            Task.FromResult(((IReadOnlyList<AutomationRunMetricsDocument>)[Document()], 1L));

        public Task<AutomationRunMetricsDocument?> GetAsync(Guid runId, CancellationToken cancellationToken = default) =>
            Task.FromResult<AutomationRunMetricsDocument?>(null);

        public Task UpsertAsync(AutomationRunMetricsDocument document, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<bool> DeleteAsync(Guid runId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<AutomationRunMetricsDocument>> ListSinceAsync(DateTimeOffset since, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<AutomationRunMetricsDocument>> ListByScenarioAsync(Guid scenarioId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<AutomationRunMetricsDocument?> GetPreviousAsync(Guid scenarioId, DateTimeOffset beforeFinishedAt, Guid excludeRunId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<AutomationRunMetricsDocument?> GetPreviousSucceededAsync(Guid scenarioId, DateTimeOffset beforeFinishedAt, Guid excludeRunId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<AutomationRunMetricsDocument?> GetPreviousSucceededSameFingerprintAsync(Guid scenarioId, string fingerprint, DateTimeOffset beforeFinishedAt, Guid excludeRunId, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        private static AutomationRunMetricsDocument Document() => new()
        {
            RunId = RunId,
            ScenarioName = "Ad hoc report",
            Outcome = "Succeeded",
            E2eDurationSeconds = 59,
            FinishedAt = DateTimeOffset.Parse("2026-10-07T15:01:00Z")
        };
    }

    public sealed class BenchmarkStore : IMetricsBenchmarkStore
    {
        private readonly Dictionary<string, AutomationMetricsBenchmarkDocument> _documents = new(StringComparer.Ordinal);

        public Task UpsertAsync(AutomationMetricsBenchmarkDocument document, CancellationToken cancellationToken = default)
        {
            _documents[document.Key] = document;
            return Task.CompletedTask;
        }

        public Task<AutomationMetricsBenchmarkDocument?> GetAsync(string key, CancellationToken cancellationToken = default) =>
            Task.FromResult(_documents.TryGetValue(key, out var document) ? document : null);

        public Task<(IReadOnlyList<AutomationMetricsBenchmarkDocument> Records, long TotalCount)> ListPageAsync(int pageNumber, int pageSize, CancellationToken cancellationToken = default) =>
            Task.FromResult(((IReadOnlyList<AutomationMetricsBenchmarkDocument>)_documents.Values.ToList(), (long)_documents.Count));
    }

    public sealed class HealthRuns : IApiHealthExecutionRunManager
    {
        private static readonly string ResultJson = JsonSerializer.Serialize(new object[]
        {
            new { endpointKey = "account.list", serviceName = "Account", endpointName = "List", passed = true, skipped = false, expectedStatusCode = 200 },
            new { endpointKey = "report.get", serviceName = "Report", endpointName = "Get schedule", passed = false, skipped = false, expectedStatusCode = 200, actualStatusCode = 500, errorMessage = "nope" },
            new { endpointKey = "census.skip", serviceName = "Census", endpointName = "Skip", passed = false, skipped = true, expectedStatusCode = 200 }
        });

        public Task<Guid> StartAllAsync() => Task.FromResult(Guid.Parse("44444444-4444-4444-4444-444444444444"));

        public bool TryGetRun(Guid runId, out ApiHealthRunInfo runInfo)
        {
            if (runId != HealthRunId)
            {
                runInfo = default!;
                return false;
            }

            runInfo = new ApiHealthRunInfo
            {
                RunId = HealthRunId,
                Scope = "All",
                Completed = true,
                Failed = false,
                StartedAt = DateTimeOffset.Parse("2026-10-07T15:00:00Z"),
                FinishedAt = DateTimeOffset.Parse("2026-10-07T15:01:00Z")
            };
            return true;
        }

        public IReadOnlyList<ApiHealthStoredEvent> GetEventsSince(Guid runId, long afterSequence)
        {
            if (runId != HealthRunId)
                return [];

            return ResultEvents();
        }

        private static IReadOnlyList<ApiHealthStoredEvent> ResultEvents()
        {
            using var document = JsonDocument.Parse(ResultJson);
            var events = new List<ApiHealthStoredEvent>();
            var sequence = 1L;
            foreach (var item in document.RootElement.EnumerateArray())
            {
                events.Add(new ApiHealthStoredEvent
                {
                    Sequence = sequence++,
                    EventName = "result",
                    Data = item.GetRawText()
                });
            }

            return events;
        }
    }
}
