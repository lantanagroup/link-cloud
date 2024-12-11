using Azure.Identity;
using DataAcquisition.Domain;
using DataAcquisition.Domain.Extensions;
using HealthChecks.UI.Client;
using LantanaGroup.Link.DataAcquisition.Application.Factories;
using LantanaGroup.Link.DataAcquisition.Application.Interfaces;
using LantanaGroup.Link.DataAcquisition.Application.Models.Kafka;
using LantanaGroup.Link.DataAcquisition.Application.Repositories;
using LantanaGroup.Link.DataAcquisition.Application.Serializers;
using LantanaGroup.Link.DataAcquisition.Application.Services;
using LantanaGroup.Link.DataAcquisition.Application.Services.Auth;
using LantanaGroup.Link.DataAcquisition.Application.Services.FhirApi;
using LantanaGroup.Link.DataAcquisition.Domain;
using LantanaGroup.Link.DataAcquisition.Domain.Entities;
using LantanaGroup.Link.DataAcquisition.Domain.Settings;
using LantanaGroup.Link.DataAcquisition.Listeners;
using LantanaGroup.Link.DataAcquisition.Services;
using LantanaGroup.Link.DataAcquisition.Services.Auth;
using LantanaGroup.Link.Shared.Application.Error.Handlers;
using LantanaGroup.Link.Shared.Application.Error.Interfaces;
using LantanaGroup.Link.Shared.Application.Extensions;
using LantanaGroup.Link.Shared.Application.Extensions.Security;
using LantanaGroup.Link.Shared.Application.Factories;
using LantanaGroup.Link.Shared.Application.Interfaces;
using LantanaGroup.Link.Shared.Application.Listeners;
using LantanaGroup.Link.Shared.Application.Middleware;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using LantanaGroup.Link.Shared.Application.Models.Kafka;
using LantanaGroup.Link.Shared.Application.Repositories.Interceptors;
using LantanaGroup.Link.Shared.Application.Repositories.Interfaces;
using LantanaGroup.Link.Shared.Application.Services;
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
using Serilog.Settings.Configuration;
using System.Diagnostics;
using System.Net;
using System.Reflection;
using System.Text.Json.Serialization;
using LantanaGroup.Link.Shared.Application;
using LantanaGroup.Link.Shared.Application.Utilities;
using Hl7.Fhir.Serialization;
using Hl7.Fhir.Model;
using LantanaGroup.Link.Shared.Application.Health;
using LantanaGroup.Link.DataAcquisition.Application.Managers;

var builder = WebApplication.CreateBuilder(args);

RegisterServices(builder);
var app = builder.Build();
SetupMiddleware(app);

app.Run();

#region Register Services

static void RegisterServices(WebApplicationBuilder builder)
{
    //load external configuration source if specified
    var externalConfigurationSource = builder.Configuration.GetSection(DataAcquisitionConstants.AppSettingsSectionNames.ExternalConfigurationSource).Get<string>();

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
                            .Select("*", DataAcquisitionConstants.ServiceName)
                            // Load configuration values for service name and environment
                            .Select("*", DataAcquisitionConstants.ServiceName + ":" + builder.Environment.EnvironmentName);

                    options.ConfigureKeyVault(kv =>
                    {
                        kv.SetCredential(new DefaultAzureCredential());
                    });

                });
                break;
        }
    }

    // Logging using Serilog
    builder.Logging.AddSerilog();
    var loggerOptions = new ConfigurationReaderOptions { SectionName = DataAcquisitionConstants.AppSettingsSectionNames.Serilog };
    Log.Logger = new LoggerConfiguration()
                    .ReadFrom.Configuration(builder.Configuration, loggerOptions)
                    .Filter.ByExcluding("RequestPath like '/health%'")
                    //.Enrich.WithExceptionDetails()
                    .Enrich.FromLogContext()
                    .Enrich.WithSpan()
                    .Enrich.With<ActivityEnricher>()
                    .CreateLogger();

    var serviceInformation = builder.Configuration.GetSection(DataAcquisitionConstants.AppSettingsSectionNames.ServiceInformation).Get<ServiceInformation>();
    builder.Services.Configure<ServiceInformation>(builder.Configuration.GetSection(DataAcquisitionConstants.AppSettingsSectionNames.ServiceInformation));

    if (serviceInformation != null)
    {
        ServiceActivitySource.Initialize(serviceInformation);    
    }
    else
    {
        throw new NullReferenceException("Service Information was null.");
    }

    //configs
    builder.Services.Configure<ServiceRegistry>(builder.Configuration.GetSection(ServiceRegistry.ConfigSectionName));
    builder.Services.AddSingleton<KafkaConnection>(builder.Configuration.GetRequiredSection(KafkaConstants.SectionName).Get<KafkaConnection>());
    builder.Services.Configure<ConsumerSettings>(builder.Configuration.GetRequiredSection(nameof(ConsumerSettings)));
    builder.Services.Configure<CorsSettings>(builder.Configuration.GetSection(ConfigurationConstants.AppSettings.CORS));
    builder.Services.Configure<LinkTokenServiceSettings>(builder.Configuration.GetSection(ConfigurationConstants.AppSettings.LinkTokenService));

    IConfigurationSection consumerSettingsSection = builder.Configuration.GetRequiredSection(nameof(ConsumerSettings));
    builder.Services.Configure<ConsumerSettings>(consumerSettingsSection);
    var consumerSettings = consumerSettingsSection.Get<ConsumerSettings>();

    //Add DbContext
    builder.Services.AddTransient<UpdateBaseEntityInterceptor>();
    builder.AddSQLServerEF_DataAcq();

    builder.Services.AddHttpClient("FhirHttpClient")
            .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler()
            {
                // FhirClient configures its internal HttpClient this way
                AutomaticDecompression = DecompressionMethods.Deflate | DecompressionMethods.GZip
            });

    // Add services to the container.
    // Additional configuration is required to successfully run gRPC on macOS.
    // For instructions on how to configure Kestrel and gRPC clients on macOS, visit https://go.microsoft.com/fwlink/?linkid=2099682
    builder.Services.AddControllers(
        options => options.SuppressImplicitRequiredAttributeForNonNullableReferenceTypes = true
        ).AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
        options.JsonSerializerOptions.PropertyNameCaseInsensitive = true;
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
        options.JsonSerializerOptions.Converters.Add(new QueryPlanConverter());
        options.JsonSerializerOptions.ForFhir(ModelInfo.ModelInspector);
    });

    //Fhir Authentication Handlers
    builder.Services.AddSingleton<EpicAuth>();
    builder.Services.AddSingleton<BasicAuth>();
    builder.Services.AddSingleton<IAuthenticationRetrievalService, AuthenticationRetrievalService>();

    //Exception Handlers
    builder.Services.AddTransient<IDeadLetterExceptionHandler<string, string>, DeadLetterExceptionHandler<string, string>>();
    builder.Services.AddTransient<IDeadLetterExceptionHandler<string, DataAcquisitionRequested>, DeadLetterExceptionHandler<string, DataAcquisitionRequested>>();
    builder.Services.AddTransient<IDeadLetterExceptionHandler<string, PatientCensusScheduled>, DeadLetterExceptionHandler<string, PatientCensusScheduled>>();
    builder.Services.AddTransient<ITransientExceptionHandler<string, string>, TransientExceptionHandler<string, string>>();
    builder.Services.AddTransient<ITransientExceptionHandler<string, DataAcquisitionRequested>, TransientExceptionHandler<string, DataAcquisitionRequested>>();
    builder.Services.AddTransient<ITransientExceptionHandler<string, PatientCensusScheduled>, TransientExceptionHandler<string, PatientCensusScheduled>>();

    //Repositories
    builder.Services.AddTransient<IEntityRepository<FhirListConfiguration>, DataEntityRepository<FhirListConfiguration>>();
    builder.Services.AddTransient<IEntityRepository<FhirQueryConfiguration>, DataEntityRepository<FhirQueryConfiguration>>();
    builder.Services.AddTransient<IEntityRepository<QueryPlan>, DataEntityRepository<QueryPlan>>();
    builder.Services.AddTransient<IEntityRepository<ReferenceResources>, DataEntityRepository<ReferenceResources>>();
    builder.Services.AddTransient<IEntityRepository<FhirQuery>, DataEntityRepository<FhirQuery>>();

    builder.Services.AddScoped<IEntityRepository<RetryEntity>, DataEntityRepository<RetryEntity>>();

    builder.Services.AddTransient<IDatabase, Database>();

    //Managers
    builder.Services.AddTransient<IFhirQueryConfigurationManager, FhirQueryConfigurationManager>();
    builder.Services.AddTransient<IFhirQueryListConfigurationManager, FhirQueryListConfigurationManager>();
    builder.Services.AddTransient<IQueryPlanManager, QueryPlanManager>();
    builder.Services.AddTransient<IReferenceResourcesManager, ReferenceResourcesManager>();
    builder.Services.AddTransient<IFhirQueryManager, FhirQueryManager>();

    //Services
    builder.Services.AddTransient<ITenantApiService, TenantApiService>();
    builder.Services.AddTransient<IValidateFacilityConnectionService, ValidateFacilityConnectionService>();
    builder.Services.AddTransient<IFhirApiService, FhirApiService>();
    builder.Services.AddTransient<IPatientDataService, PatientDataService>();
    builder.Services.AddTransient<IPatientCensusService, PatientCensusService>();
    builder.Services.AddTransient<IReferenceResourceService, ReferenceResourceService>();
    builder.Services.AddTransient<IQueryListProcessor, QueryListProcessor>();
    builder.Services.AddTransient<BundleResourceAcquiredEventService, BundleResourceAcquiredEventService>();

    //Factories - Consumer
    builder.Services.AddScoped<IKafkaConsumerFactory<string, string>, KafkaConsumerFactory<string, string>>();
    builder.Services.AddScoped<IKafkaConsumerFactory<string, DataAcquisitionRequested>, KafkaConsumerFactory<string, DataAcquisitionRequested>>();
    builder.Services.AddScoped<IKafkaConsumerFactory<string, PatientCensusScheduled>, KafkaConsumerFactory<string, PatientCensusScheduled>>();

    //Factories - Producer
    builder.Services.RegisterKafkaProducer<string, object>(
        builder.Configuration.GetRequiredSection(KafkaConstants.SectionName).Get<KafkaConnection>(),
        new Confluent.Kafka.ProducerConfig { CompressionType = Confluent.Kafka.CompressionType.Zstd});
    builder.Services.RegisterKafkaProducer<string, string>(
        builder.Configuration.GetRequiredSection(KafkaConstants.SectionName).Get<KafkaConnection>(),
        new Confluent.Kafka.ProducerConfig { CompressionType = Confluent.Kafka.CompressionType.Zstd });
    builder.Services.RegisterKafkaProducer<string, DataAcquisitionRequested>(
        builder.Configuration.GetRequiredSection(KafkaConstants.SectionName).Get<KafkaConnection>(),
        new Confluent.Kafka.ProducerConfig { CompressionType = Confluent.Kafka.CompressionType.Zstd });
    builder.Services.RegisterKafkaProducer<string, PatientCensusScheduled>(
        builder.Configuration.GetRequiredSection(KafkaConstants.SectionName).Get<KafkaConnection>(),
        new Confluent.Kafka.ProducerConfig { CompressionType = Confluent.Kafka.CompressionType.Zstd });
    builder.Services.RegisterKafkaProducer<string, ResourceAcquired>(
        builder.Configuration.GetRequiredSection(KafkaConstants.SectionName).Get<KafkaConnection>(),
        new Confluent.Kafka.ProducerConfig { CompressionType = Confluent.Kafka.CompressionType.Zstd });
    builder.Services.RegisterKafkaProducer<string, PatientIDsAcquiredMessage>(
        builder.Configuration.GetRequiredSection(KafkaConstants.SectionName).Get<KafkaConnection>(),
        new Confluent.Kafka.ProducerConfig { CompressionType = Confluent.Kafka.CompressionType.Zstd });
    builder.Services.RegisterKafkaProducer<string, AuditEventMessage>(
        builder.Configuration.GetRequiredSection(KafkaConstants.SectionName).Get<KafkaConnection>(),
        new Confluent.Kafka.ProducerConfig { CompressionType = Confluent.Kafka.CompressionType.Zstd });
    
    builder.Services.AddTransient<IKafkaProducerFactory<string, AuditEventMessage>, KafkaProducerFactory<string, AuditEventMessage>>();
    builder.Services.AddTransient<IKafkaProducerFactory<string, object>, KafkaProducerFactory<string, object>>();
    builder.Services.AddTransient<IKafkaProducerFactory<string, string>, KafkaProducerFactory<string, string>>();
    builder.Services.AddTransient<IKafkaProducerFactory<string, DataAcquisitionRequested>, KafkaProducerFactory<string, DataAcquisitionRequested>>();
    builder.Services.AddTransient<IKafkaProducerFactory<string, PatientCensusScheduled>, KafkaProducerFactory<string, PatientCensusScheduled>>();
    builder.Services.AddTransient<IKafkaProducerFactory<string, ResourceAcquired>, KafkaProducerFactory<string, ResourceAcquired>>();
    builder.Services.AddTransient<IKafkaProducerFactory<string, PatientIDsAcquiredMessage>, KafkaProducerFactory<string, PatientIDsAcquiredMessage>>();


    //Factories - Retry
    builder.Services.AddTransient<IRetryEntityFactory, RetryEntityFactory>();
    builder.Services.AddTransient<ISchedulerFactory, StdSchedulerFactory>();
    builder.Services.AddTransient<RetryJob>();
    builder.Services.AddScoped<IJobFactory, JobFactory>(); 

    //Add Hosted Services
    if (!consumerSettings?.DisableConsumer ?? true)
    {
        builder.Services.AddHostedService<DataAcquisitionRequestedListener>();
        builder.Services.AddHostedService<PatientCensusScheduledListener>();
    }

    if (!consumerSettings?.DisableRetryConsumer ?? true)
    {
        
        builder.Services.AddSingleton(new RetryListenerSettings(DataAcquisitionConstants.ServiceName, [ KafkaTopic.DataAcquisitionRequestedRetry.GetStringValue(), KafkaTopic.PatientCensusScheduledRetry.GetStringValue() ]));
        builder.Services.AddHostedService<RetryListener>();
        builder.Services.AddHostedService<RetryScheduleService>();
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
    });    

    //Add CORS
    builder.Services.AddLinkCorsService(options => {
        options.Environment = builder.Environment;
    });

    //Add telemetry if enabled
    builder.Services.AddLinkTelemetry(builder.Configuration, options =>
    {
        options.Environment = builder.Environment;
        options.ServiceName = DataAcquisitionConstants.ServiceName;
        options.ServiceVersion = serviceInformation.Version; //TODO: Get version from assembly?                
    });

    builder.Services.AddProblemDetails(options => {
        options.CustomizeProblemDetails = ctx =>
        {
            if(string.IsNullOrEmpty(ctx.ProblemDetails.Detail))
                ctx.ProblemDetails.Detail = "An error occurred in our API. Please use the trace id when requesting assistance.";

            if (!ctx.ProblemDetails.Extensions.ContainsKey("traceId"))
            {
                string? traceId = Activity.Current?.Id ?? ctx.HttpContext.TraceIdentifier;
                ctx.ProblemDetails.Extensions.Add(new KeyValuePair<string, object?>("traceId", traceId));
            }
            if (builder.Environment.IsDevelopment())
            {
                ctx.ProblemDetails.Extensions.Add("service", "Data Acquisition");
            }
            else
            {
                ctx.ProblemDetails.Extensions.Remove("exception");
            }
        };
    });

    //Add Health Check
    var kafkaConnection = builder.Configuration.GetRequiredSection(KafkaConstants.SectionName).Get<KafkaConnection>();
    var kafkaHealthOptions = new KafkaHealthCheckConfiguration(kafkaConnection, DataAcquisitionConstants.ServiceName).GetHealthCheckOptions();

    builder.Services.AddHealthChecks()
        .AddDbContextCheck<DataAcquisitionDbContext>()
        .AddKafka(kafkaHealthOptions);

    builder.Services.AddSingleton(TimeProvider.System);
    builder.Services.AddSingleton<IDataAcquisitionServiceMetrics, DataAcquisitionServiceMetrics>();
}

#endregion

#region Set up middleware

static void SetupMiddleware(WebApplication app)
{
    app.ConfigureSwagger();

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
}

#endregion
