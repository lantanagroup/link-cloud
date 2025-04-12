using HealthChecks.UI.Client;
using LantanaGroup.Link.LinkAdmin.BFF.Application.Commands.Integration;
using LantanaGroup.Link.LinkAdmin.BFF.Infrastructure;
using LantanaGroup.Link.LinkAdmin.BFF.Infrastructure.Extensions;
using LantanaGroup.Link.LinkAdmin.BFF.Presentation.Endpoints;
using LantanaGroup.Link.LinkAdmin.BFF.Settings;
using LantanaGroup.Link.LinkAdmin.BFF.Application.Validation;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using FluentValidation;
using Serilog;
using Serilog.Enrichers.Span;
using Serilog.Exceptions;
using Serilog.Settings.Configuration;
using System.Reflection;
using Microsoft.OpenApi.Models;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.DataProtection;
using LantanaGroup.Link.LinkAdmin.BFF.Application.Commands.Security;
using LantanaGroup.Link.LinkAdmin.BFF.Application.Models.Configuration;
using LantanaGroup.Link.LinkAdmin.BFF.Application.Interfaces.Services;
using LantanaGroup.Link.LinkAdmin.BFF.Infrastructure.Extensions.Security;
using LantanaGroup.Link.LinkAdmin.BFF.Infrastructure.Extensions.ExternalServices;
using LantanaGroup.Link.LinkAdmin.BFF.Infrastructure.Extensions.Telemetry;
using LantanaGroup.Link.Shared.Application.Extensions;
using LantanaGroup.Link.Shared.Settings;
using LantanaGroup.Link.LinkAdmin.BFF.Application.Interfaces.Infrastructure;
using LantanaGroup.Link.LinkAdmin.BFF.Infrastructure.Telemetry;
using LantanaGroup.Link.Shared.Application.Middleware;
using LantanaGroup.Link.Shared.Application.Extensions.ExternalServices;
using LantanaGroup.Link.Shared.Application.Extensions.Security;
using Microsoft.AspNetCore.HttpOverrides;
using LantanaGroup.Link.LinkAdmin.BFF.Infrastructure.Health;
using LantanaGroup.Link.LinkAdmin.BFF.Presentation.Endpoints.Aggregation;
using LantanaGroup.Link.LinkAdmin.BFF.Presentation.Endpoints.System;
using LantanaGroup.Link.Shared.Application.Interfaces;
using LantanaGroup.Link.Shared.Application.Extensions.Caching;
using LantanaGroup.Link.Shared.Application.Models;

var builder = WebApplication.CreateBuilder(args);

RegisterServices(builder);

var app = builder.Build();
SetupMiddleware(app);

app.Run();

#region Register Services
static void RegisterServices(WebApplicationBuilder builder)
{
    // load external configuration source if specified
    var externalConfigurationSource = builder.Configuration.GetSection(LinkAdminConstants.AppSettingsSectionNames.ExternalConfigurationSource).Get<string>();
    if (!string.IsNullOrEmpty(externalConfigurationSource))
    {
        builder.AddExternalConfiguration(options =>
        {
            options.ExternalConfigurationSource = externalConfigurationSource;
            options.ExternalConfigurationConnectionString = builder.Configuration.GetConnectionString("AzureAppConfiguration");
            options.Environment = builder.Environment;
        });
    }

    // Logging using Serilog    
    builder.Logging.AddSerilog();
    var loggerOptions = new ConfigurationReaderOptions { SectionName = LinkAdminConstants.AppSettingsSectionNames.Serilog };
    Log.Logger = new LoggerConfiguration()
                    .ReadFrom.Configuration(builder.Configuration, loggerOptions)
                    .Filter.ByExcluding("RequestPath like '/health%'")
                    .Filter.ByExcluding("RequestPath like '/swagger%'")
                    .Enrich.WithExceptionDetails()
                    .Enrich.FromLogContext()
                    .Enrich.WithSpan()
                    .Enrich.With<ActivityEnricher>()
                    .CreateLogger();

    //Serilog.Debugging.SelfLog.Enable(Console.Error);

    //Initialize activity source
    var serviceInformation = builder.Configuration.GetRequiredSection(LinkAdminConstants.AppSettingsSectionNames.ServiceInformation).Get<ServiceInformation>();
    ServiceActivitySource.Initialize(serviceInformation);

    // Add problem details
    builder.Services.AddProblemDetailsService(options =>
    {
        options.Environment = builder.Environment;
        options.ServiceName = LinkAdminConstants.ServiceName;
        options.IncludeExceptionDetails = builder.Configuration.GetValue<bool>("ProblemDetails:IncludeExceptionDetails");
    });

    // Add IOptions
    builder.Services.Configure<SecretManagerSettings>(builder.Configuration.GetSection(ConfigurationConstants.AppSettings.SecretManagement));
    builder.Services.Configure<DataProtectionSettings>(builder.Configuration.GetSection(ConfigurationConstants.AppSettings.DataProtection));
    builder.Services.Configure<ServiceRegistry>(builder.Configuration.GetSection(ServiceRegistry.ConfigSectionName));
    builder.Services.Configure<LinkTokenServiceSettings>(builder.Configuration.GetSection(ConfigurationConstants.AppSettings.LinkTokenService));
    builder.Services.Configure<CacheSettings>(builder.Configuration.GetSection(ConfigurationConstants.AppSettings.Cache));

    // Determine if anonymous access is allowed
    var allowAnonymousAccess = builder.Configuration.GetValue<bool>("Authentication:EnableAnonymousAccess");

    // Add kafka connection singleton
    var kafkaConnection = builder.Configuration.GetSection(KafkaConstants.SectionName).Get<KafkaConnection>();
    if (kafkaConnection is null) throw new NullReferenceException("Kafka Connection is required.");
    builder.Services.AddSingleton(kafkaConnection);

    // Add Kafka Producer Factories
    builder.Services.RegisterKafkaProducer<string, object>(kafkaConnection, new Confluent.Kafka.ProducerConfig { CompressionType = Confluent.Kafka.CompressionType.Zstd });

    // Add fluent validation
    builder.Services.AddValidatorsFromAssemblyContaining(typeof(PatientEventValidator));

    // Add HttpClientFactory and Clients
    builder.Services.AddHttpClient();
    builder.Services.AddLinkClients();

    // Add data protection
    builder.Services.AddDataProtection().SetApplicationName(builder.Configuration.GetValue<string>("DataProtection:KeyRing") ?? "Link");
    //TODO: https://learn.microsoft.com/en-us/aspnet/core/security/data-protection/configuration/overview?view=aspnetcore-8.0

    // Add commands
    builder.Services.AddTransient<ICreatePatientEvent, CreatePatientEvent>();
    builder.Services.AddTransient<ICreatePatientAcquired, CreatePatientAcquired>();
    builder.Services.AddTransient<ICreateReportScheduled, CreateReportScheduled>();
    builder.Services.AddTransient<ICreateDataAcquisitionRequested, CreateDataAcquisitionRequested>();
    builder.Services.AddTransient<IGetLinkAccount, GetLinkAccount>();
    if (!allowAnonymousAccess) { 
        builder.Services.AddTransient<ICreateLinkBearerToken, CreateLinkBearerToken>();
        builder.Services.AddTransient<IRefreshSigningKey, RefreshSigningKey>();
    }
   
    builder.Services.AddTransient<KafkaConsumerManager>();
    builder.Services.AddTransient<KafkaConsumerService>();

    //Add Redis   
    var cacheType = builder.Configuration.GetValue<string>("Cache:Type");
    if (cacheType == "Redis")
    {
        Log.Logger.Information("Registering Redis Cache for the Link Admin API.");
        builder.Services.AddRedisCache(options =>
        {
            options.Environment = builder.Environment;

            var redisConnection = builder.Configuration.GetConnectionString("Redis");

            if (string.IsNullOrEmpty(redisConnection))
                throw new NullReferenceException("Redis Connection String is required.");

            options.ConnectionString = redisConnection;
            options.Password = builder.Configuration.GetValue<string>("Redis:Password");

            if (builder.Configuration.GetValue<int>("Cache:Timeout") > 0)
            {
                options.Timeout = builder.Configuration.GetValue<int>("Cache:Timeout");
            }
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
        var manager = builder.Configuration.GetValue<string>("SecretManagement:Manager")!;
        Log.Logger.Information("Registering Secret Manager with provider {provider} for the Link Admin API.", manager);
        builder.Services.AddSecretManager(options =>
        {
            options.Manager = manager;
        });
    }

    // Add Link Security    
    if (!allowAnonymousAccess)
    {
        Log.Logger.Information("Registering Link Gateway Security for the Link Admin API.");
        builder.Services.AddLinkGatewaySecurity(builder.Configuration, Log.Logger, options =>
        {
            options.Environment = builder.Environment;
        });
    }
    else
    {
        Log.Logger.Information("Enabling anonymous access for the Link Admin API.");
        
        builder.Services.Configure<AuthenticationSchemaConfig>(options =>
        {
            options.EnableAnonymousAccess = allowAnonymousAccess;
        });
        
        //create anonymous access
        builder.Services.AddAuthorizationBuilder()        
            .AddPolicy("AuthenticatedUser", pb =>
            {
                pb.RequireAssertion(_ => true);
            });
    }
    
    // Configure CORS regardless of anonymous access
    var corsConfig = builder.Configuration.GetSection(LinkAdminConstants.AppSettingsSectionNames.CORS).Get<CorsConfig>();
    if (corsConfig != null)
    {
        Log.Logger.Debug("Registering CORS settings");
        builder.Services.AddCorsService(options =>
        {
            options.Environment = builder.Environment;
            options.PolicyName = corsConfig.PolicyName;
            options.AllowedHeaders = corsConfig.AllowedHeaders;
            options.AllowedExposedHeaders = corsConfig.AllowedExposedHeaders;
            options.AllowedMethods = corsConfig.AllowedMethods;
            options.AllowAllOrigins = corsConfig.AllowAllOrigins;
            options.AllowedOrigins = corsConfig.AllowedOrigins;
            options.AllowCredentials = corsConfig.AllowCredentials;
        }, Log.Logger);
    }
    else
    {
        Log.Logger.Warning("CORS settings not found.");
    }

    // Add header forwarding
    Log.Logger.Information("Registering Header Forwarding for the Link Admin API.");
    builder.Services.Configure<ForwardedHeadersOptions>(options =>
    {
        options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    });

    // Add Endpoints
    if (!allowAnonymousAccess)
    {
        builder.Services.AddTransient<IApi, AuthEndpoints>();

        if (builder.Configuration.GetValue<bool>("LinkTokenService:EnableTokenGenerationEndpoint"))
        {
            builder.Services.AddTransient<IApi, BearerServiceEndpoints>();
        }
    }    
    if (builder.Configuration.GetValue<bool>("EnableIntegrationFeature"))
    {
        builder.Services.AddTransient<IApi, IntegrationTestingEndpoints>();
    }

    // Add health checks
    var monitorBackend = builder.Configuration.GetValue<bool>("MonitorBackendHealthChecks");
    var healthCheckBuilder = builder.Services.AddHealthChecks();
    
    if (monitorBackend)
    {
        healthCheckBuilder
            .AddCheck<AccountServiceHealthCheck>("Account Service")
            .AddCheck<AuditServiceHealthCheck>("Audit Service")
            .AddCheck<CensusServiceHealthCheck>("Census Service")
            .AddCheck<DataAcquisitionHealthCheck>("Data Acquisition Service")
            .AddCheck<MeasureEvaluationServiceHealthCheck>("Measure Evaluation Service")
            .AddCheck<NormalizationServiceHealthCheck>("Normalization Service")
            .AddCheck<NotificationServiceHealthCheck>("Notification Service")
            .AddCheck<ReportServiceHealthCheck>("Report Service")
            .AddCheck<SubmissionServiceHealthCheck>("Submission Service")
            .AddCheck<TenantServiceHealthCheck>("Tenant Service");
    }

    if (builder.Configuration.GetValue<string>("Cache:Type") == "Redis")
    {
        healthCheckBuilder.AddCheck<CacheHealthCheck>("Cache");
    }


    // Add swagger generation
    builder.Services.AddEndpointsApiExplorer();    
    builder.Services.AddSwaggerGen(c =>
    {
        if (!allowAnonymousAccess)
        {
            #region Authentication Schemas
            if (builder.Configuration.GetValue<bool>("Authentication:Schemas:Jwt:Enabled"))
            {
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
            }

            c.AddSecurityDefinition("OAuth", new OpenApiSecurityScheme
            {
                Description = $"Authorization using OAuth",
                Name = "OAuth",
                Type = SecuritySchemeType.OAuth2,
                Scheme = LinkAdminConstants.AuthenticationSchemes.Oauth2,
                Flows = new OpenApiOAuthFlows
                {
                    AuthorizationCode = new OpenApiOAuthFlow
                    {
                        AuthorizationUrl = new Uri(builder.Configuration.GetValue<string>("Authentication:Schemas:Oauth2:Endpoints:Authorization")!),
                        TokenUrl = new Uri(builder.Configuration.GetValue<string>("Authentication:Schemas:Oauth2:Endpoints:Token")!),
                        Scopes = new Dictionary<string, string>
                    {
                        { "openid", "OpenId" },
                        { "profile", "Profile" },
                        { "email", "Email" }
                    }
                    }
                }

            });

            c.AddSecurityRequirement(new OpenApiSecurityRequirement
        {
            {
                new OpenApiSecurityScheme
                {
                    Reference = new OpenApiReference
                    {
                        Id = "OAuth",
                        Type = ReferenceType.SecurityScheme
                    },
                    Scheme = LinkAdminConstants.AuthenticationSchemes.Oauth2,
                    Name = "Oauth",
                    In = ParameterLocation.Header

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

    // Add logging redaction services
    Log.Logger.Information("Adding Redaction Service for the Link Admin API.");
    builder.Services.AddRedactionService(options =>
    {
        options.HmacKey = builder.Configuration.GetValue<string>("Logging:HmacKey");
    });    
    
    // builder.Services.ConfigureHttpJsonOptions(options =>
    // {
    //     options.SerializerOptions.Converters.Add(new HealthStatusJsonConverter());
    // });

    // Add YARP (reverse proxy)
    Log.Logger.Information("Registering YARP for the Link Admin API.");
    builder.Services.AddYarpProxy(builder.Configuration, Log.Logger, options => options.Environment = builder.Environment); 

    //Add telemetry if enabled
    Log.Logger.Information("Registering Open Telemetry for the Link Admin API.");
    builder.Services.AddLinkTelemetry(builder.Configuration, options =>
    {
        options.Environment = builder.Environment;
        options.ServiceName = LinkAdminConstants.ServiceName;
        options.ServiceVersion = ServiceActivitySource.Instance.Version;                
    });

    builder.Services.AddSingleton<ILinkAdminMetrics, LinkAdminMetrics>();    
}

#endregion


#region Setup Middleware
static void SetupMiddleware(WebApplication app)
{   

    if (app.Environment.IsDevelopment())
    {
        app.UseDeveloperExceptionPage();
    }
    else
    {
        app.UseForwardedHeaders();
        app.UseExceptionHandler();
    }    

    app.UseStatusCodePages();

    // Configure swagger
    app.ConfigureSwagger();

    app.UseRouting();
    app.UseCors(CorsConfig.DefaultCorsPolicyName);

    //check for anonymous access
    var allowAnonymousAccess = app.Configuration.GetValue<bool>("Authentication:EnableAnonymousAccess");

    if(!allowAnonymousAccess)
    {
        app.UseAuthentication();
        app.UseMiddleware<UserScopeMiddleware>();        
    }
    app.UseAuthorization();

    // Register endpoints
    app.MapGet("/api/info", () => Results.Ok($"Welcome to {ServiceActivitySource.Instance.Name} version {ServiceActivitySource.Instance.Version}!")).AllowAnonymous();

    var apis = app.Services.GetServices<IApi>();
    foreach (var api in apis)
    {
        if(api is null) throw new InvalidProgramException("No Endpoints were registered.");
        api.RegisterEndpoints(app);        
    }

    if (allowAnonymousAccess)
    {
        app.MapReverseProxy().AllowAnonymous();
    }
    else
    {
        app.MapReverseProxy();
    }    

    // Map health check middleware
    app.MapGroup("/api/monitor").MapMonitorEndpoints();
    app.MapGroup("/api/aggregate/").MapAggregationEndpoints();
    app.MapHealthChecks("/api/health", new HealthCheckOptions
    {
        ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
    }).RequireCors("HealthCheckPolicy");    
}

#endregion
