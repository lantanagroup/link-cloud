using System.Diagnostics;
using System.Reflection;
using Confluent.Kafka;
using HealthChecks.UI.Client;
using LantanaGroup.Link.Shared.Application.Extensions;
using LantanaGroup.Link.Shared.Application.Extensions.Quartz;
using LantanaGroup.Link.Shared.Application.Extensions.Security;
using LantanaGroup.Link.Shared.Application.Factories;
using LantanaGroup.Link.Shared.Application.Health;
using LantanaGroup.Link.Shared.Application.Interfaces;
using LantanaGroup.Link.Shared.Application.Middleware;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using LantanaGroup.Link.Shared.Application.Models.Kafka;
using LantanaGroup.Link.Shared.Domain.Repositories.Interceptors;
using LantanaGroup.Link.Shared.Domain.Repositories.Interfaces;
using LantanaGroup.Link.Shared.Settings;
using LantanaGroup.Link.Tenant.Business.Managers;
using LantanaGroup.Link.Tenant.Business.Queries;
using LantanaGroup.Link.Tenant.Commands;
using LantanaGroup.Link.Tenant.Config;
using LantanaGroup.Link.Tenant.Data.Repository;
using LantanaGroup.Link.Tenant.Entities;
using LantanaGroup.Link.Tenant.Interfaces;
using LantanaGroup.Link.Tenant.Jobs;
using LantanaGroup.Link.Tenant.Models;
using LantanaGroup.Link.Tenant.Repository.Context;
using LantanaGroup.Link.Tenant.Services;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Serilog;
using Serilog.Debugging;
using Serilog.Enrichers.Span;
using Serilog.Exceptions;
using Serilog.Settings.Configuration;

namespace Tenant
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
            builder.AddExternalConfiguration(TenantConstants.ServiceName);

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

            var serviceInformation = builder.SetupServiceInformation(TenantConstants.ServiceName, assemblyVersion);

            // Add services to the container.
            builder.Services.AddSingleton<ScheduleService>();
            builder.Services.AddSingleton<IHostedService>(sp => sp.GetRequiredService<ScheduleService>());

            builder.Services.Configure<FacilityIdSettings>(builder.Configuration.GetSection(TenantConstants.AppSettingsSectionNames.FacilityIdSettings));
            builder.Services.AddSingleton(sp => sp.GetRequiredService<IOptions<FacilityIdSettings>>().Value);


            builder.Services.Configure<MeasureConfig>(builder.Configuration.GetSection(TenantConstants.AppSettingsSectionNames.MeasureConfig));
            builder.Services.Configure<ServiceRegistry>(builder.Configuration.GetSection(ServiceRegistry.ConfigSectionName));
            var kafkaConnection = builder.Configuration.GetSection(KafkaConstants.SectionName).Get<KafkaConnection>();
            builder.Services.AddSingleton<KafkaConnection>(kafkaConnection);
            builder.Services.Configure<CorsSettings>(builder.Configuration.GetSection(ConfigurationConstants.AppSettings.CORS));
            builder.Services.Configure<LinkTokenServiceSettings>(builder.Configuration.GetSection(ConfigurationConstants.AppSettings.LinkTokenService));

            //Entity Repositories
            builder.Services.AddScoped<IEntityRepository<Facility>, FacilityRepository>();

            //Managers and Queries
            builder.Services.AddScoped<IFacilityManager, FacilityManager>();
            builder.Services.AddScoped<IFacilityQueries, FacilityQueries>();

            builder.Services.AddSingleton<UpdateBaseEntityInterceptor>();
            builder.Services.AddSingleton<CreateAuditEventCommand>();

            var dbProvider = builder.Configuration.GetValue<string>(TenantConstants.AppSettingsSectionNames.DatabaseProvider);
            string? databaseConnectionString = null;

            if (dbProvider == ConfigurationConstants.AppSettings.SqlServerDatabaseProvider)
            {
                databaseConnectionString = builder.Configuration.GetConnectionString(ConfigurationConstants.DatabaseConnections.DatabaseConnection);

                if (string.IsNullOrEmpty(databaseConnectionString))
                    throw new InvalidOperationException("Database connection string is null or empty.");

                // Add Quartz scheduler with SQL persistence
                builder.Services.RegisterQuartzDatabase(databaseConnectionString);
            }

            //Add database context
            builder.Services.AddDbContext<TenantDbContext>((sp, options) =>
            {
                var updateBaseEntityInterceptor = sp.GetService<UpdateBaseEntityInterceptor>()!;

                switch (dbProvider)
                {
                    case ConfigurationConstants.AppSettings.SqlServerDatabaseProvider:
                        options.UseSqlServer(databaseConnectionString)
                           .AddInterceptors(updateBaseEntityInterceptor);
                        break;
                    default:
                        throw new InvalidOperationException("Database provider not supported.");
                }
            });

            builder.Services.AddTransient<IKafkaProducerFactory<string, GenerateReportValue>, KafkaProducerFactory<string, GenerateReportValue>>();
            builder.Services.AddTransient<IKafkaProducerFactory<string, object>, KafkaProducerFactory<string, object>>();
            var producer = new KafkaProducerFactory<string, AuditEventMessage>(kafkaConnection).CreateProducer(new ProducerConfig());
            builder.Services.AddSingleton(producer);

            builder.Services.AddHttpClient();

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
                        ctx.ProblemDetails.Extensions.Add("service", "Tenant");
                    }
                    else
                    {
                        ctx.ProblemDetails.Extensions.Remove("exception");
                    }
                };
            });

            //Add health checks
            var kafkaHealthOptions = new KafkaHealthCheckConfiguration(kafkaConnection, TenantConstants.ServiceName).GetHealthCheckOptions();

            builder.Services.AddHealthChecks()
                .AddCheck<DatabaseHealthCheck>(HealthCheckType.Database.ToString())
                .AddKafka(kafkaHealthOptions, HealthCheckType.Kafka.ToString());

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
            var loggerOptions = new ConfigurationReaderOptions { SectionName = TenantConstants.AppSettingsSectionNames.Serilog };
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

            builder.Services.AddSingleton<ReportScheduledJob>();

            //Add CORS
            builder.Services.AddLinkCorsService(options =>
            {
                options.Environment = builder.Environment;
            });

            //Add telemetry if enabled
            builder.Services.AddLinkTelemetry(builder.Configuration, options =>
            {
                options.Environment = builder.Environment;
                options.ServiceName = TenantConstants.ServiceName;
                options.ServiceVersion = serviceInformation.Version;
            });

            builder.Services.AddSingleton<ITenantServiceMetrics, TenantServiceMetrics>();
        }

        #endregion

        #region Set up middleware

        static void SetupMiddleware(WebApplication app)
        {
            // Configure the HTTP request pipeline.
            app.ConfigureSwagger();

            app.AutoMigrateEF<TenantDbContext>();

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
            app.MapInfo(Assembly.GetExecutingAssembly(), app.Configuration, "facility");

            // Configure the HTTP request pipeline.
            //app.MapGrpcService<TenantService>();
            //app.MapGet("/", () => "Communication with gRPC endpoints must be made through a gRPC client. To learn how to create a client, visit: https://go.microsoft.com/fwlink/?linkid=2086909");
        }

        #endregion
    }
}
