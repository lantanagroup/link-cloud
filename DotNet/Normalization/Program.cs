using Azure.Identity;
using Confluent.Kafka;
using HealthChecks.UI.Client;
using LantanaGroup.Link.Normalization.Application.Models.Messages;
using LantanaGroup.Link.Normalization.Application.Services;
using LantanaGroup.Link.Normalization.Application.Services.Operations;
using LantanaGroup.Link.Normalization.Application.Settings;
using LantanaGroup.Link.Normalization.Domain;
using LantanaGroup.Link.Normalization.Domain.Entities;
using LantanaGroup.Link.Normalization.Domain.Managers;
using LantanaGroup.Link.Normalization.Domain.Queries;
using LantanaGroup.Link.Normalization.Domain.Repositories;
using LantanaGroup.Link.Normalization.Listeners;
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
using LantanaGroup.Link.Shared.Application.Services;
using LantanaGroup.Link.Shared.Application.Utilities;
using LantanaGroup.Link.Shared.Domain.Repositories.Interceptors;
using LantanaGroup.Link.Shared.Domain.Repositories.Interfaces;
using LantanaGroup.Link.Shared.Jobs;
using LantanaGroup.Link.Shared.Settings;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration.AzureAppConfiguration;
using Microsoft.OpenApi.Models;
using Quartz;
using Quartz.Impl;
using Quartz.Spi;
using Serilog;
using Serilog.Enrichers.Span;
using Serilog.Exceptions;
using System.Reflection;
using System.Text.Json;
using AuditEventMessage = LantanaGroup.Link.Shared.Application.Models.Kafka.AuditEventMessage;

var builder = WebApplication.CreateBuilder(args);

RegisterServices(builder);
var app = builder.Build();
SetupMiddleware(app);

app.Run();

#region Register Services

static void RegisterServices(WebApplicationBuilder builder)
{
    //load external configuration source if specified
    var externalConfigurationSource = builder.Configuration.GetSection(NormalizationConstants.AppSettingsSectionNames.ExternalConfigurationSource).Get<string>();

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
                         .Select("*", NormalizationConstants.ServiceName)
                         // Load configuration values for service name and environment
                         .Select("*", NormalizationConstants.ServiceName + ":" + builder.Environment.EnvironmentName);

                    options.ConfigureKeyVault(kv =>
                    {
                        kv.SetCredential(new DefaultAzureCredential());
                    });

                });
                break;
        }
    }

    IConfigurationSection serviceInformationSection = builder.Configuration.GetRequiredSection(NormalizationConstants.AppSettingsSectionNames.ServiceInformation);
    builder.Services.Configure<ServiceInformation>(serviceInformationSection);
    var serviceInformation = serviceInformationSection.Get<ServiceInformation>();
    if (serviceInformation != null)
    {
        ServiceActivitySource.Initialize(serviceInformation);;
    }
    else
    {
        throw new NullReferenceException("Service Information was null.");
    }

    IConfigurationSection consumerSettingsSection = builder.Configuration.GetRequiredSection(nameof(ConsumerSettings));
    builder.Services.Configure<ConsumerSettings>(consumerSettingsSection);
    var consumerSettings = consumerSettingsSection.Get<ConsumerSettings>();

    builder.Services.Configure<ServiceRegistry>(builder.Configuration.GetSection(ServiceRegistry.ConfigSectionName));
    builder.Services.AddSingleton<KafkaConnection>(builder.Configuration.GetSection(KafkaConstants.SectionName).Get<KafkaConnection>());
    builder.Services.Configure<CorsSettings>(builder.Configuration.GetSection(ConfigurationConstants.AppSettings.CORS));
    builder.Services.Configure<LinkTokenServiceSettings>(builder.Configuration.GetSection(ConfigurationConstants.AppSettings.LinkTokenService));

    // Additional configuration is required to successfully run gRPC on macOS.
    // For instructions on how to configure Kestrel and gRPC clients on macOS, visit https://go.microsoft.com/fwlink/?linkid=2099682

    builder.Services.AddTransient<IKafkaConsumerFactory<string, string>, KafkaConsumerFactory<string, string>>();
    builder.Services.AddTransient<IKafkaConsumerFactory<string, ResourceAcquiredMessage>, KafkaConsumerFactory<string, ResourceAcquiredMessage>>();

    builder.Services.AddTransient<IKafkaProducerFactory<string, string>, KafkaProducerFactory<string, string>>();
    builder.Services.AddTransient<IKafkaProducerFactory<string, AuditEventMessage>, KafkaProducerFactory<string, AuditEventMessage>>();
    builder.Services.AddTransient<IKafkaProducerFactory<string, ResourceAcquiredMessage>, KafkaProducerFactory<string, ResourceAcquiredMessage>>();
    builder.Services.AddTransient<IKafkaProducerFactory<string, ResourceNormalizedMessage>, KafkaProducerFactory<string, ResourceNormalizedMessage>>();

    builder.Services.RegisterKafkaProducer<string, ResourceNormalizedMessage>(kafkaConnection: builder.Configuration.GetSection(KafkaConstants.SectionName).Get<KafkaConnection>(),
        config: new ProducerConfig() { CompressionType = CompressionType.Zstd });
    builder.Services.RegisterKafkaProducer<string, AuditEventMessage>(kafkaConnection: builder.Configuration.GetSection(KafkaConstants.SectionName).Get<KafkaConnection>(), new ProducerConfig());

    builder.Services.AddTransient<IDeadLetterExceptionHandler<string, string>, DeadLetterExceptionHandler<string, string>>();
    builder.Services.AddTransient<IDeadLetterExceptionHandler<string, ResourceAcquiredMessage>, DeadLetterExceptionHandler<string, ResourceAcquiredMessage>>();
    builder.Services.AddTransient<ITransientExceptionHandler<string, ResourceAcquiredMessage>, TransientExceptionHandler<string, ResourceAcquiredMessage>>();

    builder.Services.AddTransient<ITenantApiService, TenantApiService>();

    builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new FhirResourceConverter());
    });

    builder.Services.AddHttpClient();
    builder.Services.AddProblemDetails();

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

    builder.Services.AddDbContext<NormalizationDbContext>((sp, options) => {

        var updateBaseEntityInterceptor = sp.GetRequiredService<UpdateBaseEntityInterceptor>();
        var dbProvider =
            builder.Configuration.GetValue<string>(NormalizationConstants.AppSettingsSectionNames.DatabaseProvider);
        switch (dbProvider)
        {
            case ConfigurationConstants.AppSettings.SqlServerDatabaseProvider:
                string? connectionString = builder.Configuration.GetConnectionString(ConfigurationConstants.DatabaseConnections.DatabaseConnection);

                if (string.IsNullOrEmpty(connectionString))
                    throw new InvalidOperationException("Database connection string is null or empty.");

                options
                    .UseSqlServer(connectionString)
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
    builder.Services.AddScoped<IEntityRepository<Vendor>, VendorRepository>();
    builder.Services.AddScoped<IEntityRepository<VendorVersion>, VendorVersionRepository>();
    builder.Services.AddScoped<IEntityRepository<VendorVersionOperationPreset>, VendorVersionOperationPresetRepository>();

    builder.Services.AddTransient<IRetryEntityFactory, RetryEntityFactory>();
    builder.Services.AddTransient<IBaseEntityRepository<RetryEntity>, NormalizationEntityRepository<RetryEntity>>();

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
    builder.Services.AddScoped<IDatabase, Database>();
    builder.Services.AddScoped<IOperationManager, OperationManager>();
    builder.Services.AddScoped<IResourceManager, ResourceManager>();
    builder.Services.AddScoped<IVendorManager, VendorManager>();
    builder.Services.AddScoped<IOperationQueries, OperationQueries>(); 
    builder.Services.AddScoped<IOperationSequenceQueries, OperationSequenceQueries>();
    builder.Services.AddScoped<IVendorQueries, VendorQueries>();
    builder.Services.AddScoped<IResourceQueries, ResourceQueries>();

    builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNameCaseInsensitive = true;
        options.JsonSerializerOptions.Converters.Add(new OperationConverter());
    });


    builder.Services.AddTransient<IJobFactory, JobFactory>();
    builder.Services.AddTransient<ISchedulerFactory, StdSchedulerFactory>();
    builder.Services.AddTransient<RetryJob>();

    builder.Services.AddSingleton<CopyPropertyOperationService>();
    builder.Services.AddHostedService(provider => provider.GetRequiredService<CopyPropertyOperationService>());

    builder.Services.AddSingleton<CodeMapOperationService>();
    builder.Services.AddHostedService(provider => provider.GetRequiredService<CodeMapOperationService>());

    builder.Services.AddSingleton<ConditionalTransformOperationService>();
    builder.Services.AddHostedService(provider => provider.GetRequiredService<ConditionalTransformOperationService>());

    if (consumerSettings != null && !consumerSettings.DisableConsumer)
    {
         builder.Services.AddHostedService<ResourceAcquiredListener>();
    }

    if (consumerSettings != null && !consumerSettings.DisableRetryConsumer)
    {
        builder.Services.AddSingleton(new RetryListenerSettings(NormalizationConstants.ServiceName, [KafkaTopic.ResourceAcquiredRetry.GetStringValue()]));
        builder.Services.AddHostedService<RetryListener>();
        builder.Services.AddHostedService<RetryScheduleService>();
    }

    //Add health checks
    var kafkaConnection = builder.Configuration.GetRequiredSection(KafkaConstants.SectionName).Get<KafkaConnection>();
    var kafkaHealthOptions = new KafkaHealthCheckConfiguration(kafkaConnection, NormalizationConstants.ServiceName).GetHealthCheckOptions();


    builder.Services.AddHealthChecks()
        .AddCheck<DatabaseHealthCheck>(HealthCheckType.Database.ToString())
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
    builder.Services.AddLinkCorsService(options => {
        options.Environment = builder.Environment;
    });

    //Add telemetry if enabled
    builder.Services.AddLinkTelemetry(builder.Configuration, options =>
    {
        options.Environment = builder.Environment;
        options.ServiceName = NormalizationConstants.ServiceName;
        options.ServiceVersion = serviceInformation.Version; //TODO: Get version from assembly?                
    });

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