using System.Diagnostics;
using System.Reflection;
using System.Text.Json.Serialization;
using Census.Domain.Entities;
using Confluent.Kafka;
using HealthChecks.UI.Client;
using Hl7.Fhir.Serialization;
using LantanaGroup.Link.Census.Application.HealthChecks;
using LantanaGroup.Link.Census.Application.Interfaces;
using LantanaGroup.Link.Census.Application.Jobs;
using LantanaGroup.Link.Census.Application.Repositories;
using LantanaGroup.Link.Census.Application.Repositories.Scheduling;
using LantanaGroup.Link.Census.Application.Services;
using LantanaGroup.Link.Census.Application.Settings;
using LantanaGroup.Link.Census.Domain.Context;
using LantanaGroup.Link.Census.Domain.Entities.POI;
using LantanaGroup.Link.Census.Domain.Managers;
using LantanaGroup.Link.Census.Domain.Queries;
using LantanaGroup.Link.Census.Listeners;
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
using LantanaGroup.Link.Shared.Domain.Repositories.Interceptors;
using LantanaGroup.Link.Shared.Domain.Repositories.Interfaces;
using LantanaGroup.Link.Shared.Jobs;
using LantanaGroup.Link.Shared.Settings;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using Serilog;
using Serilog.Enrichers.Span;
using Serilog.Exceptions;
using PatientEvent = LantanaGroup.Link.Census.Domain.Entities.POI.PatientEvent;

var builder = WebApplication.CreateBuilder(args);
builder.Configuration.AddStandardEnvironmentConfiguration();

RegisterServices(builder);
var app = builder.Build();
SetupMiddleware(app);

app.Run();

static void RegisterServices(WebApplicationBuilder builder)
{
    // Load external configuration source (if specified)
    builder.AddExternalConfiguration(CensusConstants.ServiceName);

    var assemblyVersion = Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? string.Empty;
    var serviceInformation = builder.SetupServiceInformation(CensusConstants.ServiceName, assemblyVersion);

    if (serviceInformation == null)
    {
        throw new InvalidOperationException("Service information could not be loaded properly.");
    }

    // Configuration settings
    builder.Services.Configure<ServiceRegistry>(builder.Configuration.GetSection(ServiceRegistry.ConfigSectionName));
    builder.Services.AddSingleton(builder.Configuration.GetSection(KafkaConstants.SectionName).Get<KafkaConnection>());
    builder.Services.Configure<CorsSettings>(builder.Configuration.GetSection(ConfigurationConstants.AppSettings.CORS));
    builder.Services.Configure<LinkTokenServiceSettings>(builder.Configuration.GetSection(ConfigurationConstants.AppSettings.LinkTokenService));
    builder.Services.Configure<ConsumerSettings>(builder.Configuration.GetSection(nameof(ConsumerSettings)));
    var consumerSettings = builder.Configuration.GetSection(nameof(ConsumerSettings)).Get<ConsumerSettings>();

    // EF Core and Interceptors
    builder.Services.AddTransient<UpdateBaseEntityInterceptor>();
    var dbProvider = builder.Configuration.GetValue<string>(CensusConstants.AppSettings.DatabaseProvider);
    string? databaseConnectionString = null;

    if (dbProvider == ConfigurationConstants.AppSettings.SqlServerDatabaseProvider)
    {
        databaseConnectionString = builder.Configuration.GetConnectionString(ConfigurationConstants.DatabaseConnections.DatabaseConnection);

        if (string.IsNullOrEmpty(databaseConnectionString))
            throw new InvalidOperationException("Database connection string is null or empty.");

        // Quartz Scheduler with SQL persistence
        builder.Services.RegisterQuartzDatabase(databaseConnectionString);
    }

    builder.Services.AddDbContext<CensusContext>((sp, options) =>
    {
        var updateBaseEntityInterceptor = sp.GetService<UpdateBaseEntityInterceptor>();
        switch (dbProvider)
        {
            case ConfigurationConstants.AppSettings.SqlServerDatabaseProvider:
                options.UseSqlServer(databaseConnectionString, sqlOptions =>
                {
                    sqlOptions.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery);
                })
                .AddInterceptors(updateBaseEntityInterceptor);

                break;
            default:
                throw new InvalidOperationException($"Database provider not supported. Attempting to find section named: {CensusConstants.AppSettings.DatabaseProvider}");
        }
    });

    // Core Services
    builder.Services.AddHttpClient();
    builder.Services.AddControllers().AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
        options.JsonSerializerOptions.PropertyNameCaseInsensitive = true;
        options.JsonSerializerOptions.ForFhir();
    });
    builder.Services.AddGrpcReflection();

    // Kafka Consumers and Producers
    builder.Services.AddTransient<IKafkaConsumerFactory<string, string>, KafkaConsumerFactory<string, string>>();
    builder.Services.AddTransient<IKafkaConsumerFactory<string, PatientListMessage>, KafkaConsumerFactory<string, PatientListMessage>>();
    builder.Services.AddTransient<IKafkaConsumerFactory<string, CernerPatientsAcquired>, KafkaConsumerFactory<string, CernerPatientsAcquired>>();
    builder.Services.AddTransient<IKafkaProducerFactory<string, string>, KafkaProducerFactory<string, string>>();
    builder.Services.AddTransient<IKafkaProducerFactory<string, PatientListMessage>, KafkaProducerFactory<string, PatientListMessage>>();
    builder.Services.AddTransient<IKafkaProducerFactory<string, CernerPatientsAcquired>, KafkaProducerFactory<string, CernerPatientsAcquired>>();
    builder.Services.AddTransient<IKafkaProducerFactory<string, object>, KafkaProducerFactory<string, object>>();
    builder.Services.AddTransient<IKafkaProducerFactory<string, AuditEventMessage>, KafkaProducerFactory<string, AuditEventMessage>>();
    builder.Services.AddTransient<IKafkaProducerFactory<string, LantanaGroup.Link.Census.Application.Models.Messages.PatientEvent>, KafkaProducerFactory<string, LantanaGroup.Link.Census.Application.Models.Messages.PatientEvent>>();

    var kafkaConnection = builder.Configuration.GetSection(KafkaConstants.SectionName).Get<KafkaConnection>();
    builder.Services.RegisterKafkaProducer<string, object>(kafkaConnection, new ProducerConfig());
    builder.Services.RegisterKafkaProducer<string, Null>(kafkaConnection, new ProducerConfig());
    builder.Services.RegisterKafkaProducer<string, LantanaGroup.Link.Census.Application.Models.Messages.PatientEvent>(kafkaConnection, new ProducerConfig());

    // Factories
    builder.Services.AddTransient<IRetryModelFactory, RetryModelFactory>();

    // Repositories
    builder.Services.AddTransient<IBaseEntityRepository<CensusConfigEntity>, CensusEntityRepository<CensusConfigEntity>>();
    builder.Services.AddTransient<IBaseEntityRepository<PatientEvent>, CensusEntityRepository<PatientEvent>>();
    builder.Services.AddTransient<IBaseEntityRepository<PatientEncounter>, CensusEntityRepository<PatientEncounter>>();
    builder.Services.AddScoped<ICensusSchedulingRepository, CensusSchedulingRepository>();

    // Managers
    builder.Services.AddTransient<ICensusConfigManager, CensusConfigManager>();
    builder.Services.AddTransient<IPatientEventManager, PatientEventManager>();
    builder.Services.AddTransient<IPatientEventQueries, PatientEventQueries>();
    builder.Services.AddTransient<IPatientEncounterQueries, PatientEncounterQueries>();
    builder.Services.AddTransient<IPatientEncounterManager, PatientEncounterManager>();

    // Application Services
    builder.Services.AddScoped<IPatientListService, PatientListService>();
    builder.Services.AddScoped<ICernerListService, CernerListService>();
    builder.Services.AddTransient<IEventProducerService<LantanaGroup.Link.Census.Application.Models.Messages.PatientEvent>, EventProducerService<LantanaGroup.Link.Census.Application.Models.Messages.PatientEvent>>();
    builder.Services.AddTransient<ITenantApiService, TenantApiService>();

    // Exception Handlers
    builder.Services.AddSingleton(typeof(IExceptionLogger<>), typeof(ExceptionLogger<>));
    builder.Services.AddSingleton(typeof(ITransientExceptionHandler<,,>), typeof(TransientExceptionHandler<,,>));
    builder.Services.AddSingleton(typeof(IDeadLetterExceptionHandler<,,>), typeof(DeadLetterExceptionHandler<,,>));

    builder.Services.AddTransient<SchedulePatientListRetrieval>();
    builder.Services.AddTransient<RetryJob>();

    // Hosted Services (avoid duplicate registrations)
    if (consumerSettings == null || !consumerSettings.DisableConsumer)
    {
        builder.Services.AddHostedService<PatientListsAcquiredListener>();
        builder.Services.AddHostedService<CernerPatientsAcquiredListener>();
    }
    if (consumerSettings == null || !consumerSettings.DisableRetryConsumer)
    {
        builder.Services.AddHostedService<ScheduleService>();
        builder.Services.AddSingleton(new RetryListenerSettings(serviceInformation.ServiceName, [KafkaTopic.PatientListsAcquiredRetry.GetStringValue()]));
        builder.Services.AddHostedService<RetryListener>();
    }

    // Security
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

    // Health Checks
    var kafkaHealthOptions = new KafkaHealthCheckConfiguration(kafkaConnection, CensusConstants.ServiceName).GetHealthCheckOptions();
    builder.Services.AddHealthChecks()
        .AddCheck<DatabaseHealthCheck>(HealthCheckType.Database.ToString())
        .AddKafka(kafkaHealthOptions, HealthCheckType.Kafka.ToString());

    // Swagger
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

    // Problem Details
    builder.Services.AddProblemDetails(options =>
    {
        options.CustomizeProblemDetails = ctx =>
        {
            ctx.ProblemDetails.Detail = "An error occurred in our API. Please use the trace id when requesting assistance.";
            if (!ctx.ProblemDetails.Extensions.ContainsKey("traceId"))
            {
                string? traceId = Activity.Current?.Id ?? ctx.HttpContext.TraceIdentifier;
                ctx.ProblemDetails.Extensions.Add(new KeyValuePair<string, object?>("traceId", traceId));
            }
            if (builder.Environment.IsDevelopment())
            {
                ctx.ProblemDetails.Extensions.Add("service", "Census");
            }
            else
            {
                ctx.ProblemDetails.Extensions.Remove("exception");
            }
        };
    });

    // CORS
    builder.Services.AddLinkCorsService(options =>
    {
        options.Environment = builder.Environment;
    });

    // Telemetry
    builder.Services.AddLinkTelemetry(builder.Configuration, options =>
    {
        options.Environment = builder.Environment;
        options.ServiceName = CensusConstants.ServiceName;
        options.ServiceVersion = serviceInformation.Version;
    });

    // Metrics
    builder.Services.AddSingleton<ICensusServiceMetrics, CensusServiceMetrics>();

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
}

static void SetupMiddleware(WebApplication app)
{
    app.AutoMigrateEF<CensusContext>();
    app.ConfigureSwagger();

    app.UseRouting();
    app.UseCors(CorsSettings.DefaultCorsPolicyName);

    if (app.Environment.IsDevelopment() || app.Environment.EnvironmentName.Equals("Local", StringComparison.InvariantCultureIgnoreCase))
    {
        app.UseDeveloperExceptionPage();
    }
    else
    {
        app.UseExceptionHandler();
    }

    var allowAnonymousAccess = app.Configuration.GetValue<bool>("Authentication:EnableAnonymousAccess");
    if (!allowAnonymousAccess)
    {
        app.UseAuthentication();
        app.UseMiddleware<UserScopeMiddleware>();
    }
    app.UseAuthorization();

    app.MapControllers();
    app.MapHealthChecks("/health", new HealthCheckOptions
    {
        ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
    });
    app.MapInfo(Assembly.GetExecutingAssembly(), app.Configuration, "census");
}
