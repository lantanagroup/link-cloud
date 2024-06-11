using Azure.Identity;
using HealthChecks.UI.Client;
using LantanaGroup.Link.Shared.Application.Error.Handlers;
using LantanaGroup.Link.Shared.Application.Error.Interfaces;
using LantanaGroup.Link.Shared.Application.Extensions;
using LantanaGroup.Link.Shared.Application.Extensions.Security;
using LantanaGroup.Link.Shared.Application.Factories;
using LantanaGroup.Link.Shared.Application.Interfaces;
using LantanaGroup.Link.Shared.Application.Middleware;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using LantanaGroup.Link.Shared.Application.Models.Kafka;
using LantanaGroup.Link.Shared.Application.Repositories.Implementations;
using LantanaGroup.Link.Shared.Application.Repositories.Interfaces;
using LantanaGroup.Link.Shared.Application.Services;
using LantanaGroup.Link.Shared.Jobs;
using LantanaGroup.Link.Shared.Settings;
using LantanaGroup.Link.Submission.Application.Config;
using LantanaGroup.Link.Submission.Application.Factories;
using LantanaGroup.Link.Submission.Application.Interfaces;
using LantanaGroup.Link.Submission.Application.Managers;
using LantanaGroup.Link.Submission.Application.Models;
using LantanaGroup.Link.Submission.Application.Queries;
using LantanaGroup.Link.Submission.Application.Repositories;
using LantanaGroup.Link.Submission.Application.Services;
using LantanaGroup.Link.Submission.Listeners;
using LantanaGroup.Link.Submission.Persistence.Interceptors;
using LantanaGroup.Link.Submission.Settings;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Configuration.AzureAppConfiguration;
using Quartz;
using Quartz.Impl;
using Quartz.Spi;
using Serilog;
using Serilog.Enrichers.Span;
using Serilog.Exceptions;
using Serilog.Settings.Configuration;
using System.Reflection;
using LantanaGroup.Link.Submission.Persistence;
using LantanaGroup.Link.Submission.Infrastructure;

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
    builder.Services.AddProblemDetails();

    //Add Settings
    builder.Services.Configure<ServiceRegistry>(builder.Configuration.GetSection(ServiceRegistry.ConfigSectionName));
    builder.Services.Configure<KafkaConnection>(builder.Configuration.GetRequiredSection(KafkaConstants.SectionName));
    builder.Services.Configure<MongoConnection>(builder.Configuration.GetRequiredSection(SubmissionConstants.AppSettingsSectionNames.Mongo));
    builder.Services.Configure<SubmissionServiceConfig>(builder.Configuration.GetRequiredSection(nameof(SubmissionServiceConfig)));
    builder.Services.Configure<ConsumerSettings>(builder.Configuration.GetRequiredSection(nameof(ConsumerSettings)));
    builder.Services.Configure<CorsSettings>(builder.Configuration.GetSection(ConfigurationConstants.AppSettings.CORS));

    // Add services to the container.
    builder.Services.AddHttpClient();
    builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(Assembly.GetExecutingAssembly()));
    builder.Services.AddSingleton<IRetryRepository, RetryRepository_Mongo>();
    builder.Services.AddTransient<ITenantSubmissionManager, TenantSubmissionManager>();
    builder.Services.AddTransient<ITenantSubmissionQueries, TenantSubmissionQueries>();
    builder.Services.AddTransient<ITenantSubmissionRepository, TenantSubmissionRepository>();

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
    builder.Services.AddHealthChecks()
        .AddCheck<SubmissionHealthCheck>("Submission");

    //Add persistence interceptors
    builder.Services.AddSingleton<UpdateTenantSubmissionConfigEntityInterceptor>();

    //Add database context
    builder.Services.AddDbContext<TenantSubmissionDbContext>((sp, options) => {
        var updateTenantSubmissionConfigEntityInterceptor = sp.GetRequiredService<UpdateTenantSubmissionConfigEntityInterceptor>();
        switch (builder.Configuration.GetValue<string>(SubmissionConstants.AppSettingsSectionNames.DatabaseProvider))
        {
            case "SqlServer":
                string? connectionString = builder.Configuration.GetValue<string>(SubmissionConstants.AppSettingsSectionNames.DatabaseConnectionString);

                if (string.IsNullOrEmpty(connectionString))
                    throw new InvalidOperationException("Database connection string is null or empty.");

                options
                    .UseSqlServer(connectionString)
                    .AddInterceptors(updateTenantSubmissionConfigEntityInterceptor);

                break;
            default:
                throw new InvalidOperationException("Database provider not supported.");
        }
    });

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

    // Add swagger
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen(c =>
    {
        var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
        var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
        c.IncludeXmlComments(xmlPath);
    });

    // Logging using Serilog
    builder.Logging.AddSerilog();
    var loggerOptions = new ConfigurationReaderOptions { SectionName = SubmissionConstants.AppSettingsSectionNames.Serilog };
    Log.Logger = new LoggerConfiguration()
                    .ReadFrom.Configuration(builder.Configuration, loggerOptions)
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
    // Configure the HTTP request pipeline.
    app.ConfigureSwagger();

    //map health check middleware
    app.MapHealthChecks("/health", new HealthCheckOptions
    {
        ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
    });

    app.UseRouting();
    app.UseCors(CorsSettings.DefaultCorsPolicyName);
    app.UseMiddleware<UserScopeMiddleware>();
    app.MapControllers();
}

#endregion