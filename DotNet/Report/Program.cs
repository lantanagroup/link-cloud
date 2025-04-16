using Azure.Identity;
using HealthChecks.UI.Client;
using LantanaGroup.Link.Report.Application.Extensions;
using LantanaGroup.Link.Report.Application.Factory;
using LantanaGroup.Link.Report.Application.Interfaces;
using LantanaGroup.Link.Report.Application.Models;
using LantanaGroup.Link.Report.Core;
using LantanaGroup.Link.Report.Domain;
using LantanaGroup.Link.Report.Domain.Managers;
using LantanaGroup.Link.Report.Entities;
using LantanaGroup.Link.Report.Jobs;
using LantanaGroup.Link.Report.KafkaProducers;
using LantanaGroup.Link.Report.Listeners;
using LantanaGroup.Link.Report.Services;
using LantanaGroup.Link.Report.Settings;
using LantanaGroup.Link.Shared.Application.Error.Handlers;
using LantanaGroup.Link.Shared.Application.Error.Interfaces;
using LantanaGroup.Link.Shared.Application.Extensions;
using LantanaGroup.Link.Shared.Application.Extensions.Security;
using LantanaGroup.Link.Shared.Application.Factories;
using LantanaGroup.Link.Shared.Application.Health;
using LantanaGroup.Link.Shared.Application.Interfaces;
using LantanaGroup.Link.Shared.Application.Listeners;
using LantanaGroup.Link.Shared.Application.Middleware;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using LantanaGroup.Link.Shared.Application.Models.Kafka;
using LantanaGroup.Link.Shared.Application.Repositories.Implementations;
using LantanaGroup.Link.Shared.Application.Repositories.Interceptors;
using LantanaGroup.Link.Shared.Application.Repositories.Interfaces;
using LantanaGroup.Link.Shared.Application.Services;
using LantanaGroup.Link.Shared.Application.Utilities;
using LantanaGroup.Link.Shared.Jobs;
using LantanaGroup.Link.Shared.Settings;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Configuration.AzureAppConfiguration;
using Microsoft.OpenApi.Models;
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

    builder.WebHost.ConfigureKestrel(options =>
    {
        options.Limits.MaxRequestBodySize = 200 * 1024 * 1024; // Set limit to 200 MB
    });

    var serviceInformation = builder.Configuration.GetRequiredSection(ReportConstants.AppSettingsSectionNames.ServiceInformation).Get<ServiceInformation>();
    if (serviceInformation != null)
    {
        ServiceActivitySource.Initialize(serviceInformation);
    }
    else
    {
        throw new NullReferenceException("Service Information was null.");
    }

    // Add problem details
    builder.Services.AddProblemDetailsService(options =>
    {
        options.Environment = builder.Environment;
        options.ServiceName = ReportConstants.ServiceName;
        options.IncludeExceptionDetails = builder.Configuration.GetValue<bool>("ProblemDetails:IncludeExceptionDetails");
    });

    // Add configuration settings
    builder.Services.Configure<ServiceRegistry>(builder.Configuration.GetSection(ServiceRegistry.ConfigSectionName));
    var kafkaConnection = builder.Configuration.GetSection(KafkaConstants.SectionName).Get<KafkaConnection>();
    builder.Services.AddSingleton<KafkaConnection>(kafkaConnection);
    builder.Services.Configure<MongoConnection>(builder.Configuration.GetRequiredSection(ReportConstants.AppSettingsSectionNames.Mongo));
    builder.Services.Configure<ConsumerSettings>(builder.Configuration.GetRequiredSection(nameof(ConsumerSettings)));
    builder.Services.Configure<CorsSettings>(builder.Configuration.GetSection(ConfigurationConstants.AppSettings.CORS));
    builder.Services.Configure<LinkTokenServiceSettings>(builder.Configuration.GetSection(ConfigurationConstants.AppSettings.LinkTokenService));

    // Add services to the container.
    builder.Services.AddHttpClient();

    // Add factories
    builder.Services.AddTransient<IKafkaConsumerFactory<ResourceEvaluatedKey, ResourceEvaluatedValue>, KafkaConsumerFactory<ResourceEvaluatedKey, ResourceEvaluatedValue>>();
    builder.Services.AddTransient<ScheduledReportFactory>();
    builder.Services.AddTransient<MeasureReportSummaryFactory>();
    builder.Services.AddTransient<ResourceSummaryFactory>();

    builder.Services.AddTransient<IKafkaConsumerFactory<string, GenerateReportValue>, KafkaConsumerFactory<string, GenerateReportValue>>();
    builder.Services.AddTransient<IKafkaConsumerFactory<string, ReportScheduledValue>, KafkaConsumerFactory<string, ReportScheduledValue>>();
    builder.Services.AddTransient<IKafkaConsumerFactory<string, string>, KafkaConsumerFactory<string, string>>();
    builder.Services.AddTransient<IKafkaConsumerFactory<string, DataAcquisitionRequestedValue>, KafkaConsumerFactory<string, DataAcquisitionRequestedValue>>();
    builder.Services.AddTransient<IKafkaConsumerFactory<string, PatientIdsAcquiredValue>, KafkaConsumerFactory<string, PatientIdsAcquiredValue>>();
    builder.Services.AddTransient<IKafkaConsumerFactory<string, ValidationCompleteValue>, KafkaConsumerFactory<string, ValidationCompleteValue>>();
    builder.Services.AddTransient<IKafkaConsumerFactory<ReportSubmittedKey, ReportSubmittedValue>, KafkaConsumerFactory<ReportSubmittedKey, ReportSubmittedValue>>();

    builder.Services.AddTransient<IRetryEntityFactory, RetryEntityFactory>();

    //Producers
    builder.Services.RegisterKafkaProducers(kafkaConnection);

    //Producers for Retry/Deadletter
    builder.Services.AddTransient<IKafkaProducerFactory<string, ReportScheduledValue>, KafkaProducerFactory<string, ReportScheduledValue>>();
    builder.Services.AddTransient<IKafkaProducerFactory<ResourceEvaluatedKey, ResourceEvaluatedValue>, KafkaProducerFactory<ResourceEvaluatedKey, ResourceEvaluatedValue>>();
    builder.Services.AddTransient<IKafkaProducerFactory<string, PatientIdsAcquiredValue>, KafkaProducerFactory<string, PatientIdsAcquiredValue>>();
    builder.Services.AddTransient<IKafkaProducerFactory<string, GenerateReportValue>, KafkaProducerFactory<string, GenerateReportValue>>();
    builder.Services.AddTransient<IKafkaProducerFactory<string, ValidationCompleteValue>, KafkaProducerFactory<string, ValidationCompleteValue>>();
    builder.Services.AddTransient<IKafkaProducerFactory<ReportSubmittedKey, ReportSubmittedValue>, KafkaProducerFactory<ReportSubmittedKey, ReportSubmittedValue>>();

    // Add repositories
    builder.Services.AddTransient<IEntityRepository<ReportScheduleModel>, MongoEntityRepository<ReportScheduleModel>>();
    builder.Services.AddTransient<IEntityRepository<MeasureReportSubmissionEntryModel>, MongoEntityRepository<MeasureReportSubmissionEntryModel>>();
    builder.Services.AddTransient<IEntityRepository<ReportModel>, MongoEntityRepository<ReportModel>>();
    builder.Services.AddTransient<IEntityRepository<SharedResourceModel>, MongoEntityRepository<SharedResourceModel>>();
    builder.Services.AddTransient<IEntityRepository<PatientResourceModel>, MongoEntityRepository<PatientResourceModel>>();
    builder.Services.AddSingleton<IEntityRepository<RetryEntity>, MongoEntityRepository<RetryEntity>>();
    builder.Services.AddTransient<IDatabase, Database>();
    
    //Add Managers
    builder.Services.AddTransient<IReportScheduledManager, ReportScheduledManager>();
    builder.Services.AddTransient<ISubmissionEntryManager, SubmissionEntryManager>();
    builder.Services.AddTransient<IResourceManager, ResourceManager>();

    // Add Link Security
    bool allowAnonymousAccess = builder.Configuration.GetValue<bool>("Authentication:EnableAnonymousAccess");
    builder.Services.AddLinkBearerServiceAuthentication(options =>
    {
        options.Environment = builder.Environment;
        options.AllowAnonymous = allowAnonymousAccess;
        options.Authority = builder.Configuration.GetValue<string>("Authentication:Schemas:LinkBearer:Authority");
        options.ValidateToken = builder.Configuration.GetValue<bool>("Authentication:Schemas:LinkBearer:ValidateToken");
        options.ProtectKey = builder.Configuration.GetValue<bool>("DataProtection:Enabled");
        options.SigningKey = builder.Configuration.GetValue<string>("LinkTokenService:SigningKey");
    });

    // Add controllers
    builder.Services.AddControllers();

    //Add health checks
    var kafkaHealthOptions = new KafkaHealthCheckConfiguration(kafkaConnection, ReportConstants.ServiceName).GetHealthCheckOptions();

    builder.Services.AddHealthChecks()
        .AddCheck<DatabaseHealthCheck>("Database")
        .AddKafka(kafkaHealthOptions);

    // Add swagger
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen(c =>
    {
        if (!allowAnonymousAccess)
        {
            #region Authentication Schemas

            c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
            {
                Description = $"Authorization using JWT",
                Name = "Authorization",
                Type = SecuritySchemeType.Http,
                BearerFormat = "JWT",
                In = ParameterLocation.Header,
                Scheme = JwtBearerDefaults.AuthenticationScheme
            });

            c.AddSecurityRequirement(new OpenApiSecurityRequirement
            {
                {
                    new OpenApiSecurityScheme
                    {
                        Reference = new OpenApiReference
                        {
                            Id = "Bearer",
                            Type = ReferenceType.SecurityScheme
                        }
                    },
                    new List<string>()
                }
            });

            #endregion
        }

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
    builder.Services.AddSingleton<RetryJob>();
    builder.Services.AddSingleton<ISchedulerFactory, StdSchedulerFactory>();
    builder.Services.AddSingleton<EndOfReportPeriodJob>();

    // Add hosted services
    builder.Services.AddHostedService<GenerateReportListener>();
    builder.Services.AddHostedService<ResourceEvaluatedListener>();
    builder.Services.AddHostedService<ReportScheduledListener>();
    builder.Services.AddHostedService<PatientIdsAcquiredListener>();
    builder.Services.AddHostedService<ValidationCompleteListener>();
    builder.Services.AddHostedService<ReportSubmittedListener>();

    builder.Services.AddSingleton(new RetryListenerSettings(ReportConstants.ServiceName, [KafkaTopic.ReportScheduledRetry.GetStringValue(), KafkaTopic.ResourceEvaluatedRetry.GetStringValue(), KafkaTopic.PatientIDsAcquiredRetry.GetStringValue(), KafkaTopic.DataAcquisitionRequestedRetry.GetStringValue()]));
    builder.Services.AddHostedService<RetryListener>();

    builder.Services.AddHostedService<RetryScheduleService>();
    builder.Services.AddHostedService<MeasureReportScheduleService>();

    builder.Services.AddTransient<PatientReportSubmissionBundler>();
    builder.Services.AddTransient<MeasureReportAggregator>();
    builder.Services.AddTransient<SubmitReportProducer>();
    builder.Services.AddTransient<ReadyForValidationProducer>();
    builder.Services.AddTransient<DataAcquisitionRequestedProducer>();
    builder.Services.AddTransient<ITenantApiService, TenantApiService>();

    //Add persistence interceptors
    builder.Services.AddSingleton<UpdateBaseEntityInterceptor>();

    #region Exception Handling
    //Generate Report Listener
    builder.Services.AddTransient<IDeadLetterExceptionHandler<string, GenerateReportValue>, DeadLetterExceptionHandler<string, GenerateReportValue>>();
    builder.Services.AddTransient<ITransientExceptionHandler<string, GenerateReportValue>, TransientExceptionHandler<string, GenerateReportValue>>();

    //Report Scheduled Listener
    builder.Services.AddTransient<IDeadLetterExceptionHandler<string, ReportScheduledValue>, DeadLetterExceptionHandler<string, ReportScheduledValue>>();
    builder.Services.AddTransient<ITransientExceptionHandler<string, ReportScheduledValue>, TransientExceptionHandler<string, ReportScheduledValue>>();

    //Resource Evaluated Listener
    builder.Services.AddTransient<IDeadLetterExceptionHandler<ResourceEvaluatedKey, ResourceEvaluatedValue>, DeadLetterExceptionHandler<ResourceEvaluatedKey, ResourceEvaluatedValue>>();
    builder.Services.AddTransient<ITransientExceptionHandler<ResourceEvaluatedKey, ResourceEvaluatedValue>, TransientExceptionHandler<ResourceEvaluatedKey, ResourceEvaluatedValue>>();

    //Retry Listener
    builder.Services.AddTransient<IDeadLetterExceptionHandler<string, string>, DeadLetterExceptionHandler<string, string>>();

    //PatientIdsAcquired Listener
    builder.Services.AddTransient<ITransientExceptionHandler<string, PatientIdsAcquiredValue>, TransientExceptionHandler<string, PatientIdsAcquiredValue>>();
    builder.Services.AddTransient<IDeadLetterExceptionHandler<string, PatientIdsAcquiredValue>, DeadLetterExceptionHandler<string, PatientIdsAcquiredValue>>();

    //DataAcquisitionRequested Listener
    builder.Services.AddTransient<IDeadLetterExceptionHandler<string, DataAcquisitionRequestedValue>, DeadLetterExceptionHandler<string, DataAcquisitionRequestedValue>>();
    builder.Services.AddTransient<ITransientExceptionHandler<string, DataAcquisitionRequestedValue>, TransientExceptionHandler<string, DataAcquisitionRequestedValue>>();

    //ValidationComplete Listener
    builder.Services.AddTransient<IDeadLetterExceptionHandler<string, ValidationCompleteValue>, DeadLetterExceptionHandler<string, ValidationCompleteValue>>();
    builder.Services.AddTransient<ITransientExceptionHandler<string, ValidationCompleteValue>, TransientExceptionHandler<string, ValidationCompleteValue>>();
    
    //ReportSubmitted Listener
    builder.Services.AddTransient<IDeadLetterExceptionHandler<ReportSubmittedKey, ReportSubmittedValue>, DeadLetterExceptionHandler<ReportSubmittedKey, ReportSubmittedValue>>();
    builder.Services.AddTransient<ITransientExceptionHandler<ReportSubmittedKey, ReportSubmittedValue>, TransientExceptionHandler<ReportSubmittedKey, ReportSubmittedValue>>();

    #endregion

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

    //Serilog.Debugging.SelfLog.Enable(Console.Error);

    //Add CORS
    builder.Services.AddLinkCorsService(options => {
        options.Environment = builder.Environment;
    });

    //Add telemetry if enabled
    builder.Services.AddLinkTelemetry(builder.Configuration, options =>
    {
        options.Environment = builder.Environment;
        options.ServiceName = ReportConstants.ServiceName;
        options.ServiceVersion = serviceInformation.Version; //TODO: Get version from assembly?                
    });

    builder.Services.AddSingleton<IReportServiceMetrics, ReportServiceMetrics>();    
}

#endregion

#region Set up middleware

static void SetupMiddleware(WebApplication app)
{
    if (app.Environment.IsDevelopment())
    {
        app.UseDeveloperExceptionPage();
    }
    else
    {
        app.UseExceptionHandler();
    }

    app.ConfigureSwagger();

    //map health check middleware
    app.MapHealthChecks("/health", new HealthCheckOptions
    {
        ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
    });

    app.UseRouting();
    app.UseCors(CorsSettings.DefaultCorsPolicyName);

    //check for anonymous access
    var allowAnonymousAccess = app.Configuration.GetValue<bool>("Authentication:EnableAnonymousAccess");
    if (!allowAnonymousAccess)
    {
        app.UseAuthentication();
        app.UseMiddleware<UserScopeMiddleware>();
    }
    app.UseAuthorization();

    app.MapControllers();
}

#endregion
