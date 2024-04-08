
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
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using LantanaGroup.Link.Tenant.Models;
using Confluent.Kafka.Extensions.OpenTelemetry;
using System.Diagnostics;
using Microsoft.Extensions.Configuration.AzureAppConfiguration;
using Azure.Identity;
using Serilog.Settings.Configuration;
using Serilog.Enrichers.Span;
using Serilog.Exceptions;
using Microsoft.EntityFrameworkCore;
using LantanaGroup.Link.Tenant.Listeners;
using LantanaGroup.Link.Tenant.Repository.Context;
using LantanaGroup.Link.Tenant.Repository.Implementations.Sql;
using LantanaGroup.Link.Tenant.Repository.Interfaces.Sql;
using LantanaGroup.Link.Tenant.Commands;
using LantanaGroup.Link.Shared.Application.Repositories.Interceptors;

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

            builder.Services.Configure<KafkaConnection>(builder.Configuration.GetRequiredSection(TenantConstants.AppSettingsSectionNames.KafkaConnection));

            builder.Services.Configure<MongoConnection>(builder.Configuration.GetRequiredSection(TenantConstants.AppSettingsSectionNames.MongoDB));

            builder.Services.Configure<MeasureApiConfig>(builder.Configuration.GetRequiredSection(TenantConstants.AppSettingsSectionNames.MeasureApiConfig));

            builder.Services.AddScoped<FacilityConfigurationService>();

            builder.Services.AddScoped<IFacilityConfigurationRepo, FacilityConfigurationRepo>();

            builder.Services.AddSingleton<UpdateBaseEntityInterceptor>();

            builder.Services.AddSingleton<CreateAuditEventCommand>();

            //Add database context
            builder.Services.AddDbContext<FacilityDbContext>((sp, options) =>
            {

                var updateBaseEntityInterceptor = sp.GetService<UpdateBaseEntityInterceptor>()!;

                switch (builder.Configuration.GetValue<string>(TenantConstants.AppSettingsSectionNames.DatabaseProvider))
                {
                    case "SqlServer":
                        options.UseSqlServer(
                            builder.Configuration.GetValue<string>(TenantConstants.AppSettingsSectionNames.DatabaseConnectionString))
                           .AddInterceptors(updateBaseEntityInterceptor);
                        break;
                    default:
                        throw new InvalidOperationException("Database provider not supported.");
                }
            });



            builder.Services.AddTransient<LantanaGroup.Link.Shared.Application.Interfaces.IKafkaProducerFactory<string, object>, LantanaGroup.Link.Shared.Application.Factories.KafkaProducerFactory<string, object>>();

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
                            .AddMeter(Counters.meter.Name)
                            .AddOtlpExporter(opts => { opts.Endpoint = new Uri(telemetryConfig.TelemetryCollectorEndpoint); }));

                if (telemetryConfig.EnableRuntimeInstrumentation)
                {
                    otel.WithMetrics(metricsProviderBuilder =>
                        metricsProviderBuilder
                            .AddRuntimeInstrumentation());
                }

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
            if (app.Configuration.GetValue<bool>(TenantConstants.AppSettingsSectionNames.EnableSwagger))
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
