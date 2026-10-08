using System.Reflection;
using Automation.UI.Services;
using Automation.UI.Services.Persistence;
using LantanaGroup.Link.Sdk.DependencyInjection;
using LantanaGroup.Link.Shared.Application.Extensions;
using LantanaGroup.Link.Shared.Application.Extensions.Security;
using LantanaGroup.Link.Shared.Application.Interfaces.Services.Security.Token;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using LantanaGroup.Link.Shared.Application.Services.Security.Token;
using Link.UI.Hubs;
using Link.UI.Models;
using Link.UI.Services;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Options;
using Yarp.ReverseProxy.Configuration;
using Yarp.ReverseProxy.Transforms;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration.AddStandardEnvironmentConfiguration();

var externalConfigSource = builder.Configuration
    .GetSection("ExternalConfigurationSource")
    .Get<string>();

if (!string.IsNullOrEmpty(externalConfigSource))
{
    var serviceName = builder.Configuration
        .GetSection("ServiceInformation:ServiceName")
        .Get<string>() ?? "Link UI";

    builder.AddExternalConfiguration(serviceName);
}

builder.Services.Configure<ServiceRegistry>(builder.Configuration.GetSection(ServiceRegistry.ConfigSectionName));
builder.Services.Configure<LinkTokenServiceSettings>(builder.Configuration.GetSection("LinkTokenService"));
builder.Services.Configure<LinkUiFeatureOptions>(options =>
{
    options.DmrpEnabled = builder.Configuration.GetValue<bool>("DMRP:Enabled");
    options.NumericOnlyFacilityId = builder.Configuration.GetValue<bool>("FacilityIdSettings:NumericOnlyFacilityId");
    options.AutomationEnabled = builder.Configuration.GetValue("LinkUi:AutomationEnabled", false);
});
builder.Services.AddMemoryCache();
builder.Services.AddScoped(FacilityHubService.Create);
builder.Services.AddScoped(FacilityViewService.Create);
builder.Services.AddScoped(ReportsService.Create);
builder.Services.Configure<LogsLinkOptions>(builder.Configuration.GetSection(LogsLinkOptions.SectionName));
builder.Services.AddScoped(LogsService.Create);
builder.Services.AddScoped(ConfigurationService.Create);
builder.Services.AddScoped(SystemService.Create);
builder.Services.AddSingleton(sp => AutomationRunReader.Create(
    sp.GetRequiredService<IConfiguration>(),
    sp.GetRequiredService<ILogger<AutomationRunReader>>()));
builder.Services.AddSingleton<AutomationOwnershipLookup>();
builder.Services.AddScoped(HomeOverviewService.Create);
builder.Services.AddScoped<IHomeOverview>(sp => sp.GetRequiredService<HomeOverviewService>());

builder.Services.AddSingleton<ICreateSystemToken, CreateSystemToken>();

var useBearerForServiceCalls = builder.Configuration.GetValue<bool?>("Authentication:UseBearerForServiceCalls") ?? true;
builder.Services.Configure<BackendAuthenticationServiceExtension.LinkBearerServiceOptions>(opts =>
{
    opts.AllowAnonymous = !useBearerForServiceCalls;
});

var allowAnonymousAccess = builder.Configuration.GetValue<bool>("Authentication:EnableAnonymousAccess");
var requireBffSession = builder.Configuration.GetValue<bool>("Authentication:RequireBffSession");

builder.Services.AddLinkSdk();

builder.Services.AddHttpContextAccessor();

// Browser proxy and the server-side user chip must share one Admin.BFF origin.
// ServiceRegistry:AdminBffServiceUrl is that origin (LinkSDK / Automation.UI convention).
var adminBffAddress = ResolveAdminBffAddress(builder.Configuration);

builder.Services.AddHttpClient<IAdminBffUserService, AdminBffUserService>((_, client) =>
{
    client.BaseAddress = new Uri(adminBffAddress);
    client.Timeout = TimeSpan.FromSeconds(15);
});
builder.Services.AddSingleton<KafkaOpsFixture>();
builder.Services.AddHttpClient<KafkaOpsClient>((_, client) =>
{
    client.BaseAddress = new Uri(adminBffAddress);
    client.Timeout = TimeSpan.FromSeconds(30);
});

var proxyRoutes = new List<RouteConfig>
{
    new()
    {
        RouteId = "admin-bff-api",
        ClusterId = "admin-bff",
        Order = int.MaxValue,
        Match = new RouteMatch { Path = "/api/{**catch-all}" }
    }
};
var proxyClusters = new List<ClusterConfig>
{
    new()
    {
        ClusterId = "admin-bff",
        Destinations = new Dictionary<string, DestinationConfig>(StringComparer.OrdinalIgnoreCase)
        {
            ["primary"] = new DestinationConfig { Address = adminBffAddress }
        }
    }
};

builder.Services.AddReverseProxy()
    .LoadFromMemory(proxyRoutes, proxyClusters)
    .AddTransforms(transformBuilder =>
    {
        // Admin.BFF login builds its post-auth RedirectUri from Referer by stripping the
        // last path segment and appending "/dashboard". A page such as /Placeholder/Reports
        // would otherwise land on /Placeholder/dashboard. Force the origin root so the
        // redirect is always {origin}/dashboard, which this host maps to the dashboard.
        transformBuilder.AddRequestTransform(transformContext =>
        {
            var request = transformContext.HttpContext.Request;
            if (!request.Path.StartsWithSegments("/api/login"))
                return default;

            if (!request.Host.HasValue)
                return default;

            transformContext.ProxyRequest.Headers.Referrer = new Uri($"{request.Scheme}://{request.Host}/");
            return default;
        });
    });

builder.Services.AddHealthChecks();
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor
        | ForwardedHeaders.XForwardedProto
        | ForwardedHeaders.XForwardedHost;
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});

builder.Services.AddControllersWithViews()
    .AddJsonOptions(opts =>
    {
        opts.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
    });

// RunHub and CleanupHub carry [Authorize]. The shell gate is the sign-in check.
// ApiBearer is a named scheme only. It is not the default authenticate or challenge scheme.
var apiBearerEnabled = Link.UI.Auth.ApiBearerAuthentication.Add(builder.Services, builder.Configuration);

var automationEnabled = builder.Configuration.GetValue("LinkUi:AutomationEnabled", false);
LinkAutomationEngineStatus? automationEngine = null;
if (automationEnabled)
    automationEngine = LinkAutomationEngine.Add(builder.Services, builder.Configuration);

builder.Services.AddProblemDetails(options =>
{
    options.CustomizeProblemDetails = context =>
    {
        context.ProblemDetails.Extensions["traceId"] = context.HttpContext.TraceIdentifier;
    };
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddSignalR();

var assemblyVersion = Assembly.GetExecutingAssembly()
    .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
    ?? Assembly.GetExecutingAssembly().GetName().Version?.ToString()
    ?? "0.0.0";

var telemetryEnabled = builder.Configuration.GetValue<bool>("Telemetry:EnableTelemetry");
if (telemetryEnabled)
{
    builder.Services.AddLinkTelemetry(builder.Configuration, options =>
    {
        options.Environment = builder.Environment;
        options.ServiceName = builder.Configuration["ServiceInformation:ServiceConfigName"] ?? "LinkUI";
        options.ServiceVersion = assemblyVersion;
    });
}

var app = builder.Build();

app.UseForwardedHeaders();

if (requireBffSession)
{
    app.Logger.LogInformation(
        "Authentication:RequireBffSession is true. Pages without an Admin.BFF session redirect to /api/login.");
}
else if (!allowAnonymousAccess && apiBearerEnabled)
{
    app.Logger.LogWarning(
        "Authentication:EnableAnonymousAccess is false. Pages, hubs, and native automation routes that are not bearer-protected return 503. Bearer routes stay on ApiBearer. /health and the Admin.BFF proxy stay open. Set Authentication:RequireBffSession to true to require an Admin.BFF session for pages.");
}
else if (!allowAnonymousAccess)
{
    app.Logger.LogWarning(
        "Authentication:EnableAnonymousAccess is false. Non-health requests outside the Admin.BFF proxy will be rejected with 503. The automation HTTP API is included while Authentication:ApiBearer:Enabled is false. Set Authentication:RequireBffSession to true to require an Admin.BFF session for pages.");
}
else
{
    app.Logger.LogInformation(
        "Authentication:EnableAnonymousAccess is true. The shell is browsable without an Admin.BFF session.");
}

app.Use(async (context, next) =>
{
    // Pages read this options object. The gate uses the same value so a late
    // configuration source cannot leave a route up after the nav has hidden it.
    var automationOn = context.RequestServices.GetRequiredService<IOptions<LinkUiFeatureOptions>>().Value.AutomationEnabled;
    if (!automationOn && AutomationSurface.IsAutomationPath(context.Request.Path))
    {
        context.Response.StatusCode = StatusCodes.Status404NotFound;
        return;
    }

    if (ShellAccessGate.IsClosedNativeApi(allowAnonymousAccess, apiBearerEnabled, context.Request.Path))
    {
        context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
        context.Response.ContentType = "text/plain; charset=utf-8";
        await context.Response.WriteAsync(ShellAccessGate.AnonymousBlockedMessage, context.RequestAborted);
        return;
    }

    AdminBffUser? user = null;
    if (requireBffSession && !ShellAccessGate.IsSessionPublic(context.Request.Path))
    {
        var userService = context.RequestServices.GetRequiredService<IAdminBffUserService>();
        user = await userService.GetCurrentUserAsync(context.RequestAborted);
    }

    var decision = ShellAccessGate.Evaluate(
        allowAnonymousAccess,
        requireBffSession,
        context.Request.Path,
        user,
        out var message);

    switch (decision)
    {
        case ShellAccessGate.Decision.Continue:
            if (user is { IsAuthenticated: true })
                context.Items[AdminBffUserService.HttpContextItemKey] = user;
            await next();
            return;
        case ShellAccessGate.Decision.RedirectToLogin:
            context.Response.Redirect("/api/login");
            return;
        default:
            context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            context.Response.ContentType = "text/plain; charset=utf-8";
            await context.Response.WriteAsync(message ?? ShellAccessGate.AnonymousBlockedMessage, context.RequestAborted);
            return;
    }
});

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
    app.UseHttpsRedirection();
}

app.UseStaticFiles();
app.UseWebSockets();
app.UseRouting();
if (apiBearerEnabled)
    app.UseAuthentication();
app.UseAuthorization();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.MapHub<LinkStubHub>("/hubs/link");
if (automationEnabled)
{
    app.MapHub<RunHub>("/hubs/runs");
    app.MapHub<CleanupHub>("/hubs/cleanup");
}
app.MapHealthChecks("/health");
app.MapReverseProxy();

if (automationEngine is { Ready: true })
{
    try
    {
        app.Services.GetRequiredService<MongoIndexManager>().EnsureAllIndexes();
    }
    catch (Exception ex)
    {
        app.Logger.LogWarning(
            "Automation indexes could not be ensured ({ExceptionType}).",
            ex.GetType().Name);
    }
}
else if (automationEnabled)
{
    app.Logger.LogWarning("Automation run engine is off. {Message}", automationEngine?.Message);
}

app.Run();

static string ResolveAdminBffAddress(IConfiguration configuration)
{
    var address = configuration["ServiceRegistry:AdminBffServiceUrl"];
    if (string.IsNullOrWhiteSpace(address))
        address = "http://localhost:8063";

    return address.TrimEnd('/') + "/";
}

public partial class Program
{
}


