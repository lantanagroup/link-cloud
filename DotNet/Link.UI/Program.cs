using System.Reflection;
using LantanaGroup.Link.Sdk.DependencyInjection;
using LantanaGroup.Link.Shared.Application.Extensions;
using LantanaGroup.Link.Shared.Application.Extensions.Security;
using LantanaGroup.Link.Shared.Application.Interfaces.Services.Security.Token;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using LantanaGroup.Link.Shared.Application.Services.Security.Token;
using Link.UI.Hubs;
using Link.UI.Services;
using Microsoft.AspNetCore.HttpOverrides;
using Yarp.ReverseProxy.Configuration;

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
builder.Services.AddHttpClient<IAdminBffUserService, AdminBffUserService>((sp, client) =>
{
    var registry = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<ServiceRegistry>>().Value;
    var baseUrl = registry.AdminBffServiceUrl?.TrimEnd('/') ?? "http://localhost:8063";
    client.BaseAddress = new Uri(baseUrl + "/");
    client.Timeout = TimeSpan.FromSeconds(15);
});

var adminBffAddress = builder.Configuration["ReverseProxy:Clusters:admin-bff:Destinations:primary:Address"]
    ?? builder.Configuration["ServiceRegistry:AdminBffServiceUrl"]
    ?? "http://localhost:8063/";

if (!adminBffAddress.EndsWith('/'))
    adminBffAddress += "/";

builder.Services.AddReverseProxy()
    .LoadFromMemory(
        new[]
        {
            new RouteConfig
            {
                RouteId = "admin-bff-api",
                ClusterId = "admin-bff",
                Match = new RouteMatch { Path = "/api/{**catch-all}" }
            }
        },
        new[]
        {
            new ClusterConfig
            {
                ClusterId = "admin-bff",
                Destinations = new Dictionary<string, DestinationConfig>(StringComparer.OrdinalIgnoreCase)
                {
                    ["primary"] = new DestinationConfig { Address = adminBffAddress }
                }
            }
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

if (!allowAnonymousAccess)
{
    app.Logger.LogWarning(
        "Authentication:EnableAnonymousAccess is false. Non-health, non-api requests require an upstream authenticating proxy or Admin.BFF session.");

    app.Use(async (context, next) =>
    {
        if (context.Request.Path.StartsWithSegments("/health")
            || context.Request.Path.StartsWithSegments("/api"))
        {
            await next();
            return;
        }

        context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
        context.Response.ContentType = "text/plain; charset=utf-8";
        await context.Response.WriteAsync(
            "Link UI is not configured to serve requests anonymously in this environment. " +
            "Set Authentication:EnableAnonymousAccess to true when deploying behind an authenticating proxy, " +
            "or enable Authentication:RequireBffSession with Admin.BFF available.");
    });
}

if (requireBffSession && allowAnonymousAccess)
{
    app.Use(async (context, next) =>
    {
        if (context.Request.Path.StartsWithSegments("/health")
            || context.Request.Path.StartsWithSegments("/api")
            || context.Request.Path.StartsWithSegments("/Auth")
            || context.Request.Path.StartsWithSegments("/swagger")
            || context.Request.Path.StartsWithSegments("/hubs")
            || Path.HasExtension(context.Request.Path.Value))
        {
            await next();
            return;
        }

        var userService = context.RequestServices.GetRequiredService<IAdminBffUserService>();
        var user = await userService.GetCurrentUserAsync(context.RequestAborted);
        if (user is null || !user.IsAuthenticated)
        {
            context.Response.Redirect("/api/login");
            return;
        }

        context.Items[AdminBffUserService.HttpContextItemKey] = user;
        await next();
    });
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
    app.UseHttpsRedirection();
}

app.UseStaticFiles();
app.UseRouting();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.MapHub<LinkStubHub>("/hubs/link");
app.MapHealthChecks("/health");
app.MapReverseProxy();

app.Run();
