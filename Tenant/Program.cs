
using LantanaGroup.Link.Tenant;
using LantanaGroup.Link.Tenant.Listeners;
using LantanaGroup.Link.Tenant.Services;
using Quartz;
using Serilog;
using LantanaGroup.Link.Tenant.Config;
using LantanaGroup.Link.Tenant.Jobs;

using Quartz.Impl;
using Quartz.Spi;


using System.Reflection;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using HealthChecks.UI.Client;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using LantanaGroup.Link.Tenant.Repository;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using LantanaGroup.Link.Tenant.Models;
using Confluent.Kafka.Extensions.OpenTelemetry;

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
            // Additional configuration is required to successfully run gRPC on macOS.
            // For instructions on how to configure Kestrel and gRPC clients on macOS, visit https://go.microsoft.com/fwlink/?linkid=2099682

            var serviceInformation = builder.Configuration.GetRequiredSection(TenantConstants.AppSettingsSectionNames.ServiceInformation).Get<ServiceInformation>();
            if (serviceInformation != null)
            {
                ServiceActivitySource.Initialize(serviceInformation);
                Counters.Initialize(serviceInformation);
            }
            else
            {
                throw new NullReferenceException("Service Information was null.");
            }

            // Add services to the container.
            builder.Services.AddGrpc();
            builder.Services.AddGrpcReflection();
            builder.Services.AddHostedService<ListenerXX>();
            builder.Services.AddHostedService<ScheduleService>(); 

            builder.Services.Configure<KafkaConnection>(builder.Configuration.GetRequiredSection(TenantConstants.AppSettingsSectionNames.Kafka));

            builder.Services.Configure<MongoConnection>(builder.Configuration.GetRequiredSection(TenantConstants.AppSettingsSectionNames.Mongo));

            builder.Services.Configure<TenantConfig>(builder.Configuration.GetRequiredSection(TenantConstants.AppSettingsSectionNames.TenantConfig));

            builder.Services.AddSingleton<FacilityConfigurationService>();

            builder.Services.AddSingleton<IFacilityConfigurationRepo, FacilityConfigurationRepo>();

            builder.Services.AddTransient<LantanaGroup.Link.Shared.Application.Interfaces.IKafkaProducerFactory<string,object>, LantanaGroup.Link.Shared.Application.Factories.KafkaProducerFactory<string, object>>();

            builder.Services.AddTransient<LantanaGroup.Link.Shared.Application.Interfaces.IKafkaConsumerFactory<string,object>, LantanaGroup.Link.Shared.Application.Factories.KafkaConsumerFactory<string, object>>();

            builder.Services.AddHttpClient();

            builder.Services.AddControllers();

            //Add repositories
            //builder.Services.AddSingleton<IPersistenceRepository<FacilityConfigModel>>();

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
                //c.IncludeGrpcXmlComments(xmlPath, includeControllerXmlComments: true);
            });

            // Logging using Serilog
            builder.Logging.AddSerilog();
            Log.Logger = new LoggerConfiguration()
                            .ReadFrom.Configuration(builder.Configuration)
                            .CreateLogger();

            builder.Services.AddSingleton<IJobFactory, JobFactory>();

            builder.Services.AddSingleton<ISchedulerFactory, StdSchedulerFactory>();

            builder.Services.AddSingleton<ReportScheduledJob>();

            builder.Services.AddSingleton<RetentionCheckScheduledJob>();

            var telemetryConfig = builder.Configuration.GetRequiredSection(TenantConstants.AppSettingsSectionNames.Telemetry).Get<TelemetryConfig>();
            if (telemetryConfig != null)
            {
                var otel = builder.Services.AddOpenTelemetry();

                //configure OpenTelemetry resources with application name
                otel.ConfigureResource(resource => resource
                    .AddService(ServiceActivitySource.Instance.Name, ServiceActivitySource.Instance.Version));

                otel.WithTracing(tracerProviderBuilder =>
                        tracerProviderBuilder
                            .AddSource(ServiceActivitySource.Instance.Name)
                            .AddAspNetCoreInstrumentation(options =>
                            {
                                options.Filter = (httpContext) => httpContext.Request.Path != "/health"; //do not capture traces for the health check endpoint
                            })
                            .AddConfluentKafkaInstrumentation()
                            .AddOtlpExporter(opts => { opts.Endpoint = new Uri(telemetryConfig.TelemetryCollectorEndpoint); }));

                otel.WithMetrics(metricsProviderBuilder =>
                        metricsProviderBuilder
                            .AddAspNetCoreInstrumentation()
                            .AddRuntimeInstrumentation()
                            .AddMeter(Counters.meter.Name)
                            .AddOtlpExporter(opts => { opts.Endpoint = new Uri(telemetryConfig.TelemetryCollectorEndpoint); }));

                if (builder.Environment.IsDevelopment())
                {
                    otel.WithTracing(tracerProviderBuilder =>
                        tracerProviderBuilder
                        .AddConsoleExporter());

                    //metrics are very verbose, only enable console exporter if you really want to see metric details
                    //otel.WithMetrics(metricsProviderBuilder =>
                    //    metricsProviderBuilder
                    //        .AddConsoleExporter());                
                }
            }
        }

        #endregion

        #region Set up middleware

        static void SetupMiddleware(WebApplication app)
        {
            // Configure the HTTP request pipeline.
            if (app.Configuration.GetValue<bool>("EnableSwagger"))
            {
                app.UseSwagger();
                app.UseSwaggerUI(opts => opts.SwaggerEndpoint("/swagger/v1/swagger.json", "Tenant Service v1"));
            }

            app.UseRouting();
            app.MapControllers();

            //map health check middleware
            app.MapHealthChecks("/health", new HealthCheckOptions
            {
                ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
            });

            if (app.Configuration.GetValue<bool>("AllowReflection"))
            {
                app.MapGrpcReflectionService();
            }

            // Configure the HTTP request pipeline.
            //app.MapGrpcService<TenantService>();
            //app.MapGet("/", () => "Communication with gRPC endpoints must be made through a gRPC client. To learn how to create a client, visit: https://go.microsoft.com/fwlink/?linkid=2086909");
        }

        #endregion

    }

}
