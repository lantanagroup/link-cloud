using Azure.Identity;
using Confluent.Kafka.Extensions.OpenTelemetry;
using HealthChecks.UI.Client;
using LantanaGroup.Link.Account.Domain.Entities;
using LantanaGroup.Link.Account.Infrastructure.Health;
using LantanaGroup.Link.Account.Repositories;
using LantanaGroup.Link.Account.Services;
using LantanaGroup.Link.Account.Settings;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using LantanaGroup.Link.Shared.Application.Services;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.Extensions.Configuration.AzureAppConfiguration;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;
using System.Reflection;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

// Additional configuration is required to successfully run gRPC on macOS.
// For instructions on how to configure Kestrel and gRPC clients on macOS, visit https://go.microsoft.com/fwlink/?linkid=2099682

RegisterServices(builder);
var app = builder.Build();
SetupMiddleware(app);

app.Run();



#region Register Services

static void RegisterServices(WebApplicationBuilder builder)
{
    //load external configuration source if specified
    var externalConfigurationSource = builder.Configuration.GetSection(AccountConstants.AppSettingsSectionNames.ExternalConfigurationSource).Get<string>();

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
                            .Select("*", AccountConstants.ServiceName)
                            // Load configuration values for service name and environment
                            .Select("*", AccountConstants.ServiceName + ":" + builder.Environment.EnvironmentName);

                    options.ConfigureKeyVault(kv =>
                    {
                        kv.SetCredential(new DefaultAzureCredential());
                    });

                });
                break;
        }
    }

    var serviceInformation = builder.Configuration.GetRequiredSection(AccountConstants.AppSettingsSectionNames.ServiceInformation).Get<ServiceInformation>();
    if (serviceInformation != null)
    {
        ServiceActivitySource.Initialize(serviceInformation);
        Counters.Initialize(serviceInformation);
    }
    else
    {
        throw new NullReferenceException("Service Information was null.");
    }

    builder.Services.Configure<KafkaConnection>(builder.Configuration.GetRequiredSection(AccountConstants.AppSettingsSectionNames.Kafka));
    builder.Services.Configure<PostgresConnection>(builder.Configuration.GetRequiredSection(AccountConstants.AppSettingsSectionNames.Postgres));
    builder.Services.AddDbContext<DataContext>();
    //builder.Services.AddDbContext<TestDataContext>();
    builder.Services.AddSingleton(builder.Configuration.GetRequiredSection(AccountConstants.AppSettingsSectionNames.TenantApiSettings).Get<TenantApiSettings>() ?? new TenantApiSettings());

    // Add services to the container.
    // Additional configuration is required to successfully run gRPC on macOS.
    // For instructions on how to configure Kestrel and gRPC clients on macOS, visit https://go.microsoft.com/fwlink/?linkid=2099682
    builder.Services.AddGrpc().AddJsonTranscoding();
    builder.Services.AddGrpcReflection();

    // Add repositories
    builder.Services.AddScoped<AccountRepository>();
    builder.Services.AddScoped<GroupRepository>();
    builder.Services.AddScoped<RoleRepository>();

    // Add tenant API service
    builder.Services.AddHttpClient();
    builder.Services.AddTransient<ITenantApiService, TenantApiService>();

    //Add health checks
    builder.Services.AddHealthChecks()
        .AddCheck<DatabaseHealthCheck>("Database");

    // Add controllers
    builder.Services.AddControllers().AddJsonOptions(x => x.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles);

    // Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen(c =>
    {
        var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
        var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
        c.IncludeXmlComments(xmlPath);
    });
    builder.Services.AddGrpcSwagger();


    builder.Services.Configure<JsonOptions>(opt => opt.SerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles);

    // Logging using Serilog
    builder.Logging.AddSerilog();
    Log.Logger = new LoggerConfiguration()
                    .ReadFrom.Configuration(builder.Configuration)
                    .CreateLogger();

    Serilog.Debugging.SelfLog.Enable(Console.Error);

    var telemetryConfig = builder.Configuration.GetRequiredSection(AccountConstants.AppSettingsSectionNames.Telemetry).Get<TelemetryConfig>();
    if (telemetryConfig != null)
    {
        var otel = builder.Services.AddOpenTelemetry();

        //configure OpenTelemetry resources with application name
        otel.ConfigureResource(resource => resource
            .AddService(ServiceActivitySource.Instance.Name, ServiceActivitySource.Instance.Version));

        otel.WithTracing(tracerProviderBuilder =>
                tracerProviderBuilder
                    .AddSource(ServiceActivitySource.Instance.Name)
                    .AddAspNetCoreInstrumentation(options =>
                    {
                        options.Filter = (httpContext) => httpContext.Request.Path != "/health"; //do not capture traces for the health check endpoint
                    })
                    .AddConfluentKafkaInstrumentation()
                    .AddOtlpExporter(opts => { opts.Endpoint = new Uri(telemetryConfig.TelemetryCollectorEndpoint); }));

        otel.WithMetrics(metricsProviderBuilder =>
                metricsProviderBuilder
                    .AddAspNetCoreInstrumentation()
                    .AddRuntimeInstrumentation()
                    .AddMeter(Counters.meter.Name)
                    .AddOtlpExporter(opts => { opts.Endpoint = new Uri(telemetryConfig.TelemetryCollectorEndpoint); }));

        if (builder.Environment.IsDevelopment())
        {
            otel.WithTracing(tracerProviderBuilder =>
                tracerProviderBuilder
                .AddConsoleExporter());

            //metrics are very verbose, only enable console exporter if you really want to see metric details
            //otel.WithMetrics(metricsProviderBuilder =>
            //    metricsProviderBuilder
            //        .AddConsoleExporter());                
        }
    }
}

#endregion




#region Set up middleware

static void SetupMiddleware(WebApplication app)
{

    if (app.Environment.IsDevelopment() || app.Environment.EnvironmentName.ToLower() == "local" || app.Configuration.GetValue<bool>("EnableSwagger"))
    {
        app.UseSwagger();
        app.UseSwaggerUI();
    }

    app.UseRouting();
    app.MapControllers();

    if (app.Configuration.GetValue<bool>("AllowReflection"))
    {
        app.MapGrpcReflectionService();
    }

    // Map gRPC services
    app.MapGrpcService<AccountService>();
    app.MapGrpcService<GroupService>();
    app.MapGrpcService<RoleService>();
    app.MapGet("/", () => "Communication with gRPC endpoints must be made through a gRPC client. To learn how to create a client, visit: https://go.microsoft.com/fwlink/?linkid=2086909");

    app.MapGet("/api/account/email/{email}", async (AccountRepository accountRepository, string email) =>
    {
        AccountModel? account = await accountRepository.GetAccountByEmailAsync(email);
        return account;
    });

    // Ensure database created
    using (var scope = app.Services.CreateScope())
    {
        var context = scope.ServiceProvider.GetRequiredService<DataContext>();
        context.Database.EnsureCreated();
    }

    //map health check middleware
    app.MapHealthChecks("/health", new HealthCheckOptions
    {
        ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
    });
}

#endregion