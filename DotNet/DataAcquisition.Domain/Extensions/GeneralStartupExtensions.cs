using Azure.Identity;
using DataAcquisition.Domain.Application.Queries;
using FluentValidation;
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
using LantanaGroup.Link.DataAcquisition.Domain.Settings;
using LantanaGroup.Link.Shared.Application.Error.Handlers;
using LantanaGroup.Link.Shared.Application.Error.Interfaces;
using LantanaGroup.Link.Shared.Application.Extensions;
using LantanaGroup.Link.Shared.Application.Extensions.Caching;
using LantanaGroup.Link.Shared.Application.Factories;
using LantanaGroup.Link.Shared.Application.Interfaces;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using LantanaGroup.Link.Shared.Application.Models.Kafka;
using LantanaGroup.Link.Shared.Application.Services;
using LantanaGroup.Link.Shared.Domain.Repositories.Implementations;
using LantanaGroup.Link.Shared.Domain.Repositories.Interceptors;
using LantanaGroup.Link.Shared.Domain.Repositories.Interfaces;
using LantanaGroup.Link.Shared.Settings;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.AzureAppConfiguration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Trace;
using Serilog;
using Serilog.Enrichers.Span;
using Serilog.Settings.Configuration;
using System.Diagnostics;
using System.Net;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Interfaces;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Services.Auth;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Kafka;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Services.FhirApi;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Validators;
using FluentValidation;
using Microsoft.Extensions.Hosting;
using LantanaGroup.Link.Shared.Domain.Repositories.Interceptors;
using LantanaGroup.Link.Shared.Domain.Repositories.Interfaces;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Entities;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Context;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Queries;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Managers;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Domain;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Services.FhirApi.Commands;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Serializers;

namespace LantanaGroup.Link.DataAcquisition.Domain.Extensions;
public static class GeneralStartupExtensions
{
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

    public static void RegisterConfigs(this IServiceCollection services, IConfigurationManager configuration)
    {
        //configs
        services.Configure<ServiceRegistry>(configuration.GetSection(ServiceRegistry.ConfigSectionName));
        services.AddSingleton<KafkaConnection>(configuration.GetRequiredSection(KafkaConstants.SectionName).Get<KafkaConnection>());
        services.Configure<ConsumerSettings>(configuration.GetRequiredSection(nameof(ConsumerSettings)));
        services.Configure<CorsSettings>(configuration.GetSection(ConfigurationConstants.AppSettings.CORS));
        services.Configure<LinkTokenServiceSettings>(configuration.GetSection(ConfigurationConstants.AppSettings.LinkTokenService));
        services.Configure<ApiSettings>(configuration.GetSection(ConfigurationConstants.AppSettings.ApiSettings));

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

                    options.UseSqlServer(connectionString)
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
        services.AddSingleton<IAuthenticationRetrievalService, AuthenticationRetrievalService>();
    }

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

    public static void RegisterRepositories(this IServiceCollection services)
    {
        //Repositories
        services.AddTransient<IEntityRepository<FhirListConfiguration>, EntityRepository<FhirListConfiguration, DataAcquisitionDbContext>>();
        services.AddTransient<IEntityRepository<FhirQueryConfiguration>, EntityRepository<FhirQueryConfiguration, DataAcquisitionDbContext>>();
        services.AddTransient<IEntityRepository<QueryPlan>, EntityRepository<QueryPlan, DataAcquisitionDbContext>>();
        services.AddTransient<IEntityRepository<ResourceReferenceType>, EntityRepository<ResourceReferenceType, DataAcquisitionDbContext>>();
        services.AddTransient<IEntityRepository<ReferenceResources>, EntityRepository<ReferenceResources, DataAcquisitionDbContext>>();
        services.AddTransient<IEntityRepository<FhirQuery>, EntityRepository<FhirQuery, DataAcquisitionDbContext>>();
        services.AddTransient<IEntityRepository<DataAcquisitionLog>, EntityRepository<DataAcquisitionLog, DataAcquisitionDbContext>>();
        services.AddTransient<IEntityRepository<FhirQueryResourceType>, EntityRepository<FhirQueryResourceType, DataAcquisitionDbContext>>();

        //Database
        services.AddTransient<IDatabase, Database>();
    }

    public static void RegisterManagers(this IServiceCollection services)
    {
        //Queries
        services.AddTransient<IDataAcquisitionLogQueries, DataAcquisitionLogQueries>();
        services.AddTransient<IFhirQueryConfigurationQueries, FhirQueryConfigurationQueries>();
        services.AddTransient<IFhirQueryListConfigurationQueries, FhirQueryListConfigurationQueries>();
        services.AddTransient<IFhirQueryQueries, FhirQueryQueries>();
        services.AddTransient<IQueryPlanQueries, QueryPlanQueries>();
        services.AddTransient<IReferenceResourcesQueries, ReferenceResourcesQueries>();

        //Managers
        services.AddTransient<IFhirQueryConfigurationManager, FhirQueryConfigurationManager>();
        services.AddTransient<IFhirListQueryConfigurationManager, FhirListQueryConfigurationManager>();
        services.AddTransient<IQueryPlanManager, QueryPlanManager>();
        services.AddTransient<IReferenceResourcesManager, ReferenceResourcesManager>();
        services.AddTransient<IFhirQueryManager, FhirQueryManager>();
        services.AddTransient<IDataAcquisitionLogManager, DataAcquisitionLogManager>();
    }

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
        services.RegisterKafkaProducer<string, List<PatientListModel>>(kafkaConnection, producerConfig, null, new IndentedJsonSerializer<List<PatientListModel>>());
        services.RegisterKafkaProducer<string, AuditEventMessage>(kafkaConnection, producerConfig);
        services.RegisterKafkaProducer<long, ReadyToAcquire>(kafkaConnection, producerConfig);

        services.AddTransient<IKafkaProducerFactory<string, AuditEventMessage>, KafkaProducerFactory<string, AuditEventMessage>>();
        services.AddTransient<IKafkaProducerFactory<string, object>, KafkaProducerFactory<string, object>>();
        services.AddTransient<IKafkaProducerFactory<string, string>, KafkaProducerFactory<string, string>>();
        services.AddTransient<IKafkaProducerFactory<string, DataAcquisitionRequested>, KafkaProducerFactory<string, DataAcquisitionRequested>>();
        services.AddTransient<IKafkaProducerFactory<string, PatientCensusScheduled>, KafkaProducerFactory<string, PatientCensusScheduled>>();
        services.AddTransient<IKafkaProducerFactory<string, ResourceAcquired>, KafkaProducerFactory<string, ResourceAcquired>>();
        services.AddTransient<IKafkaProducerFactory<string, PatientListModel>, KafkaProducerFactory<string, PatientListModel>>();
        services.AddTransient<IKafkaProducerFactory<long, ReadyToAcquire>, KafkaProducerFactory<long, ReadyToAcquire>>();
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
}
