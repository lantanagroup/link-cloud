using System.Reflection;
using Confluent.Kafka;
using HealthChecks.UI.Client;
using Hl7.Fhir.Model.CdsHooks;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using LantanaGroup.Link.Normalization.Application.Models.Messages;
using LantanaGroup.Link.Normalization.Application.Services;
using LantanaGroup.Link.Normalization.Application.Services.Operations;
using LantanaGroup.Link.Normalization.Application.Settings;
using LantanaGroup.Link.Normalization.Domain;
using LantanaGroup.Link.Normalization.Domain.Entities;
using LantanaGroup.Link.Normalization.Domain.Managers;
using LantanaGroup.Link.Normalization.Domain.Queries;
using LantanaGroup.Link.Normalization.Domain.Repositories;
using LantanaGroup.Link.Normalization.Domain.Services;
using LantanaGroup.Link.Normalization.Listeners;
using LantanaGroup.Link.Sdk.DependencyInjection;
using LantanaGroup.Link.Shared.Application.Error.Handlers;
using LantanaGroup.Link.Shared.Application.Error.Interfaces;
using LantanaGroup.Link.Shared.Application.Extensions;
using LantanaGroup.Link.Shared.Application.Extensions.Caching;
using LantanaGroup.Link.Shared.Application.Extensions.ExternalServices;
using LantanaGroup.Link.Shared.Application.Extensions.Quartz;
using LantanaGroup.Link.Shared.Application.Extensions.Security;
using LantanaGroup.Link.Shared.Application.Factories;
using LantanaGroup.Link.Shared.Application.Health;
using LantanaGroup.Link.Shared.Application.Interfaces;
using LantanaGroup.Link.Shared.Application.Listeners;
using LantanaGroup.Link.Shared.Application.Middleware;
using LantanaGroup.Link.Shared.Application.Models;
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
using Microsoft.Extensions.Caching.Memory;
using Microsoft.OpenApi.Models;
using Serilog;
using Serilog.Enrichers.Span;
using Serilog.Exceptions;
using AuditEventMessage = LantanaGroup.Link.Shared.Application.Models.Kafka.AuditEventMessage;

var builder = WebApplication.CreateBuilder(args);
builder.Configuration.AddStandardEnvironmentConfiguration();

RegisterServices(builder);
var app = builder.Build();
SetupMiddleware(app);

app.Run();

#region Register Services

static void RegisterServices(WebApplicationBuilder builder)
{
    // load external configuration source (if specified)
    builder.AddExternalConfiguration(NormalizationConstants.ServiceName);

    var assemblyVersion = Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? string.Empty;

    var serviceInformation = builder.SetupServiceInformation(NormalizationConstants.ServiceName, assemblyVersion);

    IConfigurationSection consumerSettingsSection = builder.Configuration.GetRequiredSection(nameof(ConsumerSettings));
    builder.Services.Configure<ConsumerSettings>(consumerSettingsSection);
    var consumerSettings = consumerSettingsSection.Get<ConsumerSettings>();

    builder.Services.Configure<ResourceCacheBlobStorageSettings>(builder.Configuration.GetSection(ResourceCacheBlobStorageSettings.Key));
    builder.Services.Configure<ServiceRegistry>(builder.Configuration.GetSection(ServiceRegistry.ConfigSectionName));
    builder.Services.AddSingleton<KafkaConnection>(builder.Configuration.GetSection(KafkaConstants.SectionName).Get<KafkaConnection>());
    builder.Services.Configure<CorsSettings>(builder.Configuration.GetSection(ConfigurationConstants.AppSettings.CORS));
    builder.Services.Configure<LinkTokenServiceSettings>(builder.Configuration.GetSection(ConfigurationConstants.AppSettings.LinkTokenService));
    builder.Services.AddResourceCache(builder.Configuration);

    // Additional configuration is required to successfully run gRPC on macOS.
    // For instructions on how to configure Kestrel and gRPC clients on macOS, visit https://go.microsoft.com/fwlink/?linkid=2099682

    builder.Services.AddTransient<IKafkaConsumerFactory<string, string>, KafkaConsumerFactory<string, string>>();
    builder.Services.AddTransient<IKafkaConsumerFactory<ResourceKey, ResourcesAcquiredValue>, KafkaConsumerFactory<ResourceKey, ResourcesAcquiredValue>>();

    builder.Services.AddTransient<IKafkaProducerFactory<string, string>, KafkaProducerFactory<string, string>>();
    builder.Services.AddTransient<IKafkaProducerFactory<ResourceKey, string>, KafkaProducerFactory<ResourceKey, string>>();
    builder.Services.AddTransient<IKafkaProducerFactory<string, AuditEventMessage>, KafkaProducerFactory<string, AuditEventMessage>>();
    builder.Services.AddTransient<IKafkaProducerFactory<ResourceKey, ResourcesAcquiredValue>, KafkaProducerFactory<ResourceKey, ResourcesAcquiredValue>>();
    builder.Services.AddTransient<IKafkaProducerFactory<ResourceKey, ResourcesNormalizedValue>, KafkaProducerFactory<ResourceKey, ResourcesNormalizedValue>>();

    builder.Services.RegisterKafkaProducer<ResourceKey, ResourcesNormalizedValue>(
        builder.Configuration.GetSection(KafkaConstants.SectionName).Get<KafkaConnection>(),
        new ProducerConfig() { CompressionType = CompressionType.Zstd });
    builder.Services.RegisterKafkaProducer<string, AuditEventMessage>(kafkaConnection: builder.Configuration.GetSection(KafkaConstants.SectionName).Get<KafkaConnection>(), new ProducerConfig());

    builder.Services.AddSingleton(typeof(IExceptionLogger<>), typeof(ExceptionLogger<>));
    builder.Services.AddSingleton(typeof(ITransientExceptionHandler<,,>), typeof(TransientExceptionHandler<,,>));
    builder.Services.AddSingleton(typeof(IDeadLetterExceptionHandler<,,>), typeof(DeadLetterExceptionHandler<,,>));

    builder.Services.AddTransient<ITenantApiService, TenantApiService>();

    builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new FhirResourceConverter());
    });

    builder.Services.AddHttpClient();
    builder.Services.AddLinkSdk();
    builder.Services.AddProblemDetails();

    builder.Services.AddMemoryCache();

    var provider = builder.Services.BuildServiceProvider();
    var cache = provider.GetRequiredService<IMemoryCache>();

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

    //Add persistence interceptors
    builder.Services.AddSingleton<UpdateBaseEntityInterceptor>();

    var dbProvider =
        builder.Configuration.GetValue<string>(NormalizationConstants.AppSettingsSectionNames.DatabaseProvider);
    string? databaseConnectionString = null;

    if (dbProvider == ConfigurationConstants.AppSettings.SqlServerDatabaseProvider)
    {
        databaseConnectionString = builder.Configuration.GetConnectionString(ConfigurationConstants.DatabaseConnections.DatabaseConnection);

        if (string.IsNullOrEmpty(databaseConnectionString))
            throw new InvalidOperationException("Database connection string is null or empty.");

        //Add Quartz scheduler with SQL persistence
        builder.Services.RegisterQuartzDatabase(databaseConnectionString);
    }

    builder.Services.AddDbContext<NormalizationDbContext>((sp, options) =>
    {

        var updateBaseEntityInterceptor = sp.GetRequiredService<UpdateBaseEntityInterceptor>();
        switch (dbProvider)
        {
            case ConfigurationConstants.AppSettings.SqlServerDatabaseProvider:
                options
                    .UseSqlServer(databaseConnectionString)
                    .AddInterceptors(updateBaseEntityInterceptor);
                break;
            default:
                throw new InvalidOperationException("Database provider not supported.");
        }
    });

    builder.Services.AddScoped<IEntityRepository<Operation>, OperationRepository>();
    builder.Services.AddScoped<IEntityRepository<OperationSequence>, OperationSequenceRepository>();
    builder.Services.AddScoped<IEntityRepository<ResourceType>, ResourceTypeRepository>();
    builder.Services.AddScoped<IEntityRepository<OperationResourceType>, OperationResourceTypeRepository>();
    builder.Services.AddScoped<IEntityRepository<VendorVersionOperationPreset>, VendorVersionOperationPresetRepository>();

    builder.Services.AddTransient<IRetryModelFactory, RetryModelFactory>();

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

    //Managers
    builder.Services.AddScoped<LantanaGroup.Link.Normalization.Domain.IDatabase, Database>();
    builder.Services.AddScoped<IOperationManager, OperationManager>();
    builder.Services.AddScoped<IResourceManager, ResourceManager>();
    builder.Services.AddScoped<IVendorVersionOperationPresetManager, VendorVersionOperationPresetManager>();
    builder.Services.AddScoped<IOperationQueries, OperationQueries>();
    builder.Services.AddScoped<IOperationSequenceQueries, OperationSequenceQueries>();
    builder.Services.AddScoped<IVendorVersionOperationPresetQueries, VendorVersionOperationPresetQueries>();
    builder.Services.AddScoped<IVendorVersionResolver, VendorVersionResolver>();
    builder.Services.AddScoped<IResourceQueries, ResourceQueries>();

    builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNameCaseInsensitive = true;
        options.JsonSerializerOptions.Converters.Add(new OperationConverter());
    });

    builder.Services.AddTransient<RetryJob>();

    builder.Services.AddSingleton<CopyPropertyOperationService>();
    builder.Services.AddSingleton<CodeMapOperationService>();
    builder.Services.AddSingleton<ConditionalTransformOperationService>();
    builder.Services.AddSingleton<CopyLocationOperationService>();
    builder.Services.AddSingleton<CopyLocationAliasToTypeIterativelyOperationService>();
    builder.Services.AddSingleton<RemoveExtensionsOperationService>();
    
    if (consumerSettings != null && !consumerSettings.DisableConsumer)
    {
        builder.Services.AddHostedService<ResourcesAcquiredListener>();
    }

    if (consumerSettings != null && !consumerSettings.DisableRetryConsumer)
    {
        builder.Services.AddSingleton(new RetryListenerSettings(serviceInformation.ServiceName, [KafkaTopic.ResourcesAcquiredRetry.GetStringValue()]));
        builder.Services.AddHostedService<RetryListener>();
        builder.Services.AddHostedService<RetryScheduleService>();
    }

    //Add health checks
    var kafkaConnection = builder.Configuration.GetRequiredSection(KafkaConstants.SectionName).Get<KafkaConnection>();
    var kafkaHealthOptions = new KafkaHealthCheckConfiguration(kafkaConnection, NormalizationConstants.ServiceName).GetHealthCheckOptions();


    builder.Services.AddHealthChecks()
        .AddCheck<DatabaseHealthCheck>(HealthCheckType.Database.ToString())
        .AddCheck<ResourceCacheHealthCheck>(HealthCheckType.Cache.ToString())
        .AddKafka(kafkaHealthOptions, HealthCheckType.Kafka.ToString());

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
        c.DocumentFilter<HealthChecksFilter>();
    });

    //Add CORS
    builder.Services.AddLinkCorsService(options =>
    {
        options.Environment = builder.Environment;
    });

    //Add telemetry if enabled
    builder.Services.AddLinkTelemetry(builder.Configuration, options =>
    {
        options.Environment = builder.Environment;
        options.ServiceName = NormalizationConstants.ServiceName;
        options.ServiceVersion = serviceInformation.Version; //TODO: Get version from assembly?                
    });

    builder.Services.AddSingleton(TimeProvider.System);
    builder.Services.AddSingleton<INormalizationServiceMetrics, NormalizationServiceMetrics>();
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

    //map health check middleware and info endpoint
    app.MapHealthChecks("/health", new HealthCheckOptions
    {
        ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
    }).RequireCors("HealthCheckPolicy");
    app.MapInfo(Assembly.GetExecutingAssembly(), app.Configuration, "normalization");

    app.AutoMigrateEF<NormalizationDbContext>();

    app.UseCors(CorsSettings.DefaultCorsPolicyName);

    // Configure the HTTP request pipeline.
    app.MapControllers();
}

#endregion
