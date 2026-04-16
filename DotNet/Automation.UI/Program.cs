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
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
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
builder.Services.Configure<ServiceRegistry>(builder.Configuration.GetSection(ServiceRegistry.ConfigSectionName));
builder.Services.Configure<LinkTokenServiceSettings>(builder.Configuration.GetSection("LinkTokenService"));

// -- Token services required by LinkSdk service clients for service-to-service calls --
builder.Services.AddSingleton<ICreateSystemToken, CreateSystemToken>();

// -- Data Protection key persistence (shared key ring across restarts/instances) --
var dataProtectionAppName = builder.Configuration.GetValue<string>("DataProtection:ApplicationName") ?? "Link.Automation.UI";
var dataProtectionKeyRingPath = builder.Configuration.GetValue<string>("DataProtection:KeyRingPath");

var dataProtectionBuilder = builder.Services
    .AddDataProtection()
    .SetApplicationName(dataProtectionAppName);

if (!string.IsNullOrWhiteSpace(dataProtectionKeyRingPath))
{
    var configuredKeyRingPath = Path.IsPathRooted(dataProtectionKeyRingPath)
        ? dataProtectionKeyRingPath
        : Path.Combine(builder.Environment.ContentRootPath, dataProtectionKeyRingPath);

    try
    {
        Directory.CreateDirectory(configuredKeyRingPath);

        // Probe write access now so we fail over before runtime antiforgery/key writes.
        var probePath = Path.Combine(configuredKeyRingPath, $".dp-write-test-{Guid.NewGuid():N}.tmp");
        File.WriteAllText(probePath, "ok");
        File.Delete(probePath);

        dataProtectionBuilder.PersistKeysToFileSystem(new DirectoryInfo(configuredKeyRingPath));
    }
    catch (Exception ex)
    {
        var fallbackKeyRingPath = Path.Combine(Path.GetTempPath(), "link-automation-ui-dataprotection");
        try
        {
            Directory.CreateDirectory(fallbackKeyRingPath);
            dataProtectionBuilder.PersistKeysToFileSystem(new DirectoryInfo(fallbackKeyRingPath));
            Console.WriteLine($"[WARN] DataProtection key ring path '{configuredKeyRingPath}' is not writable. Falling back to '{fallbackKeyRingPath}'. Error: {ex.Message}");
        }
        catch (Exception fallbackEx)
        {
            Console.WriteLine($"[WARN] Failed to configure DataProtection key persistence. Configured='{configuredKeyRingPath}', fallback='{fallbackKeyRingPath}'. Error: {fallbackEx.Message}");
        }
    }
}

// -- Authentication / authorization --
var enableAnonymousAccess = builder.Configuration.GetValue<bool>("Authentication:EnableAnonymousAccess");
var useBearerForServiceCalls = builder.Configuration.GetValue<bool?>("Authentication:UseBearerForServiceCalls") ?? true;

// Configure whether LinkSdk clients attach bearer tokens on outbound calls.
// This is intentionally decoupled from inbound UI auth mode.
builder.Services.Configure<BackendAuthenticationServiceExtension.LinkBearerServiceOptions>(opts =>
{
    opts.AllowAnonymous = !useBearerForServiceCalls;
});

if (enableAnonymousAccess)
{
    // Development bypass: no schemes registered, all policies pass-through.
    builder.Services.AddAuthorization(options =>
    {
        options.DefaultPolicy = new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder()
            .RequireAssertion(_ => true)
            .Build();
    });
}
else
{
    var oidcAuthority = builder.Configuration.GetValue<string>("Authentication:Schemas:OpenIdConnect:Authority")
        ?? throw new InvalidOperationException("Authentication:Schemas:OpenIdConnect:Authority is required when anonymous access is disabled.");
    var oidcClientId = builder.Configuration.GetValue<string>("Authentication:Schemas:OpenIdConnect:ClientId")
        ?? throw new InvalidOperationException("Authentication:Schemas:OpenIdConnect:ClientId is required when anonymous access is disabled.");
    var oidcClientSecret = builder.Configuration.GetValue<string>("Authentication:Schemas:OpenIdConnect:ClientSecret") ?? "";
    var oidcCallbackPath = builder.Configuration.GetValue<string>("Authentication:Schemas:OpenIdConnect:CallbackPath") ?? "/signin-oidc";

    builder.Services
        .AddAuthentication(options =>
        {
            options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = OpenIdConnectDefaults.AuthenticationScheme;
        })
        .AddCookie(options =>
        {
            options.Cookie.HttpOnly = true;
            options.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
                ? CookieSecurePolicy.SameAsRequest
                : CookieSecurePolicy.Always;
            options.Cookie.SameSite = SameSiteMode.Lax;
            options.LoginPath = "/Account/Login";
            options.LogoutPath = "/Account/Logout";
            options.ExpireTimeSpan = TimeSpan.FromHours(8);
            options.SlidingExpiration = true;
        })
        .AddOpenIdConnect(options =>
        {
            options.Authority = oidcAuthority;
            options.ClientId = oidcClientId;
            options.ClientSecret = oidcClientSecret;
            options.ResponseType = OpenIdConnectResponseType.Code;
            options.CallbackPath = oidcCallbackPath;
            // Tokens are not saved in the cookie — service calls use system tokens
            // (ICreateSystemToken), not the user's token. Change only if user-delegated calls are added.
            options.SaveTokens = false;
            options.MapInboundClaims = false;
            options.RequireHttpsMetadata = !builder.Environment.IsDevelopment();
            options.Scope.Add("email");
            options.GetClaimsFromUserInfoEndpoint = true;
        });

    builder.Services.AddAuthorization();
}

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
builder.Services.AddSingleton<MongoIndexManager>();
builder.Services.AddSingleton<ISnapshotStore, MongoSnapshotStore>();
builder.Services.AddSingleton<IScenarioStore, MongoScenarioStore>();
builder.Services.AddSingleton<IQueryPlanTemplateStore, MongoQueryPlanTemplateStore>();
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

var app = builder.Build();

// -- Ensure MongoDB indexes (Cosmos DB compatible) --
app.Services.GetRequiredService<MongoIndexManager>().EnsureAllIndexes();

// -- Respect reverse-proxy forwarded headers before redirect/auth logic --
app.UseForwardedHeaders();

// -- Middleware --
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Runs/Index");
    app.UseHsts();
    app.UseHttpsRedirection();
}

app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Runs}/{action=Index}/{id?}");

app.MapHub<RunHub>("/hubs/runs");
app.MapHealthChecks("/health");

app.Run();
