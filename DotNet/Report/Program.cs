using Confluent.Kafka;
using HealthChecks.UI.Client;
using Hl7.Fhir.Serialization;
using LantanaGroup.Link.Report.Application.Core;
using LantanaGroup.Link.Report.Application.Extensions;
using LantanaGroup.Link.Report.Application.Interfaces;
using LantanaGroup.Link.Report.Application.Options;
using LantanaGroup.Link.Report.Application.Serialization;
using LantanaGroup.Link.Report.Data;
using LantanaGroup.Link.Report.Data.Entities;
using LantanaGroup.Link.Report.Domain.Managers;
using LantanaGroup.Link.Report.KafkaProducers;
using LantanaGroup.Link.Report.Listeners;
using LantanaGroup.Link.Report.Models;
using LantanaGroup.Link.Report.Services;
using LantanaGroup.Link.Report.Settings;
using LantanaGroup.Link.Shared.Application.Error.Handlers;
using LantanaGroup.Link.Shared.Application.Error.Interfaces;
using LantanaGroup.Link.Shared.Application.Extensions;
using LantanaGroup.Link.Shared.Application.Extensions.Quartz;
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
using LantanaGroup.Link.Shared.Settings;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
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
    var loggerConfig = new LoggerConfiguration()
        .ReadFrom.Configuration(builder.Configuration)
        .Filter.ByExcluding("RequestPath like '/health%'")
        .Filter.ByExcluding("RequestPath like '/swagger%'")
        .Enrich.WithExceptionDetails()
        .Enrich.FromLogContext()
        .Enrich.WithSpan()
        .Enrich.With<ActivityEnricher>();

    Log.Logger = loggerConfig.CreateLogger();
    builder.Logging.AddSerilog();
}

static void RegisterServices(WebApplicationBuilder builder)
{


    var assemblyVersion = Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? string.Empty;
    var serviceInformation = builder.SetupServiceInformation(ReportConstants.ServiceName, assemblyVersion);

    builder.AddExternalConfiguration(serviceInformation.ServiceConfigName);

    builder.WebHost.ConfigureKestrel(options =>
    {
        options.Limits.MaxRequestBodySize = 200 * 1024 * 1024; // Set limit to 200 MB
    });

    builder.Services.AddProblemDetailsService(options =>
    {
        options.Environment = builder.Environment;
        options.ServiceName = serviceInformation.ServiceConfigName;
        options.IncludeExceptionDetails = builder.Configuration.GetValue<bool>("ProblemDetails:IncludeExceptionDetails");
    });

    builder.Services.Configure<ServiceRegistry>(builder.Configuration.GetSection(ServiceRegistry.ConfigSectionName));
    var kafkaConnection = builder.Configuration.GetSection(KafkaConstants.SectionName).Get<KafkaConnection>();
    builder.Services.AddSingleton<KafkaConnection>(kafkaConnection);
    builder.Services.Configure<ConsumerSettings>(builder.Configuration.GetRequiredSection(nameof(ConsumerSettings)));
    builder.Services.Configure<CorsSettings>(builder.Configuration.GetSection(ConfigurationConstants.AppSettings.CORS));
    builder.Services.Configure<LinkTokenServiceSettings>(builder.Configuration.GetSection(ConfigurationConstants.AppSettings.LinkTokenService));
    builder.Services.Configure<BlobStorageSettings>(builder.Configuration.GetSection(BlobStorageSettings.Key));
    builder.Services.Configure<PatientAggregatorSettings>(builder.Configuration.GetSection(PatientAggregatorSettings.Key));
    // Bound by explicit key rather than GetSection: this flag is one cross-runtime decision shared
    // with the Java Validation service, so both runtimes read a single Azure App Configuration row.
    // App Config's convention is '/'-separated, which Spring maps to '.' but .NET passes through
    // verbatim, so the slashed form is checked first and the dotted form (appsettings / compose env,
    // where a leading-slash name is not usable) is the fallback. See PreQualificationSettings.
    builder.Services.Configure<PreQualificationSettings>(options =>
        options.WritePreQualOperationOutcome =
            builder.Configuration.GetValue<bool?>(PreQualificationSettings.AppConfigurationKey)
            ?? builder.Configuration.GetValue<bool>(PreQualificationSettings.WritePreQualOperationOutcomeKey));


    string? connectionString = builder.Configuration.GetConnectionString("DatabaseConnection");

    builder.Services.AddDbContext<ReportDbContext>((sp, options) =>
    {
        var querySettings = builder.Configuration.GetRequiredSection(nameof(EnhancedQueryLoggingSettings)).Get<EnhancedQueryLoggingSettings>();

        if (string.IsNullOrEmpty(connectionString))
            throw new InvalidOperationException("Database connection string is null or empty.");

        if (querySettings != null && querySettings.EnableEnhancedQueryLogging)
        {
            var loggerFactory = sp.GetRequiredService<ILoggerFactory>();
            options.UseSqlServer(connectionString)
                   .UseLoggerFactory(loggerFactory)
                   .EnableSensitiveDataLogging();
        }
        else
        {
            options.UseSqlServer(connectionString);
        }
    });

    builder.Services.RegisterQuartzDatabase(connectionString);
    builder.Services.AddSingleton<IQuartzJobHelper, QuartzJobHelper>();

    builder.Services.AddHttpClient();

    builder.Services.AddTransient<IKafkaConsumerFactory<string, GenerateReportValue>, KafkaConsumerFactory<string, GenerateReportValue>>();
    builder.Services.AddTransient<IKafkaConsumerFactory<string, ReportScheduledValue>, KafkaConsumerFactory<string, ReportScheduledValue>>();
    builder.Services.AddTransient<IKafkaConsumerFactory<string, string>, KafkaConsumerFactory<string, string>>();
    builder.Services.AddTransient<IKafkaConsumerFactory<string, DataAcquisitionRequestedValue>, KafkaConsumerFactory<string, DataAcquisitionRequestedValue>>();
    builder.Services.AddTransient<IKafkaConsumerFactory<string, PatientEventValue>, KafkaConsumerFactory<string, PatientEventValue>>();
    builder.Services.AddTransient<IKafkaConsumerFactory<string, ValidationCompleteValue>, KafkaConsumerFactory<string, ValidationCompleteValue>>();
    builder.Services.AddTransient<IKafkaConsumerFactory<PayloadSubmittedKey, PayloadSubmittedValue>, KafkaConsumerFactory<PayloadSubmittedKey, PayloadSubmittedValue>>();
    builder.Services.AddTransient<IKafkaConsumerFactory<Null, MeasureReportGeneratedValue>, KafkaConsumerFactory<Null, MeasureReportGeneratedValue>>();

    builder.Services.AddTransient<IRetryModelFactory, RetryModelFactory>();

    builder.Services.RegisterKafkaProducers(kafkaConnection);

    builder.Services.AddTransient<IKafkaProducerFactory<string, ReportScheduledValue>, KafkaProducerFactory<string, ReportScheduledValue>>();
    builder.Services.AddTransient<IKafkaProducerFactory<string, PatientEventValue>, KafkaProducerFactory<string, PatientEventValue>>();
    builder.Services.AddTransient<IKafkaProducerFactory<string, GenerateReportValue>, KafkaProducerFactory<string, GenerateReportValue>>();
    builder.Services.AddTransient<IKafkaProducerFactory<string, ValidationCompleteValue>, KafkaProducerFactory<string, ValidationCompleteValue>>();
    builder.Services.AddTransient<IKafkaProducerFactory<PayloadSubmittedKey, PayloadSubmittedValue>, KafkaProducerFactory<PayloadSubmittedKey, PayloadSubmittedValue>>();
    builder.Services.AddTransient<IKafkaProducerFactory<string, AuditEventMessage>, KafkaProducerFactory<string, AuditEventMessage>>();
    builder.Services.AddTransient<IKafkaProducerFactory<Null, MeasureReportGeneratedValue>, KafkaProducerFactory<Null, MeasureReportGeneratedValue>>();

    builder.Services.AddTransient<IEntityRepository<ReportSchedule>, EntityRepository<ReportSchedule, ReportDbContext>>();
    builder.Services.AddTransient<IEntityRepository<ReportEntry>, EntityRepository<ReportEntry, ReportDbContext>>();
    builder.Services.AddTransient<IEntityRepository<ReportPopulation>, EntityRepository<ReportPopulation, ReportDbContext>>();
    builder.Services.AddTransient<IEntityRepository<ReportResource>, EntityRepository<ReportResource, ReportDbContext>>();
    builder.Services.AddTransient<IEntityRepository<GroupPopulation>, EntityRepository<GroupPopulation, ReportDbContext>>();
    builder.Services.AddTransient<IEntityRepository<MeasureReportPopulation>, EntityRepository<MeasureReportPopulation, ReportDbContext>>();
    builder.Services.AddTransient<IDatabase, Database>();

    builder.Services.AddTransient<IReportScheduledManager, ReportScheduledManager>();
    builder.Services.AddTransient<IReportEntryManager, ReportEntryManager>();
    builder.Services.AddTransient<IReportPopulationManager, ReportPopulationManager>();
    builder.Services.AddTransient<IReportResourceManager, ReportResourceManager>();

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

    builder.Services.AddControllers().AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.ForFhir();
        options.JsonSerializerOptions.Converters.Add(new JsonDateTimeConverter());
        options.JsonSerializerOptions.Converters.Add(new JsonNullableDateTimeConverter());
    });

    var kafkaHealthOptions = new KafkaHealthCheckConfiguration(kafkaConnection, serviceInformation.ServiceConfigName).GetHealthCheckOptions();
    builder.Services.AddHealthChecks()
        .AddCheck<DatabaseHealthCheck>(HealthCheckType.Database.ToString())
        .AddKafka(kafkaHealthOptions, HealthCheckType.Kafka.ToString());

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


    builder.Services.AddSingleton(new RetryListenerSettings(serviceInformation.ServiceConfigName, [
            KafkaTopic.ReportScheduledRetry.GetStringValue(),
            KafkaTopic.MeasureReportGeneratedRetry.GetStringValue(),
            KafkaTopic.PatientEventRetry.GetStringValue(),
            KafkaTopic.GenerateReportRequestedRetry.GetStringValue(),
            KafkaTopic.PayloadSubmittedRetry.GetStringValue(),
            KafkaTopic.ValidationCompleteRetry.GetStringValue(),
        ]));

    builder.Services.AddHostedService<RetryScheduleService>();
    builder.Services.AddHostedService<RetryListener>();
    builder.Services.AddHostedService<GenerateReportListener>();
    builder.Services.AddHostedService<ReportScheduledListener>();
    builder.Services.AddHostedService<PatientEventListener>();
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

    builder.Services.AddSingleton<UpdateBaseEntityInterceptor>();

    builder.Services.AddSingleton(typeof(IExceptionLogger<>), typeof(ExceptionLogger<>));
    builder.Services.AddSingleton(typeof(ITransientExceptionHandler<,,>), typeof(TransientExceptionHandler<,,>));
    builder.Services.AddSingleton(typeof(IDeadLetterExceptionHandler<,,>), typeof(DeadLetterExceptionHandler<,,>));

    builder.Services.AddLinkCorsService(options =>
    {
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
    app.AutoMigrateEF<ReportDbContext>();

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