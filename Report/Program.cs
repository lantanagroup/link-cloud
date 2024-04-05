using Azure.Identity;
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
using LantanaGroup.Link.Shared.Application.Error.Handlers;
using LantanaGroup.Link.Shared.Application.Error.Interfaces;
using LantanaGroup.Link.Shared.Application.Factories;
using LantanaGroup.Link.Shared.Application.Interfaces;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using LantanaGroup.Link.Shared.Application.Models.Kafka;
using LantanaGroup.Link.Shared.Application.Services;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Configuration.AzureAppConfiguration;
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
using LantanaGroup.Link.Shared.Application.Repositories.Implementations;

var builder = WebApplication.CreateBuilder(args);


RegisterServices(builder);
var app = builder.Build();
SetupMiddleware(app);

app.Run();

#region Register Services

static void RegisterServices(WebApplicationBuilder builder)
{
    //load external configuration source if specified
    var externalConfigurationSource = builder.Configuration.GetSection(ReportConstants.AppSettingsSectionNames.ExternalConfigurationSource).Get<string>();

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
                            .Select("*", ReportConstants.ServiceName)
                            // Load configuration values for service name and environment
                            .Select("*", ReportConstants.ServiceName + ":" + builder.Environment.EnvironmentName);

                    options.ConfigureKeyVault(kv =>
                    {
                        kv.SetCredential(new DefaultAzureCredential());
                    });

                });
                break;
        }
    }

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
    builder.Services.AddSingleton(builder.Configuration.GetRequiredSection(ReportConstants.AppSettingsSectionNames.Kafka).Get<KafkaConnection>() ?? new KafkaConnection());
    builder.Services.AddSingleton(builder.Configuration.GetRequiredSection(ReportConstants.AppSettingsSectionNames.TenantApiSettings).Get<TenantApiSettings>() ?? new TenantApiSettings());
    builder.Services.Configure<KafkaConnection>(builder.Configuration.GetRequiredSection(ReportConstants.AppSettingsSectionNames.Kafka));
    builder.Services.Configure<MongoConnection>(builder.Configuration.GetRequiredSection(ReportConstants.AppSettingsSectionNames.Mongo));


    // Add services to the container.
    builder.Services.AddHttpClient();
    builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(Assembly.GetExecutingAssembly()));                 

    // Add factories
    builder.Services.AddTransient<IKafkaConsumerFactory<MeasureEvaluatedKey, MeasureEvaluatedValue>, KafkaConsumerFactory<MeasureEvaluatedKey, MeasureEvaluatedValue>>();

    builder.Services.AddTransient<IKafkaConsumerFactory<string, PatientsToQueryValue>, KafkaConsumerFactory<string, PatientsToQueryValue>>();
    builder.Services.AddTransient<IKafkaConsumerFactory<MeasureReportScheduledKey, MeasureReportScheduledValue>, KafkaConsumerFactory<MeasureReportScheduledKey, MeasureReportScheduledValue>>();
    builder.Services.AddTransient<IKafkaConsumerFactory<ReportSubmittedKey, ReportSubmittedValue>, KafkaConsumerFactory<ReportSubmittedKey, ReportSubmittedValue>>();
    builder.Services.AddTransient<IKafkaConsumerFactory<string, string>, KafkaConsumerFactory<string, string>>();

    //Producres
    builder.Services.AddTransient<IKafkaProducerFactory<string, DataAcquisitionRequestedValue>, KafkaProducerFactory<string, DataAcquisitionRequestedValue>>();
    builder.Services.AddTransient<IKafkaProducerFactory<SubmissionReportKey, SubmissionReportValue>, KafkaProducerFactory<SubmissionReportKey, SubmissionReportValue>>();

    //Producers for Retry/Deadletter
    builder.Services.AddTransient<IKafkaProducerFactory<ReportSubmittedKey, ReportSubmittedValue>, KafkaProducerFactory<ReportSubmittedKey, ReportSubmittedValue>>();
    builder.Services.AddTransient<IKafkaProducerFactory<MeasureReportScheduledKey, MeasureReportScheduledValue>, KafkaProducerFactory<MeasureReportScheduledKey, MeasureReportScheduledValue>>();
    builder.Services.AddTransient<IKafkaProducerFactory<string, PatientsToQueryValue>, KafkaProducerFactory<string, PatientsToQueryValue>>();
    builder.Services.AddTransient<IKafkaProducerFactory<MeasureEvaluatedKey, MeasureEvaluatedValue>, KafkaProducerFactory<MeasureEvaluatedKey, MeasureEvaluatedValue>>();

    // Add repositories
    builder.Services.AddSingleton<MeasureReportConfigRepository>();
    builder.Services.AddSingleton<MeasureReportScheduleRepository>();
    builder.Services.AddSingleton<MeasureReportSubmissionRepository>();
    builder.Services.AddSingleton<MeasureReportSubmissionEntryRepository>();
    builder.Services.AddSingleton<ReportRepository>();
    builder.Services.AddSingleton<PatientsToQueryRepository>();
    builder.Services.AddSingleton<RetryRepository>();

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

    // Add quartz scheduler
    builder.Services.AddSingleton<IJobFactory, JobFactory>();
    builder.Services.AddSingleton<ISchedulerFactory, StdSchedulerFactory>();
    builder.Services.AddSingleton<GenerateDataAcquisitionRequestsForPatientsToQuery>();

    // Add hosted services
    builder.Services.AddHostedService<MeasureEvaluatedListener>();
    builder.Services.AddHostedService<ReportScheduledListener>();
    builder.Services.AddHostedService<ReportSubmittedListener>();
    builder.Services.AddHostedService<PatientsToQueryListener>();
    builder.Services.AddHostedService<MeasureReportScheduleService>();
    builder.Services.AddHostedService<RetryListener>();
    builder.Services.AddHostedService<RetryScheduleService>();

    builder.Services.AddTransient<MeasureReportSubmissionBundler>();
    builder.Services.AddTransient<ITenantApiService, TenantApiService>();

    #region Exception Handling
    //Report Scheduled Listener
    builder.Services.AddTransient<IDeadLetterExceptionHandler<MeasureReportScheduledKey, MeasureReportScheduledValue>, DeadLetterExceptionHandler<MeasureReportScheduledKey, MeasureReportScheduledValue>>();
    builder.Services.AddTransient<ITransientExceptionHandler<MeasureReportScheduledKey, MeasureReportScheduledValue>, TransientExceptionHandler<MeasureReportScheduledKey, MeasureReportScheduledValue>>();

    //Report Submitted Listener
    builder.Services.AddTransient<IDeadLetterExceptionHandler<ReportSubmittedKey, ReportSubmittedValue>, DeadLetterExceptionHandler<ReportSubmittedKey, ReportSubmittedValue>>();
    builder.Services.AddTransient<ITransientExceptionHandler<ReportSubmittedKey, ReportSubmittedValue>, TransientExceptionHandler<ReportSubmittedKey, ReportSubmittedValue>>();

    //Patients To Query Listener
    builder.Services.AddTransient<IDeadLetterExceptionHandler<string, PatientsToQueryValue>, DeadLetterExceptionHandler<string, PatientsToQueryValue>>();
    builder.Services.AddTransient<ITransientExceptionHandler<string, PatientsToQueryValue>, TransientExceptionHandler<string, PatientsToQueryValue>>();

    //Measure Evaluated Listener
    builder.Services.AddTransient<IDeadLetterExceptionHandler<MeasureEvaluatedKey, MeasureEvaluatedValue>, DeadLetterExceptionHandler<MeasureEvaluatedKey, MeasureEvaluatedValue>>();
    builder.Services.AddTransient<ITransientExceptionHandler<MeasureEvaluatedKey, MeasureEvaluatedValue>, TransientExceptionHandler<MeasureEvaluatedKey, MeasureEvaluatedValue>>();
    #endregion

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
