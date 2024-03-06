using Census.Jobs;
using Census.Repositories;
using Census.Services;
using Census.Settings;
using Confluent.Kafka;
using Confluent.Kafka.Extensions.OpenTelemetry;
using HealthChecks.UI.Client;
using LantanaGroup.Link.Census.Application.Errors;
using LantanaGroup.Link.Census.Application.Interfaces;
using LantanaGroup.Link.Census.HealthChecks;
using LantanaGroup.Link.Census.Listeners;
using LantanaGroup.Link.Census.Repositories;
using LantanaGroup.Link.Census.Repositories.Scheduling;
using LantanaGroup.Link.Census.Services;
using LantanaGroup.Link.Census.Settings;
using LantanaGroup.Link.Shared.Application.Factories;
using LantanaGroup.Link.Shared.Application.Interfaces;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using LantanaGroup.Link.Shared.Application.Models.Kafka;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
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

    builder.Services.Configure<KafkaConnection>(builder.Configuration.GetRequiredSection(CensusConstants.AppSettings.Kafka));
    builder.Services.Configure<TenantConfig>(builder.Configuration.GetRequiredSection(CensusConstants.AppSettings.TenantConfig));

    builder.Services.Configure<MongoConnection>(builder.Configuration.GetRequiredSection(CensusConstants.AppSettings.Mongo));
    builder.Services.AddSingleton<IKafkaConsumerFactory<string, string>, KafkaConsumerFactory<string, string>>();
    builder.Services.AddSingleton<IKafkaProducerFactory<string, string>, KafkaProducerFactory<string, string>>();
    builder.Services.AddSingleton<IKafkaProducerFactory<string, object>, KafkaProducerFactory<string, object>>();
    builder.Services.AddSingleton<IKafkaProducerFactory<string, Null>, KafkaProducerFactory<string, Null>>();
    builder.Services.AddSingleton<IKafkaProducerFactory<string, Ignore>, KafkaProducerFactory<string, Ignore>>();
    builder.Services.AddSingleton<IKafkaProducerFactory<string, AuditEventMessage>, KafkaProducerFactory<string, AuditEventMessage>>();
    builder.Services.AddScoped<ICensusConfigMongoRepository, CensusConfigMongoRepository>();
    builder.Services.AddScoped<ICensusHistoryRepository, CensusHistoryRepository>();
    builder.Services.AddScoped<ICensusPatientListRepository, CensusPatientListRepository>();
    builder.Services.AddTransient<INonTransientExceptionHandler<string, string>, NonTransientPatientIDsAcquiredExceptionHandler<string, string>>();

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

    builder.Services.AddSingleton<ICensusSchedulingRepository, CensusSchedulingRepository>();
    builder.Services.AddSingleton<CensusConfigMongoRepository>();
    builder.Services.AddSingleton<IJobFactory, JobFactory>();
    builder.Services.AddSingleton<ISchedulerFactory, StdSchedulerFactory>();
    builder.Services.AddSingleton<SchedulePatientListRetrieval>();

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