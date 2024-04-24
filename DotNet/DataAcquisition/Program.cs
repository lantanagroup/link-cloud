using Azure.Identity;
using Confluent.Kafka;
using HealthChecks.UI.Client;
using LantanaGroup.Link.DataAcquisition.Application.Interfaces;
using LantanaGroup.Link.DataAcquisition.Application.Models;
using LantanaGroup.Link.DataAcquisition.Application.Repositories;
using LantanaGroup.Link.DataAcquisition.Application.Repositories.FhirApi;
using LantanaGroup.Link.DataAcquisition.Application.Services;
using LantanaGroup.Link.DataAcquisition.Application.Services.Auth;
using LantanaGroup.Link.DataAcquisition.Application.Settings;
using LantanaGroup.Link.DataAcquisition.Domain;
using LantanaGroup.Link.DataAcquisition.HealthChecks;
using LantanaGroup.Link.DataAcquisition.Listeners;
using LantanaGroup.Link.DataAcquisition.Services;
using LantanaGroup.Link.DataAcquisition.Services.Auth;
using LantanaGroup.Link.Shared.Application.Error.Handlers;
using LantanaGroup.Link.Shared.Application.Error.Interfaces;
using LantanaGroup.Link.Shared.Application.Extensions;
using LantanaGroup.Link.Shared.Application.Factories;
using LantanaGroup.Link.Shared.Application.Interfaces;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using LantanaGroup.Link.Shared.Application.Models.Kafka;
using LantanaGroup.Link.Shared.Application.Repositories.Interceptors;
using LantanaGroup.Link.Shared.Application.Wrappers;
using LantanaGroup.Link.Shared.Settings;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration.AzureAppConfiguration;
using Serilog;
using Serilog.Enrichers.Span;
using Serilog.Exceptions;
using System.Net;
using System.Reflection;
using System.Text.Json.Serialization;

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

    builder.Services.Configure<ServiceRegistry>(builder.Configuration.GetSection(ServiceRegistry.ConfigSectionName));
    builder.Services.Configure<KafkaConnection>(builder.Configuration.GetRequiredSection(KafkaConstants.SectionName));
    builder.Services.Configure<MongoConnection>(builder.Configuration.GetRequiredSection(DataAcquisitionConstants.AppSettingsSectionNames.Mongo));

    builder.Services.AddTransient<UpdateBaseEntityInterceptor>();

    builder.Services.AddDbContext<DataAcquisitionDbContext>((sp, options) =>
    {

        var updateBaseEntityInterceptor = sp.GetService<UpdateBaseEntityInterceptor>()!;

        switch (builder.Configuration.GetValue<string>(DataAcquisitionConstants.AppSettingsSectionNames.DatabaseProvider))
        {
            case "SqlServer":
                options.UseSqlServer(
                    builder.Configuration.GetConnectionString(DataAcquisitionConstants.AppSettingsSectionNames.DatabaseConnection))
                   .AddInterceptors(updateBaseEntityInterceptor);
                break;
            default:
                throw new InvalidOperationException($"Database provider not supported. Attempting to find section named: {DataAcquisitionConstants.AppSettingsSectionNames.DatabaseProvider}");
        }
    });


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
    });
    builder.Services.AddGrpc();
    builder.Services.AddGrpcReflection();
    builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(Assembly.GetExecutingAssembly()));
    builder.Services.AddGrpcClient<LantanaGroup.Link.DataAcquisition.Tenant.TenantClient>(o =>
    {
        //TODO: figure out how to handle service url. Need some sort of service discovery.
        o.Address = new Uri("TBD on what to do here.");
    });

    //Add health checks
    builder.Services.AddHealthChecks()
        .AddCheck<DatabaseHealthCheck>("Database");

    builder.Services.AddSingleton<EpicAuth>();
    builder.Services.AddSingleton<BasicAuth>();
    builder.Services.AddScoped<IAuthenticationRetrievalService, AuthenticationRetrievalService>();

    builder.Services.AddSingleton<IDeadLetterExceptionHandler<string, string>, DeadLetterExceptionHandler<string, string>>();

    builder.Services.AddSingleton<DataAcqTenantConfigMongoRepo>();
    builder.Services.AddSingleton<IFhirQueryConfigurationRepository,FhirQueryConfigurationRepository>();
    builder.Services.AddSingleton<IFhirQueryListConfigurationRepository,FhirQueryListConfigurationRepository>();
    builder.Services.AddSingleton<IQueryPlanRepository,QueryPlanRepository>();
    builder.Services.AddSingleton<IReferenceResourcesRepository,ReferenceResourcesRepository>();
    builder.Services.AddSingleton<IFhirApiRepository,FhirApiRepository>();
    builder.Services.AddSingleton<IQueriedFhirResourceRepository,QueriedFhirResourceRepository>();
    
    builder.Services.AddScoped<IKafkaConsumerFactory<string, string>, KafkaConsumerFactory<string, string>>();
    builder.Services.AddScoped<IKafkaProducerFactory<string, object>, KafkaProducerFactory<string, object>>();
    builder.Services.AddScoped<IKafkaProducerFactory<string, string>, KafkaProducerFactory<string, string>>();
    builder.Services.AddScoped<IKafkaProducerFactory<string, AuditEventMessage>, KafkaProducerFactory<string, AuditEventMessage>>();
    builder.Services.AddScoped<IKafkaWrapper<string, string, string, object>, DataAcquisitionKafkaService<string, string, string, object>>();
    builder.Services.AddScoped<IKafkaWrapper<Ignore, Ignore, string, string>, KafkaWrapper<Ignore, Ignore, string, string>>();

    //Add Hosted Services
    builder.Services.AddHostedService<QueryListener>();

    // Logging using Serilog
    builder.Logging.AddSerilog();
    Log.Logger = new LoggerConfiguration()
        .ReadFrom.Configuration(builder.Configuration)
        .Enrich.WithExceptionDetails()
        .Enrich.FromLogContext()
        .Enrich.WithSpan()
        .Enrich.With<ActivityEnricher>()
        .CreateLogger();

    Serilog.Debugging.SelfLog.Enable(Console.Error);

    builder.Services.AddSwaggerGen(c =>
    {
        var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
        var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
        c.IncludeXmlComments(xmlPath);
    });

    //Add telemetry if enabled
    builder.Services.AddLinkTelemetry(builder.Configuration, options =>
    {
        options.Environment = builder.Environment;
        options.ServiceName = DataAcquisitionConstants.ServiceName;
        options.ServiceVersion = serviceInformation.Version; //TODO: Get version from assembly?                
    });          

    builder.Services.AddSingleton(TimeProvider.System);
    builder.Services.AddSingleton<IDataAcquisitionServiceMetrics, DataAcquisitionServiceMetrics>();
}

#endregion

#region Set up middleware

static void SetupMiddleware(WebApplication app)
{
    app.UseSwagger();
    app.UseSwaggerUI();

    if (app.Configuration.GetValue<bool>("AllowReflection"))
    {
        app.MapGrpcReflectionService();
    }

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
