using Azure.Identity;
using FluentValidation;
using HealthChecks.UI.Client;
using LantanaGroup.Link.Account.Application.Interfaces.Infrastructure;
using LantanaGroup.Link.Account.Application.Interfaces.Persistence;
using LantanaGroup.Link.Account.Application.Interfaces.Presentation;
using LantanaGroup.Link.Account.Application.Models;
using LantanaGroup.Link.Account.Application.Validators;
using LantanaGroup.Link.Account.Infrastructure;
using LantanaGroup.Link.Account.Infrastructure.Extensions;
using LantanaGroup.Link.Account.Infrastructure.Health;
using LantanaGroup.Link.Account.Infrastructure.Logging;
using LantanaGroup.Link.Account.Infrastructure.Telemetry;
using LantanaGroup.Link.Account.Persistence;
using LantanaGroup.Link.Account.Persistence.Interceptors;
using LantanaGroup.Link.Account.Persistence.Repositories;
using LantanaGroup.Link.Account.Presentation.Endpoints.Claims;
using LantanaGroup.Link.Account.Presentation.Endpoints.Role;
using LantanaGroup.Link.Account.Presentation.Endpoints.User;
using LantanaGroup.Link.Account.Settings;
using LantanaGroup.Link.Shared.Application.Extensions;
using LantanaGroup.Link.Shared.Application.Extensions.Caching;
using LantanaGroup.Link.Shared.Application.Extensions.ExternalServices;
using LantanaGroup.Link.Shared.Application.Extensions.Security;
using LantanaGroup.Link.Shared.Application.Health;
using LantanaGroup.Link.Shared.Application.Interfaces;
using LantanaGroup.Link.Shared.Application.Middleware;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using LantanaGroup.Link.Shared.Application.Services;
using LantanaGroup.Link.Shared.Settings;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Compliance.Classification;
using Microsoft.Extensions.Compliance.Redaction;
using Microsoft.Extensions.Configuration.AzureAppConfiguration;
using Microsoft.OpenApi.Models;
using Serilog;
using Serilog.Enrichers.Span;
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

    //Add IOptions
    builder.Services.Configure<KafkaConnection>(builder.Configuration.GetRequiredSection(KafkaConstants.SectionName));
    var kafkaConnection = builder.Configuration.GetSection(KafkaConstants.SectionName).Get<KafkaConnection>();
    builder.Services.AddSingleton<KafkaConnection>(kafkaConnection);
    builder.Services.Configure<ServiceRegistry>(builder.Configuration.GetRequiredSection(ServiceRegistry.ConfigSectionName));
    builder.Services.Configure<CorsSettings>(builder.Configuration.GetSection(ConfigurationConstants.AppSettings.CORS));
    builder.Services.Configure<LinkTokenServiceSettings>(builder.Configuration.GetSection(ConfigurationConstants.AppSettings.LinkTokenService));
    builder.Services.Configure<UserManagementSettings>(builder.Configuration.GetSection(AccountConstants.AppSettingsSectionNames.UserManagement));

    //add factories
    builder.Services.AddFactories(kafkaConnection);

    //add command and queries
    builder.Services.AddCommandAndQueries();

    // Add fluent validation
    builder.Services.AddValidatorsFromAssemblyContaining(typeof(UserValidator));

    //Add data protection
    builder.Services.AddLinkDataProtection(options =>
    {
        options.Environment = builder.Environment;
        options.KeyRing = builder.Configuration.GetValue<string>("DataProtection:KeyRing") ?? "Link";
    });

    var cacheType = builder.Configuration.GetValue<string>("Cache:Type") ?? "InMemory"; 
    var supportedCacheTypes = new[] { "Redis", "InMemory" }; 
    if (!supportedCacheTypes.Contains(cacheType)) 
    { 
        Log.Logger.Warning("Unsupported cache type '{CacheType}'. Defaulting to InMemory cache.", cacheType); 
        cacheType = "InMemory";
    }
    if (cacheType == "Redis")
    {
        builder.Services.AddRedisCache(options =>
        {
            options.Environment = builder.Environment;

            var redisConnection = builder.Configuration.GetConnectionString("Redis");

            if (string.IsNullOrEmpty(redisConnection))
                throw new NullReferenceException("Redis Connection String is required.");

            options.ConnectionString = redisConnection;
            options.Password = builder.Configuration.GetValue<string>("Redis:Password");
        });
        builder.Services.AddSingleton<ICacheService, RedisCacheService>();
    }
    else // defaults to InMemory cache
    {
        Log.Logger.Warning("InMemory Cache is enabled.");
        builder.Services.AddMemoryCache();

        builder.Services.AddSingleton<ICacheService, InMemoryCacheService>();
    }


    // Add Secret Manager
    if (builder.Configuration.GetValue<bool>("SecretManagement:Enabled"))
    {
        builder.Services.AddSecretManager(options =>
        {
            options.Manager = builder.Configuration.GetValue<string>("SecretManagement:Manager")!;
        });
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

    //Add persistence interceptors
    builder.Services.AddSingleton<UpdateBaseEntityInterceptor>();

    //Add database context
    builder.Services.AddDbContext<AccountDbContext>((sp, options) => {

        var updateBaseEntityInterceptor = sp.GetRequiredService<UpdateBaseEntityInterceptor>();
        var dbProvider = builder.Configuration.GetValue<string>(AccountConstants.AppSettingsSectionNames.DatabaseProvider);
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
                throw new InvalidOperationException($"Database provider {dbProvider} is not supported.");
        }
    });

    //Add repositories
    builder.Services.AddScoped<IUserRepository, UserRepository>();
    builder.Services.AddScoped<IUserSearchRepository, UserSearchRepository>();
    builder.Services.AddScoped<IRoleRepository, RoleRepository>();

    //Add health checks
    var kafkaHealthOptions = new KafkaHealthCheckConfiguration(kafkaConnection, AccountConstants.ServiceName).GetHealthCheckOptions();

    builder.Services.AddHealthChecks()
        .AddCheck<DatabaseHealthCheck>("Database")
        .AddKafka(kafkaHealthOptions);

    // Add tenant API service
    builder.Services.AddHttpClient();
    builder.Services.AddTransient<ITenantApiService, TenantApiService>();

    //Add endpoints
    builder.Services.AddTransient<IApi, UserEndpoints>();
    builder.Services.AddTransient<IApi, RoleEndpoints>();
    builder.Services.AddTransient<IApi, ClaimsEndpoints>();

    // Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
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
        .ReadFrom.Configuration(builder.Configuration)
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
    app.AutoMigrateEF<AccountDbContext>();

    if (app.Environment.IsDevelopment())
    {
        app.UseDeveloperExceptionPage();
    }
    else
    {
        app.UseExceptionHandler();
    }

    // Configure swagger
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

    // Register endpoints
    app.MapGet("/api/account/info", () => Results.Ok($"Welcome to {ServiceActivitySource.ServiceName} version {ServiceActivitySource.Version}!")).AllowAnonymous();

    var apis = app.Services.GetServices<IApi>();
    foreach (var api in apis)
    {
        if (api is null) throw new InvalidProgramException("No Endpoints were registered.");
        api.RegisterEndpoints(app);
    }

    //map health check middleware
    app.MapHealthChecks("/health", new HealthCheckOptions
    {
        ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
    }).RequireCors("HealthCheckPolicy");
}

#endregion
