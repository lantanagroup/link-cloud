using Automation.UI.Services;
using Automation.UI.Services.Persistence;
using LantanaGroup.Link.Automation.Configuration;
using LantanaGroup.Link.Automation.Helpers;
using LantanaGroup.Link.Sdk.DependencyInjection;
using LantanaGroup.Link.Shared.Application.Extensions;
using LantanaGroup.Link.Shared.Application.Extensions.Security;
using LantanaGroup.Link.Shared.Application.Interfaces.Services.Security.Token;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using LantanaGroup.Link.Shared.Application.Services.Security.Token;
using MongoDB.Driver;

var builder = WebApplication.CreateBuilder(args);

// ── Standard environment configuration (env vars, .env files, substitution) ──
builder.Configuration.AddStandardEnvironmentConfiguration();

// ── External configuration (Azure App Configuration when deployed) ──
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

// ── Bind options ──
builder.Services.Configure<AutomationConfig>(builder.Configuration.GetSection("Automation"));
builder.Services.Configure<ServiceRegistry>(builder.Configuration.GetSection(ServiceRegistry.ConfigSectionName));
builder.Services.Configure<LinkTokenServiceSettings>(builder.Configuration.GetSection("LinkTokenService"));

// ── Authentication / token plumbing ──
bool allowAnonymousAccess = builder.Configuration.GetValue<bool>("Authentication:EnableAnonymousAccess");
builder.Services.Configure<BackendAuthenticationServiceExtension.LinkBearerServiceOptions>(opts =>
{
    opts.AllowAnonymous = allowAnonymousAccess;
});
builder.Services.AddSingleton<ICreateSystemToken, CreateSystemToken>();

// ── LinkSdk service clients (all resolve URLs from ServiceRegistry) ──
builder.Services.AddLinkSdk();

// ── MongoDB persistence ──
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
builder.Services.AddSingleton<ISnapshotStore, MongoSnapshotStore>();

// ── Pipeline data reader (scoped so each poller gets its own) ──
builder.Services.AddScoped<PipelineDataReader>();

// ── MVC + SignalR ──
builder.Services.AddControllersWithViews();
builder.Services.AddSignalR();
builder.Services.AddSingleton<RunSnapshotOrchestrator>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<RunSnapshotOrchestrator>());
builder.Services.AddSingleton<IAutomationRunManager, AutomationRunManager>();

var app = builder.Build();

// ── Middleware ──
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Runs/Index");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Runs}/{action=Index}/{id?}");

app.MapHub<RunHub>("/hubs/runs");

app.Run();
