using Confluent.Kafka.Extensions.OpenTelemetry;
using HealthChecks.UI.Client;
using LantanaGroup.Link.Report.Application.Factory;
using LantanaGroup.Link.Report.Application.Models;
using LantanaGroup.Link.Report.Core;
using LantanaGroup.Link.Report.Jobs;
using LantanaGroup.Link.Report.Listeners;
using LantanaGroup.Link.Report.Repositories;
using LantanaGroup.Link.Report.Services;
using LantanaGroup.Link.Report.Settings;
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
using Serilog.Enrichers.Span;
using Serilog.Exceptions;
using System.Reflection;

var builder = WebApplication.CreateBuilder(args);


RegisterServices(builder);
var app = builder.Build();
SetupMiddleware(app);

app.Run();

#region Register Services

static void RegisterServices(WebApplicationBuilder builder)
{
    var serviceInformation = builder.Configuration.GetRequiredSection(ReportConstants.AppSettingsSectionNames.ServiceInformation).Get<ServiceInformation>();
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
    builder.Services.AddSingleton(builder.Configuration.GetRequiredSection(ReportConstants.AppSettingsSectionNames.Kafka).Get<KafkaConnection>());
    builder.Services.Configure<KafkaConnection>(builder.Configuration.GetRequiredSection(ReportConstants.AppSettingsSectionNames.Kafka));
    builder.Services.Configure<MongoConnection>(builder.Configuration.GetRequiredSection(ReportConstants.AppSettingsSectionNames.Mongo));
    builder.Services.AddSingleton(builder.Configuration
        .GetRequiredSection(ReportConstants.AppSettingsSectionNames.TenantApiSettings).Get<TenantApiSettings>());

    // Add services to the container.
    builder.Services.AddHttpClient();
    builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(Assembly.GetExecutingAssembly()));                 

    // Add factories
    builder.Services.AddTransient<IKafkaConsumerFactory<MeasureEvaluatedKey, MeasureEvaluatedValue>, KafkaConsumerFactory<MeasureEvaluatedKey, MeasureEvaluatedValue>>();

    builder.Services.AddTransient<IKafkaConsumerFactory<string, PatientsToQueryValue>, KafkaConsumerFactory<string, PatientsToQueryValue>>();
    builder.Services.AddTransient<IKafkaConsumerFactory<MeasureReportScheduledKey, MeasureReportScheduledValue>, 
        KafkaConsumerFactory<MeasureReportScheduledKey, MeasureReportScheduledValue>>();
    builder.Services.AddTransient<IKafkaConsumerFactory<ReportSubmittedKey, ReportSubmittedValue>, KafkaConsumerFactory<ReportSubmittedKey, ReportSubmittedValue>>();

    builder.Services.AddTransient<IKafkaProducerFactory<string, DataAcquisitionRequestedValue>, KafkaProducerFactory<string, DataAcquisitionRequestedValue>>();
    builder.Services.AddTransient<IKafkaProducerFactory<SubmissionReportKey, SubmissionReportValue>, KafkaProducerFactory<SubmissionReportKey, SubmissionReportValue>>();

    // Add repositories
    builder.Services.AddSingleton<MeasureReportConfigRepository>();
    builder.Services.AddSingleton<MeasureReportScheduleRepository>();
    builder.Services.AddSingleton<MeasureReportSubmissionRepository>();
    builder.Services.AddSingleton<MeasureReportSubmissionEntryRepository>();
    builder.Services.AddSingleton<ReportRepository>();
    builder.Services.AddSingleton<PatientsToQueryRepository>();

    // Add controllers
    builder.Services.AddControllers();

    //Add health checks
    builder.Services.AddHealthChecks()
        .AddCheck<DatabaseHealthCheck>("Database");

    // Add swagger
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen(c =>
    {
        var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
        var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
        c.IncludeXmlComments(xmlPath);
    });

    // Add kafka wrappers
    //builder.Services.AddSingleton<IKafkaWrapper<Ignore, MeasureReportCreatedMessage, Null, Ignore>, KafkaWrapper<Ignore, MeasureReportCreatedMessage, Null, Ignore>>();
    //builder.Services.AddSingleton<IKafkaWrapper<Ignore, ReportRequestedMessage, Null, ReportScheduledMessage>, KafkaWrapper<Ignore, ReportRequestedMessage, Null, ReportScheduledMessage>>();
    builder.Services.AddTransient<IKafkaProducerFactory<string, AuditEventMessage>, KafkaProducerFactory<string, AuditEventMessage>>();

    // Add hosted services
    builder.Services.AddHostedService<MeasureEvaluatedListener>();
    builder.Services.AddHostedService<ReportScheduledListener>();
    builder.Services.AddHostedService<ReportSubmittedListener>();
    builder.Services.AddHostedService<PatientsToQueryListener>();

    // Add quartz scheduler
    builder.Services.AddSingleton<IJobFactory, JobFactory>();
    builder.Services.AddSingleton<ISchedulerFactory, StdSchedulerFactory>();
    builder.Services.AddSingleton<GenerateDataAcquisitionRequestsForPatientsToQuery>();
    builder.Services.AddHostedService<MeasureReportScheduleService>();

    builder.Services.AddTransient<MeasureReportSubmissionBundler>();


    // Logging using Serilog
    builder.Logging.AddSerilog();
    Log.Logger = new LoggerConfiguration()
        .ReadFrom.Configuration(builder.Configuration)
        .Enrich.WithExceptionDetails()
        .Enrich.FromLogContext()
        .Enrich.WithSpan()
        .Enrich.With<ActivityEnricher>()
        .CreateLogger();

    Serilog.Debugging.SelfLog.Enable(Console.Error);

    var telemetryConfig = builder.Configuration.GetRequiredSection(ReportConstants.AppSettingsSectionNames.Telemetry).Get<TelemetryConfig>();
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

    if (app.Environment.IsDevelopment() || app.Environment.EnvironmentName.ToLower() == "local")
    {
        app.UseSwagger();
        app.UseSwaggerUI();
    }

    //map health check middleware
    app.MapHealthChecks("/health", new HealthCheckOptions
    {
        ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
    });

    app.UseRouting();
    app.MapControllers();
}

#endregion
