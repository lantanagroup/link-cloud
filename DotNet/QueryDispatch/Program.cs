using Azure.Identity;
using Confluent.Kafka.Extensions.OpenTelemetry;
using HealthChecks.UI.Client;
using LanatanGroup.Link.QueryDispatch.Jobs;
using LantanaGroup.Link.QueryDispatch.Application.Factory;
using LantanaGroup.Link.QueryDispatch.Application.Interfaces;
using LantanaGroup.Link.QueryDispatch.Application.Models;
using LantanaGroup.Link.QueryDispatch.Application.PatientDispatch.Commands;
using LantanaGroup.Link.QueryDispatch.Application.Queries;
using LantanaGroup.Link.QueryDispatch.Application.QueryDispatchConfiguration.Commands;
using LantanaGroup.Link.QueryDispatch.Application.ScheduledReport.Commands;
using LantanaGroup.Link.QueryDispatch.Application.ScheduledReport.Queries;
using LantanaGroup.Link.QueryDispatch.Listeners;
using LantanaGroup.Link.QueryDispatch.Persistence;
using LantanaGroup.Link.QueryDispatch.Persistence.PatientDispatch;
using LantanaGroup.Link.QueryDispatch.Persistence.QueryDispatchConfiguration;
using LantanaGroup.Link.QueryDispatch.Persistence.ScheduledReport;
using LantanaGroup.Link.QueryDispatch.Presentation.Services;
using LantanaGroup.Link.Shared.Application.Error.Handlers;
using LantanaGroup.Link.Shared.Application.Error.Interfaces;
using LantanaGroup.Link.Shared.Application.Factories;
using LantanaGroup.Link.Shared.Application.Interfaces;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using LantanaGroup.Link.Shared.Application.Models.Kafka;
using LantanaGroup.Link.Shared.Application.Repositories.Implementations;
using LantanaGroup.Link.Shared.Application.Services;
using LantanaGroup.Link.Shared.Jobs;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Configuration.AzureAppConfiguration;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Quartz;
using Quartz.Impl;
using Quartz.Spi;
using QueryDispatch.Application.Models;
using QueryDispatch.Application.Services;
using QueryDispatch.Application.Settings;
using QueryDispatch.Presentation.Services;
using Serilog;
using System.Reflection;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

//load external configuration source if specified
var externalConfigurationSource = builder.Configuration.GetSection(QueryDispatchConstants.AppSettingsSectionNames.ExternalConfigurationSource).Get<string>();

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
                        .Select("*", QueryDispatchConstants.ServiceName)
                        // Load configuration values for service name and environment
                        .Select("*", QueryDispatchConstants.ServiceName + ":" + builder.Environment.EnvironmentName);

                options.ConfigureKeyVault(kv =>
                {
                    kv.SetCredential(new DefaultAzureCredential());
                });

            });
            break;
    }
}

var serviceInformation = builder.Configuration.GetRequiredSection(QueryDispatchConstants.AppSettingsSectionNames.ServiceInformation).Get<ServiceInformation>();
if (serviceInformation != null)
{
    ServiceActivitySource.Initialize(serviceInformation);
    Counters.Initialize(serviceInformation);
}
else
{
    throw new NullReferenceException("Service Information was null.");
}

// Additional configuration is required to successfully run gRPC on macOS.
// For instructions on how to configure Kestrel and gRPC clients on macOS, visit https://go.microsoft.com/fwlink/?linkid=2099682


builder.Services.Configure<KafkaConnection>(builder.Configuration.GetRequiredSection("KafkaConnection"));
builder.Services.Configure<MongoConnection>(builder.Configuration.GetRequiredSection("MongoDB"));
builder.Services.Configure<ServiceRegistry>(builder.Configuration.GetSection(ServiceRegistry.ConfigSectionName));
builder.Services.Configure<ConsumerSettings>(builder.Configuration.GetRequiredSection(nameof(ConsumerSettings)));

// Add services to the container.
builder.Services.AddGrpc();
builder.Services.AddControllers(options => { options.ReturnHttpNotAcceptable = true; }).AddXmlDataContractSerializerFormatters().AddJsonOptions(opt => opt.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddHttpClient();

//Add commands
builder.Services.AddTransient<ICreateScheduledReportCommand, CreateScheduledReportCommand>();

builder.Services.AddTransient<ICreatePatientDispatchCommand, CreatePatientDispatchCommand>();
builder.Services.AddTransient<IDeletePatientDispatchCommand, DeletePatientDispatchCommand>();

builder.Services.AddTransient<ICreateQueryDispatchConfigurationCommand, CreateQueryDispatchConfigurationCommand>();
builder.Services.AddTransient<IDeleteQueryDispatchConfigurationCommand, DeleteQueryDispatchConfigurationCommand>();
builder.Services.AddTransient<IUpdateQueryDispatchConfigurationCommand, UpdateQueryDispatchConfigurationCommand>();

//Add factories
builder.Services.AddTransient<IKafkaConsumerFactory<ReportScheduledKey, ReportScheduledValue>, KafkaConsumerFactory<ReportScheduledKey, ReportScheduledValue>>();
builder.Services.AddTransient<IKafkaConsumerFactory<string, PatientEventValue>, KafkaConsumerFactory<string, PatientEventValue>>();
builder.Services.AddTransient<IKafkaConsumerFactory<string, string>, KafkaConsumerFactory<string, string>>();

builder.Services.AddTransient<IKafkaProducerFactory<string, DataAcquisitionRequestedValue>, KafkaProducerFactory<string, DataAcquisitionRequestedValue>>();
builder.Services.AddTransient<IKafkaProducerFactory<string, AuditEventMessage>, KafkaProducerFactory<string, AuditEventMessage>>();
builder.Services.AddTransient<IKafkaProducerFactory<string, string>, KafkaProducerFactory<string, string>>();
builder.Services.AddTransient<IKafkaProducerFactory<string, PatientEventValue>, KafkaProducerFactory<string, PatientEventValue>>();
builder.Services.AddTransient<IKafkaProducerFactory<ReportScheduledKey, ReportScheduledValue>, KafkaProducerFactory<ReportScheduledKey, ReportScheduledValue>>();

builder.Services.AddTransient<IRetryEntityFactory, RetryEntityFactory>();

builder.Services.AddTransient<IQueryDispatchFactory, QueryDispatchFactory>();
builder.Services.AddTransient<IQueryDispatchConfigurationFactory, QueryDispatchConfigurationFactory>();

//Add repos
builder.Services.AddSingleton<IScheduledReportRepository, ScheduledReportMongoRepo>();
builder.Services.AddSingleton<IPatientDispatchRepository, PatientDispatchMongoRepo>();
builder.Services.AddSingleton<IQueryDispatchConfigurationRepository, QueryDispatchConfigurationMongoRepo>();
builder.Services.AddSingleton<RetryRepository>();

//Add Queries
builder.Services.AddTransient<IGetScheduledReportQuery, GetScheduledReportQuery>();
builder.Services.AddTransient<IUpdateScheduledReportCommand, UpdateScheduledReportCommand>();
builder.Services.AddTransient<IGetQueryDispatchConfigurationQuery, GetQueryDispatchConfigurationQuery>();
builder.Services.AddTransient<IGetAllQueryDispatchConfigurationQuery, GetAllQueryDispatchConfigurationQuery>();
builder.Services.AddTransient<IGetAllPatientDispatchQuery, GetAllPatientDispatchQuery>();

//Excepation Handlers
builder.Services.AddTransient<IDeadLetterExceptionHandler<string, PatientEventValue>, DeadLetterExceptionHandler<string, PatientEventValue>>();
builder.Services.AddTransient<IDeadLetterExceptionHandler<ReportScheduledKey, ReportScheduledValue>, DeadLetterExceptionHandler<ReportScheduledKey, ReportScheduledValue>>();
builder.Services.AddTransient<IDeadLetterExceptionHandler<string, string>, DeadLetterExceptionHandler<string, string>>();
builder.Services.AddTransient<ITransientExceptionHandler<string, PatientEventValue>, TransientExceptionHandler<string, PatientEventValue>>();

//Add Services
builder.Services.AddTransient<ITenantApiService, TenantApiService>();

//Add Hosted Services
builder.Services.AddHostedService<PatientEventListener>();
builder.Services.AddHostedService<ReportScheduledEventListener>();
builder.Services.AddHostedService<RetryListener>();
builder.Services.AddHostedService<ScheduleService>();
builder.Services.AddHostedService<RetryScheduleService>();

builder.Services.AddSingleton<IJobFactory, JobFactory>();
builder.Services.AddSingleton<RetryJob>();
builder.Services.AddSingleton<ISchedulerFactory, StdSchedulerFactory>();
builder.Services.AddSingleton<QueryDispatchJob>();


// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    c.IncludeXmlComments(xmlPath);
});

//Add health checks
builder.Services.AddHealthChecks()
    .AddCheck<DatabaseHealthCheck>("Database");

// Logging using Serilog
builder.Logging.AddSerilog();
Log.Logger = new LoggerConfiguration()
                .ReadFrom.Configuration(builder.Configuration)
                .CreateLogger();

var telemetryConfig = builder.Configuration.GetRequiredSection(QueryDispatchConstants.AppSettingsSectionNames.Telemetry).Get<TelemetryConfig>();
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

var app = builder.Build();

// Configure the HTTP request pipeline.
SetupMiddleware(app);

app.Run();


static void SetupMiddleware(WebApplication app)
{
    // Configure the HTTP request pipeline.
    if (app.Configuration.GetValue<bool>("EnableSwagger"))
    {
        app.UseSwagger();
        app.UseSwaggerUI(opts => opts.SwaggerEndpoint("/swagger/v1/swagger.json", "QueryDispatch Service v1"));
    }

    //map health check middleware
    app.MapHealthChecks("/health", new HealthCheckOptions
    {
        ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
    });

    app.UseRouting();
    app.UseEndpoints(endpoints => endpoints.MapControllers());

    if (app.Configuration.GetValue<bool>("AllowReflection"))
    {
        //app.MapGrpcReflectionService();
    }
}