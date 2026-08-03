using System.Diagnostics;
using System.Reflection;
using HealthChecks.UI.Client;
using LantanaGroup.Link.DMRP.Business.Managers;
using LantanaGroup.Link.DMRP.Business.Queries;
using LantanaGroup.Link.DMRP.Config;
using LantanaGroup.Link.DMRP.Data.Entities;
using LantanaGroup.Link.DMRP.Data.Repository;
using LantanaGroup.Link.DMRP.Services;
using LantanaGroup.Link.Shared.Application.Extensions;
using LantanaGroup.Link.Shared.Application.Extensions.Security;
using LantanaGroup.Link.Shared.Application.Health;
using LantanaGroup.Link.Shared.Application.Middleware;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using LantanaGroup.Link.Shared.Domain.Repositories.Interceptors;
using LantanaGroup.Link.Shared.Domain.Repositories.Interfaces;
using LantanaGroup.Link.Shared.Settings;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Serilog;
using Serilog.Debugging;
using Serilog.Enrichers.Span;
using Serilog.Exceptions;
using Serilog.Settings.Configuration;

namespace DMRP
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);
            builder.Configuration.AddStandardEnvironmentConfiguration();

            RegisterServices(builder);

            var app = builder.Build();

            SetupMiddleware(app);

            app.Run();
        }

        #region Register Services

        static void RegisterServices(WebApplicationBuilder builder)
        {
            // load external configuration source (if specified)
            builder.AddExternalConfiguration(DmrpConstants.ServiceName);

            // Add Link Security
            bool allowAnonymousAccess = builder.Configuration.GetValue<bool>("Authentication:EnableAnonymousAccess");
            builder.Services.AddLinkBearerServiceAuthentication(options =>
            {
                options.Environment = builder.Environment;
                options.AllowAnonymous = allowAnonymousAccess;
                options.Authority = builder.Configuration.GetValue<string>("Authentication:Schemas:LinkBearer:Authority");
                options.ValidateToken = builder.Configuration.GetValue<bool>("Authentication:Schemas:LinkBearer:ValidateToken");
                options.ProtectKey = builder.Configuration.GetValue<bool>("DataProtection:Enabled");
                options.SigningKey = builder.Configuration.GetValue<string>("LinkTokenService:SigningKey");
            });

            var assemblyVersion = Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? string.Empty;

            var serviceInformation = builder.SetupServiceInformation(DmrpConstants.ServiceName, assemblyVersion);

            builder.Services.Configure<ServiceRegistry>(builder.Configuration.GetSection(ServiceRegistry.ConfigSectionName));
            builder.Services.Configure<CorsSettings>(builder.Configuration.GetSection(ConfigurationConstants.AppSettings.CORS));
            builder.Services.Configure<LinkTokenServiceSettings>(builder.Configuration.GetSection(ConfigurationConstants.AppSettings.LinkTokenService));

            //Add database context
            builder.Services.AddSingleton<UpdateBaseEntityInterceptor>();
            builder.AddSQLServerEF<DmrpDbContext>(true);

            //Entity Repositories
            builder.Services.AddScoped<IEntityRepository<MeasureMapping>, MeasureMappingRepository>();
            builder.Services.AddScoped<IEntityRepository<FacilityReportingPlan>, FacilityReportingPlanRepository>();

            //Managers and Queries
            builder.Services.AddScoped<IMeasureMappingManager, MeasureMappingManager>();
            builder.Services.AddScoped<IMeasureMappingQueries, MeasureMappingQueries>();
            builder.Services.AddScoped<IFacilityReportingPlanManager, FacilityReportingPlanManager>();
            builder.Services.AddScoped<IFacilityReportingPlanQueries, FacilityReportingPlanQueries>();

            builder.Services.AddControllers();

            //Add problem details
            builder.Services.AddProblemDetails(options =>
            {
                options.CustomizeProblemDetails = ctx =>
                {
                    ctx.ProblemDetails.Detail = "An error occured in our API. Please use the trace id when requesting assistence.";
                    if (!ctx.ProblemDetails.Extensions.ContainsKey("traceId"))
                    {
                        string? traceId = Activity.Current?.Id ?? ctx.HttpContext.TraceIdentifier;
                        ctx.ProblemDetails.Extensions.Add(new KeyValuePair<string, object?>("traceId", traceId));
                    }

                    if (builder.Environment.IsDevelopment())
                    {
                        ctx.ProblemDetails.Extensions.Add("service", "DMRP");
                    }
                    else
                    {
                        ctx.ProblemDetails.Extensions.Remove("exception");
                    }
                };
            });

            //Add health checks
            builder.Services.AddHealthChecks()
                .AddCheck<DatabaseHealthCheck>(HealthCheckType.Database.ToString());

            // Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen(c =>
            {
                var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
                var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
                c.IncludeXmlComments(xmlPath);
                c.DocumentFilter<HealthChecksFilter>();
            });

            // Logging using Serilog
            builder.Logging.AddSerilog();
            var loggerOptions = new ConfigurationReaderOptions { SectionName = DmrpConstants.AppSettingsSectionNames.Serilog };
            Log.Logger = new LoggerConfiguration()
                .ReadFrom.Configuration(builder.Configuration, loggerOptions)
                .Filter.ByExcluding("RequestPath like '/health%'")
                .Filter.ByExcluding("RequestPath like '/swagger%'")
                .Enrich.WithExceptionDetails()
                .Enrich.FromLogContext()
                .Enrich.WithSpan()
                .Enrich.With<ActivityEnricher>()
                .Enrich.FromLogContext()
                .CreateLogger();

            SelfLog.Enable(Console.Error);

            //Add CORS
            builder.Services.AddLinkCorsService(options =>
            {
                options.Environment = builder.Environment;
            });

            //Add telemetry if enabled
            builder.Services.AddLinkTelemetry(builder.Configuration, options =>
            {
                options.Environment = builder.Environment;
                options.ServiceName = DmrpConstants.ServiceName;
                options.ServiceVersion = serviceInformation.Version;
            });
        }

        #endregion

        #region Set up middleware

        static void SetupMiddleware(WebApplication app)
        {
            // Configure the HTTP request pipeline.
            app.ConfigureSwagger();

            app.AutoMigrateEF<DmrpDbContext>();

            app.UseRouting();
            app.UseCors(CorsSettings.DefaultCorsPolicyName);

            //check for anonymous access
            var allowAnonymousAccess = app.Configuration.GetValue<bool>("Authentication:EnableAnonymousAccess");
            if (!allowAnonymousAccess)
            {
                app.UseAuthentication();
                app.UseMiddleware<UserScopeMiddleware>();
            }
            app.UseAuthorization();

            app.MapControllers();

            //map health check middleware and info endpoint
            app.MapHealthChecks("/health", new HealthCheckOptions
            {
                ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
            });
            app.MapInfo(Assembly.GetExecutingAssembly(), app.Configuration, "dmrp");
        }

        #endregion
    }
}
