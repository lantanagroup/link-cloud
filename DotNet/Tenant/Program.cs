using Azure.Identity;
using HealthChecks.UI.Client;
using LantanaGroup.Link.Shared.Application.Extensions;
using LantanaGroup.Link.Shared.Application.Extensions.Security;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using LantanaGroup.Link.Shared.Application.Repositories.Interceptors;
using LantanaGroup.Link.Shared.Settings;
using LantanaGroup.Link.Tenant.Commands;
using LantanaGroup.Link.Tenant.Config;
using LantanaGroup.Link.Tenant.Interfaces;
using LantanaGroup.Link.Tenant.Jobs;
using LantanaGroup.Link.Tenant.Models;
using LantanaGroup.Link.Tenant.Repository.Context;
using LantanaGroup.Link.Tenant.Repository.Implementations.Sql;
using LantanaGroup.Link.Tenant.Repository.Interfaces.Sql;
using LantanaGroup.Link.Tenant.Services;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration.AzureAppConfiguration;
using Quartz;
using Quartz.Impl;
using Quartz.Spi;
using Serilog;
using Serilog.Enrichers.Span;
using Serilog.Exceptions;
using Serilog.Settings.Configuration;
using System.Diagnostics;
using System.Reflection;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Middleware;
using LantanaGroup.Link.Shared.Application.Factories;
using Confluent.Kafka;

namespace Tenant
{
    public class Program
    {

        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            RegisterServices(builder);

            var app = builder.Build();

            SetupMiddleware(app);

            app.Run();
        }



        #region Register Services

        static void RegisterServices(WebApplicationBuilder builder)
        {
            //load external configuration source if specified
            var externalConfigurationSource = builder.Configuration.GetSection(TenantConstants.AppSettingsSectionNames.ExternalConfigurationSource).Get<string>();

            if (!string.IsNullOrEmpty(externalConfigurationSource))
            {
                switch (externalConfigurationSource)
                {
                    case ("AzureAppConfiguration"):
                        builder.Configuration.AddAzureAppConfiguration(options =>
                        {
                            options.Connect(builder.Configuration.GetConnectionString("AzureAppConfiguration"))
                                    // Load configuration values with no label
                                    .Select("*", LabelFilter.Null)
                                    // Load configuration values for service name
                                    .Select("*", TenantConstants.ServiceName)
                                    // Load configuration values for service name and environment
                                    .Select("*", TenantConstants.ServiceName + ":" + builder.Environment.EnvironmentName);

                            options.ConfigureKeyVault(kv =>
                            {
                                kv.SetCredential(new DefaultAzureCredential());
                            });

                        });
                        break;
                }
            }

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

            var serviceInformation = builder.Configuration.GetRequiredSection(TenantConstants.AppSettingsSectionNames.ServiceInformation).Get<ServiceInformation>();
            if (serviceInformation != null)
            {
                ServiceActivitySource.Initialize(serviceInformation);                
            }
            else
            {
                throw new NullReferenceException("Service Information was null.");
            }

            // Add services to the container.
            builder.Services.AddHostedService<ScheduleService>();

            builder.Services.Configure<MeasureConfig>(builder.Configuration.GetSection(TenantConstants.AppSettingsSectionNames.MeasureConfig));
            builder.Services.Configure<ServiceRegistry>(builder.Configuration.GetSection(ServiceRegistry.ConfigSectionName));
            var kafkaConnection = builder.Configuration.GetSection(KafkaConstants.SectionName).Get<KafkaConnection>();
            builder.Services.AddSingleton<KafkaConnection>(kafkaConnection);
            builder.Services.Configure<CorsSettings>(builder.Configuration.GetSection(ConfigurationConstants.AppSettings.CORS));
            builder.Services.Configure<LinkTokenServiceSettings>(builder.Configuration.GetSection(ConfigurationConstants.AppSettings.LinkTokenService));

            builder.Services.AddScoped<IFacilityConfigurationService, FacilityConfigurationService>();
            builder.Services.AddScoped<IFacilityConfigurationRepo, FacilityConfigurationRepo>();

            builder.Services.AddSingleton<UpdateBaseEntityInterceptor>();
            builder.Services.AddSingleton<CreateAuditEventCommand>();

            //Add database context
            builder.Services.AddDbContext<FacilityDbContext>((sp, options) =>
            {

                var updateBaseEntityInterceptor = sp.GetService<UpdateBaseEntityInterceptor>()!;

                switch (builder.Configuration.GetValue<string>(TenantConstants.AppSettingsSectionNames.DatabaseProvider))
                {
                    case ConfigurationConstants.AppSettings.SqlServerDatabaseProvider:
                        string? connectionString =
                            builder.Configuration.GetConnectionString(ConfigurationConstants.DatabaseConnections
                                .DatabaseConnection);

                        if (string.IsNullOrEmpty(connectionString))
                            throw new InvalidOperationException("Database connection string is null or empty.");
                        
                        options.UseSqlServer(connectionString)
                           .AddInterceptors(updateBaseEntityInterceptor);
                        break;
                    default:
                        throw new InvalidOperationException("Database provider not supported.");
                }
            });



            builder.Services.AddTransient<LantanaGroup.Link.Shared.Application.Interfaces.IKafkaProducerFactory<string, object>, LantanaGroup.Link.Shared.Application.Factories.KafkaProducerFactory<string, object>>();
            var producer = new KafkaProducerFactory<string, object>(kafkaConnection).CreateProducer(new Confluent.Kafka.ProducerConfig());
            builder.Services.AddSingleton<IProducer<string, object>>(producer);

            builder.Services.AddTransient<LantanaGroup.Link.Shared.Application.Interfaces.IKafkaConsumerFactory<string, object>, LantanaGroup.Link.Shared.Application.Factories.KafkaConsumerFactory<string, object>>();


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
            builder.Services.AddHealthChecks()
                .AddCheck<DatabaseHealthCheck>("Database");

            // Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen(c =>
            {
                var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
                var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
                c.IncludeXmlComments(xmlPath);
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
            
            Serilog.Debugging.SelfLog.Enable(Console.Error);


            builder.Services.AddSingleton<IJobFactory, JobFactory>();

            builder.Services.AddSingleton<ISchedulerFactory, StdSchedulerFactory>();

            builder.Services.AddSingleton<ReportScheduledJob>();

            builder.Services.AddSingleton<RetentionCheckScheduledJob>();

            //Add CORS
            builder.Services.AddLinkCorsService(options => { 
                options.Environment = builder.Environment;
            });            

            //Add telemetry if enabled
            builder.Services.AddLinkTelemetry(builder.Configuration, options =>
            {
                options.Environment = builder.Environment;
                options.ServiceName = TenantConstants.ServiceName;
                options.ServiceVersion = serviceInformation.Version; //TODO: Get version from assembly?                
            });

            builder.Services.AddSingleton<ITenantServiceMetrics, TenantServiceMetrics>();
        }

        #endregion

        #region Set up middleware

        static void SetupMiddleware(WebApplication app)
        {
            // Configure the HTTP request pipeline.
            app.ConfigureSwagger();

            app.AutoMigrateEF<FacilityDbContext>();

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

            //map health check middleware
            app.MapHealthChecks("/health", new HealthCheckOptions
            {
                ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
            });

            // Ensure database created
            using (var scope = app.Services.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<FacilityDbContext>();
                context.Database.EnsureCreated();

            }
            // Configure the HTTP request pipeline.
            //app.MapGrpcService<TenantService>();
            //app.MapGet("/", () => "Communication with gRPC endpoints must be made through a gRPC client. To learn how to create a client, visit: https://go.microsoft.com/fwlink/?linkid=2086909");
        }

        #endregion

    }

}
