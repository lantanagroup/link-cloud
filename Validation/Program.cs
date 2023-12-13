using Confluent.Kafka;

using LantanaGroup.Link.Validation.Listeners;
using LantanaGroup.Link.Validation.Models;
using LantanaGroup.Link.Shared.Application.Factories;
using LantanaGroup.Link.Shared.Application.Interfaces;
using Serilog;
using Validation.Services;
using LantanaGroup.Link.Shared.Application.Repositories.Interfaces;
using LantanaGroup.Link.Shared.Application.Repositories.Implementations;
using LantanaGroup.Link.Validation.Entities;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using LantanaGroup.Link.Validation.Services;
using LantanaGroup.Link.Validation.Settings;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Confluent.Kafka.Extensions.OpenTelemetry;
using HealthChecks.UI.Client;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;

var builder = WebApplication.CreateBuilder(args);

// Additional configuration is required to successfully run gRPC on macOS.
// For instructions on how to configure Kestrel and gRPC clients on macOS, visit https://go.microsoft.com/fwlink/?linkid=2099682

var serviceInformation = builder.Configuration.GetSection(ValidationConstants.AppSettingsSectionNames.ServiceInformation).Get<ServiceInformation>();
if (serviceInformation != null)
{
    ServiceActivitySource.Initialize(serviceInformation);
    Counters.Initialize(serviceInformation);
}
else
{
    throw new NullReferenceException("Service Information was null.");
}

// Add configuration settings
builder.Services.Configure<KafkaConnection>(builder.Configuration.GetSection("KafkaConnection"));
builder.Services.Configure<MongoConnection>(builder.Configuration.GetSection("MongoDB"));

// Add services to the container.
builder.Services.AddHttpClient();
builder.Services.AddGrpc();
builder.Services.AddGrpcReflection();

//Add custom services to the container.
builder.Services.AddTransient<IKafkaConsumerFactory<PatientDataEvaluatedKey, PatientDataEvaluatedMessage>, 
    KafkaConsumerFactory<PatientDataEvaluatedKey, PatientDataEvaluatedMessage>>();
builder.Services.AddTransient<IKafkaProducerFactory<string, MeasureReportValidationMessage>, KafkaProducerFactory<string, MeasureReportValidationMessage>>();

//Add repos
builder.Services.AddSingleton<IMongoDbRepository<ValidationEntity>, MongoDbRepository<ValidationEntity>>();

//Add listeners
builder.Services.AddHostedService<PatientDataEvaluatedListener>();

// Logging using Serilog
builder.Logging.AddSerilog();
Log.Logger = new LoggerConfiguration()
                .ReadFrom.Configuration(builder.Configuration)
                .CreateLogger();

//Serilog.Debugging.SelfLog.Enable(Console.Error);  

// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var telemetryConfig = builder.Configuration.GetSection(ValidationConstants.AppSettingsSectionNames.Telemetry).Get<TelemetryConfig>();
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

//Add health checks
builder.Services.AddHealthChecks();

var app = builder.Build();
SetupMiddleware(app);

app.Run();


#region Set up middleware

static void SetupMiddleware(WebApplication app)
{
    if (app.Configuration.GetValue<bool>("EnableSwagger"))
    {
        app.UseSwagger();
        app.UseSwaggerUI(opts => opts.SwaggerEndpoint("/swagger/v1/swagger.json", "Validation Service v1"));
    }

    if (app.Configuration.GetValue<bool>("AllowReflection"))
    {
        app.MapGrpcReflectionService();
    }

    //map health check middleware
    app.MapHealthChecks("/health", new HealthCheckOptions
    {
        ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
    });

    // Configure the HTTP request pipeline.
    app.MapGrpcService<ValidationService>();
    app.MapGet("/", () => "Communication with gRPC endpoints must be made through a gRPC client. To learn how to create a client, visit: https://go.microsoft.com/fwlink/?linkid=2086909");
}

#endregion

