using Automation.UI.Services;
using Automation.UI.Services;
using Automation.UI.Services.Persistence;
using LantanaGroup.Link.Automation.Link.Configuration;
using LantanaGroup.Link.Automation.Link.Helpers;
using LantanaGroup.Link.Sdk.DependencyInjection;
using LantanaGroup.Link.Shared.Application.Extensions;
using LantanaGroup.Link.Shared.Application.Extensions.Security;
using LantanaGroup.Link.Shared.Application.Interfaces.Services.Security.Token;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using LantanaGroup.Link.Shared.Application.Services.Security.Token;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.AspNetCore.HttpOverrides;
using MongoDB.Driver;

var builder = WebApplication.CreateBuilder(args);

// -- Standard environment configuration (env vars, .env files, substitution) --
builder.Configuration.AddStandardEnvironmentConfiguration();

// -- External configuration (Azure App Configuration when deployed) --
var externalConfigSource = builder.Configuration
    .GetSection("ExternalConfigurationSource")
    .Get<string>();

if (!string.IsNullOrEmpty(externalConfigSource))
{
    var serviceName = builder.Configuration
        .GetSection("ServiceInformation:ServiceName")
        .Get<string>() ?? "Link Automation UI";

    builder.AddExternalConfiguration(serviceName);
}

// -- Bind options --
builder.Services.Configure<AutomationConfig>(builder.Configuration.GetSection("Automation"));

var grafanaBaseUrlFallback =
    builder.Configuration["GRAFANA_URL"]
    ?? builder.Configuration["GRAFANA_BASE_URL"]
    ?? builder.Configuration["GrafanaBaseUrl"];

if (!string.IsNullOrWhiteSpace(grafanaBaseUrlFallback))
{
    builder.Services.PostConfigure<AutomationConfig>(cfg =>
    {
        if (string.IsNullOrWhiteSpace(cfg.GrafanaBaseUrl)
            || string.Equals(cfg.GrafanaBaseUrl, "http://localhost:3000", StringComparison.OrdinalIgnoreCase))
        {
            cfg.GrafanaBaseUrl = grafanaBaseUrlFallback.TrimEnd('/');
        }
    });
}

builder.Services.Configure<ServiceRegistry>(builder.Configuration.GetSection(ServiceRegistry.ConfigSectionName));
builder.Services.Configure<LinkTokenServiceSettings>(builder.Configuration.GetSection("LinkTokenService"));

// -- Token services required by LinkSdk service clients for service-to-service calls --
builder.Services.AddSingleton<ICreateSystemToken, CreateSystemToken>();

// -- Link bearer token configuration for inter-service calls via LinkSdk --
var useBearerForServiceCalls = builder.Configuration.GetValue<bool?>("Authentication:UseBearerForServiceCalls") ?? true;

builder.Services.Configure<BackendAuthenticationServiceExtension.LinkBearerServiceOptions>(opts =>
{
    opts.AllowAnonymous = !useBearerForServiceCalls;
});

// External authentication is handled at the infrastructure layer (domain-level
// OAuth2 via reverse proxy / gateway). The app itself does not enforce
// inbound authentication -- all authorization policies are pass-through.
//
// To avoid exposing the UI when deployed somewhere *without* that upstream
// authentication in place, anonymous access is gated by an opt-in flag that
// mirrors the convention used by `Admin.BFF` and other Link services
// (`Authentication:EnableAnonymousAccess`, default = false). When the flag is
// false, a terminal short-circuit middleware below returns 503 for all
// non-health requests, so a misconfigured deployment fails closed rather than
// serving the UI anonymously.
var allowAnonymousAccess = builder.Configuration
    .GetValue<bool>("Authentication:EnableAnonymousAccess");

builder.Services.AddAuthorization(options =>
{
    options.DefaultPolicy = new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder()
        .RequireAssertion(_ => true)
        .Build();
});

// -- LinkSdk service clients (all resolve URLs from ServiceRegistry) --
builder.Services.AddLinkSdk();

// -- MongoDB persistence --
// Keys match Azure App Configuration: MongoDB:ConnectionString (shared, no label)
// and MongoDB:DatabaseName (per-service via label "Link Automation UI").
// In deployed environments these come from Key Vault references via App Config.
var mongoConnectionString = builder.Configuration.GetValue<string>("MongoDB:ConnectionString");
var mongoDatabaseName = builder.Configuration.GetValue<string>("MongoDB:DatabaseName");

// For local host access to Docker Mongo replica-set (rs0), ensure required query params exist.
if (!string.IsNullOrWhiteSpace(mongoConnectionString)
    && (mongoConnectionString.Contains("localhost:17017", StringComparison.OrdinalIgnoreCase)
        || mongoConnectionString.Contains("127.0.0.1:17017", StringComparison.OrdinalIgnoreCase)))
{
    var hasReplicaSet = mongoConnectionString.Contains("replicaSet=", StringComparison.OrdinalIgnoreCase);
    var hasDirectConnection = mongoConnectionString.Contains("directConnection=", StringComparison.OrdinalIgnoreCase);
    if (!hasReplicaSet || !hasDirectConnection)
    {
        var separator = mongoConnectionString.Contains('?') ? "&" : "?";
        if (!hasReplicaSet)
        {
            mongoConnectionString += $"{separator}replicaSet=rs0";
            separator = "&";
        }

        if (!hasDirectConnection)
            mongoConnectionString += $"{separator}directConnection=true";
    }
}

var mongoUrl = new MongoUrl(mongoConnectionString);
var mongoClientSettings = MongoClientSettings.FromUrl(mongoUrl);

builder.Services.AddSingleton<IMongoClient>(_ => new MongoClient(mongoClientSettings));
builder.Services.AddSingleton(sp => sp.GetRequiredService<IMongoClient>().GetDatabase(mongoDatabaseName));

var dataProtectionApplicationName = builder.Configuration.GetValue<string>("DataProtection:ApplicationName")
    ?? $"Link.Automation.UI:{builder.Environment.EnvironmentName}";
var dataProtectionKeyCollectionName = builder.Configuration.GetValue<string>("DataProtection:KeyCollectionName")
    ?? "automation_data_protection_keys";

builder.Services.AddSingleton(new MongoDataProtectionOptions
{
    ApplicationName = dataProtectionApplicationName,
    KeyCollectionName = dataProtectionKeyCollectionName
});
builder.Services.AddSingleton<MongoDataProtectionXmlRepository>();
builder.Services.AddDataProtection()
    .SetApplicationName(dataProtectionApplicationName);
builder.Services.AddOptions<KeyManagementOptions>()
    .Configure<MongoDataProtectionXmlRepository>((options, repository) =>
    {
        options.XmlRepository = repository;
    });

builder.Services.AddSingleton<MongoIndexManager>();
builder.Services.AddSingleton<ISnapshotStore, MongoSnapshotStore>();
builder.Services.AddSingleton<IScenarioStore, MongoScenarioStore>();
builder.Services.AddSingleton<IQueryPlanTemplateStore, MongoQueryPlanTemplateStore>();
builder.Services.AddSingleton<IApiHealthRunStore, MongoApiHealthRunStore>();

// -- API Health test suites --
builder.Services.AddSingleton<Automation.UI.Services.ApiHealth.TestSuites.IServiceTestSuite, Automation.UI.Services.ApiHealth.TestSuites.TenantServiceTestSuite>();
builder.Services.AddSingleton<Automation.UI.Services.ApiHealth.TestSuites.IServiceTestSuite, Automation.UI.Services.ApiHealth.TestSuites.DataAcquisitionTestSuite>();
builder.Services.AddSingleton<Automation.UI.Services.ApiHealth.TestSuites.IServiceTestSuite, Automation.UI.Services.ApiHealth.TestSuites.NormalizationTestSuite>();
builder.Services.AddSingleton<Automation.UI.Services.ApiHealth.TestSuites.IServiceTestSuite, Automation.UI.Services.ApiHealth.TestSuites.CensusServiceTestSuite>();
builder.Services.AddSingleton<Automation.UI.Services.ApiHealth.TestSuites.IServiceTestSuite, Automation.UI.Services.ApiHealth.TestSuites.ReportServiceTestSuite>();
builder.Services.AddSingleton<Automation.UI.Services.ApiHealth.TestSuites.IServiceTestSuite, Automation.UI.Services.ApiHealth.TestSuites.QueryDispatchTestSuite>();
builder.Services.AddSingleton<Automation.UI.Services.ApiHealth.TestSuites.IServiceTestSuite, Automation.UI.Services.ApiHealth.TestSuites.SubmissionServiceTestSuite>();
builder.Services.AddSingleton<Automation.UI.Services.ApiHealth.TestSuites.IServiceTestSuite, Automation.UI.Services.ApiHealth.TestSuites.MeasureEvalTestSuite>();
builder.Services.AddSingleton<Automation.UI.Services.ApiHealth.TestSuites.IServiceTestSuite, Automation.UI.Services.ApiHealth.TestSuites.ValidationServiceTestSuite>();
builder.Services.AddSingleton<Automation.UI.Services.ApiHealth.TestSuites.IServiceTestSuite, Automation.UI.Services.ApiHealth.TestSuites.AdminBffTestSuite>();
builder.Services.AddSingleton<Automation.UI.Services.ApiHealth.ApiEndpointRegistry>();
builder.Services.AddSingleton<Automation.UI.Services.ApiHealth.ApiHealthExecutionRunManager>();
builder.Services.AddSingleton<Automation.UI.Services.ApiHealth.Seeding.IApiHealthSeedContextAccessor, Automation.UI.Services.ApiHealth.Seeding.ApiHealthSeedContextAccessor>();
builder.Services.AddSingleton<Automation.UI.Services.ApiHealth.Seeding.IApiHealthSeedOrchestrator, Automation.UI.Services.ApiHealth.Seeding.ApiHealthSeedOrchestrator>();
builder.Services.AddHostedService<ScenarioRunStartupRecoveryService>();
builder.Services.AddHostedService<Automation.UI.Services.ApiHealth.ApiHealthStartupRecoveryService>();
builder.Services.AddHttpClient("ApiHealthTest");
builder.Services.AddHealthChecks();
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor
        | ForwardedHeaders.XForwardedProto
        | ForwardedHeaders.XForwardedHost;

    // In container/cloud proxy scenarios, allow forwarded headers from the front-end proxy.
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});

// -- Pipeline data reader (scoped so each poller gets its own) --
builder.Services.AddScoped<PipelineDataReader>();

// -- Seed system scenarios and query plan templates --
builder.Services.AddHostedService<ScenarioSeedService>();
builder.Services.AddHostedService<QueryPlanTemplateSeedService>();

// -- Seed synthetic runs for dashboard verification.
//    Gated on config (Dashboard:SeedFakeRuns). Used for Debugging Dashbhoard.
if (builder.Configuration.GetValue<bool?>("Dashboard:SeedFakeRuns") ?? false)
{
    builder.Services.AddHostedService<DashboardSeedService>();
}

// -- MVC + SignalR --
builder.Services.AddControllersWithViews()
    .AddJsonOptions(opts =>
    {
        opts.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
    });
builder.Services.AddSignalR();
builder.Services.AddSingleton<RunSnapshotOrchestrator>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<RunSnapshotOrchestrator>());
builder.Services.AddSingleton<IAutomationRunManager, AutomationRunManager>();
builder.Services.AddSingleton<IRunExportService, RunExportService>();

var app = builder.Build();

// -- Ensure MongoDB indexes (Cosmos DB compatible) --
app.Services.GetRequiredService<MongoIndexManager>().EnsureAllIndexes();

// -- Respect reverse-proxy forwarded headers before redirect/auth logic --
app.UseForwardedHeaders();

// -- Anonymous-access guard --
// When Authentication:EnableAnonymousAccess is false (the default), the UI must
// not serve content because it has no built-in authentication. /health is
// exempted so infrastructure probes keep working. Operators opt in by setting
// the flag to true only after ensuring an upstream authenticating proxy is in
// place (as is done for the docker-compose deployment).
if (!allowAnonymousAccess)
{
    app.Logger.LogWarning(
        "Authentication:EnableAnonymousAccess is false. All non-health requests will be rejected with 503. " +
        "Set the flag to true only when deploying behind an authenticating proxy.");

    app.Use(async (context, next) =>
    {
        if (context.Request.Path.StartsWithSegments("/health"))
        {
            await next();
            return;
        }

        context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
        context.Response.ContentType = "text/plain; charset=utf-8";
        await context.Response.WriteAsync(
            "Link Automation UI is not configured to serve requests in this environment. " +
            "Set 'Authentication:EnableAnonymousAccess' to true only when deploying behind an authenticating proxy.");
    });
}
else
{
    app.Logger.LogInformation(
        "Authentication:EnableAnonymousAccess is true. The UI is accessible anonymously; " +
        "upstream authentication is assumed to be in place.");
}

// -- Middleware --
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Runs/Index");
    app.UseHsts();
    app.UseHttpsRedirection();
}

app.UseStaticFiles();

app.UseRouting();

app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Runs}/{action=Index}/{id?}");

app.MapHub<RunHub>("/hubs/runs");
app.MapHealthChecks("/health");

app.Run();
