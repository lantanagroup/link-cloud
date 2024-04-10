using Confluent.Kafka;
using LantanaGroup.Link.MeasureEval.Auditing;
using LantanaGroup.Link.MeasureEval.Listeners;
using LantanaGroup.Link.MeasureEval.Models;
using LantanaGroup.Link.MeasureEval.Repository;
using LantanaGroup.Link.MeasureEval.Services;
using LantanaGroup.Link.MeasureEval.Wrappers;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using LantanaGroup.Link.Shared.Application.Wrappers;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using HealthChecks.UI.Client;
using Serilog;
using System.Reflection;
using LantanaGroup.Link.MeasureEval.Settings;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Confluent.Kafka.Extensions.OpenTelemetry;
using Microsoft.Extensions.Configuration.AzureAppConfiguration;
using Azure.Identity;

var builder = WebApplication.CreateBuilder(args);

RegisterServices(builder);
var app = builder.Build();
SetupMiddleware(app);

app.Run();

#region Register services

static void RegisterServices(WebApplicationBuilder builder)
{
    //load external configuration source if specified
    var externalConfigurationSource = builder.Configuration.GetSection(MeasureEvalConstants.AppSettingsSectionNames.ExternalConfigurationSource).Get<string>();

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
                         .Select("*", MeasureEvalConstants.AppSettingsSectionNames.ServiceName)
                         // Load configuration values for service name and environment
                         .Select("*", MeasureEvalConstants.AppSettingsSectionNames.ServiceName + ":" + builder.Environment.EnvironmentName);

                    options.ConfigureKeyVault(kv =>
                    {
                        kv.SetCredential(new DefaultAzureCredential());
                    });

                });
                break;
        }
    }

    // Additional configuration is required to successfully run gRPC on macOS.
    // For instructions on how to configure Kestrel and gRPC clients on macOS, visit https://go.microsoft.com/fwlink/?linkid=2099682

    var serviceInformation = builder.Configuration.GetRequiredSection(MeasureEvalConstants.AppSettingsSectionNames.ServiceInformation).Get<ServiceInformation>();
    if (serviceInformation != null)
    {
        ServiceActivitySource.Initialize(serviceInformation);
        Counters.Initialize(serviceInformation);
    }
    else
    {
        throw new NullReferenceException("Service Information was null.");
    }

    //Add Settings
    builder.Services.AddSingleton(builder.Configuration.GetRequiredSection(nameof(MeasureEvalConfig)).Get<MeasureEvalConfig>());
    builder.Services.AddSingleton(builder.Configuration.GetRequiredSection(nameof(KafkaConnection)).Get<KafkaConnection>());

    // Add services to the container.
    builder.Services.AddHttpClient();
    builder.Services.AddControllers();
    builder.Services.AddGrpc();
    builder.Services.AddGrpcReflection();

    builder.Services.AddSingleton<MeasureDefinitionRepo>();
    builder.Services.AddSingleton<MeasureEvalService>();
    builder.Services.AddSingleton<MeasureDefinitionService>();

    builder.Services.Configure<MongoConnection>(builder.Configuration.GetRequiredSection(nameof(MongoConnection)));
    builder.Services.Configure<KafkaConnection>(builder.Configuration.GetRequiredSection(nameof(KafkaConnection)));

    //Add Kafka factories
    builder.Services.AddTransient<IKafkaProducerFactory, KafkaProducerFactory>();
    builder.Services.AddTransient<IKafkaConsumerFactory, KafkaConsumerFactory>();

    // Add custom services to the container.
    builder.Services.AddSingleton<IKafkaWrapper<string, PatientDataNormalizedMessage, PatientDataEvaluatedKey, PatientDataEvaluatedMessage>, MeasureEvalKafkaWrapper<string, PatientDataNormalizedMessage, PatientDataEvaluatedKey, PatientDataEvaluatedMessage>>();
    builder.Services.AddSingleton<IMeasureEvalReportService,MeasureEvalReportService>();
    builder.Services.AddSingleton<IKafkaWrapper<Ignore, Null, Null, MeasureChanged>, KafkaWrapper<Ignore, Null, Null, MeasureChanged>>();
    builder.Services.AddSingleton<IKafkaWrapper<Ignore, Null, string, NotificationMessage>, KafkaWrapper<Ignore, Null, string, NotificationMessage>>();
    builder.Services.AddSingleton<IKafkaWrapper<Ignore, Null, string, AuditEventMessage>, KafkaWrapper<Ignore, Null, string, AuditEventMessage>>();

    //Add Listeners
    builder.Services.AddHostedService<PatientDataNormalizedListener>();

    // Logging using Serilog
    builder.Logging.AddSerilog();
    Log.Logger = new LoggerConfiguration()
        .ReadFrom.Configuration(builder.Configuration)
        .CreateLogger();

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

    var telemetryConfig = builder.Configuration.GetRequiredSection(MeasureEvalConstants.AppSettingsSectionNames.Telemetry).Get<TelemetryConfig>();
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
    if (app.Configuration.GetValue<bool>("EnableSwagger"))
    {
        app.UseSwagger();
        app.UseSwaggerUI(opts => opts.SwaggerEndpoint("/swagger/v1/swagger.json", "MeasureEval Service v1"));
    }

    app.MapControllers();

    //map health check middleware
    app.MapHealthChecks("/health", new HealthCheckOptions
    {
        ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
    });

    if (app.Configuration.GetValue<bool>("AllowReflection"))
    {
        app.MapGrpcReflectionService();
        Log.Logger.Information("gRPC Reflection is turned on");
    }

    // Configure the HTTP request pipeline.
    //app.MapGrpcService<MeasureEvalService>();
    //app.MapGet("/", () => "Communication with gRPC endpoints must be made through a gRPC client. To learn how to create a client, visit: https://go.microsoft.com/fwlink/?linkid=2086909");
}

#endregion
