using Confluent.Kafka;
using HealthChecks.UI.Client;
using Hl7.Fhir.Serialization;
using LantanaGroup.Link.Report.Application.Extensions;
using LantanaGroup.Link.Report.Application.Factory;
using LantanaGroup.Link.Report.Application.Interfaces;
using LantanaGroup.Link.Report.Application.Models;
using LantanaGroup.Link.Report.Application.Options;
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
using LantanaGroup.Link.Shared.Application.Services;
using LantanaGroup.Link.Shared.Application.Utilities;
using LantanaGroup.Link.Shared.Domain.Repositories.Implementations;
using LantanaGroup.Link.Shared.Domain.Repositories.Interceptors;
using LantanaGroup.Link.Shared.Domain.Repositories.Interfaces;
using LantanaGroup.Link.Shared.Jobs;
using LantanaGroup.Link.Shared.Settings;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi.Models;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;
using MongoDB.Driver;
using Quartz;
using Serilog;
using Serilog.Enrichers.Span;
using Serilog.Exceptions;
using System.Reflection;

var builder = WebApplication.CreateBuilder(args);
builder.Configuration.AddStandardEnvironmentConfiguration();

ConfigureLogging(builder);
RegisterServices(builder);
var app = builder.Build();
SetupMiddleware(app);

app.Run();

static void ConfigureLogging(WebApplicationBuilder builder)
{
    // Create Serilog logger configuration
    var loggerConfig = new LoggerConfiguration()
        .ReadFrom.Configuration(builder.Configuration)
        .Filter.ByExcluding("RequestPath like '/health%'")
        .Filter.ByExcluding("RequestPath like '/swagger%'")
        .Enrich.WithExceptionDetails()
        .Enrich.FromLogContext()
        .Enrich.WithSpan()
        .Enrich.With<ActivityEnricher>();

    Log.Logger = loggerConfig.CreateLogger();

    // Add Serilog to the logging pipeline
    builder.Logging.AddSerilog();
}

static void RegisterServices(WebApplicationBuilder builder)
{
    var objectSerializer = new ObjectSerializer(type =>
        ObjectSerializer.DefaultAllowedTypes(type) || // Keep defaults (primitives, safe types, etc.)
        type.FullName?.StartsWith("LantanaGroup.Link.Shared") == true || // Your shared assembly/namespace
        type.FullName?.StartsWith("LantanaGroup.Link.Report") == true);   // Add report-specific if needed

    BsonClassMap.RegisterClassMap<RetryModel>(cm =>
    {
        cm.AutoMap();
        cm.SetIgnoreExtraElements(true);
        cm.GetMemberMap(c => c.Id).SetSerializer(new GuidSerializer(GuidRepresentation.Standard));
    });

    var assemblyVersion = Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? string.Empty;
    var serviceInformation = builder.SetupServiceInformation(ReportConstants.ServiceName, assemblyVersion);

    // load external configuration source (if specified)
    builder.AddExternalConfiguration(serviceInformation.ServiceConfigName);

    BsonSerializer.RegisterSerializer(objectSerializer);

    // Bind MongoConnection settings from configuration (e.g., appsettings.json)
    builder.Services.Configure<MongoConnection>(builder.Configuration.GetRequiredSection(ReportConstants.AppSettingsSectionNames.Mongo));
    var mongoSettings = builder.Services.BuildServiceProvider().GetRequiredService<IOptions<MongoConnection>>().Value;

    builder.WebHost.ConfigureKestrel(options =>
    {
        options.Limits.MaxRequestBodySize = 200 * 1024 * 1024; // Set limit to 200 MB
    });

    // Add problem details
    builder.Services.AddProblemDetailsService(options =>
    {
        options.Environment = builder.Environment;
        options.ServiceName = serviceInformation.ServiceConfigName;
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
    builder.Services.Configure<BlobStorageSettings>(builder.Configuration.GetSection(BlobStorageSettings.Key));

    // Bind MongoConnection settings from configuration (e.g., appsettings.json)
    builder.Services.Configure<MongoConnection>(builder.Configuration.GetRequiredSection(ReportConstants.AppSettingsSectionNames.Mongo));

    // Register IMongoClient as a singleton using the configured connection string
    builder.Services.AddSingleton<IMongoClient>(sp =>
    {
        return new MongoClient(mongoSettings.ConnectionString);
    });

    // Register IMongoDatabase as a singleton using the shared client and database name
    builder.Services.AddSingleton<IMongoDatabase>(sp =>
    {
        var mongoSettings = sp.GetRequiredService<IOptions<MongoConnection>>().Value;
        var client = sp.GetRequiredService<IMongoClient>();
        return client.GetDatabase(mongoSettings.DatabaseName);
    });

    // Add the MongoDbContext to the DI container, using the shared client
    builder.Services.AddDbContext<MongoDbContext>((sp, options) =>
    {
        var querySettings = builder.Configuration.GetRequiredSection(nameof(EnhancedQueryLoggingSettings)).Get<EnhancedQueryLoggingSettings>();

        var client = sp.GetRequiredService<IMongoClient>();
        var mongoSettings = sp.GetRequiredService<IOptions<MongoConnection>>().Value;

        if (querySettings != null && querySettings.EnableEnhancedQueryLogging)
        {
            var loggerFactory = sp.GetRequiredService<ILoggerFactory>();
            options.UseMongoDB(client, mongoSettings.DatabaseName)
            .UseLoggerFactory(loggerFactory)
            .EnableSensitiveDataLogging();
        }
        else
        {
            options.UseMongoDB(client, mongoSettings.DatabaseName);
        }
    });

    builder.Services.AddHostedService<MongoIndexCreationService>();

    // Add services to the container
    builder.Services.AddHttpClient();

    // Add factories
    builder.Services.AddTransient<ScheduledReportFactory>();
    builder.Services.AddTransient<MeasureReportSummaryFactory>();

    builder.Services.AddTransient<IKafkaConsumerFactory<string, GenerateReportValue>, KafkaConsumerFactory<string, GenerateReportValue>>();
    builder.Services.AddTransient<IKafkaConsumerFactory<string, ReportScheduledValue>, KafkaConsumerFactory<string, ReportScheduledValue>>();
    builder.Services.AddTransient<IKafkaConsumerFactory<string, string>, KafkaConsumerFactory<string, string>>();
    builder.Services.AddTransient<IKafkaConsumerFactory<string, DataAcquisitionRequestedValue>, KafkaConsumerFactory<string, DataAcquisitionRequestedValue>>();
    builder.Services.AddTransient<IKafkaConsumerFactory<string, PatientListMessage>, KafkaConsumerFactory<string, PatientListMessage>>();
    builder.Services.AddTransient<IKafkaConsumerFactory<string, ValidationCompleteValue>, KafkaConsumerFactory<string, ValidationCompleteValue>>();
    builder.Services.AddTransient<IKafkaConsumerFactory<PayloadSubmittedKey, PayloadSubmittedValue>, KafkaConsumerFactory<PayloadSubmittedKey, PayloadSubmittedValue>>();
    builder.Services.AddTransient<IKafkaConsumerFactory<Null, MeasureReportGeneratedValue>, KafkaConsumerFactory<Null, MeasureReportGeneratedValue>>();

    builder.Services.AddTransient<IRetryModelFactory, RetryModelFactory>();

    // Producers
    builder.Services.RegisterKafkaProducers(kafkaConnection);

    // Producers for Retry/Deadletter
    builder.Services.AddTransient<IKafkaProducerFactory<string, ReportScheduledValue>, KafkaProducerFactory<string, ReportScheduledValue>>();
    builder.Services.AddTransient<IKafkaProducerFactory<string, PatientListMessage>, KafkaProducerFactory<string, PatientListMessage>>();
    builder.Services.AddTransient<IKafkaProducerFactory<string, GenerateReportValue>, KafkaProducerFactory<string, GenerateReportValue>>();
    builder.Services.AddTransient<IKafkaProducerFactory<string, ValidationCompleteValue>, KafkaProducerFactory<string, ValidationCompleteValue>>();
    builder.Services.AddTransient<IKafkaProducerFactory<PayloadSubmittedKey, PayloadSubmittedValue>, KafkaProducerFactory<PayloadSubmittedKey, PayloadSubmittedValue>>();
    builder.Services.AddTransient<IKafkaProducerFactory<string, AuditEventMessage>, KafkaProducerFactory<string, AuditEventMessage>>();
    builder.Services.AddTransient<IKafkaProducerFactory<Null, MeasureReportGeneratedValue>, KafkaProducerFactory<Null, MeasureReportGeneratedValue>>();

    // Add repositories
    builder.Services.AddTransient<IEntityRepository<ReportSchedule>, EntityRepository<ReportSchedule, MongoDbContext>>();
    builder.Services.AddTransient<IEntityRepository<ReportEntry>, EntityRepository<ReportEntry, MongoDbContext>>();
    builder.Services.AddTransient<IEntityRepository<ReportPopulation>, EntityRepository<ReportPopulation, MongoDbContext>>();
    builder.Services.AddTransient<IEntityRepository<ReportResource>, EntityRepository<ReportResource, MongoDbContext>>();
    builder.Services.AddTransient<IDatabase, Database>();

    // Add Managers
    builder.Services.AddTransient<IReportScheduledManager, ReportScheduledManager>();
    builder.Services.AddTransient<IReportEntryManager, ReportEntryManager>();
    builder.Services.AddTransient<IReportPopulationManager, ReportPopulationManager>();
    builder.Services.AddTransient<IReportResourceManager, ReportResourceManager>();

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
    builder.Services.AddControllers().AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.ForFhir();
    });

    // Add health checks
    var kafkaHealthOptions = new KafkaHealthCheckConfiguration(kafkaConnection, serviceInformation.ServiceConfigName).GetHealthCheckOptions();
    builder.Services.AddHealthChecks()
        .AddCheck<DatabaseHealthCheck>(HealthCheckType.Database.ToString())
        .AddKafka(kafkaHealthOptions, HealthCheckType.Kafka.ToString());

    // Add swagger
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen(c =>
    {
        if (!allowAnonymousAccess)
        {
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
        }

        var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
        var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
        c.IncludeXmlComments(xmlPath);
        c.DocumentFilter<HealthChecksFilter>();
    });

    // Add Quartz schedulers
    // 1. MongoDB scheduler
    builder.Services.AddQuartz();
    builder.Services.AddSingleton<ReportMongoSchedulerFactory>();
    builder.Services.AddSingleton<ISchedulerFactory>(sp => sp.GetRequiredService<ReportMongoSchedulerFactory>());

    // Register job factory and jobsS
    builder.Services.AddSingleton<RetryJob>();
    builder.Services.AddSingleton<EndOfReportPeriodJob>();

    builder.Services.AddHostedService<RetryScheduleService>();
    builder.Services.AddHostedService<MeasureReportScheduleService>();

    // Register listeners
    builder.Services.AddSingleton(new RetryListenerSettings(serviceInformation.ServiceConfigName, [
            KafkaTopic.ReportScheduledRetry.GetStringValue(),
            KafkaTopic.MeasureReportGeneratedRetry.GetStringValue(),
            KafkaTopic.PatientListsAcquiredRetry.GetStringValue(),
            KafkaTopic.GenerateReportRequestedRetry.GetStringValue(),
            KafkaTopic.PayloadSubmittedRetry.GetStringValue(),
            KafkaTopic.ValidationCompleteRetry.GetStringValue(),
        ]));

    builder.Services.AddHostedService<RetryListener>();
    builder.Services.AddHostedService<GenerateReportListener>();
    builder.Services.AddHostedService<ReportScheduledListener>();
    builder.Services.AddHostedService<PatientListsAcquiredListener>();
    builder.Services.AddHostedService<ValidationCompleteListener>();
    builder.Services.AddHostedService<PayloadSubmittedListener>();
    builder.Services.AddHostedService<MeasureReportGeneratedListener>();

    builder.Services.AddTransient<PatientAggregator>();
    builder.Services.AddTransient<MeasureReportAggregator>();
    builder.Services.AddTransient<ReportManifestProducer>();
    builder.Services.AddTransient<SubmitPayloadProducer>();
    builder.Services.AddTransient<ReadyForValidationProducer>();
    builder.Services.AddTransient<DataAcquisitionRequestedProducer>();
    builder.Services.AddTransient<ITenantApiService, TenantApiService>();
    builder.Services.AddSingleton<BlobStorageService>();

    builder.Services.AddTransient<AuditableEventOccurredProducer>();

    // Add persistence interceptors
    builder.Services.AddSingleton<UpdateBaseEntityInterceptor>();

    // Exception Handling
    builder.Services.AddTransient<IDeadLetterExceptionHandler<string, GenerateReportValue>, DeadLetterExceptionHandler<string, GenerateReportValue>>();
    builder.Services.AddTransient<ITransientExceptionHandler<string, GenerateReportValue>, TransientExceptionHandler<string, GenerateReportValue>>();

    builder.Services.AddTransient<IDeadLetterExceptionHandler<string, ReportScheduledValue>, DeadLetterExceptionHandler<string, ReportScheduledValue>>();
    builder.Services.AddTransient<ITransientExceptionHandler<string, ReportScheduledValue>, TransientExceptionHandler<string, ReportScheduledValue>>();

    builder.Services.AddTransient<IDeadLetterExceptionHandler<Null, MeasureReportGeneratedValue>, DeadLetterExceptionHandler<Null, MeasureReportGeneratedValue>>();

    builder.Services.AddTransient<ITransientExceptionHandler<Null, MeasureReportGeneratedValue>, TransientExceptionHandler<Null, MeasureReportGeneratedValue>>();

    builder.Services.AddTransient<IDeadLetterExceptionHandler<string, string>, DeadLetterExceptionHandler<string, string>>();

    //PatientListsAcquired Listener
    builder.Services.AddTransient<ITransientExceptionHandler<string, PatientListMessage>, TransientExceptionHandler<string, PatientListMessage>>();
    builder.Services.AddTransient<IDeadLetterExceptionHandler<string, PatientListMessage>, DeadLetterExceptionHandler<string, PatientListMessage>>();

    //DataAcquisitionRequested Listener
    builder.Services.AddTransient<IDeadLetterExceptionHandler<string, DataAcquisitionRequestedValue>, DeadLetterExceptionHandler<string, DataAcquisitionRequestedValue>>();
    builder.Services.AddTransient<ITransientExceptionHandler<string, DataAcquisitionRequestedValue>, TransientExceptionHandler<string, DataAcquisitionRequestedValue>>();

    //ValidationComplete Listener
    builder.Services.AddTransient<IDeadLetterExceptionHandler<string, ValidationCompleteValue>, DeadLetterExceptionHandler<string, ValidationCompleteValue>>();
    builder.Services.AddTransient<ITransientExceptionHandler<string, ValidationCompleteValue>, TransientExceptionHandler<string, ValidationCompleteValue>>();

    builder.Services.AddTransient<IDeadLetterExceptionHandler<PayloadSubmittedKey, PayloadSubmittedValue>, DeadLetterExceptionHandler<PayloadSubmittedKey, PayloadSubmittedValue>>();
    builder.Services.AddTransient<ITransientExceptionHandler<PayloadSubmittedKey, PayloadSubmittedValue>, TransientExceptionHandler<PayloadSubmittedKey, PayloadSubmittedValue>>();

    builder.Services.AddTransient<IDeadLetterExceptionHandler<string, DataAcquisitionRequestedValue>, DeadLetterExceptionHandler<string, DataAcquisitionRequestedValue>>();
    builder.Services.AddTransient<ITransientExceptionHandler<string, DataAcquisitionRequestedValue>, TransientExceptionHandler<string, DataAcquisitionRequestedValue>>();

    builder.Services.AddLinkCorsService(options => {
        options.Environment = builder.Environment;
    });

    builder.Services.AddLinkTelemetry(builder.Configuration, options =>
    {
        options.Environment = builder.Environment;
        options.ServiceName = ReportConstants.ServiceName;
        options.ServiceVersion = serviceInformation.Version;
    });

    builder.Services.AddSingleton<IReportServiceMetrics, ReportServiceMetrics>();
}

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
    app.MapHealthChecks("/health", new HealthCheckOptions
    {
        ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
    });
    app.MapInfo(Assembly.GetExecutingAssembly(), app.Configuration, "report");

    app.UseRouting();
    app.UseCors(CorsSettings.DefaultCorsPolicyName);

    var allowAnonymousAccess = app.Configuration.GetValue<bool>("Authentication:EnableAnonymousAccess");
    if (!allowAnonymousAccess)
    {
        app.UseAuthentication();
        app.UseMiddleware<UserScopeMiddleware>();
    }
    app.UseAuthorization();

    app.MapControllers();
}