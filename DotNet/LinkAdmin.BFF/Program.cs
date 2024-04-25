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

    // Add service information
    var serviceInformation = builder.Configuration.GetSection(LinkAdminConstants.AppSettingsSectionNames.ServiceInformation).Get<ServiceInformation>();
    if (serviceInformation != null)
    {
        ServiceActivitySource.Initialize(serviceInformation);
    }
    else
    {
        throw new NullReferenceException("Service Information was null.");
    }      

    // Add problem details
    builder.Services.AddProblemDetailsService(options =>
    {
        options.Environment = builder.Environment;  
        options.IncludeExceptionDetails = builder.Configuration.GetValue<bool>("ProblemDetails:IncludeExceptionDetails");
    });

    // Add IOptions
    builder.Services.Configure<KafkaConnection>(builder.Configuration.GetSection(KafkaConstants.SectionName));
    builder.Services.Configure<SecretManagerConfig>(builder.Configuration.GetSection(LinkAdminConstants.AppSettingsSectionNames.SecretManagement));
    builder.Services.Configure<ServiceRegistry>(builder.Configuration.GetSection(ServiceRegistry.ConfigSectionName));
    builder.Services.Configure<LinkBearerServiceConfig>(builder.Configuration.GetSection(LinkAdminConstants.AppSettingsSectionNames.LinkBearerService));

    // Add Kafka Producer Factories
    builder.Services.AddSingleton<IKafkaProducerFactory<string, object>, KafkaProducerFactory<string, object>>();

    // Add fluent validation
    builder.Services.AddValidatorsFromAssemblyContaining(typeof(PatientEventValidator));

    // Add HttpClientFactory
    builder.Services.AddHttpClient();

    // Add data protection
    builder.Services.AddDataProtection().SetApplicationName("Link");
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
    builder.Services.AddLinkGatewaySecurity(builder.Configuration, options =>
    {
        options.Environment = builder.Environment;
    });

    // Add Endpoints
    builder.Services.AddTransient<IApi, AuthEndpoints>();
    if (builder.Configuration.GetValue<bool>("EnableIntegrationFeature"))
    {
        builder.Services.AddTransient<IApi, IntegrationTestingEndpoints>();
    }
    if (builder.Configuration.GetValue<bool>("LinkBearerService:EnableTokenGenrationEndpoint"))
    {
        builder.Services.AddTransient<IApi, BearerServiceEndpoints>();
    }

    // Add YARP (reverse proxy)
    builder.Services.AddYarpProxy(builder.Configuration, options => options.Environment = builder.Environment);

    // Add health checks
    builder.Services.AddHealthChecks();    

    // Add swagger generation
    builder.Services.AddEndpointsApiExplorer();    
    builder.Services.AddSwaggerGen(c =>
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

    //Serilog.Debugging.SelfLog.Enable(Console.Error); 

    //Add telemetry if enabled
    builder.Services.AddLinkTelemetry(builder.Configuration, options =>
    {
        options.Environment = builder.Environment;
        options.ServiceName = LinkAdminConstants.ServiceName;
        options.ServiceVersion = serviceInformation.Version; //TODO: Get version from assembly?                
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
    //app.UseHttpsRedirection();

    // Configure swagger
    if (app.Configuration.GetValue<bool>(LinkAdminConstants.AppSettingsSectionNames.EnableSwagger))
    {
        var serviceInformation = app.Configuration.GetSection(LinkAdminConstants.AppSettingsSectionNames.ServiceInformation).Get<ServiceInformation>();
        app.UseSwagger(opts => { opts.RouteTemplate = "api/swagger/{documentname}/swagger.json"; });
        app.UseSwaggerUI(opts => {
            opts.SwaggerEndpoint("/api/swagger/v1/swagger.json", serviceInformation != null ? $"{serviceInformation.Name} - {serviceInformation.Version}" : "Link Admin API");
            opts.RoutePrefix = "api/swagger";
        });
    }

    app.UseRouting();
    var corsConfig = app.Configuration.GetSection(ConfigurationConstants.AppSettings.CORS).Get<CorsSettings>();
    app.UseCors(CorsConfig.DefaultCorsPolicyName);
    app.UseAuthentication();
    app.UseMiddleware<UserScopeMiddleware>();
    app.UseAuthorization(); 

    // Register endpoints
    app.MapGet("/api/info", (HttpContext ctx) => Results.Ok($"Welcome to {ServiceActivitySource.Instance.Name} version {ServiceActivitySource.Instance.Version}!")).AllowAnonymous();

    var apis = app.Services.GetServices<IApi>();
    foreach (var api in apis)
    {
        if(api is null) throw new InvalidProgramException("No Endpoints were registered.");
        api.RegisterEndpoints(app);        
    }

    if (app.Environment.IsDevelopment() && app.Configuration.GetValue<bool>("Authentication:EnableAnonymousAccess"))
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



