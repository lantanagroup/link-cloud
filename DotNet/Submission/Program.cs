using Azure.Identity;
using HealthChecks.UI.Client;
using Hellang.Middleware.ProblemDetails;
using LantanaGroup.Link.Shared.Application.Error.Handlers;
using LantanaGroup.Link.Shared.Application.Error.Interfaces;
using LantanaGroup.Link.Shared.Application.Extensions;
using LantanaGroup.Link.Shared.Application.Extensions.Security;
using LantanaGroup.Link.Shared.Application.Factories;
using LantanaGroup.Link.Shared.Application.Interfaces;
using LantanaGroup.Link.Shared.Application.Middleware;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using LantanaGroup.Link.Shared.Application.Models.Kafka;
using LantanaGroup.Link.Shared.Application.Repositories.Implementations;
using LantanaGroup.Link.Shared.Application.Services;
using LantanaGroup.Link.Shared.Jobs;
using LantanaGroup.Link.Shared.Settings;
using LantanaGroup.Link.Submission.Application.Factories;
using LantanaGroup.Link.Submission.Application.Interfaces;
using LantanaGroup.Link.Submission.Application.Managers;
using LantanaGroup.Link.Submission.Application.Models;
using LantanaGroup.Link.Submission.Application.Queries;
using LantanaGroup.Link.Submission.Application.Services;
using LantanaGroup.Link.Submission.Listeners;
using LantanaGroup.Link.Submission.Settings;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Configuration.AzureAppConfiguration;
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

#region Services

static void RegisterServices(WebApplicationBuilder builder)
{
    //load external configuration source if specified
    var externalConfigurationSource = builder.Configuration.GetSection(SubmissionConstants.AppSettingsSectionNames.ExternalConfigurationSource).Get<string>();

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
                         .Select("*", SubmissionConstants.ServiceName)
                         // Load configuration values for service name and environment
                         .Select("*", SubmissionConstants.ServiceName + ":" + builder.Environment.EnvironmentName);

                    options.ConfigureKeyVault(kv =>
                    {
                        kv.SetCredential(new DefaultAzureCredential());
                    });

                });
                break;
        }
    }

    // Add service information
    var serviceInformation = builder.Configuration.GetSection(ConfigurationConstants.AppSettings.ServiceInformation).Get<ServiceInformation>();
    if (serviceInformation != null)
    {
        ServiceActivitySource.Initialize(serviceInformation);
    }
    else
    {
        throw new NullReferenceException("Service Information was null.");
    }

    //Add problem details
    builder.Services.AddProblemDetails(opts => {
        opts.IncludeExceptionDetails = (ctx, ex) => false;
        opts.OnBeforeWriteDetails = (ctx, dtls) =>
        {
            if (dtls.Status == 500)
            {
                dtls.Detail = "An error occured in our API. Please use the trace id when requesting assistence.";
            }
        };
    });

    //Add Settings
    builder.Services.Configure<ServiceRegistry>(builder.Configuration.GetSection(ServiceRegistry.ConfigSectionName));
    builder.Services.Configure<KafkaConnection>(builder.Configuration.GetRequiredSection(KafkaConstants.SectionName));
    builder.Services.Configure<MongoConnection>(builder.Configuration.GetRequiredSection(SubmissionConstants.AppSettingsSectionNames.Mongo));
    builder.Services.Configure<SubmissionServiceConfig>(builder.Configuration.GetRequiredSection(nameof(SubmissionServiceConfig)));
    builder.Services.Configure<ConsumerSettings>(builder.Configuration.GetRequiredSection(nameof(ConsumerSettings)));
    builder.Services.Configure<CorsSettings>(builder.Configuration.GetSection(ConfigurationConstants.AppSettings.CORS));

    // Add services to the container.
    builder.Services.AddHttpClient();
    builder.Services.AddGrpc().AddJsonTranscoding();
    builder.Services.AddGrpcReflection();
    builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(Assembly.GetExecutingAssembly()));
    builder.Services.AddSingleton<RetryRepository>();
    builder.Services.AddTransient<ITenantSubmissionManager, TenantSubmissionManager>();
    builder.Services.AddTransient<ITenantSubmissionQueries, TenantSubmissionQueries>();

    // Add Controllers
    builder.Services.AddControllers();

    // Add hosted services
    builder.Services.AddHostedService<SubmitReportListener>();
    builder.Services.AddHostedService<RetryListener>();
    builder.Services.AddHostedService<RetryScheduleService>();

    // Add quartz scheduler
    builder.Services.AddSingleton<IJobFactory, JobFactory>();
    builder.Services.AddSingleton<ISchedulerFactory, StdSchedulerFactory>();
    builder.Services.AddSingleton<RetryJob>();

    //Add health checks
    builder.Services.AddHealthChecks();

    // Add commands
    // TODO

    // Add queries
    // TODO

    // Add factories
    builder.Services.AddTransient<IKafkaConsumerFactory<SubmitReportKey, SubmitReportValue>, KafkaConsumerFactory<SubmitReportKey, SubmitReportValue>>();
    builder.Services.AddTransient<IKafkaConsumerFactory<string, string>, KafkaConsumerFactory<string, string>>();
    builder.Services.AddTransient<IKafkaProducerFactory<string, AuditEventMessage>, KafkaProducerFactory<string, AuditEventMessage>>();
    builder.Services.AddTransient<IKafkaProducerFactory<SubmitReportKey, SubmitReportValue>, KafkaProducerFactory<SubmitReportKey, SubmitReportValue>>();
    builder.Services.AddTransient<IKafkaProducerFactory<string, string>, KafkaProducerFactory<string, string>>();
    builder.Services.AddTransient<IRetryEntityFactory, RetryEntityFactory>();

    // Add repositories
    // TODO

    #region Exception Handling
    //Report Scheduled Listener
    builder.Services.AddTransient<IDeadLetterExceptionHandler<SubmitReportKey, SubmitReportValue>, DeadLetterExceptionHandler<SubmitReportKey, SubmitReportValue>>();
    builder.Services.AddTransient<ITransientExceptionHandler<SubmitReportKey, SubmitReportValue>, TransientExceptionHandler<SubmitReportKey, SubmitReportValue>>();

    //Retry Listener
    //Retry Listener
    builder.Services.AddTransient<IDeadLetterExceptionHandler<string, string>, DeadLetterExceptionHandler<string, string>>();
    #endregion

    // Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen();

    // Logging using Serilog
    builder.Logging.AddSerilog();
    Log.Logger = new LoggerConfiguration()
                    .ReadFrom.Configuration(builder.Configuration)
                    .Filter.ByExcluding("RequestPath like '/health%'")
                    .Filter.ByExcluding("RequestPath like '/swagger%'")
                    .Enrich.WithExceptionDetails()
                    .Enrich.FromLogContext()
                    .Enrich.WithSpan()
                    .Enrich.With<ActivityEnricher>()
                    .CreateLogger();

    //Add CORS
    builder.Services.AddLinkCorsService(options => {
        options.Environment = builder.Environment;
    });

    //Add telemetry if enabled
    builder.Services.AddLinkTelemetry(builder.Configuration, options =>
    {
        options.Environment = builder.Environment;
        options.ServiceName = SubmissionConstants.ServiceName;
        options.ServiceVersion = serviceInformation.Version; //TODO: Get version from assembly?                
    });

    builder.Services.AddSingleton<ISubmissionServiceMetrics, SubmissionServiceMetrics>();
}

#endregion

#region Midleware

static void SetupMiddleware(WebApplication app)
{
    app.UseProblemDetails();

    //app.UseOpenTelemetryPrometheusScrapingEndpoint();

    // Configure the HTTP request pipeline.
    if (app.Configuration.GetValue<bool>("EnableSwagger"))
    {
        app.UseSwagger();
        app.UseSwaggerUI(opts => opts.SwaggerEndpoint("/swagger/v1/swagger.json", "Submission Service v1"));
    }

    //map health check middleware
    app.MapHealthChecks("/health", new HealthCheckOptions
    {
        ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
    });

    app.UseRouting();
    app.UseCors(CorsSettings.DefaultCorsPolicyName);
    //app.UseAuthentication();
    app.UseMiddleware<UserScopeMiddleware>();
    //app.UseAuthorization();
    app.UseEndpoints(endpoints => endpoints.MapControllers());
}

#endregion