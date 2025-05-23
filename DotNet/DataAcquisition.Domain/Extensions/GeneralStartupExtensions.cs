using LantanaGroup.Link.DataAcquisition.Domain.Settings;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration.AzureAppConfiguration;
using Azure.Identity;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;
using Quartz;
using Quartz.Impl;
using Quartz.Spi;
using Serilog;
using Serilog.Enrichers.Span;
using Serilog.Settings.Configuration;
using System.Diagnostics;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Error.Handlers;
using LantanaGroup.Link.Shared.Application.Error.Interfaces;
using LantanaGroup.Link.Shared.Application.Extensions;
using LantanaGroup.Link.Shared.Application.Factories;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Factories;
using LantanaGroup.Link.Shared.Application.Interfaces;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using LantanaGroup.Link.Shared.Application.Models.Kafka;
using LantanaGroup.Link.Shared.Application.Services;
using LantanaGroup.Link.Shared.Jobs;
using LantanaGroup.Link.Shared.Settings;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Services;
using Microsoft.AspNetCore.Builder;
using LantanaGroup.Link.Shared.Application.Extensions.Caching;
using LantanaGroup.Link.Shared.Application.Extensions.Quartz;
using System.Net;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Interfaces;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Services.Auth;
using LantanaGroup.Link.DataAcquisition.Domain.Services.Auth;
using LantanaGroup.Link.DataAcquisition.Domain.Services;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Kafka;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Services.FhirApi;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Validators;
using FluentValidation;
using Confluent.Kafka;
using Microsoft.Extensions.Hosting;
using LantanaGroup.Link.Shared.Domain.Repositories.Interceptors;
using LantanaGroup.Link.Shared.Domain.Repositories.Interfaces;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Entities;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Context;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Queries;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Managers;

namespace LantanaGroup.Link.DataAcquisition.Domain.Extensions;
public static class GeneralStartupExtensions
{
    public static void RegisterAll(this WebApplicationBuilder builder, string serviceName, List<Func<bool>> addExtraItems = default)
    {
        builder.Configuration.RegisterAzureConfigService(builder.Environment, serviceName);
        builder.Configuration.RegisterMonitoring(builder.Logging, builder.Services);
        builder.Services.RegisterConfigs(builder.Configuration);
        builder.RegisterDatabases();
        builder.Services.RegisterHittpClient();
        builder.Services.RegisterFhirAuthHandlers();
        builder.Services.RegisterExceptionHandlers();
        builder.Services.RegisterRepositories();
        builder.Services.RegisterManagers();
        builder.Services.RegisterServices();
        builder.Services.RegisterFactories(builder.Configuration);
        builder.Services.RegisterTelemetry(builder.Configuration, builder.Environment, serviceName);

        foreach(var function in addExtraItems)
        {
            function();
        }
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

        IConfigurationSection consumerSettingsSection = configuration.GetRequiredSection(nameof(ConsumerSettings));
        services.Configure<ConsumerSettings>(consumerSettingsSection);
        var consumerSettings = consumerSettingsSection.Get<ConsumerSettings>();
    }

    public static void RegisterDatabases(this WebApplicationBuilder builder)
    {
        //in-memory cache
        builder.Services.AddMemoryCache();
        builder.Services.AddSingleton<ICacheService, InMemoryCacheService>();

        DistributedLockSettingsExtensions.DistributedLockBuildAndAddToDI(builder.Services, builder.Configuration, ConfigurationConstants.DatabaseConnections.RedisConnection);

        //Add DbContext
        builder.Services.AddTransient<UpdateBaseEntityInterceptor>();
        builder.AddSQLServerEF_DataAcq();

        //add quartz
        builder.Services.RegisterQuartzDatabase(builder.Configuration.GetConnectionString(ConfigurationConstants.DatabaseConnections.DatabaseConnection));
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
        services.AddTransient<IDeadLetterExceptionHandler<string, string>, DeadLetterExceptionHandler<string, string>>();
        services.AddTransient<IDeadLetterExceptionHandler<string, DataAcquisitionRequested>, DeadLetterExceptionHandler<string, DataAcquisitionRequested>>();
        services.AddTransient<IDeadLetterExceptionHandler<string, PatientCensusScheduled>, DeadLetterExceptionHandler<string, PatientCensusScheduled>>();
        services.AddTransient<ITransientExceptionHandler<string, string>, TransientExceptionHandler<string, string>>();
        services.AddTransient<ITransientExceptionHandler<string, DataAcquisitionRequested>, TransientExceptionHandler<string, DataAcquisitionRequested>>();
        services.AddTransient<ITransientExceptionHandler<string, PatientCensusScheduled>, TransientExceptionHandler<string, PatientCensusScheduled>>();
    }

    public static void RegisterRepositories(this IServiceCollection services)
    {
        //Repositories
        services.AddTransient<IEntityRepository<FhirListConfiguration>, DataEntityRepository<FhirListConfiguration, DataAcquisitionDbContext>>();
        services.AddTransient<IEntityRepository<FhirQueryConfiguration>, DataEntityRepository<FhirQueryConfiguration, DataAcquisitionDbContext>>();
        services.AddTransient<IEntityRepository<QueryPlan>, DataEntityRepository<QueryPlan, DataAcquisitionDbContext>>();
        services.AddTransient<IEntityRepository<ReferenceResources>, DataEntityRepository<ReferenceResources, DataAcquisitionDbContext>>();
        services.AddTransient<IEntityRepository<FhirQuery>, DataEntityRepository<FhirQuery, DataAcquisitionDbContext>>();
        services.AddTransient<IEntityRepository<DataAcquisitionLog>, DataEntityRepository<DataAcquisitionLog, DataAcquisitionDbContext>>();
        services.AddScoped<IEntityRepository<RetryEntity>, DataEntityRepository<RetryEntity, DataAcquisitionDbContext>>();

        services.AddTransient<IDatabase, Database>();
    }

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
        services.AddTransient<BundleResourceAcquiredEventService, BundleResourceAcquiredEventService>();
        services.AddTransient<IDataAcquisitionLogService, DataAcquisitionLogService>();
    }

    public static void RegisterFactories(this IServiceCollection services, IConfigurationManager configuration)
    {
        //Factories - Consumer
        services.AddScoped<IKafkaConsumerFactory<string, string>, KafkaConsumerFactory<string, string>>();
        services.AddScoped<IKafkaConsumerFactory<string, DataAcquisitionRequested>, KafkaConsumerFactory<string, DataAcquisitionRequested>>();
        services.AddScoped<IKafkaConsumerFactory<string, PatientCensusScheduled>, KafkaConsumerFactory<string, PatientCensusScheduled>>();

        //Validation
        services.AddValidatorsFromAssemblyContaining<UpdateDataAcquisitionLogModelValidator>();

        //Factories - Producer
        services.RegisterKafkaProducer<string, object>(
            configuration.GetRequiredSection(KafkaConstants.SectionName).Get<KafkaConnection>(),
            new Confluent.Kafka.ProducerConfig { CompressionType = Confluent.Kafka.CompressionType.Zstd });
        services.RegisterKafkaProducer<string, string>(
            configuration.GetRequiredSection(KafkaConstants.SectionName).Get<KafkaConnection>(),
            new Confluent.Kafka.ProducerConfig { CompressionType = Confluent.Kafka.CompressionType.Zstd });
        services.RegisterKafkaProducer<string, DataAcquisitionRequested>(
            configuration.GetRequiredSection(KafkaConstants.SectionName).Get<KafkaConnection>(),
            new Confluent.Kafka.ProducerConfig { CompressionType = Confluent.Kafka.CompressionType.Zstd });
        services.RegisterKafkaProducer<string, PatientCensusScheduled>(
            configuration.GetRequiredSection(KafkaConstants.SectionName).Get<KafkaConnection>(),
            new Confluent.Kafka.ProducerConfig { CompressionType = Confluent.Kafka.CompressionType.Zstd });
        services.RegisterKafkaProducer<string, ResourceAcquired>(
            configuration.GetRequiredSection(KafkaConstants.SectionName).Get<KafkaConnection>(),
            new Confluent.Kafka.ProducerConfig { CompressionType = Confluent.Kafka.CompressionType.Zstd });
        services.RegisterKafkaProducer<string, PatientIDsAcquiredMessage>(
            configuration.GetRequiredSection(KafkaConstants.SectionName).Get<KafkaConnection>(),
            new Confluent.Kafka.ProducerConfig { CompressionType = Confluent.Kafka.CompressionType.Zstd });
        services.RegisterKafkaProducer<string, AuditEventMessage>(
            configuration.GetRequiredSection(KafkaConstants.SectionName).Get<KafkaConnection>(),
            new Confluent.Kafka.ProducerConfig { CompressionType = Confluent.Kafka.CompressionType.Zstd });
        services.RegisterKafkaProducer<Null, ReadyToAcquire>(
            configuration.GetRequiredSection(KafkaConstants.SectionName).Get<KafkaConnection>(),
            new Confluent.Kafka.ProducerConfig { CompressionType = Confluent.Kafka.CompressionType.Zstd });

        services.AddTransient<IKafkaProducerFactory<string, AuditEventMessage>, KafkaProducerFactory<string, AuditEventMessage>>();
        services.AddTransient<IKafkaProducerFactory<string, object>, KafkaProducerFactory<string, object>>();
        services.AddTransient<IKafkaProducerFactory<string, string>, KafkaProducerFactory<string, string>>();
        services.AddTransient<IKafkaProducerFactory<string, DataAcquisitionRequested>, KafkaProducerFactory<string, DataAcquisitionRequested>>();
        services.AddTransient<IKafkaProducerFactory<string, PatientCensusScheduled>, KafkaProducerFactory<string, PatientCensusScheduled>>();
        services.AddTransient<IKafkaProducerFactory<string, ResourceAcquired>, KafkaProducerFactory<string, ResourceAcquired>>();
        services.AddTransient<IKafkaProducerFactory<string, PatientIDsAcquiredMessage>, KafkaProducerFactory<string, PatientIDsAcquiredMessage>>();


        //Factories - Retry
        services.AddTransient<IRetryEntityFactory, RetryEntityFactory>();
        services.AddTransient<ISchedulerFactory, StdSchedulerFactory>();
        services.AddTransient<RetryJob>();
        services.AddScoped<IJobFactory, JobFactory>();
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
