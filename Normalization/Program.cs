using LantanaGroup.Link.Normalization.Listeners;
using Serilog;
using System.Reflection;
using Serilog.Enrichers.Span;
using Serilog.Exceptions;
using Hellang.Middleware.ProblemDetails;
using LantanaGroup.Link.Normalization.Application.Models.Messages;
using LantanaGroup.Link.Normalization.Application.Services;
using LantanaGroup.Link.Normalization.Application.Settings;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using LantanaGroup.Link.Shared.Application.Interfaces;
using LantanaGroup.Link.Shared.Application.Factories;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using HealthChecks.UI.Client;
using LantanaGroup.Link.Normalization.Application.Models;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using OpenTelemetry.Metrics;
using Confluent.Kafka.Extensions.OpenTelemetry;

var builder = WebApplication.CreateBuilder(args);

RegisterServices(builder);
var app = builder.Build();
SetupMiddleware(app);

app.Run();

#region Register Services

static void RegisterServices(WebApplicationBuilder builder)
{
    var serviceInformation = builder.Configuration.GetRequiredSection(NormalizationConstants.AppSettingsSectionNames.ServiceInformation).Get<ServiceInformation>();
    if (serviceInformation != null)
    {
        ServiceActivitySource.Initialize(serviceInformation);
        Counters.Initialize(serviceInformation);
    }
    else
    {
        throw new NullReferenceException("Service Information was null.");
    }

    builder.Services.Configure<KafkaConnection>(builder.Configuration.GetRequiredSection(NormalizationConstants.AppSettingsSectionNames.Kafka));
    builder.Services.Configure<MongoConnection>(builder.Configuration.GetRequiredSection(NormalizationConstants.AppSettingsSectionNames.Mongo));

    var tenantApiSettings = builder.Configuration.GetRequiredSection(NormalizationConstants.AppSettingsSectionNames.TenantApiSettings).Get<TenantApiSettings>();
    if(tenantApiSettings != null)
        builder.Services.AddSingleton(tenantApiSettings);

    // Additional configuration is required to successfully run gRPC on macOS.
    // For instructions on how to configure Kestrel and gRPC clients on macOS, visit https://go.microsoft.com/fwlink/?linkid=2099682

    builder.Services.AddTransient<IKafkaProducerFactory<string, PatientNormalizedMessage>, KafkaProducerFactory<string, PatientNormalizedMessage>>();
    builder.Services.AddTransient<
        IKafkaProducerFactory<string, LantanaGroup.Link.Shared.Application.Models.Kafka.AuditEventMessage>,
        KafkaProducerFactory<string, LantanaGroup.Link.Shared.Application.Models.Kafka.AuditEventMessage>
        >();
    builder.Services.AddTransient<IKafkaConsumerFactory<string, PatientDataAcquiredMessage>, KafkaConsumerFactory<string, PatientDataAcquiredMessage>>();
    
    builder.Services.AddTransient<ITenantApiService, TenantApiService>();

    builder.Services.AddControllers();

    // Logging using Serilog
    builder.Logging.AddSerilog();
    Log.Logger = new LoggerConfiguration()
                    .ReadFrom.Configuration(builder.Configuration)
                    .Enrich.WithExceptionDetails()
                    .Enrich.FromLogContext()
                    .Enrich.With<ActivityEnricher>()
                    .CreateLogger();

    builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(Assembly.GetExecutingAssembly()));

    builder.Services.AddSingleton<IConditionalTransformationEvaluationService, ConditionalTransformationEvaluationService>();
    builder.Services.AddSingleton<IConfigRepository, ConfigRepository>();
    builder.Services.AddHostedService<PatientDataAcquiredListener>();

    //Serilog.Debugging.SelfLog.Enable(Console.Error);  
    // Add services to the container.

    //Add health checks
    builder.Services.AddHealthChecks()
        .AddCheck<DatabaseHealthCheck>("Database");

    builder.Services.AddSwaggerGen(c =>
    {
        var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
        var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
        c.IncludeXmlComments(xmlPath);
    });
    builder.Services.AddGrpc();
    builder.Services.AddGrpcReflection();

    //builder.Services.AddSwaggerGen();

    var telemetryConfig = builder.Configuration.GetRequiredSection(NormalizationConstants.AppSettingsSectionNames.Telemetry).Get<TelemetryConfig>();
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
    //if (app.Environment.IsDevelopment())
    //{
    app.UseSwagger();
    app.UseSwaggerUI();
    //}

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
    app.MapGrpcService<NormalizationService>();
    app.MapGet("/", () => "Communication with gRPC endpoints must be made through a gRPC client. To learn how to create a client, visit: https://go.microsoft.com/fwlink/?linkid=2086909");
    app.MapControllers();
    //app.UseProblemDetails();


}

#endregion
