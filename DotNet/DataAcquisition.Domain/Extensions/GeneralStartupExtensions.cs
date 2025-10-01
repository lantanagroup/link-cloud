using Azure.Identity;
using FluentValidation;
using HealthChecks.UI.Client;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Interfaces;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Managers;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Kafka;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Queries;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Services;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Services.Auth;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Services.FhirApi;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Services.FhirApi.Commands;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Validators;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Context;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Entities;
using LantanaGroup.Link.DataAcquisition.Domain.Services;
using LantanaGroup.Link.DataAcquisition.Domain.Services.Auth;
using LantanaGroup.Link.DataAcquisition.Domain.Settings;
using LantanaGroup.Link.Shared.Application.Error.Handlers;
using LantanaGroup.Link.Shared.Application.Error.Interfaces;
using LantanaGroup.Link.Shared.Application.Extensions;
using LantanaGroup.Link.Shared.Application.Extensions.Caching;
using LantanaGroup.Link.Shared.Application.Extensions.Security;
using LantanaGroup.Link.Shared.Application.Factories;
using LantanaGroup.Link.Shared.Application.Health;
using LantanaGroup.Link.Shared.Application.Interfaces;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using LantanaGroup.Link.Shared.Application.Models.Kafka;
using LantanaGroup.Link.Shared.Application.Services;
using LantanaGroup.Link.Shared.Domain.Repositories.Implementations;
using LantanaGroup.Link.Shared.Domain.Repositories.Interceptors;
using LantanaGroup.Link.Shared.Domain.Repositories.Interfaces;
using LantanaGroup.Link.Shared.Settings;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.AzureAppConfiguration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.OpenApi.Models;
using Serilog;
using Serilog.Enrichers.Span;
using Serilog.Settings.Configuration;
using System.Diagnostics;
using System.Net;
using System.Reflection;

namespace LantanaGroup.Link.DataAcquisition.Domain.Extensions;

public static class GeneralStartupExtensions
{
    #region Configuration and Monitoring Registration

    /// <summary>
    /// Registers all common services, with optional parameters for customization.
    /// </summary>
    /// <param name="builder">The WebApplicationBuilder.</param>
    /// <param name="serviceName">The name of the service.</param>
    /// <param name="configureRedis">Whether to configure Redis caching.</param>
    /// <param name="addExtraItems">Optional list of functions to add extra registrations.</param>
    public static void RegisterAll(
        this WebApplicationBuilder builder,
        string serviceName,
        bool? configureRedis = false)
    {
        builder.Configuration.RegisterAzureConfigService(builder.Environment, serviceName);
        builder.Configuration.RegisterMonitoring(builder.Logging, builder.Services);
        builder.Services.RegisterConfigs(builder.Configuration);
        builder.RegisterEntityFramework();

        if (configureRedis.GetValueOrDefault())
        {
            builder.RegisterRedis();
        }

        builder.Services.RegisterInMemoryCache();
        builder.Services.RegisterHittpClient();
        builder.Services.RegisterFhirAuthHandlers();
        builder.Services.RegisterExceptionHandlers();
        builder.Services.RegisterRepositories();
        builder.Services.RegisterManagers();
        builder.Services.RegisterServices();
        builder.Services.RegisterFactories(builder.Configuration);
        builder.Services.RegisterTelemetry(builder.Configuration, builder.Environment, serviceName);
        builder.Services.RegisterProblemDetails((Microsoft.Extensions.Hosting.IHostingEnvironment)builder.Environment);
    }

    /// <summary>
    /// Registers Azure App Configuration if specified.
    /// </summary>
    public static void RegisterAzureConfigService(this IConfigurationManager configuration, IWebHostEnvironment environment, string serviceName)
    {
        //load external configuration source if specified
        var externalConfigurationSource = configuration.GetSection(DataAcquisitionConstants.AppSettingsSectionNames.ExternalConfigurationSource).Get<string>();

        if (!string.IsNullOrEmpty(externalConfigurationSource))
        {
            switch (externalConfigurationSource)
            {
                case ("AzureAppConfiguration"):
                    configuration.AddAzureAppConfiguration(options =>
                    {
                        options.Connect(configuration.GetConnectionString("AzureAppConfiguration"))
                                // Load configuration values with no label
                                .Select("*", LabelFilter.Null)
                                // Load configuration values for service name
                                .Select("*", serviceName)
                                // Load configuration values for service name and environment
                                .Select("*", serviceName + ":" + environment.EnvironmentName);

                        options.ConfigureKeyVault(kv =>
                        {
                            kv.SetCredential(new DefaultAzureCredential());
                        });

                    });
                    break;
            }
        }
    }

    /// <summary>
    /// Registers monitoring and logging with Serilog.
    /// </summary>
    public static void RegisterMonitoring(this IConfigurationManager configuration, ILoggingBuilder logging, IServiceCollection services)
    {
        // Logging using Serilog
        logging.AddSerilog();
        var loggerOptions = new ConfigurationReaderOptions { SectionName = DataAcquisitionConstants.AppSettingsSectionNames.Serilog };
        Log.Logger = new LoggerConfiguration()
                        .ReadFrom.Configuration(configuration, loggerOptions)
                        .Filter.ByExcluding("RequestPath like '/health%'")
                        //.Enrich.WithExceptionDetails()
                        .Enrich.FromLogContext()
                        .Enrich.WithSpan()
                        .Enrich.With<ActivityEnricher>()
                        .CreateLogger();

        var serviceInformation = configuration.GetSection(DataAcquisitionConstants.AppSettingsSectionNames.ServiceInformation).Get<ServiceInformation>();
        services.Configure<ServiceInformation>(configuration.GetSection(DataAcquisitionConstants.AppSettingsSectionNames.ServiceInformation));

        if (serviceInformation != null)
        {
            ServiceActivitySource.Initialize(serviceInformation);
        }
        else
        {
            throw new NullReferenceException("Service Information was null.");
        }
    }

    /// <summary>
    /// Registers application configurations.
    /// </summary>
    public static void RegisterConfigs(this IServiceCollection services, IConfigurationManager configuration)
    {
        //configs
        services.Configure<ServiceRegistry>(configuration.GetSection(ServiceRegistry.ConfigSectionName));
        services.AddSingleton<KafkaConnection>(configuration.GetRequiredSection(KafkaConstants.SectionName).Get<KafkaConnection>());
        services.Configure<ConsumerSettings>(configuration.GetRequiredSection(nameof(ConsumerSettings)));
        services.Configure<CorsSettings>(configuration.GetSection(ConfigurationConstants.AppSettings.CORS));
        services.Configure<LinkTokenServiceSettings>(configuration.GetSection(ConfigurationConstants.AppSettings.LinkTokenService));

        IConfigurationSection consumerSettingsSection = configuration.GetRequiredSection(nameof(ConsumerSettings));
        services.Configure<ConsumerSettings>(consumerSettingsSection);
        var consumerSettings = consumerSettingsSection.Get<ConsumerSettings>();
    }

    #endregion

    #region Database and Caching Registration

    /// <summary>
    /// Registers Entity Framework and DbContext.
    /// </summary>
    public static void RegisterEntityFramework(this WebApplicationBuilder builder)
    {
        //Add DbContext
        builder.Services.AddTransient<UpdateBaseEntityInterceptor>();
        builder.AddSQLServerEF_DataAcq();
        builder.RegisterDbContext();  // Call the new extracted method
    }

    /// <summary>
    /// Registers the DbContext with provider-specific configuration.
    /// </summary>
    public static void RegisterDbContext(this WebApplicationBuilder builder)
    {
        builder.Services.AddDbContext<DataAcquisitionDbContext>((sp, options) =>
        {
            var updateBaseEntityInterceptor = sp.GetRequiredService<UpdateBaseEntityInterceptor>();
            var dbProvider = builder.Configuration.GetValue<string>(DataAcquisitionConstants.AppSettingsSectionNames.DatabaseProvider);

            switch (dbProvider)
            {
                case ConfigurationConstants.AppSettings.SqlServerDatabaseProvider:
                    string? connectionString = builder.Configuration.GetConnectionString(ConfigurationConstants.DatabaseConnections.DatabaseConnection);
                    if (string.IsNullOrEmpty(connectionString))
                        throw new InvalidOperationException("Database connection string is null or empty.");
                    options.UseSqlServer(connectionString).AddInterceptors(updateBaseEntityInterceptor);
                    break;
                default:
                    throw new InvalidOperationException("Database provider not supported.");
            }
        });

        builder.Services.AddScoped<DbContext, DataAcquisitionDbContext>();
    }

    /// <summary>
    /// Registers Redis caching if enabled.
    /// </summary>
    public static void RegisterRedis(this WebApplicationBuilder builder)
    {
        DistributedLockSettingsExtensions.DistributedLockBuildAndAddToDI(builder.Services, builder.Configuration, ConfigurationConstants.DatabaseConnections.RedisConnection);
    }

    /// <summary>
    /// Registers in-memory caching.
    /// </summary>
    public static void RegisterInMemoryCache(this IServiceCollection services)
    {
        //in-memory cache
        services.AddMemoryCache();
        services.AddSingleton<ICacheService, InMemoryCacheService>();
    }

    #endregion

    #region HTTP and Authentication Registration

    /// <summary>
    /// Registers HTTP client factories.
    /// </summary>
    public static void RegisterHittpClient(this IServiceCollection services)
    {
        services.AddHttpClient("FhirHttpClient")
            .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler()
            {
                // FhirClient configures its internal HttpClient this way
                AutomaticDecompression = DecompressionMethods.Deflate | DecompressionMethods.GZip
            });
    }

    /// <summary>
    /// Registers FHIR authentication handlers.
    /// </summary>
    public static void RegisterFhirAuthHandlers(this IServiceCollection services)
    {
        //Fhir Authentication Handlers
        services.AddSingleton<EpicAuth>();
        services.AddSingleton<BasicAuth>();
        services.AddSingleton<IAuthenticationRetrievalService, AuthenticationRetrievalService>();
    }

    #endregion

    #region Exception Handling and Repositories

    /// <summary>
    /// Registers exception handlers.
    /// </summary>
    public static void RegisterExceptionHandlers(this IServiceCollection services)
    {
        services.AddSingleton<IDeadLetterExceptionHandler<string, string>, DeadLetterExceptionHandler<string, string>>();
        services.AddSingleton<IDeadLetterExceptionHandler<string, DataAcquisitionRequested>, DeadLetterExceptionHandler<string, DataAcquisitionRequested>>();
        services.AddSingleton<IDeadLetterExceptionHandler<string, PatientCensusScheduled>, DeadLetterExceptionHandler<string, PatientCensusScheduled>>();
        services.AddSingleton<IDeadLetterExceptionHandler<long, ReadyToAcquire>, DeadLetterExceptionHandler<long, ReadyToAcquire>>();
        services.AddSingleton<ITransientExceptionHandler<string, string>, TransientExceptionHandler<string, string>>();
        services.AddSingleton<ITransientExceptionHandler<string, DataAcquisitionRequested>, TransientExceptionHandler<string, DataAcquisitionRequested>>();
        services.AddSingleton<ITransientExceptionHandler<string, PatientCensusScheduled>, TransientExceptionHandler<string, PatientCensusScheduled>>();
        services.AddSingleton<ITransientExceptionHandler<long, ReadyToAcquire>, TransientExceptionHandler<long, ReadyToAcquire>>();
    }

    /// <summary>
    /// Registers repositories.
    /// </summary>
    public static void RegisterRepositories(this IServiceCollection services)
    {
        //Repositories
        services.AddTransient<IEntityRepository<FhirListConfiguration>, EntityRepository<FhirListConfiguration, DataAcquisitionDbContext>>();
        services.AddTransient<IEntityRepository<FhirQueryConfiguration>, EntityRepository<FhirQueryConfiguration, DataAcquisitionDbContext>>();
        services.AddTransient<IEntityRepository<QueryPlan>, EntityRepository<QueryPlan, DataAcquisitionDbContext>>();
        services.AddTransient<IEntityRepository<ReferenceResources>, EntityRepository<ReferenceResources, DataAcquisitionDbContext>>();
        services.AddTransient<IEntityRepository<FhirQuery>, EntityRepository<FhirQuery, DataAcquisitionDbContext>>();
        services.AddTransient<IEntityRepository<DataAcquisitionLog>, EntityRepository<DataAcquisitionLog, DataAcquisitionDbContext>>();
        services.AddScoped<IBaseEntityRepository<RetryEntity>, BaseEntityRepository<RetryEntity>>();

        //Database
        services.AddTransient<IDatabase, Database>();
    }

    #endregion

    #region Managers and Services

    /// <summary>
    /// Registers managers.
    /// </summary>
    public static void RegisterManagers(this IServiceCollection services)
    {
        //Queries
        services.AddTransient<IDataAcquisitionLogQueries, DataAcquisitionLogQueries>();

        //Managers
        services.AddTransient<IFhirQueryConfigurationManager, FhirQueryConfigurationManager>();
        services.AddTransient<IFhirQueryListConfigurationManager, FhirQueryListConfigurationManager>();
        services.AddTransient<IQueryPlanManager, QueryPlanManager>();
        services.AddTransient<IReferenceResourcesManager, ReferenceResourcesManager>();
        services.AddTransient<IFhirQueryManager, FhirQueryManager>();
        services.AddTransient<IDataAcquisitionLogManager, DataAcquisitionLogManager>();
    }

    /// <summary>
    /// Registers services.
    /// </summary>
    public static void RegisterServices(this IServiceCollection services)
    {
        //Services
        services.AddTransient<ITenantApiService, TenantApiService>();
        services.AddTransient<IValidateFacilityConnectionService, ValidateFacilityConnectionService>();
        services.AddTransient<IFhirApiService, FhirApiService>();
        services.AddTransient<IPatientDataService, PatientDataService>();
        services.AddTransient<IPatientCensusService, PatientCensusService>();
        services.AddTransient<IReferenceResourceService, ReferenceResourceService>();
        services.AddTransient<IQueryListProcessor, QueryListProcessor>();
        services.AddTransient<IBundleEventService<string, ResourceAcquired, ResourceAcquiredMessageGenerationRequest>, BundleResourceAcquiredEventService>();
        services.AddTransient<IDataAcquisitionLogService, DataAcquisitionLogService>();

        //Data Pull Commands
        services.AddTransient<IReadFhirCommand, ReadFhirCommand>();
        services.AddTransient<ISearchFhirCommand, SearchFhirCommand>();
    }

    #endregion

    #region Factories and Validation

    /// <summary>
    /// Registers factories for consumers, producers, and validation.
    /// </summary>
    public static void RegisterFactories(this IServiceCollection services, IConfigurationManager configuration)
    {
        //Factories - Consumer
        services.AddTransient<IKafkaConsumerFactory<string, string>, KafkaConsumerFactory<string, string>>();
        services.AddTransient<IKafkaConsumerFactory<string, DataAcquisitionRequested>, KafkaConsumerFactory<string, DataAcquisitionRequested>>();
        services.AddTransient<IKafkaConsumerFactory<string, PatientCensusScheduled>, KafkaConsumerFactory<string, PatientCensusScheduled>>();
        services.AddTransient<IKafkaConsumerFactory<long, ReadyToAcquire>, KafkaConsumerFactory<long, ReadyToAcquire>>();

        //Validation
        services.AddValidatorsFromAssemblyContaining<UpdateDataAcquisitionLogModelValidator>();

        //Factories - Producer
        var kafkaConnection = configuration.GetRequiredSection(KafkaConstants.SectionName).Get<KafkaConnection>() ?? throw new Exception("Missing Kafka Connection Settings");
        var producerConfig = new Confluent.Kafka.ProducerConfig { CompressionType = Confluent.Kafka.CompressionType.Zstd };
        services.RegisterKafkaProducer<string, object>(kafkaConnection, producerConfig);
        services.RegisterKafkaProducer<string, string>(kafkaConnection, producerConfig);
        services.RegisterKafkaProducer<string, DataAcquisitionRequested>(kafkaConnection, producerConfig);
        services.RegisterKafkaProducer<string, PatientCensusScheduled>(kafkaConnection, producerConfig);
        services.RegisterKafkaProducer<string, ResourceAcquired>(kafkaConnection, producerConfig);
        services.RegisterKafkaProducer<string, PatientIDsAcquired>(kafkaConnection, producerConfig);
        services.RegisterKafkaProducer<string, AuditEventMessage>(kafkaConnection, producerConfig);
        services.RegisterKafkaProducer<long, ReadyToAcquire>(kafkaConnection, producerConfig);

        services.AddTransient<IKafkaProducerFactory<string, AuditEventMessage>, KafkaProducerFactory<string, AuditEventMessage>>();
        services.AddTransient<IKafkaProducerFactory<string, object>, KafkaProducerFactory<string, object>>();
        services.AddTransient<IKafkaProducerFactory<string, string>, KafkaProducerFactory<string, string>>();
        services.AddTransient<IKafkaProducerFactory<string, DataAcquisitionRequested>, KafkaProducerFactory<string, DataAcquisitionRequested>>();
        services.AddTransient<IKafkaProducerFactory<string, PatientCensusScheduled>, KafkaProducerFactory<string, PatientCensusScheduled>>();
        services.AddTransient<IKafkaProducerFactory<string, ResourceAcquired>, KafkaProducerFactory<string, ResourceAcquired>>();
        services.AddTransient<IKafkaProducerFactory<string, PatientIDsAcquired>, KafkaProducerFactory<string, PatientIDsAcquired>>();
        services.AddTransient<IKafkaProducerFactory<long, ReadyToAcquire>, KafkaProducerFactory<long, ReadyToAcquire>>();
    }

    #endregion

    #region Telemetry and Problem Details

    /// <summary>
    /// Registers telemetry services.
    /// </summary>
    public static void RegisterTelemetry(this IServiceCollection services, IConfigurationManager configuration, IWebHostEnvironment environment, string serviceName)
    {
        var serviceInformation = configuration.GetSection(DataAcquisitionConstants.AppSettingsSectionNames.ServiceInformation).Get<ServiceInformation>();
        //Add telemetry if enabled
        services.AddLinkTelemetry(configuration, options =>
        {
            options.Environment = environment;
            options.ServiceName = serviceName;
            options.ServiceVersion = serviceInformation.Version; //TODO: Get version from assembly?                
        });
    }

    /// <summary>
    /// Registers problem details handling.
    /// </summary>
    public static void RegisterProblemDetails(this IServiceCollection services, Microsoft.Extensions.Hosting.IHostingEnvironment environment)
    {
        services.AddProblemDetails(options => {
            options.CustomizeProblemDetails = ctx =>
            {
                if (string.IsNullOrEmpty(ctx.ProblemDetails.Detail))
                    ctx.ProblemDetails.Detail = "An error occurred in our API. Please use the trace id when requesting assistance.";

                if (!ctx.ProblemDetails.Extensions.ContainsKey("traceId"))
                {
                    string? traceId = Activity.Current?.Id ?? ctx.HttpContext.TraceIdentifier;
                    ctx.ProblemDetails.Extensions.Add(new KeyValuePair<string, object?>("traceId", traceId));
                }
                if (environment.IsDevelopment())
                {
                    ctx.ProblemDetails.Extensions.Add("service", "Data Acquisition");
                }
                else
                {
                    ctx.ProblemDetails.Extensions.Remove("exception");
                }
            };
        });
    }

    #endregion

    #region Health Checks and CORS

    /// <summary>
    /// Registers health checks for database and Kafka.
    /// </summary>
    public static void RegisterHealthChecks(this WebApplicationBuilder builder)
    {
        var kafkaConnection = builder.Configuration.GetRequiredSection(KafkaConstants.SectionName).Get<KafkaConnection>();
        var kafkaHealthOptions = new KafkaHealthCheckConfiguration(kafkaConnection, DataAcquisitionConstants.ServiceName).GetHealthCheckOptions();

        builder.Services.AddHealthChecks()
            .AddDbContextCheck<DataAcquisitionDbContext>(HealthCheckType.Database.ToString())
            .AddKafka(kafkaHealthOptions, HealthCheckType.Kafka.ToString());
    }

    /// <summary>
    /// Registers CORS policies.
    /// </summary>
    public static void RegisterCors(this WebApplicationBuilder builder)
    {
        builder.Services.AddLinkCorsService(options => { options.Environment = builder.Environment; });
    }

    #endregion

    #region Swagger and Middleware

    /// <summary>
    /// Registers Swagger with optional authentication support.
    /// </summary>
    public static void RegisterSwagger(this WebApplicationBuilder builder, bool includeAuth = false)
    {
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen(c =>
        {
            var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
            var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
            c.IncludeXmlComments(xmlPath);
            c.DocumentFilter<HealthChecksFilter>();

            if (includeAuth)
            {
                var allowAnonymousAccess = builder.Configuration.GetValue<bool>("Authentication:EnableAnonymousAccess");
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
            }
        });
    }

    /// <summary>
    /// Configures common middleware for the application.
    /// </summary>
    public static void ConfigureCommonMiddleware(this WebApplication app, string serviceName, bool autoMigrateDb = true)
    {
        app.ConfigureSwagger();

        if (autoMigrateDb)
            app.AutoMigrateEF<DataAcquisitionDbContext>();

        if (app.Environment.IsDevelopment())
        {
            app.UseDeveloperExceptionPage();
        }
        else
        {
            app.UseExceptionHandler();
        }

        app.UseRouting();
        app.UseCors(CorsSettings.DefaultCorsPolicyName);

        app.MapControllers();
        app.MapHealthChecks("/health", new HealthCheckOptions
        {
            ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse,
        });
        app.MapInfo(Assembly.GetExecutingAssembly(), app.Configuration, serviceName);
    }

    #endregion

    #region Hosted Services

    /// <summary>
    /// Registers hosted services with project-specific customization.
    /// </summary>
    public static void RegisterHostedServices(this WebApplicationBuilder builder, Action<IServiceCollection, ConsumerSettings> addProjectSpecificServices)
    {
        var consumerSettings = builder.Configuration.GetRequiredSection(nameof(ConsumerSettings)).Get<ConsumerSettings>();
        addProjectSpecificServices(builder.Services, consumerSettings);
    }

    #endregion
}