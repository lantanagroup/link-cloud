using Azure.Identity;
using FluentValidation;
using HealthChecks.UI.Client;
using LantanaGroup.Link.Account.Application.Interfaces.Infrastructure;
using LantanaGroup.Link.Account.Application.Interfaces.Presentation;
using LantanaGroup.Link.Account.Application.Validators;
using LantanaGroup.Link.Account.Domain.Entities;
using LantanaGroup.Link.Account.Infrastructure;
using LantanaGroup.Link.Account.Infrastructure.Extensions;
using LantanaGroup.Link.Account.Infrastructure.Health;
using LantanaGroup.Link.Account.Infrastructure.Logging;
using LantanaGroup.Link.Account.Infrastructure.Telemetry;
using LantanaGroup.Link.Account.Persistence;
using LantanaGroup.Link.Account.Persistence.Interceptors;
using LantanaGroup.Link.Account.Presentation.Endpoints.Role;
using LantanaGroup.Link.Account.Presentation.Endpoints.User;
using LantanaGroup.Link.Account.Settings;
using LantanaGroup.Link.Shared.Application.Extensions;
using LantanaGroup.Link.Shared.Application.Extensions.Security;
using LantanaGroup.Link.Shared.Application.Middleware;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using LantanaGroup.Link.Shared.Application.Services;
using LantanaGroup.Link.Shared.Settings;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Compliance.Classification;
using Microsoft.Extensions.Compliance.Redaction;
using Microsoft.Extensions.Configuration.AzureAppConfiguration;
using Serilog;
using Serilog.Enrichers.Span;
using System.Diagnostics;
using System.Reflection;
using System.Text;
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
    //Initialize activity source
    var version = Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? string.Empty;
    ServiceActivitySource.Initialize(version);

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

    //Add problem details
    builder.Services.AddProblemDetailsService(options =>
    {
        options.Environment = builder.Environment;
        options.ServiceName = AccountConstants.ServiceName;
        options.IncludeExceptionDetails = builder.Configuration.GetValue<bool>("ProblemDetails:IncludeExceptionDetails");
    });

    builder.Services.Configure<KafkaConnection>(builder.Configuration.GetRequiredSection(KafkaConstants.SectionName));    
    builder.Services.Configure<ServiceRegistry>(builder.Configuration.GetRequiredSection(ServiceRegistry.ConfigSectionName));
    builder.Services.Configure<CorsSettings>(builder.Configuration.GetSection(ConfigurationConstants.AppSettings.CORS));

    //add command and queries
    builder.Services.AddCommandAndQueries();

    // Add fluent validation
    builder.Services.AddValidatorsFromAssemblyContaining(typeof(UserValidator));

    //Add database context
    builder.Services.AddDbContext<AccountDbContext>((sp, options) => {

        var updateBaseEntityInterceptor = sp.GetRequiredService<UpdateBaseEntityInterceptor>();
        var dbProvider = builder.Configuration.GetValue<string>(AccountConstants.AppSettingsSectionNames.DatabaseProvider);
        switch (dbProvider)
        {
            case "SqlServer":
                string? connectionString = builder.Configuration.GetValue<string>(AccountConstants.AppSettingsSectionNames.DatabaseConnectionString);

                if (string.IsNullOrEmpty(connectionString))
                    throw new InvalidOperationException("Database connection string is null or empty.");

                options
                    .UseSqlServer(connectionString)
                    .AddInterceptors(updateBaseEntityInterceptor);

                break;
            default:
                throw new InvalidOperationException($"Database provider {dbProvider} is not supported.");
        }
    });          

    //Add health checks
    builder.Services.AddHealthChecks()
        .AddCheck<DatabaseHealthCheck>("Database");

    // Add tenant API service
    builder.Services.AddHttpClient();
    builder.Services.AddTransient<ITenantApiService, TenantApiService>();

    //Add endpoints
    builder.Services.AddTransient<IApi, UserEndpoints>();
    builder.Services.AddTransient<IApi, RoleEndpoints>();

    // Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen(c =>
    {
        var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
        var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
        c.IncludeXmlComments(xmlPath);
    });

    builder.Services.Configure<JsonOptions>(opt => opt.SerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles);

    //Add logging redaction
    builder.Logging.EnableRedaction();
    builder.Services.AddRedaction(x => {

        x.SetRedactor<StarRedactor>(new DataClassificationSet(DataTaxonomy.SensitiveData));

        var hmacKey = builder.Configuration.GetValue<string>("Logging:HmacKey");
        if (!string.IsNullOrEmpty(hmacKey))
        {
            x.SetHmacRedactor(opts => {
                opts.Key = Convert.ToBase64String(Encoding.UTF8.GetBytes(hmacKey));
                opts.KeyId = 808;
            }, new DataClassificationSet(DataTaxonomy.PiiData));
        }
    });

    // Logging using Serilog
    builder.Logging.AddSerilog();
    Log.Logger = new LoggerConfiguration()
                    .Filter.ByExcluding("RequestPath like '/health%'")
                    .Filter.ByExcluding("RequestPath like '/swagger%'")
                    //.Enrich.WithExceptionDetails()
                    .Enrich.FromLogContext()
                    .Enrich.WithSpan()
                    .Enrich.With<ActivityEnricher>()
                    .CreateLogger();

    //Serilog.Debugging.SelfLog.Enable(Console.Error);

    //Add CORS
    builder.Services.AddLinkCorsService(options => {
        options.Environment = builder.Environment;
    });

    //Add telemetry if enabled
    builder.Services.AddLinkTelemetry(builder.Configuration, options =>
    {
        options.Environment = builder.Environment;
        options.ServiceName = AccountConstants.ServiceName;
        options.ServiceVersion = ServiceActivitySource.Version;              
    });

    builder.Services.AddSingleton<IAccountServiceMetrics, AccountServiceMetrics>();
}

#endregion




#region Set up middleware

static void SetupMiddleware(WebApplication app)
{

    // Configure swagger
    if (app.Configuration.GetValue<bool>(AccountConstants.AppSettingsSectionNames.EnableSwagger))
    {
        app.UseSwagger(opts => { opts.RouteTemplate = "api/swagger/{documentname}/swagger.json"; });
        app.UseSwaggerUI(opts => {
            opts.SwaggerEndpoint("/api/swagger/v1/swagger.json", $"{ServiceActivitySource.ServiceName} - {ServiceActivitySource.Version}");
            opts.RoutePrefix = "api/swagger";
        });
    }

    app.UseRouting();
    app.UseCors(CorsSettings.DefaultCorsPolicyName);
    app.UseMiddleware<UserScopeMiddleware>();

    // Register endpoints
    app.MapGet("/api/account/info", () => Results.Ok($"Welcome to {ServiceActivitySource.ServiceName} version {ServiceActivitySource.Version}!")).AllowAnonymous();

    var apis = app.Services.GetServices<IApi>();
    foreach (var api in apis)
    {
        if (api is null) throw new InvalidProgramException("No Endpoints were registered.");
        api.RegisterEndpoints(app);
    }

    // Ensure database created
    using (var scope = app.Services.CreateScope())
    {
        var context = scope.ServiceProvider.GetRequiredService<AccountDbContext>();
        context.Database.EnsureCreated();
    }

    //map health check middleware
    app.MapHealthChecks("/health", new HealthCheckOptions
    {
        ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
    }).RequireCors("HealthCheckPolicy"); ;
}

#endregion