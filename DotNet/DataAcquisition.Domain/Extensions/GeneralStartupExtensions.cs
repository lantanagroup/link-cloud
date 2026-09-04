using Confluent.Kafka;
using DataAcquisition.Domain.Application.Queries;
using FluentValidation;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Factories.ParameterFactories;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Factories.QueryFactories;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Interfaces;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Managers;
using LantanaGroup.Link.Shared.Application.Interfaces.Services;
using LantanaGroup.Link.Shared.Application.Services.SecretManager;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Kafka;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Queries;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Serializers;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Services;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Services.Auth;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Services.FhirApi;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Services.FhirApi.Commands;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Services.Sftp;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Services.Sftp.Parsers;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Services.Sftp.Processors;
using LantanaGroup.Link.Shared.Application.Models.Kafka;
using LantanaGroup.Link.Shared.Application.Models.Mapping;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Validators;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Context;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Entities;
using LantanaGroup.Link.DataAcquisition.Domain.Settings;
using LantanaGroup.Link.Shared.Application.Error.Handlers;
using LantanaGroup.Link.Shared.Application.Error.Interfaces;
using LantanaGroup.Link.Shared.Application.Extensions;
using LantanaGroup.Link.Shared.Application.Extensions.Caching;
using LantanaGroup.Link.Shared.Application.Extensions.ExternalServices;
using LantanaGroup.Link.Shared.Application.Extensions.Quartz;
using LantanaGroup.Link.Shared.Application.Factories;
using LantanaGroup.Link.Shared.Application.Interfaces;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using LantanaGroup.Link.Shared.Application.Services;
using LantanaGroup.Link.Shared.Domain.Repositories.Implementations;
using LantanaGroup.Link.Shared.Domain.Repositories.Interceptors;
using LantanaGroup.Link.Shared.Domain.Repositories.Interfaces;
using LantanaGroup.Link.Shared.Settings;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Enrichers.Span;
using Serilog.Settings.Configuration;
using Serilog.Sinks.SystemConsole.Themes;
using System.Diagnostics;
using System.Net;
using System.Reflection;
using IHostingEnvironment = Microsoft.Extensions.Hosting.IHostingEnvironment;
using LantanaGroup.Link.Shared.Application.Extensions;
using LantanaGroup.Link.Shared.Application.Services.ResourceCache;

namespace LantanaGroup.Link.DataAcquisition.Domain.Extensions;
public static class GeneralStartupExtensions
{
    // Shared (not per-service) Redis key prefix for ICacheService. The DataAcquisition service and the
    // AcquisitionWorker must use the SAME value so their keys resolve identically for cross-process
    // sharing; isolating it from other services on the same Redis instance is the goal.
    private const string CacheKeyPrefix = "DataAcquisition:";

    public static void RegisterAll(
        this WebApplicationBuilder builder,
        string serviceName,
        bool? configureRedis = false)
    {
        var assemblyVersion = Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? string.Empty;

        var serviceInformation = builder.SetupServiceInformation(serviceName, assemblyVersion);

        // load external configuration source (if specified)
        builder.AddExternalConfiguration(serviceInformation.ServiceConfigName);

        //Add Quartz scheduler with SQL persistence
        var connectionString = builder.Configuration.GetConnectionString(ConfigurationConstants.DatabaseConnections.DatabaseConnection);
        builder.Services.RegisterQuartzDatabase(connectionString);

        builder.RegisterMonitoring();
        builder.Services.RegisterConfigs(builder.Configuration);
        builder.RegisterEntityFramework();

        var useExistingRedisConnection = configureRedis.GetValueOrDefault();

        if (useExistingRedisConnection)
        {
            builder.RegisterRedis();
        }

        builder.Services.RegisterSecretManager(builder.Configuration);

        builder.RegisterCacheService();
        builder.Services.AddPipelineAbortRegistry(builder.Configuration);
        builder.Services.RegisterHittpClient();
        builder.Services.RegisterFhirAuthHandlers();
        builder.Services.RegisterExceptionHandlers();
        builder.Services.RegisterRepositories();
        builder.Services.RegisterManagers();
        builder.Services.RegisterServices();
        builder.Services.AddResourceCache(builder.Configuration, useExistingRedisConnection);
        builder.Services.RegisterFactories(builder.Configuration);
        builder.Services.RegisterTelemetry(builder.Configuration, builder.Environment, serviceInformation.ServiceConfigName);
        builder.Services.RegisterProblemDetails((IHostingEnvironment)builder.Environment);
    }

    public static void RegisterMonitoring(this WebApplicationBuilder builder)
    {
        // Clear default providers first to avoid duplicate logging
        builder.Logging.ClearProviders();

        var loggerOptions = new ConfigurationReaderOptions { SectionName = DataAcquisitionConstants.AppSettingsSectionNames.Serilog };
        var serilogConfig = new LoggerConfiguration()
            .ReadFrom.Configuration(builder.Configuration, loggerOptions)
            .Filter.ByExcluding("RequestPath like '/health%'")
            .Enrich.FromLogContext()
            .Enrich.WithSpan()
            .Enrich.With<ActivityEnricher>();

        Log.Logger = serilogConfig.CreateLogger();

        // Use Serilog as the logging provider
        builder.Logging.AddSerilog(Log.Logger, dispose: true);
    }

    public static void RegisterConfigs(this IServiceCollection services, IConfigurationManager configuration)
    {
        //configs
        services.Configure<ServiceRegistry>(configuration.GetSection(ServiceRegistry.ConfigSectionName));
        services.AddSingleton<KafkaConnection>(configuration.GetRequiredSection(KafkaConstants.SectionName).Get<KafkaConnection>());
        services.Configure<ConsumerSettings>(configuration.GetRequiredSection(nameof(ConsumerSettings)));
        services.Configure<CorsSettings>(configuration.GetSection(ConfigurationConstants.AppSettings.CORS));
        services.Configure<LinkTokenServiceSettings>(configuration.GetSection(ConfigurationConstants.AppSettings.LinkTokenService));
        services.Configure<ApiSettings>(configuration.GetSection(ConfigurationConstants.AppSettings.ApiSettings));
        services.Configure<SecretManagerSettings>(configuration.GetSection(ConfigurationConstants.AppSettings.SecretManagement));
        services.Configure<SftpValidationSettings>(configuration.GetSection(SftpValidationSettings.SectionName));
        services.Configure<SftpAcquisitionSettings>(configuration.GetSection(SftpAcquisitionSettings.SectionName));
        services.Configure<DataSourceAuthSettings>(configuration.GetSection(DataSourceAuthSettings.SectionName));
        services.Configure<FhirSearchSettings>(configuration.GetSection(FhirSearchSettings.SectionName));

        IConfigurationSection consumerSettingsSection = configuration.GetRequiredSection(nameof(ConsumerSettings));
        services.Configure<ConsumerSettings>(consumerSettingsSection);
        var consumerSettings = consumerSettingsSection.Get<ConsumerSettings>();
    }

    public static void RegisterEntityFramework(this WebApplicationBuilder builder)
    {
        //Add DbContext
        builder.Services.AddTransient<UpdateBaseEntityInterceptor>();

        builder.Services.AddDbContext<DataAcquisitionDbContext>((sp, options) =>
        {
            var updateBaseEntityInterceptor = sp.GetService<UpdateBaseEntityInterceptor>()!;

            switch (builder.Configuration.GetValue<string>(DataAcquisitionConstants.AppSettingsSectionNames.DatabaseProvider))
            {
                case ConfigurationConstants.AppSettings.SqlServerDatabaseProvider:
                    string? connectionString =
                        builder.Configuration.GetConnectionString(ConfigurationConstants.DatabaseConnections
                            .DatabaseConnection);

                    if (string.IsNullOrEmpty(connectionString))
                        throw new InvalidOperationException("Database connection string is null or empty.");

                    options.UseSqlServer(connectionString, sqlOptions =>
                        {
                            sqlOptions.EnableRetryOnFailure(
                                maxRetryCount: 5,
                                maxRetryDelay: TimeSpan.FromSeconds(10),
                                errorNumbersToAdd: null);
                        })
                       .AddInterceptors(updateBaseEntityInterceptor);

                    break;
                default:
                    throw new InvalidOperationException($"Database provider not supported. Attempting to find section named: {DataAcquisitionConstants.AppSettingsSectionNames.DatabaseProvider}");
            }
        });
    }

    public static void RegisterRedis(this WebApplicationBuilder builder)
    {
        DistributedLockSettingsExtensions.DistributedLockBuildAndAddToDI(builder.Services, builder.Configuration, ConfigurationConstants.DatabaseConnections.RedisConnection);
    }

    public static void RegisterInMemoryCache(this IServiceCollection services)
    {
        //in-memory cache
        services.AddMemoryCache();
        services.AddSingleton<ICacheService, InMemoryCacheService>();
    }

    /// <summary>
    /// Registers the <see cref="ICacheService"/> backend selected by the <c>Cache:Type</c> config key
    /// (defaults to <c>InMemory</c>). Set <c>Cache:Type=Redis</c> to back it with the shared Redis cache
    /// so that the DataAcquisition service and the AcquisitionWorker — separate processes — see each
    /// other's writes/evictions (e.g. the org-location conditions cache invalidated on a config change),
    /// instead of each keeping its own per-process copy until the entry's TTL expires.
    /// </summary>
    public static void RegisterCacheService(this WebApplicationBuilder builder)
    {
        var cacheType = builder.Configuration.GetValue<string>("Cache:Type") ?? "InMemory";

        if (!string.Equals(cacheType, "Redis", StringComparison.OrdinalIgnoreCase))
        {
            builder.Services.RegisterInMemoryCache();
            return;
        }

        builder.Services.AddRedisCache(options =>
        {
            options.Environment = builder.Environment;

            var redisConnection = builder.Configuration
                .GetConnectionString(ConfigurationConstants.DatabaseConnections.RedisConnection);
            if (string.IsNullOrEmpty(redisConnection))
                throw new InvalidOperationException(
                    $"ConnectionStrings:{ConfigurationConstants.DatabaseConnections.RedisConnection} is required when Cache:Type is Redis.");

            options.ConnectionString = redisConnection;
            options.Password = builder.Configuration.GetValue<string>("Redis:Password");

            // Namespace this service's ICacheService keys so they can't collide with other
            // services sharing the same Redis instance (notably the bare "{facilityId}" auth-token
            // key). This prefix is deliberately a single fixed value — NOT per-service — because the
            // DataAcquisition service and the AcquisitionWorker must resolve to the same Redis keys
            // for cross-process sharing (e.g. org-location conditions invalidation) to work.
            options.InstanceName = CacheKeyPrefix;
        });

        builder.Services.AddSingleton<ICacheService, RedisCacheService>();
    }

    public static void RegisterHittpClient(this IServiceCollection services)
    {
        services.AddHttpClient("FhirHttpClient")
            .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler()
            {
                // FhirClient configures its internal HttpClient this way
                AutomaticDecompression = DecompressionMethods.Deflate | DecompressionMethods.GZip
            });
    }

    public static void RegisterFhirAuthHandlers(this IServiceCollection services)
    {
        //Fhir Authentication Handlers
        services.AddSingleton<EpicAuth>();
        services.AddSingleton<BasicAuth>();
        services.AddSingleton<CustomHeaderAuth>();
        services.AddSingleton<OAuth>();
        services.AddSingleton<IAuthenticationRetrievalService, AuthenticationRetrievalService>();
    }

    public static void RegisterExceptionHandlers(this IServiceCollection services)
    {
        services.AddSingleton(typeof(IExceptionLogger<>), typeof(ExceptionLogger<>));
        services.AddSingleton(typeof(ITransientExceptionHandler<,,>), typeof(TransientExceptionHandler<,,>));
        services.AddSingleton(typeof(IDeadLetterExceptionHandler<,,>), typeof(DeadLetterExceptionHandler<,,>));
    }

    public static void RegisterRepositories(this IServiceCollection services)
    {
        //Repositories
        services.AddScoped<IEntityRepository<FhirListConfiguration>, EntityRepository<FhirListConfiguration, DataAcquisitionDbContext>>();
        services.AddScoped<IEntityRepository<FhirQueryConfiguration>, EntityRepository<FhirQueryConfiguration, DataAcquisitionDbContext>>();
        services.AddScoped<IEntityRepository<QueryPlan>, EntityRepository<QueryPlan, DataAcquisitionDbContext>>();
        services.AddScoped<IEntityRepository<ResourceReferenceType>, EntityRepository<ResourceReferenceType, DataAcquisitionDbContext>>();
        services.AddScoped<IEntityRepository<ReferenceResources>, EntityRepository<ReferenceResources, DataAcquisitionDbContext>>();
        services.AddScoped<IEntityRepository<FhirQuery>, EntityRepository<FhirQuery, DataAcquisitionDbContext>>();
        services.AddScoped<IEntityRepository<DataAcquisitionLog>, EntityRepository<DataAcquisitionLog, DataAcquisitionDbContext>>();
        services.AddScoped<IEntityRepository<FhirQueryResourceType>, EntityRepository<FhirQueryResourceType, DataAcquisitionDbContext>>();
        services.AddScoped<IEntityRepository<SftpAcquisitionLog>, EntityRepository<SftpAcquisitionLog, DataAcquisitionDbContext>>();
        services.AddScoped<IEntityRepository<SftpConfiguration>, EntityRepository<SftpConfiguration, DataAcquisitionDbContext>>();
        services.AddScoped<IEntityRepository<OrganizationLocationConfiguration>, EntityRepository<OrganizationLocationConfiguration, DataAcquisitionDbContext>>();
        services.AddScoped<IEntityRepository<OrganizationLocationCondition>, EntityRepository<OrganizationLocationCondition, DataAcquisitionDbContext>>();
        services.AddScoped<IEntityRepository<OrganizationLocationMapping>, EntityRepository<OrganizationLocationMapping, DataAcquisitionDbContext>>();
        services.AddScoped<IEntityRepository<EncounterMapping>, EntityRepository<EncounterMapping, DataAcquisitionDbContext>>();
        services.AddScoped<IEntityRepository<EncounterLocation>, EntityRepository<EncounterLocation, DataAcquisitionDbContext>>();

        //Database
        services.AddScoped<IDatabase, Database>();
    }

    public static void RegisterManagers(this IServiceCollection services)
    {
        //Queries
        services.AddScoped<IDataAcquisitionLogQueries, DataAcquisitionLogQueries>();
        services.AddScoped<IDataAcquisitionLogNotesQueries, DataAcquisitionLogNotesQueries>();
        services.AddScoped<ISftpAcquisitionLogQueries, SftpAcquisitionLogQueries>();
        services.AddScoped<IFhirQueryConfigurationQueries, FhirQueryConfigurationQueries>();
        services.AddScoped<IFhirQueryListConfigurationQueries, FhirQueryListConfigurationQueries>();
        services.AddScoped<IFhirQueryQueries, FhirQueryQueries>();
        services.AddScoped<IQueryPlanQueries, QueryPlanQueries>();
        services.AddScoped<IReferenceResourcesQueries, ReferenceResourcesQueries>();
        services.AddScoped<ISftpConfigurationQueries, SftpConfigurationQueries>();
        services.AddScoped<IOrganizationLocationConfigurationQueries, OrganizationLocationConfigurationQueries>();
        services.AddScoped<IOrganizationLocationMappingQueries, OrganizationLocationMappingQueries>();

        //Managers
        services.AddScoped<IFhirQueryConfigurationManager, FhirQueryConfigurationManager>();
        services.AddScoped<IFhirListQueryConfigurationManager, FhirListQueryConfigurationManager>();
        services.AddScoped<IQueryPlanManager, QueryPlanManager>();
        services.AddScoped<IReferenceResourcesManager, ReferenceResourcesManager>();
        services.AddScoped<IFhirQueryManager, FhirQueryManager>();
        services.AddScoped<IDataAcquisitionLogManager, DataAcquisitionLogManager>();
        services.AddScoped<IScheduledReportManager, ScheduledReportManager>();
        services.AddScoped<ISftpAcquisitionLogManager, SftpAcquisitionLogManager>();
        services.AddScoped<ISftpConfigurationManager, SftpConfigurationManager>();
        services.AddScoped<IOrganizationLocationConfigurationManager, OrganizationLocationConfigurationManager>();
        services.AddScoped<IOrganizationLocationMappingManager, OrganizationLocationMappingManager>();
        services.AddScoped<IEncounterMappingManager, EncounterMappingManager>();
        services.AddScoped<IEncounterMappingQueries, EncounterMappingQueries>();
    }

    public static void RegisterServices(this IServiceCollection services)
    {
        //Services
        services.AddTransient<ITenantApiService, TenantApiService>();
        services.AddTransient<IValidateFacilityConnectionService, ValidateFacilityConnectionService>();
        services.AddTransient<IFhirApiService, FhirApiService>();
        services.AddTransient<ILocationMappingService, LocationMappingService>();
        services.AddTransient<IResourcesAcquiredTailFinalizer, ResourcesAcquiredTailFinalizer>();
        services.AddTransient<IPatientDataService, PatientDataService>();
        services.AddTransient<IPatientCensusService, PatientCensusService>();
        services.AddTransient<IReferenceResourceService, ReferenceResourceService>();
        services.AddTransient<IQueryListProcessor, QueryListProcessor>();
        services.AddTransient<IDataAcquisitionLogService, DataAcquisitionLogService>();
        services.AddTransient<IAcquisitionDependencyChecker, AcquisitionDependencyChecker>();

        //Data Pull Commands
        services.AddTransient<IReadFhirCommand, ReadFhirCommand>();
        services.AddTransient<ISearchFhirCommand, SearchFhirCommand>();

        //SFTP Services
        services.AddTransient<ISftpClientService, SftpClientService>();
        services.AddTransient<IFileParserFactory, FileParserFactory>();
        services.AddTransient<ISftpAcquisitionProcessorFactory, SftpAcquisitionProcessorFactory>();

        //File Parsers
        services.AddTransient<IFileParser<CernerEncounters>, CernerCclExtractParser>();

        //SFTP Acquisition Processors
        services.AddTransient<ISftpAcquisitionProcessor, CernerCclExtractProcessor>();
    }

    private static void RegisterSecretManager(this IServiceCollection services, IConfiguration configuration)
    {
        var manager = configuration.GetValue<string>("SecretManagement:Manager");

        if (string.Equals(manager, "AzureKeyVault", StringComparison.OrdinalIgnoreCase))
        {
            Log.Information("Registering Azure Key Vault Secret Manager");
            services.AddSingleton<ISecretManager, AzureKeyVaultSecretManager>();
        }
        else
        {
            // LocalSecretManager requires Data Protection for encrypted file storage
            services.AddDataProtection()
                .SetApplicationName(configuration.GetValue<string>("DataProtection:KeyRing") ?? "Link");

            Log.Information("Registering Local Secret Manager (file-based with encryption)");
            services.AddSingleton<ISecretManager, LocalSecretManager>();
        }

        // Register credential service
        services.AddScoped<ISftpCredentialService, SftpCredentialService>();
    }

    public static void RegisterFactories(this IServiceCollection services, IConfigurationManager configuration)
    {
        //Factories - Consumer
        services.AddTransient<IKafkaConsumerFactory<string, string>, KafkaConsumerFactory<string, string>>();
        services.AddTransient<IKafkaConsumerFactory<string, DataAcquisitionRequested>, KafkaConsumerFactory<string, DataAcquisitionRequested>>();
        services.AddTransient<IKafkaConsumerFactory<string, PatientCensusScheduled>, KafkaConsumerFactory<string, PatientCensusScheduled>>();
        services.AddTransient<IKafkaConsumerFactory<long, ReadyToAcquire>, KafkaConsumerFactory<long, ReadyToAcquire>>();

        //Validation
        services.AddValidatorsFromAssemblyContaining<UpdateDataAcquisitionLogModelValidator>();
        services.AddScoped<IQueryPlanValidator, QueryPlanValidator>();
        services.AddScoped<ILocationResolutionValidator, LocationResolutionValidator>();

        //Factories - Producer
        var kafkaConnection = configuration.GetRequiredSection(KafkaConstants.SectionName).Get<KafkaConnection>() ?? throw new Exception("Missing Kafka Connection Settings");
        var producerConfig = new ProducerConfig { CompressionType = CompressionType.Zstd };

        services.RegisterKafkaProducer<string, object>(kafkaConnection, producerConfig);
        services.RegisterKafkaProducer<string, string>(kafkaConnection, producerConfig);
        services.RegisterKafkaProducer<string, DataAcquisitionRequested>(kafkaConnection, producerConfig);
        services.RegisterKafkaProducer<string, PatientCensusScheduled>(kafkaConnection, producerConfig);
        services.RegisterKafkaProducer<ResourceKey, ResourceAcquired>(kafkaConnection, producerConfig);
        services.RegisterKafkaProducer<ResourceKey, ResourcesAcquired>(kafkaConnection, producerConfig);
        services.RegisterKafkaProducer<ResourceKey, MappingOutcomeEvaluatedValue>(kafkaConnection, producerConfig);
        services.RegisterKafkaProducer<string, PatientListMessage>(kafkaConnection, producerConfig, null, new IndentedJsonSerializer<PatientListMessage>());
        services.RegisterKafkaProducer<string, AuditEventMessage>(kafkaConnection, producerConfig);
        services.RegisterKafkaProducer<long, ReadyToAcquire>(kafkaConnection, producerConfig);
        services.RegisterKafkaProducer<string, CernerPatientsAcquired>(kafkaConnection, producerConfig);

        services.AddTransient<IKafkaProducerFactory<string, AuditEventMessage>, KafkaProducerFactory<string, AuditEventMessage>>();
        services.AddTransient<IKafkaProducerFactory<string, object>, KafkaProducerFactory<string, object>>();
        services.AddTransient<IKafkaProducerFactory<string, string>, KafkaProducerFactory<string, string>>();
        services.AddTransient<IKafkaProducerFactory<string, DataAcquisitionRequested>, KafkaProducerFactory<string, DataAcquisitionRequested>>();
        services.AddTransient<IKafkaProducerFactory<string, PatientCensusScheduled>, KafkaProducerFactory<string, PatientCensusScheduled>>();
        services.AddTransient<IKafkaProducerFactory<ResourceKey, ResourceAcquired>, KafkaProducerFactory<ResourceKey, ResourceAcquired>>();
        services.AddTransient<IKafkaProducerFactory<ResourceKey, ResourcesAcquired>, KafkaProducerFactory<ResourceKey, ResourcesAcquired>>();
        services.AddTransient<IKafkaProducerFactory<string, PatientListMessage>, KafkaProducerFactory<string, PatientListMessage>>();
        services.AddTransient<IKafkaProducerFactory<long, ReadyToAcquire>, KafkaProducerFactory<long, ReadyToAcquire>>();
        services.AddTransient<IKafkaProducerFactory<string, CernerPatientsAcquired>, KafkaProducerFactory<string, CernerPatientsAcquired>>();

        //Factories - Application
        services.AddTransient<IParameterQueryFactory, ParameterQueryFactory>();
        services.AddTransient<ILiteralParameterFactory, LiteralParameterFactory>();
        services.AddTransient<IVariableParameterFactory, VariableParameterFactory>();
        services.AddTransient<IResourceIdParameterFactory, ResourceIdParameterFactory>();
    }

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

    public static void RegisterProblemDetails(this IServiceCollection services, IHostingEnvironment environment)
    {
        services.AddProblemDetails(options =>
        {
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
}
