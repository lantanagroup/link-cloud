using HealthChecks.UI.Client;
using LantanaGroup.Link.MockDmrpApi.Application.Middleware;
using LantanaGroup.Link.MockDmrpApi.Application.Services;
using LantanaGroup.Link.MockDmrpApi.Domain.Context;
using LantanaGroup.Link.MockDmrpApi.Domain.Entities;
using LantanaGroup.Link.MockDmrpApi.Presentation.Controllers;
using LantanaGroup.Link.MockDmrpApi.Settings;
using LantanaGroup.Link.Shared.Application.Extensions;
using LantanaGroup.Link.Shared.Domain.Repositories.Implementations;
using LantanaGroup.Link.Shared.Domain.Repositories.Interceptors;
using LantanaGroup.Link.Shared.Domain.Repositories.Interfaces;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using System.Reflection;

var builder = WebApplication.CreateBuilder(args);

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

builder.Services.AddControllers();

// NhsnAuthController delegates to DmrpController, so the concrete type has to be
// resolvable rather than only discovered as a controller.
builder.Services.AddScoped<DmrpController>();

builder.Services.AddProblemDetails();

builder.Services.AddHealthChecks()
    .AddDbContextCheck<ReportingPlanDbContext>("database");

var app = builder.Build();

if (enabled)
{
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
app.UseDmrpAvailabilityGate();

app.UseExceptionHandler();
app.UseStatusCodePages();

app.MapHealthChecks("/health", new HealthCheckOptions
{
    ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
});

app.MapInfo(Assembly.GetExecutingAssembly(), app.Configuration, "mock-dmrp");

app.MapControllers();

app.Run();
