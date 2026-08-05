using HealthChecks.UI.Client;
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

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<DmrpApiSettings>(
    builder.Configuration.GetSection(DmrpApiSettings.ConfigSectionName));

// Must be registered before AddSQLServerEF resolves it.
builder.Services.AddSingleton<UpdateBaseEntityInterceptor>();
builder.AddSQLServerEF<ReportingPlanDbContext>(useUpdateBaseEntityInterceptor: true);

builder.Services.AddScoped<IBaseEntityRepository<ReportingPlanEntryEntity>,
                           BaseEntityRepository<ReportingPlanEntryEntity, ReportingPlanDbContext>>();

builder.Services.AddScoped<IReportingPlanService, ReportingPlanService>();
builder.Services.AddSingleton<IAuthTokenService, AuthTokenService>();

builder.Services.AddControllers();

// DmrpAliasController delegates to DmrpController, so the concrete type has to be
// resolvable rather than only discovered as a controller.
builder.Services.AddScoped<DmrpController>();

builder.Services.AddProblemDetails();

builder.Services.AddHealthChecks()
    .AddDbContextCheck<ReportingPlanDbContext>("database");

var app = builder.Build();

app.AutoMigrateEF<ReportingPlanDbContext>();

app.UseExceptionHandler();
app.UseStatusCodePages();

app.MapHealthChecks("/health", new HealthCheckOptions
{
    ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
});

app.MapControllers();

app.Run();
