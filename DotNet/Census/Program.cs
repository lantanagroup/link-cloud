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
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.OpenApi.Models;
using Quartz;
using Quartz.Impl;
using Quartz.Spi;
using Serilog;
using Serilog.Enrichers.Span;
using Serilog.Exceptions;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Reflection;
using PatientEvent = LantanaGroup.Link.Census.Domain.Entities.POI.PatientEvent;

var builder = WebApplication.CreateBuilder(args);

RegisterServices(builder);
var app = builder.Build();
SetupMiddleware(app);

app.Run();

static void RegisterServices(WebApplicationBuilder builder)
{
    // load external configuration source (if specified)
    builder.AddExternalConfiguration(CensusConstants.ServiceName);

    var serviceInformation = builder.Configuration.GetRequiredSection(CensusConstants.AppSettings.ServiceInformation).Get<ServiceInformation>();
    if (serviceInformation != null)
    {
        ServiceActivitySource.Initialize(serviceInformation);
    }
    else
    {
        throw new NullReferenceException("Service Information was null.");
    }

    // Add configuration settings
    builder.Services.Configure<ServiceRegistry>(builder.Configuration.GetSection(ServiceRegistry.ConfigSectionName));
    builder.Services.AddSingleton(builder.Configuration.GetSection(KafkaConstants.SectionName).Get<KafkaConnection>());
    builder.Services.Configure<CorsSettings>(builder.Configuration.GetSection(ConfigurationConstants.AppSettings.CORS));
    builder.Services.Configure<LinkTokenServiceSettings>(builder.Configuration.GetSection(ConfigurationConstants.AppSettings.LinkTokenService));

    IConfigurationSection consumerSettingsSection = builder.Configuration.GetSection(nameof(ConsumerSettings));
    builder.Services.Configure<ConsumerSettings>(consumerSettingsSection);
    var consumerSettings = consumerSettingsSection.Get<ConsumerSettings>();

    // Add EF Core
    builder.Services.AddTransient<UpdateBaseEntityInterceptor>();
    builder.Services.AddDbContext<CensusContext>((sp, options) =>
    {
        var updateBaseEntityInterceptor = sp.GetService<UpdateBaseEntityInterceptor>();
        switch (builder.Configuration.GetValue<string>(CensusConstants.AppSettings.DatabaseProvider))
        {
            case ConfigurationConstants.AppSettings.SqlServerDatabaseProvider:
                string? connectionString = builder.Configuration.GetConnectionString(ConfigurationConstants.DatabaseConnections.DatabaseConnection);
                if (string.IsNullOrEmpty(connectionString))
                    throw new InvalidOperationException("Database connection string is null or empty.");

                options.UseSqlServer(connectionString,
                        sqlServerOptionsAction: sqlOptions =>
                        {
                            // Ensure JSON capabilities are enabled
                            sqlOptions.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery);
                        })
                   .AddInterceptors(updateBaseEntityInterceptor);
                options.UseSqlServer(connectionString)
                       .AddInterceptors(updateBaseEntityInterceptor);
                break;
            default:
                throw new InvalidOperationException($"Database provider not supported. Attempting to find section named: {CensusConstants.AppSettings.DatabaseProvider}");
        }
    });

    // Add services
    builder.Services.AddHttpClient();
    builder.Services.AddControllers().AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull;
        options.JsonSerializerOptions.PropertyNameCaseInsensitive = true;
        options.JsonSerializerOptions.ForFhir();
    });
    builder.Services.AddGrpcReflection();

    // Add Kafka consumers and producers
    builder.Services.AddTransient<IKafkaConsumerFactory<string, string>, KafkaConsumerFactory<string, string>>();
    builder.Services.AddTransient<IKafkaConsumerFactory<string, PatientListMessage>, KafkaConsumerFactory<string, PatientListMessage>>();
    builder.Services.AddTransient<IKafkaProducerFactory<string, string>, KafkaProducerFactory<string, string>>();
    builder.Services.AddTransient<IKafkaProducerFactory<string, PatientListMessage>, KafkaProducerFactory<string, PatientListMessage>>();
    builder.Services.AddTransient<IKafkaProducerFactory<string, object>, KafkaProducerFactory<string, object>>();
    builder.Services.AddTransient<IKafkaProducerFactory<string, AuditEventMessage>, KafkaProducerFactory<string, AuditEventMessage>>();
    builder.Services.AddTransient<IKafkaProducerFactory<string, LantanaGroup.Link.Census.Application.Models.Messages.PatientEvent>, KafkaProducerFactory<string, LantanaGroup.Link.Census.Application.Models.Messages.PatientEvent>>();

    var kafkaConnection = builder.Configuration.GetSection(KafkaConstants.SectionName).Get<KafkaConnection>();
    builder.Services.RegisterKafkaProducer<string, object>(kafkaConnection, new ProducerConfig());
    builder.Services.RegisterKafkaProducer<string, Null>(kafkaConnection, new ProducerConfig());
    builder.Services.RegisterKafkaProducer<string, LantanaGroup.Link.Census.Application.Models.Messages.PatientEvent>(kafkaConnection, new ProducerConfig());


    // Add factories
    builder.Services.AddTransient<IRetryModelFactory, RetryModelFactory>();

    // Add repositories
    builder.Services.AddTransient<IBaseEntityRepository<CensusConfigEntity>, CensusEntityRepository<CensusConfigEntity>>();
    builder.Services.AddTransient<IBaseEntityRepository<PatientEvent>, CensusEntityRepository<PatientEvent>>();
    builder.Services.AddTransient<IBaseEntityRepository<PatientEncounter>, CensusEntityRepository<PatientEncounter>>();

    // Add managers
    builder.Services.AddTransient<ICensusConfigManager, CensusConfigManager>();
    builder.Services.AddTransient<IPatientEventManager, PatientEventManager>();
    builder.Services.AddTransient<IPatientEventQueries, PatientEventQueries>();
    builder.Services.AddTransient<IPatientEncounterQueries, PatientEncounterQueries>();
    builder.Services.AddTransient<IPatientEncounterManager, PatientEncounterManager>();


    //Services
    builder.Services.AddScoped<IPatientListService, PatientListService>();
    builder.Services.AddTransient<IEventProducerService<LantanaGroup.Link.Census.Application.Models.Messages.PatientEvent>, EventProducerService<LantanaGroup.Link.Census.Application.Models.Messages.PatientEvent>>();
    builder.Services.AddTransient<ITenantApiService, TenantApiService>();

    // Add exception handlers
    builder.Services.AddTransient<IDeadLetterExceptionHandler<string, string>, DeadLetterExceptionHandler<string, string>>();
    builder.Services.AddTransient<IDeadLetterExceptionHandler<string, PatientListMessage>, DeadLetterExceptionHandler<string, PatientListMessage>>();
    builder.Services.AddTransient<ITransientExceptionHandler<string, string>, TransientExceptionHandler<string, string>>();
    builder.Services.AddTransient<ITransientExceptionHandler<string, PatientListMessage>, TransientExceptionHandler<string, PatientListMessage>>();

    // Quartz
    var quartzProps = new NameValueCollection
    {
        ["quartz.scheduler.instanceName"] = "CensusScheduler",
        ["quartz.scheduler.instanceId"] = "AUTO",
        ["quartz.jobStore.clustered"] = "true",
        ["quartz.jobStore.type"] = "Quartz.Impl.AdoJobStore.JobStoreTX, Quartz",
        ["quartz.jobStore.driverDelegateType"] = "Quartz.Impl.AdoJobStore.SqlServerDelegate, Quartz",
        ["quartz.jobStore.tablePrefix"] = "quartz.QRTZ_",
        ["quartz.jobStore.dataSource"] = "default",
        ["quartz.dataSource.default.connectionString"] = builder.Configuration.GetConnectionString(ConfigurationConstants.DatabaseConnections.DatabaseConnection),
        ["quartz.dataSource.default.provider"] = "SqlServer",
        ["quartz.threadPool.type"] = "Quartz.Simpl.SimpleThreadPool, Quartz",
        ["quartz.threadPool.threadCount"] = "5",
        ["quartz.jobStore.useProperties"] = "false",
        ["quartz.serializer.type"] = "json"
    };

    builder.Services.AddSingleton<ISchedulerFactory>(new StdSchedulerFactory(quartzProps));
    builder.Services.AddKeyedSingleton(ConfigurationConstants.RunTimeConstants.RetrySchedulerKeyedSingleton, (provider, key) => provider.GetRequiredService<ISchedulerFactory>());

    // Add Quartz schedulers
    builder.Services.AddQuartz(q =>
    {

        q.UseMicrosoftDependencyInjectionJobFactory();
    });
	
    builder.Services.AddSingleton<IJobFactory, JobFactory>();
    builder.Services.AddTransient<SchedulePatientListRetrieval>();
    builder.Services.AddTransient<RetryJob>();

    builder.Services.AddSingleton<ISchedulerFactory>(new StdSchedulerFactory(quartzProps));
    builder.Services.AddKeyedSingleton(ConfigurationConstants.RunTimeConstants.RetrySchedulerKeyedSingleton, (provider, key) => provider.GetRequiredService<ISchedulerFactory>());

    // Add hosted services
    builder.Services.AddScoped<ICensusSchedulingRepository, CensusSchedulingRepository>();
    if (consumerSettings == null || !consumerSettings.DisableConsumer)
    {
        builder.Services.AddHostedService<PatientListsAcquiredListener>();
    }
    if (consumerSettings == null || !consumerSettings.DisableRetryConsumer)
    {
        builder.Services.AddHostedService<ScheduleService>();
        builder.Services.AddSingleton(new RetryListenerSettings(CensusConstants.ServiceName, [KafkaTopic.PatientListsAcquiredRetry.GetStringValue()]));
        builder.Services.AddHostedService<RetryListener>();
    }

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

    // Add health checks
    var kafkaHealthOptions = new KafkaHealthCheckConfiguration(kafkaConnection, CensusConstants.ServiceName).GetHealthCheckOptions();
    builder.Services.AddHealthChecks()
        .AddCheck<DatabaseHealthCheck>(HealthCheckType.Database.ToString())
        .AddKafka(kafkaHealthOptions, HealthCheckType.Kafka.ToString());

    // Add Swagger
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

    // Add problem details
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


    builder.Services.AddHostedService<PatientListsAcquiredListener>();


    if (consumerSettings == null || !consumerSettings.DisableRetryConsumer)
    {
        builder.Services.AddHostedService<ScheduleService>();
        builder.Services.AddSingleton(new RetryListenerSettings(CensusConstants.ServiceName, [KafkaTopic.PatientListsAcquiredRetry.GetStringValue()]));
        builder.Services.AddHostedService<RetryListener>();
    }

    //Add CORS
    builder.Services.AddLinkCorsService(options => {
        options.Environment = builder.Environment;
    });

    // Add telemetry
    builder.Services.AddLinkTelemetry(builder.Configuration, options =>
    {
        options.Environment = builder.Environment;
        options.ServiceName = CensusConstants.ServiceName;
        options.ServiceVersion = serviceInformation.Version;
    });

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