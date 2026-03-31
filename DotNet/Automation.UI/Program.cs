using Automation.UI.Services;
using LantanaGroup.Link.Automation.Configuration;
using LantanaGroup.Link.Sdk.DependencyInjection;
using LantanaGroup.Link.Shared.Application.Extensions;
using LantanaGroup.Link.Shared.Application.Extensions.Security;
using LantanaGroup.Link.Shared.Application.Interfaces.Services.Security.Token;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using LantanaGroup.Link.Shared.Application.Services.Security.Token;

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

// ── MVC + SignalR ──
builder.Services.AddControllersWithViews();
builder.Services.AddSignalR();
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
