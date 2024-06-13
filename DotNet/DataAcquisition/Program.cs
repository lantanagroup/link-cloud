using Azure.Identity;
using DataAcquisition.Domain.Extensions;
using HealthChecks.UI.Client;
using LantanaGroup.Link.DataAcquisition.Application.Interfaces;
using LantanaGroup.Link.DataAcquisition.Application.Repositories;
using LantanaGroup.Link.DataAcquisition.Application.Repositories.FhirApi;
using LantanaGroup.Link.DataAcquisition.Application.Services;
using LantanaGroup.Link.DataAcquisition.Application.Services.Auth;
using LantanaGroup.Link.DataAcquisition.Application.Settings;
using LantanaGroup.Link.DataAcquisition.Domain;
using LantanaGroup.Link.DataAcquisition.Listeners;
using LantanaGroup.Link.DataAcquisition.Services;
using LantanaGroup.Link.DataAcquisition.Services.Auth;
using LantanaGroup.Link.Shared.Application.Error.Handlers;
using LantanaGroup.Link.Shared.Application.Error.Interfaces;
using LantanaGroup.Link.Shared.Application.Extensions;
using LantanaGroup.Link.Shared.Application.Extensions.Security;
using LantanaGroup.Link.Shared.Application.Factories;
using LantanaGroup.Link.Shared.Application.Interfaces;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using LantanaGroup.Link.Shared.Application.Models.Kafka;
using LantanaGroup.Link.Shared.Application.Repositories.Interceptors;
using LantanaGroup.Link.Shared.Application.Repositories.Interfaces;
using LantanaGroup.Link.Shared.Settings;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration.AzureAppConfiguration;
using Quartz.Impl;
using Quartz;
using Serilog;
using Serilog.Enrichers.Span;
using System.Net;
using System.Reflection;
using System.Text.Json.Serialization;
using System.Diagnostics;
using Serilog.Settings.Configuration;
using LantanaGroup.Link.Shared.Application.Services;
using Quartz.Spi;
using LantanaGroup.Link.DataAcquisition.Application.Factories;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Jobs;
using LantanaGroup.Link.DataAcquisition.Application.Serializers;
using LantanaGroup.Link.DataAcquisition.Application.Models.Kafka;

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

    var serviceInformation = builder.Configuration.GetSection(DataAcquisitionConstants.AppSettingsSectionNames.ServiceInformation).Get<ServiceInformation>();

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
    builder.Services.Configure<KafkaConnection>(builder.Configuration.GetRequiredSection(KafkaConstants.SectionName));
    builder.Services.Configure<ConsumerSettings>(builder.Configuration.GetRequiredSection(nameof(ConsumerSettings)));
    builder.Services.Configure<CorsSettings>(builder.Configuration.GetSection(ConfigurationConstants.AppSettings.CORS));

    IConfigurationSection consumerSettingsSection = builder.Configuration.GetSection(nameof(ConsumerSettings));
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
        options.JsonSerializerOptions.Converters.Add(new QueryPlanResultConverter());
    });
    builder.Services.AddGrpc();
    builder.Services.AddGrpcReflection();
    builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(Assembly.GetExecutingAssembly()));
    builder.Services.AddGrpcClient<LantanaGroup.Link.DataAcquisition.Tenant.TenantClient>(o =>
    {
        //TODO: figure out how to handle service url. Need some sort of service discovery.
        o.Address = new Uri("TBD on what to do here.");
    });

    //Fhir Authentication Handlers
    builder.Services.AddSingleton<EpicAuth>();
    builder.Services.AddSingleton<BasicAuth>();
    builder.Services.AddSingleton<IAuthenticationRetrievalService, AuthenticationRetrievalService>();

    //Exception Handlers
    builder.Services.AddSingleton<IDeadLetterExceptionHandler<string, string>, DeadLetterExceptionHandler<string, string>>();
    builder.Services.AddSingleton<IDeadLetterExceptionHandler<string, DataAcquisitionRequested>, DeadLetterExceptionHandler<string, DataAcquisitionRequested>>();
    builder.Services.AddSingleton<IDeadLetterExceptionHandler<string, PatientCensusScheduled>, DeadLetterExceptionHandler<string, PatientCensusScheduled>>();
    builder.Services.AddTransient<ITransientExceptionHandler<string, string>, TransientExceptionHandler<string, string>>();
    builder.Services.AddTransient<ITransientExceptionHandler<string, DataAcquisitionRequested>, TransientExceptionHandler<string, DataAcquisitionRequested>>();
    builder.Services.AddTransient<ITransientExceptionHandler<string, PatientCensusScheduled>, TransientExceptionHandler<string, PatientCensusScheduled>>();

    //Repositories
    builder.Services.AddScoped<IFhirQueryConfigurationRepository,FhirQueryConfigurationRepository>();
    builder.Services.AddScoped<IFhirQueryListConfigurationRepository,FhirQueryListConfigurationRepository>();
    builder.Services.AddScoped<IQueryPlanRepository,QueryPlanRepository>();
    builder.Services.AddScoped<IReferenceResourcesRepository,ReferenceResourcesRepository>();
    builder.Services.AddScoped<IFhirApiRepository,FhirApiRepository>();
    builder.Services.AddScoped<IQueriedFhirResourceRepository,QueriedFhirResourceRepository>();
    builder.Services.AddScoped<IRetryRepository, RetryRepository_SQL_DataAcq>();

    //Factories
    builder.Services.AddScoped<IKafkaConsumerFactory<string, string>, KafkaConsumerFactory<string, string>>();
    builder.Services.AddScoped<IKafkaConsumerFactory<string, DataAcquisitionRequested>, KafkaConsumerFactory<string, DataAcquisitionRequested>>();
    builder.Services.AddScoped<IKafkaConsumerFactory<string, PatientCensusScheduled>, KafkaConsumerFactory<string, PatientCensusScheduled>>();
    builder.Services.AddScoped<IKafkaProducerFactory<string, object>, KafkaProducerFactory<string, object>>();
    builder.Services.AddScoped<IKafkaProducerFactory<string, string>, KafkaProducerFactory<string, string>>();
    builder.Services.AddScoped<IKafkaProducerFactory<string, DataAcquisitionRequested>, KafkaProducerFactory<string, DataAcquisitionRequested>>();
    builder.Services.AddScoped<IKafkaProducerFactory<string, PatientCensusScheduled>, KafkaProducerFactory<string, PatientCensusScheduled>>();
    builder.Services.AddScoped<IKafkaProducerFactory<string, ResourceAcquired>, KafkaProducerFactory<string, ResourceAcquired>>();
    builder.Services.AddScoped<IKafkaProducerFactory<string, PatientIDsAcquiredMessage>, KafkaProducerFactory<string, PatientIDsAcquiredMessage>>();
    builder.Services.AddScoped<IKafkaProducerFactory<string, AuditEventMessage>, KafkaProducerFactory<string, AuditEventMessage>>();
    builder.Services.AddTransient<IRetryEntityFactory, RetryEntityFactory>();
    builder.Services.AddTransient<ISchedulerFactory, StdSchedulerFactory>();
    builder.Services.AddTransient<RetryJob>();
    builder.Services.AddScoped<IJobFactory, JobFactory>();

    //Custom Logic
    builder.Services.AddTransient<IConsumerCustomLogic<string, DataAcquisitionRequested, string, ResourceAcquired>, DataAcquisitionRequestedProcessingLogic>();
    builder.Services.AddTransient<IConsumerCustomLogic<string, PatientCensusScheduled, string, PatientIDsAcquiredMessage>, PatientCensusScheduledProcessingLogic>();

    //Add Hosted Services
    if(consumerSettings == null || !consumerSettings.DisableConsumer)
    {
        builder.Services.AddHostedService<BaseListener<DataAcquisitionRequested, string, DataAcquisitionRequested, string, ResourceAcquired>>();
        builder.Services.AddHostedService<BaseListener<PatientCensusScheduled, string, PatientCensusScheduled, string, PatientIDsAcquiredMessage>>();
    }

    if(consumerSettings == null || !consumerSettings.DisableRetryConsumer)
    {
        builder.Services.AddHostedService<RetryListener>();
        builder.Services.AddHostedService<RetryScheduleService>();
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

    //Serilog.Debugging.SelfLog.Enable(Console.Error);

    builder.Services.AddSwaggerGen(c =>
    {
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
            ctx.ProblemDetails.Detail = "An error occured in our API. Please use the trace id when requesting assistence.";
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
    builder.Services.AddHealthChecks()
        .AddDbContextCheck<DataAcquisitionDbContext>();

    builder.Services.AddSingleton(TimeProvider.System);
    builder.Services.AddSingleton<IDataAcquisitionServiceMetrics, DataAcquisitionServiceMetrics>();
}

#endregion

#region Set up middleware

static void SetupMiddleware(WebApplication app)
{
    app.ConfigureSwagger();

    app.AutoMigrateEF<DataAcquisitionDbContext>();

    if (app.Configuration.GetValue<bool>("AllowReflection"))
    {
        app.MapGrpcReflectionService();
    }

    if (app.Environment.IsDevelopment())
    {
        app.UseDeveloperExceptionPage();
    }
    else
    {
        app.UseExceptionHandler();
    }

    app.UseCors(CorsSettings.DefaultCorsPolicyName);

    // Configure the HTTP request pipeline.
    app.MapGrpcService<DataAcquisitionService>();
    app.MapGet("/", () => "Communication with gRPC endpoints must be made through a gRPC client. To learn how to create a client, visit: https://go.microsoft.com/fwlink/?linkid=2086909");
    app.MapControllers();

    //map health check middleware
    app.MapHealthChecks("/health", new HealthCheckOptions
    {
        ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
    });
}

#endregion
