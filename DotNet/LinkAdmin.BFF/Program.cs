using HealthChecks.UI.Client;
using LantanaGroup.Link.LinkAdmin.BFF.Application.Commands.Integration;
using LantanaGroup.Link.LinkAdmin.BFF.Infrastructure;
using LantanaGroup.Link.LinkAdmin.BFF.Infrastructure.Extensions;
using LantanaGroup.Link.LinkAdmin.BFF.Presentation.Endpoints;
using LantanaGroup.Link.LinkAdmin.BFF.Settings;
using LantanaGroup.Link.LinkAdmin.BFF.Application.Validation;
using LantanaGroup.Link.Shared.Application.Factories;
using LantanaGroup.Link.Shared.Application.Interfaces;
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
using System.Configuration;

var builder = WebApplication.CreateBuilder(args);

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

    // Add problem details
    builder.Services.AddProblemDetailsService(options =>
    {
        options.Environment = builder.Environment;  
        options.ServiceName = LinkAdminConstants.ServiceName;
        options.IncludeExceptionDetails = builder.Configuration.GetValue<bool>("ProblemDetails:IncludeExceptionDetails");
    });

    // Add IOptions
    builder.Services.Configure<KafkaConnection>(builder.Configuration.GetSection(KafkaConstants.SectionName));
    builder.Services.Configure<SecretManagerSettings>(builder.Configuration.GetSection(ConfigurationConstants.AppSettings.SecretManagement));
    builder.Services.Configure<DataProtectionSettings>(builder.Configuration.GetSection(ConfigurationConstants.AppSettings.DataProtection));
    builder.Services.Configure<ServiceRegistry>(builder.Configuration.GetSection(ServiceRegistry.ConfigSectionName));
    builder.Services.Configure<LinkTokenServiceSettings>(builder.Configuration.GetSection(ConfigurationConstants.AppSettings.LinkTokenService));

    // Add Kafka Producer Factories
    builder.Services.AddSingleton<IKafkaProducerFactory<string, object>, KafkaProducerFactory<string, object>>();

    // Add fluent validation
    builder.Services.AddValidatorsFromAssemblyContaining(typeof(PatientEventValidator));

    // Add HttpClientFactory
    builder.Services.AddHttpClient();

    // Add data protection
    builder.Services.AddDataProtection().SetApplicationName(builder.Configuration.GetValue<string>("DataProtection:KeyRing") ?? "Link");
    //TODO: https://learn.microsoft.com/en-us/aspnet/core/security/data-protection/configuration/overview?view=aspnetcore-8.0

    // Add commands
    builder.Services.AddTransient<ICreatePatientEvent, CreatePatientEvent>();
    builder.Services.AddTransient<ICreateReportScheduled, CreateReportScheduled>();
    builder.Services.AddTransient<ICreateDataAcquisitionRequested, CreateDataAcquisitionRequested>();
    builder.Services.AddTransient<ICreateLinkBearerToken, CreateLinkBearerToken>();
    builder.Services.AddTransient<IRefreshSigningKey, RefreshSigningKey>();
    builder.Services.AddTransient<IGetLinkAccount, GetLinkAccount>();

    //Add Redis     
    builder.Services.AddRedisCache(options =>
    {
        options.Environment = builder.Environment;

        var redisConnection = builder.Configuration.GetConnectionString("Redis");

        if (string.IsNullOrEmpty(redisConnection))
            throw new NullReferenceException("Redis Connection String is required.");

        options.ConnectionString = redisConnection;
        options.Password = builder.Configuration.GetValue<string>("Redis:Password");
    });

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
    if (!allowAnonymousAccess)
    {
        builder.Services.AddLinkGatewaySecurity(builder.Configuration, options =>
        {
            options.Environment = builder.Environment;
        });
    }
    else
    {  
        //create anonymous access
        builder.Services.AddAuthorizationBuilder()        
            .AddPolicy("AuthenticatedUser", pb =>
            {
                pb.RequireAssertion(context => true);
            });
    }

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
    builder.Services.AddHealthChecks();    

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
    builder.Services.AddRedactionService(options =>
    {
        options.HmacKey = builder.Configuration.GetValue<string>("Logging:HmacKey");
    });

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

    // Add YARP (reverse proxy)
    builder.Services.AddYarpProxy(builder.Configuration, Log.Logger, options => options.Environment = builder.Environment);

    //Serilog.Debugging.SelfLog.Enable(Console.Error); 

    //Add telemetry if enabled
    builder.Services.AddLinkTelemetry(builder.Configuration, options =>
    {
        options.Environment = builder.Environment;
        options.ServiceName = LinkAdminConstants.ServiceName;
        options.ServiceVersion = ServiceActivitySource.Version; //TODO: Get version from assembly?                
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
        app.UseExceptionHandler();
    }

    app.UseStatusCodePages();
    app.UseHttpsRedirection();

    // Configure swagger
    if (app.Configuration.GetValue<bool>(ConfigurationConstants.AppSettings.EnableSwagger))
    {
        app.UseSwagger(opts => { opts.RouteTemplate = "api/swagger/{documentname}/swagger.json"; });
        app.UseSwaggerUI(opts => {
            opts.SwaggerEndpoint("/api/swagger/v1/swagger.json", $"{ServiceActivitySource.ServiceName} - {ServiceActivitySource.Version}");
            opts.RoutePrefix = "api/swagger";
        });
    }

    app.UseRouting();
    var corsConfig = app.Configuration.GetSection(ConfigurationConstants.AppSettings.CORS).Get<CorsSettings>();
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
    app.MapHealthChecks("/api/health", new HealthCheckOptions
    {
        ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
    }).RequireCors("HealthCheckPolicy");    
}

#endregion



