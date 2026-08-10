using HealthChecks.UI.Client;
using LantanaGroup.Link.MockDmrpApi.Application.Extensions;
using LantanaGroup.Link.MockDmrpApi.Application.Middleware;
using LantanaGroup.Link.MockDmrpApi.Application.Services;
using LantanaGroup.Link.MockDmrpApi.Domain.Context;
using LantanaGroup.Link.MockDmrpApi.Domain.Entities;
using LantanaGroup.Link.MockDmrpApi.Settings;
using LantanaGroup.Link.Shared.Application.Extensions;
using LantanaGroup.Link.Shared.Application.Extensions.Security;
using LantanaGroup.Link.Shared.Application.Middleware;
using LantanaGroup.Link.Shared.Domain.Repositories.Implementations;
using LantanaGroup.Link.Shared.Domain.Repositories.Interceptors;
using LantanaGroup.Link.Shared.Domain.Repositories.Interfaces;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using System.Reflection;

var builder = WebApplication.CreateBuilder(args);

// Load external configuration first: Azure App Configuration is appended after the
// built-in sources, so anything it defines outranks appsettings and environment
// variables. Reading it before the availability check is deliberate -- the check should
// see the same values every other consumer of configuration sees.
builder.AddExternalConfiguration(DmrpApiConstants.ServiceName);

builder.Services.Configure<DmrpApiSettings>(
    builder.Configuration.GetSection(DmrpApiSettings.ConfigSectionName));

var enabled = DmrpAvailability.IsEnabled(builder.Environment, builder.Configuration);

// Must be registered before AddSQLServerEF resolves it.
builder.Services.AddSingleton<UpdateBaseEntityInterceptor>();
builder.AddSQLServerEF<ReportingPlanDbContext>(useUpdateBaseEntityInterceptor: true);

builder.Services.AddScoped<IBaseEntityRepository<ReportingPlanEntryEntity>,
                           BaseEntityRepository<ReportingPlanEntryEntity, ReportingPlanDbContext>>();

builder.Services.AddScoped<IReportingPlanService, ReportingPlanService>();
builder.Services.AddSingleton<IAuthTokenService, AuthTokenService>();

// Singleton because the delay is process state, not per-request state, and it is deliberately
// not persisted -- a restart clears it.
builder.Services.AddSingleton<IResponseDelayService, ResponseDelayService>();

// Link's own authentication, guarding the support surface at /api/mock-dmrp. The contract
// endpoints take the third party's token instead and opt out with [AllowAnonymous]; see
// DmrpController.
var allowAnonymousAccess = builder.Configuration.GetValue<bool>("Authentication:EnableAnonymousAccess");
builder.Services.AddLinkBearerServiceAuthentication(options =>
{
    options.Environment = builder.Environment;
    options.AllowAnonymous = allowAnonymousAccess;
    options.Authority = builder.Configuration.GetValue<string>("Authentication:Schemas:LinkBearer:Authority");
    options.ValidateToken = builder.Configuration.GetValue<bool>("Authentication:Schemas:LinkBearer:ValidateToken");
    options.ProtectKey = builder.Configuration.GetValue<bool>("DataProtection:Enabled");
    options.SigningKey = builder.Configuration.GetValue<string>("LinkTokenService:SigningKey");
});

builder.Services.AddControllers();

builder.Services.AddDmrpProblemDetails(
    builder.Environment,
    builder.Configuration.GetValue<bool>("ProblemDetails:IncludeExceptionDetails"));

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath))
    {
        options.IncludeXmlComments(xmlPath);
    }
});

// Health is registered in both modes so /health always answers. The database check is not:
// a disabled deployment deliberately skips migration (below), so probing the DbContext would
// report Unhealthy and a platform probe would restart the container in a loop -- which is the
// "looks like an outage" failure the disabled path exists to avoid. A dormant service has no
// database to be unhealthy about.
var healthChecks = builder.Services.AddHealthChecks();

if (enabled)
{
    healthChecks.AddDbContextCheck<ReportingPlanDbContext>("database");
}

var app = builder.Build();

if (enabled)
{
    // Resolve the token service here, while the host is still starting, so a missing or
    // too-short signing key stops the process rather than surfacing as a 500 on the first
    // token request. It is a singleton, so this is the only construction that happens.
    // Deliberately inside this branch and ahead of migration: a disabled deployment must
    // stay dormant rather than crash-loop, and a key that is going to fail should fail
    // before any schema is altered.
    _ = app.Services.GetRequiredService<IAuthTokenService>();

    app.AutoMigrateEF<ReportingPlanDbContext>();
}
else
{
    // A dormant deployment must not create or alter a schema. Health still answers, so the
    // container reports healthy rather than looking like an outage.
    app.Logger.LogWarning(
        "Mock DMRP API is disabled in the {Environment} environment. Every route except "
        + "{AllowedPaths} will answer 503, and schema migration has been skipped.",
        app.Environment.EnvironmentName,
        string.Join(", ", DmrpAvailability.AlwaysAvailablePaths));
}

// Before routing, so nothing added later can be reached while the service is disabled.
// Swagger is registered after it deliberately: a disabled deployment should not advertise
// a surface it will not serve.
app.UseDmrpAvailabilityGate();

// After the availability gate: a disabled deployment should refuse a request immediately
// rather than refuse it slowly. Before routing, so the delay models a slow upstream rather
// than slow handler code. Reaches the contract endpoints only -- see the middleware.
app.UseContractResponseDelay();

app.UseStatusCodePages();

// The developer page only outside deployment, as Terminology does: it renders the stack trace,
// which is what you want on a workstation and never what a caller should receive.
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}
else
{
    app.UseExceptionHandler();
}

// Reflected from the controllers, so it shows both surfaces as this service hosts them.
// It is NOT the contract: Contracts/dmrp-openapi.yaml describes only the two third-party
// endpoints, and says nothing about the support surface at /api/mock-dmrp.
app.ConfigureSwagger();

// Guards /api/mock-dmrp. The contract endpoints opt out with [AllowAnonymous] and check the
// third party's token themselves, so this scheme never sees them.
if (!allowAnonymousAccess)
{
    app.UseAuthentication();
    app.UseMiddleware<UserScopeMiddleware>();
}

app.UseAuthorization();

app.MapHealthChecks("/health", new HealthCheckOptions
{
    ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
});

app.MapInfo(Assembly.GetExecutingAssembly(), app.Configuration, "mock-dmrp");

app.MapControllers();

app.Run();
