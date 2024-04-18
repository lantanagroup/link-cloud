using Azure.Identity;
using Confluent.Kafka;
using Confluent.Kafka.Extensions.OpenTelemetry;
using HealthChecks.UI.Client;
using LantanaGroup.Link.Census.Application.Errors;
using LantanaGroup.Link.Census.Application.HealthChecks;
using LantanaGroup.Link.Census.Application.Interfaces;
using LantanaGroup.Link.Census.Application.Jobs;
using LantanaGroup.Link.Census.Application.Repositories;
using LantanaGroup.Link.Census.Application.Repositories.Scheduling;
using LantanaGroup.Link.Census.Application.Services;
using LantanaGroup.Link.Census.Application.Settings;
using LantanaGroup.Link.Census.Domain.Context;
using LantanaGroup.Link.Census.Listeners;
using LantanaGroup.Link.Shared.Application.Factories;
using LantanaGroup.Link.Shared.Application.Interfaces;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using LantanaGroup.Link.Shared.Application.Models.Kafka;
using LantanaGroup.Link.Shared.Application.Repositories.Interceptors;
using LantanaGroup.Link.Shared.Application.Services;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration.AzureAppConfiguration;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Quartz;
using Quartz.Impl;
using Quartz.Spi;
using Serilog;
using System.Reflection;

var builder = WebApplication.CreateBuilder(args);

RegisterServices(builder);
var app = builder.Build();
SetupMiddleware(app);

app.Run();


static void RegisterServices(WebApplicationBuilder builder)
{
    //load external configuration source if specified
    var externalConfigurationSource = builder.Configuration.GetSection(CensusConstants.AppSettings.ExternalConfigurationSource).Get<string>();

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
                         .Select("*", CensusConstants.ServiceName)
                         // Load configuration values for service name and environment
                         .Select("*", CensusConstants.ServiceName + ":" + builder.Environment.EnvironmentName);

                    options.ConfigureKeyVault(kv =>
                    {
                        kv.SetCredential(new DefaultAzureCredential());
                    });

                });
                break;
        }
    }

    var serviceInformation = builder.Configuration.GetRequiredSection(CensusConstants.AppSettings.ServiceInformation).Get<ServiceInformation>();
    if (serviceInformation != null)
    {
        ServiceActivitySource.Initialize(serviceInformation);
        Counters.Initialize(serviceInformation);
    }
    else
    {
        throw new NullReferenceException("Service Information was null.");
    }

    builder.Services.Configure<KafkaConnection>(builder.Configuration.GetSection(CensusConstants.AppSettings.Kafka));
    builder.Services.AddSingleton(builder.Configuration.GetRequiredSection(CensusConstants.AppSettings.TenantApiSettings).Get<TenantApiSettings>() ?? new TenantApiSettings());

    builder.Services.AddTransient<UpdateBaseEntityInterceptor>();

    var test1 = builder.Configuration.GetConnectionString(CensusConstants.AppSettings.DatabaseConnection);
    var test2 = builder.Configuration.GetValue<string>(CensusConstants.AppSettings.DatabaseProvider);

    Console.WriteLine($"Connection String: {test1}");
    Console.WriteLine($"Database Provider: {test2}");

    builder.Services.AddDbContext<CensusContext>((sp, options) =>
    {

        var updateBaseEntityInterceptor = sp.GetService<UpdateBaseEntityInterceptor>()!;

        switch (builder.Configuration.GetValue<string>(CensusConstants.AppSettings.DatabaseProvider))
        {
            case "SqlServer":
                options.UseSqlServer(
                    builder.Configuration.GetConnectionString(CensusConstants.AppSettings.DatabaseConnection))
                   .AddInterceptors(updateBaseEntityInterceptor);
                break;
            default:
                throw new InvalidOperationException("Database provider not supported.");
        }
    });

    builder.Services.AddHttpClient();

    builder.Services.AddTransient<IKafkaConsumerFactory<string, string>, KafkaConsumerFactory<string, string>>();
    builder.Services.AddTransient<IKafkaProducerFactory<string, string>, KafkaProducerFactory<string, string>>();
    builder.Services.AddTransient<IKafkaProducerFactory<string, object>, KafkaProducerFactory<string, object>>();
    builder.Services.AddTransient<IKafkaProducerFactory<string, Null>, KafkaProducerFactory<string, Null>>();
    builder.Services.AddTransient<IKafkaProducerFactory<string, Ignore>, KafkaProducerFactory<string, Ignore>>();
    builder.Services.AddTransient<IKafkaProducerFactory<string, AuditEventMessage>, KafkaProducerFactory<string, AuditEventMessage>>();
    builder.Services.AddScoped<ICensusConfigRepository, CensusConfigRepository>();
    builder.Services.AddScoped<ICensusHistoryRepository, CensusHistoryRepository>();
    builder.Services.AddScoped<ICensusPatientListRepository, CensusPatientListRepository>();
    builder.Services.AddTransient<INonTransientExceptionHandler<string, string>, NonTransientPatientIDsAcquiredExceptionHandler<string, string>>();
    builder.Services.AddTransient<ITenantApiService, TenantApiService>();

    //Add health checks
    builder.Services.AddHealthChecks()
        .AddCheck<DatabaseHealthCheck>("Database");

    builder.Services.AddControllers().AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull;
        options.JsonSerializerOptions.PropertyNameCaseInsensitive = true;
    });

    builder.Services.AddGrpc();
    builder.Services.AddGrpcReflection();

    builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(Assembly.GetExecutingAssembly()));
    builder.Services.AddSwaggerGen(c =>
    {
        var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
        var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
        c.IncludeXmlComments(xmlPath);
    });

    builder.Logging.AddSerilog();
    Log.Logger = new LoggerConfiguration()
                    .ReadFrom.Configuration(builder.Configuration)
                    .CreateLogger();
    Serilog.Debugging.SelfLog.Enable(Console.Error);

    builder.Services.AddScoped<ICensusSchedulingRepository, CensusSchedulingRepository>();
    builder.Services.AddTransient<IJobFactory, JobFactory>();
    builder.Services.AddTransient<ISchedulerFactory, StdSchedulerFactory>();
    builder.Services.AddTransient<SchedulePatientListRetrieval>();

    builder.Services.AddHostedService<CensusListener>();
    builder.Services.AddHostedService<ScheduleService>();

    var telemetryConfig = builder.Configuration.GetRequiredSection(CensusConstants.AppSettings.Telemetry).Get<TelemetryConfig>();
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

static void SetupMiddleware(WebApplication app)
{
    //if (app.Environment.IsDevelopment())
    //{
    app.UseSwagger();
    app.UseSwaggerUI();
    //}

    if (app.Configuration.GetValue<bool>("AllowReflection"))
    {
        app.MapGrpcReflectionService();
    }

    //map health check middleware
    app.MapHealthChecks("/health", new HealthCheckOptions
    {
        ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
    });

    //app.MapGrpcService<CensusConfigService>();
    app.MapGet("/", () => "Communication with gRPC endpoints must be made through a gRPC client. To learn how to create a client, visit: https://go.microsoft.com/fwlink/?linkid=2086909");
    app.MapControllers();
}