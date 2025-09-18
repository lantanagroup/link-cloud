using Azure.Identity;
using Confluent.Kafka;
using HealthChecks.UI.Client;
using LantanaGroup.Link.Census.Application.HealthChecks;
using LantanaGroup.Link.Census.Application.Interfaces;
using LantanaGroup.Link.Census.Application.Jobs;
using LantanaGroup.Link.Census.Application.Repositories;
using LantanaGroup.Link.Census.Application.Repositories.Scheduling;
using LantanaGroup.Link.Census.Application.Services;
using LantanaGroup.Link.Census.Application.Settings;
using LantanaGroup.Link.Census.Domain.Context;
using LantanaGroup.Link.Census.Listeners;
using LantanaGroup.Link.Shared.Application.Error.Handlers;
using LantanaGroup.Link.Shared.Application.Error.Interfaces;
using LantanaGroup.Link.Shared.Application.Extensions;
using LantanaGroup.Link.Shared.Application.Extensions.Security;
using LantanaGroup.Link.Shared.Application.Factories;
using LantanaGroup.Link.Shared.Application.Interfaces;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using LantanaGroup.Link.Shared.Application.Models.Kafka;
using LantanaGroup.Link.Shared.Application.Services;
using LantanaGroup.Link.Shared.Settings;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration.AzureAppConfiguration;
using Quartz;
using Quartz.Impl;
using Quartz.Spi;
using Serilog;
using Serilog.Enrichers.Span;
using Serilog.Exceptions;
using System.Diagnostics;
using System.Reflection;
using Census.Domain.Entities;
using Hl7.Fhir.Serialization;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Middleware;
using Microsoft.OpenApi.Models;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using LantanaGroup.Link.Census.Application.Models.Messages;
using LantanaGroup.Link.Census.Domain.Entities;
using LantanaGroup.Link.Census.Domain.Managers;
using LantanaGroup.Link.Shared.Application.Utilities;
using LantanaGroup.Link.Shared.Application.Listeners;
using LantanaGroup.Link.Shared.Application.Health;
using LantanaGroup.Link.Shared.Domain.Repositories.Interfaces;
using LantanaGroup.Link.Shared.Domain.Repositories.Interceptors;

var builder = WebApplication.CreateBuilder(args);

RegisterServices(builder);
var app = builder.Build();
SetupMiddleware(app);

app.Run();


static void RegisterServices(WebApplicationBuilder builder)
{
    //load external configuration source if specified
    var externalConfigurationSource = builder.Configuration.GetSection(CensusConstants.AppSettings.ExternalConfigurationSource).Get<string>();

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
                         .Select("*", CensusConstants.ServiceName)
                         // Load configuration values for service name and environment
                         .Select("*", CensusConstants.ServiceName + ":" + builder.Environment.EnvironmentName);

                    options.ConfigureKeyVault(kv =>
                    {
                        kv.SetCredential(new DefaultAzureCredential());
                    });

                });
                break;
        }
    }

    var serviceInformation = builder.Configuration.GetRequiredSection(CensusConstants.AppSettings.ServiceInformation).Get<ServiceInformation>();
    if (serviceInformation != null)
    {
        ServiceActivitySource.Initialize(serviceInformation);
    }
    else
    {
        throw new NullReferenceException("Service Information was null.");
    }

    builder.Services.Configure<ServiceRegistry>(builder.Configuration.GetSection(ServiceRegistry.ConfigSectionName));
    builder.Services.AddSingleton(builder.Configuration.GetSection(KafkaConstants.SectionName).Get<KafkaConnection>());
    builder.Services.Configure<CorsSettings>(builder.Configuration.GetSection(ConfigurationConstants.AppSettings.CORS));
    builder.Services.Configure<LinkTokenServiceSettings>(builder.Configuration.GetSection(ConfigurationConstants.AppSettings.LinkTokenService));

    IConfigurationSection consumerSettingsSection = builder.Configuration.GetSection(nameof(ConsumerSettings));
    builder.Services.Configure<ConsumerSettings>(consumerSettingsSection);
    var consumerSettings = consumerSettingsSection.Get<ConsumerSettings>();

    builder.Services.AddTransient<UpdateBaseEntityInterceptor>();

    builder.Services.AddDbContext<CensusContext>((sp, options) =>
    {

        var updateBaseEntityInterceptor = sp.GetService<UpdateBaseEntityInterceptor>()!;

        switch (builder.Configuration.GetValue<string>(CensusConstants.AppSettings.DatabaseProvider))
        {
            case ConfigurationConstants.AppSettings.SqlServerDatabaseProvider:
                string? connectionString =
                    builder.Configuration.GetConnectionString(ConfigurationConstants.DatabaseConnections
                        .DatabaseConnection);

                if (string.IsNullOrEmpty(connectionString))
                    throw new InvalidOperationException("Database connection string is null or empty.");

                options.UseSqlServer(connectionString)
                   .AddInterceptors(updateBaseEntityInterceptor);
                break;
            default:
                throw new InvalidOperationException($"Database provider not supported. Attempting to find section named: {CensusConstants.AppSettings.DatabaseProvider}");
        }
    });

    builder.Services.AddHttpClient();

    //Consumers
    builder.Services.AddTransient<IKafkaConsumerFactory<string, string>, KafkaConsumerFactory<string, string>>();
    builder.Services.AddTransient<IKafkaConsumerFactory<string, PatientIDsAcquired>, KafkaConsumerFactory<string, PatientIDsAcquired>>();

    //Producers
    builder.Services.AddTransient<IKafkaProducerFactory<string, string>, KafkaProducerFactory<string, string>>();
    builder.Services.AddTransient<IKafkaProducerFactory<string, PatientIDsAcquired>, KafkaProducerFactory<string, PatientIDsAcquired>>();
    builder.Services.AddTransient<IKafkaProducerFactory<string, object>, KafkaProducerFactory<string, object>>();
    builder.Services.AddTransient<IKafkaProducerFactory<string, AuditEventMessage>, KafkaProducerFactory<string, AuditEventMessage>>();

    var kafkaConnection = builder.Configuration.GetSection(KafkaConstants.SectionName).Get<KafkaConnection>();
    builder.Services.RegisterKafkaProducer<string, object>(kafkaConnection, new ProducerConfig());
    builder.Services.RegisterKafkaProducer<string, Null>(kafkaConnection, new ProducerConfig());

    //Factories
    builder.Services.AddTransient<IRetryEntityFactory, RetryEntityFactory>();

    //Repositories
    builder.Services.AddTransient<IBaseEntityRepository<CensusConfigEntity>, CensusEntityRepository<CensusConfigEntity>>();
    builder.Services.AddTransient<IBaseEntityRepository<CensusPatientListEntity>, CensusEntityRepository<CensusPatientListEntity>>();
    builder.Services.AddTransient<IBaseEntityRepository<PatientCensusHistoricEntity>, CensusEntityRepository<PatientCensusHistoricEntity>>();
    builder.Services.AddScoped<IBaseEntityRepository<RetryEntity>, CensusEntityRepository<RetryEntity>>();

    //Managers
    builder.Services.AddTransient<ICensusConfigManager, CensusConfigManager>();
    builder.Services.AddTransient<ICensusPatientListManager, CensusPatientListManager>();
    builder.Services.AddTransient<IPatientCensusHistoryManager, PatientCensusHistoryManager>();

    //Services
    builder.Services.AddScoped<IPatientIdsAcquiredService, PatientIdsAcquiredService>();

    //Handlers
    builder.Services.AddTransient<IDeadLetterExceptionHandler<string, string>, DeadLetterExceptionHandler<string, string>>();
    builder.Services.AddTransient<IDeadLetterExceptionHandler<string, PatientIDsAcquired>, DeadLetterExceptionHandler<string, PatientIDsAcquired>>();
    builder.Services.AddTransient<ITransientExceptionHandler<string, string>, TransientExceptionHandler<string, string>>();
    builder.Services.AddTransient<ITransientExceptionHandler<string, PatientIDsAcquired>, TransientExceptionHandler<string, PatientIDsAcquired>>();

    //Services
    builder.Services.AddTransient<ITenantApiService, TenantApiService>();

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

    //Add health checks
    var kafkaHealthOptions = new KafkaHealthCheckConfiguration(kafkaConnection, CensusConstants.ServiceName).GetHealthCheckOptions();

    builder.Services.AddHealthChecks()
        .AddCheck<DatabaseHealthCheck>(HealthCheckType.Database.ToString())
        .AddKafka(kafkaHealthOptions, HealthCheckType.Kafka.ToString());

    builder.Services.AddControllers().AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull;
        options.JsonSerializerOptions.PropertyNameCaseInsensitive = true;
        options.JsonSerializerOptions.ForFhir();
    });

    builder.Services.AddGrpcReflection();

    builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(Assembly.GetExecutingAssembly()));
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
        c.DocumentFilter<HealthChecksFilter>();
    });

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

    builder.Services.AddProblemDetails(options => {
        options.CustomizeProblemDetails = ctx =>
        {
            ctx.ProblemDetails.Detail = "An error occured in our API. Please use the trace id when requesting assistence.";
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

    builder.Services.AddScoped<ICensusSchedulingRepository, CensusSchedulingRepository>();
    builder.Services.AddTransient<IJobFactory, JobFactory>();
    builder.Services.AddTransient<ISchedulerFactory, StdSchedulerFactory>();
    builder.Services.AddTransient<SchedulePatientListRetrieval>();

    if (consumerSettings == null || !consumerSettings.DisableConsumer)
    {
        builder.Services.AddHostedService<CensusListener>();
    }

    if (consumerSettings == null || !consumerSettings.DisableRetryConsumer)
    {
        builder.Services.AddHostedService<ScheduleService>();
        builder.Services.AddSingleton(new RetryListenerSettings(CensusConstants.ServiceName, [KafkaTopic.PatientIDsAcquiredRetry.GetStringValue()]));
        builder.Services.AddHostedService<RetryListener>();
    }

    //Add CORS
    builder.Services.AddLinkCorsService(options => {
        options.Environment = builder.Environment;
    });

    //Add telemetry if enabled
    builder.Services.AddLinkTelemetry(builder.Configuration, options =>
    {
        options.Environment = builder.Environment;
        options.ServiceName = CensusConstants.ServiceName;
        options.ServiceVersion = serviceInformation.Version; //TODO: Get version from assembly?                
    });

    builder.Services.AddSingleton<ICensusServiceMetrics, CensusServiceMetrics>();

}

static void SetupMiddleware(WebApplication app)
{
    app.AutoMigrateEF<CensusContext>();

    app.ConfigureSwagger();

    app.UseCors(CorsSettings.DefaultCorsPolicyName);

    if (app.Environment.IsDevelopment() || app.Environment.EnvironmentName.Equals("Local", StringComparison.InvariantCultureIgnoreCase))
    {
        app.UseDeveloperExceptionPage();
    }
    else
    {
        app.UseExceptionHandler();
    }

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

    //map health check middleware
    app.MapHealthChecks("/health", new HealthCheckOptions
    {
        ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
    });
    app.MapInfo(Assembly.GetExecutingAssembly(), app.Configuration, "census");
}