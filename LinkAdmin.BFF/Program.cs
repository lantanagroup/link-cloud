using HealthChecks.UI.Client;
using LantanaGroup.Link.LinkAdmin.BFF.Application.Commands.Integration;
using LantanaGroup.Link.LinkAdmin.BFF.Application.Interfaces;
using LantanaGroup.Link.LinkAdmin.BFF.Application.Models;
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
using LantanaGroup.Link.LinkAdmin.BFF.Application.Commands.Security;
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
    var serviceInformation = builder.Configuration.GetRequiredSection(LinkAdminConstants.AppSettingsSectionNames.ServiceInformation).Get<ServiceInformation>();
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
    builder.Services.Configure<KafkaConnection>(builder.Configuration.GetSection(LinkAdminConstants.AppSettingsSectionNames.Kafka));
    builder.Services.Configure<SecretManagerConfig>(builder.Configuration.GetSection(LinkAdminConstants.AppSettingsSectionNames.SecretManagement));

    // Add Kafka Producer Factories
    builder.Services.AddSingleton<IKafkaProducerFactory<string, object>, KafkaProducerFactory<string, object>>();

    //Add Redis 
    builder.Services.AddRedisCache(options =>
    {
        options.Environment = builder.Environment;

        var redisConnection = builder.Configuration.GetConnectionString("Redis");
        
        if(string.IsNullOrEmpty(redisConnection))
            throw new NullReferenceException("Redis Connection String is required.");

        options.ConnectionString = redisConnection;
    });

    // Add Secret Manager
    if (builder.Configuration.GetValue<bool>("SecretManagement:Enabled"))
    {
        builder.Services.AddSecretManager(options =>
        {
            options.Manager = builder.Configuration.GetValue<string>("SecretManagement:Manager")!;
        });
    }    

    // Add Authentication
    List<string> authSchemas = [ LinkAdminConstants.AuthenticationSchemes.Cookie];

    var defaultChallengeScheme = builder.Configuration.GetValue<string>("Authentication:DefaultChallengeScheme");
    builder.Services.Configure<AuthenticationSchemaConfig>(options =>
    {
        options.DefaultScheme = LinkAdminConstants.AuthenticationSchemes.Cookie;

        if (string.IsNullOrEmpty(defaultChallengeScheme))
            throw new NullReferenceException("DefaultChallengeScheme is required.");

        options.DefaultChallengeScheme = defaultChallengeScheme;
    });

    var authBuilder = builder.Services.AddAuthentication(options => { 
        options.DefaultScheme = LinkAdminConstants.AuthenticationSchemes.Cookie;
        options.DefaultChallengeScheme = defaultChallengeScheme;
    });    

    authBuilder.AddCookie(LinkAdminConstants.AuthenticationSchemes.Cookie, options =>
    {
        options.Cookie.Name = LinkAdminConstants.AuthenticationSchemes.Cookie;
        options.Cookie.SameSite = SameSiteMode.Strict;
        options.ExpireTimeSpan = TimeSpan.FromMinutes(30);
    });

    //Add Oauth authorization scheme if enabled
    if(builder.Configuration.GetValue<bool>("Authentication:Schemas:Oauth2:Enabled"))
    {
        if(!LinkAdminConstants.AuthenticationSchemes.Oauth2.Equals(defaultChallengeScheme))
            authSchemas.Add(LinkAdminConstants.AuthenticationSchemes.Oauth2);

        authBuilder.AddOAuthAuthentication(options =>
        {
            options.Environment = builder.Environment;
            options.ClientId = builder.Configuration.GetValue<string>("Authentication:Schemas:Oauth2:ClientId")!;
            options.ClientSecret = builder.Configuration.GetValue<string>("Authentication:Schemas:Oauth2:ClientSecret")!;
            options.AuthorizationEndpoint = builder.Configuration.GetValue<string>("Authentication:Schemas:Oauth2:Endpoints:Authorization")!;
            options.TokenEndpoint = builder.Configuration.GetValue<string>("Authentication:Schemas:Oauth2:Endpoints:Token")!;
            options.UserInformationEndpoint = builder.Configuration.GetValue<string>("Authentication:Schemas:Oauth2:Endpoints:UserInformation")!;
            options.CallbackPath = builder.Configuration.GetValue<string>("Authentication:Schemas:Oauth2:CallbackPath");
        });
    }  
   
    // Add OpenIdConnect authorization scheme if enabled
    if (builder.Configuration.GetValue<bool>("Authentication:Schemas:OpenIdConnect:Enabled"))
    {
        if(!LinkAdminConstants.AuthenticationSchemes.OpenIdConnect.Equals(defaultChallengeScheme))
            authSchemas.Add(LinkAdminConstants.AuthenticationSchemes.OpenIdConnect);

        authBuilder.AddOpenIdConnectAuthentication(options =>
        {
            options.Environment = builder.Environment;
            options.Authority = builder.Configuration.GetValue<string>("Authentication:Schemas:OpenIdConnect:Authority")!;
            options.ClientId = builder.Configuration.GetValue<string>("Authentication:Schemas:OpenIdConnect:ClientId")!;
            options.ClientSecret = builder.Configuration.GetValue<string>("Authentication:Schemas:OpenIdConnect:ClientSecret")!;
            options.NameClaimType = builder.Configuration.GetValue<string>("Authentication:Schemas:OpenIdConnect:NameClaimType");
            options.RoleClaimType = builder.Configuration.GetValue<string>("Authentication:Schemas:OpenIdConnect:RoleClaimType");
        });
    }    

    // Add JWT authorization scheme if enabled
    if (builder.Configuration.GetValue<bool>("Authentication:Schemas:Jwt:Enabled"))
    {
        if(!LinkAdminConstants.AuthenticationSchemes.JwtBearerToken.Equals(defaultChallengeScheme))
            authSchemas.Add(LinkAdminConstants.AuthenticationSchemes.JwtBearerToken);

        authBuilder.AddJwTBearerAuthentication(options =>
        {            
            options.Environment = builder.Environment;
            options.Authority = builder.Configuration.GetValue<string>("Authentication:Schemas:Jwt:Authority");
            options.Audience = builder.Configuration.GetValue<string>("Authentication:Schemas:Jwt:Audience");
            options.NameClaimType = builder.Configuration.GetValue<string>("Authentication:Schemas:Jwt:NameClaimType");
            options.RoleClaimType = builder.Configuration.GetValue<string>("Authentication:Schemas:Jwt:RoleClaimType");
            
        });        
    }  

    // Add Link Bearer Token authorization schema if feature is enabled
    if(builder.Configuration.GetValue<bool>("EnableBearerTokenFeature"))
    {
        if(!LinkAdminConstants.AuthenticationSchemes.LinkBearerToken.Equals(defaultChallengeScheme))
            authSchemas.Add(LinkAdminConstants.AuthenticationSchemes.LinkBearerToken);

        builder.Services.AddLinkBearerServiceAuthentication(options =>
        {
            options.Environment = builder.Environment;
            options.Authority = LinkAdminConstants.LinkBearerService.LinkBearerIssuer;
            options.Audience = LinkAdminConstants.LinkBearerService.LinkBearerAudience;
        });
    }
    
    // Add Authorization
    builder.Services.AddAuthorization(builder =>
    {
        builder.AddPolicy("AuthenticatedUser", pb => {
            pb.RequireAuthenticatedUser()
                .AddAuthenticationSchemes([.. authSchemas]);         
        });
    });

    // Add Endpoints
    builder.Services.AddTransient<IApi, AuthEndpoints>();
    if (builder.Configuration.GetValue<bool>("EnableIntegrationFeature"))
    {
        builder.Services.AddTransient<IApi, IntegrationTestingEndpoints>();
    }    
    if(builder.Configuration.GetValue<bool>("EnableBearerTokenFeature"))
    {
        builder.Services.AddTransient<IApi, BearerServiceEndpoints>();
    }

    // Add fluent validation
    builder.Services.AddValidatorsFromAssemblyContaining(typeof(PatientEventValidator));
       
    // Add commands
    builder.Services.AddTransient<ICreatePatientEvent, CreatePatientEvent>();
    builder.Services.AddTransient<ICreateReportScheduled,  CreateReportScheduled>();
    builder.Services.AddTransient<ICreateDataAcquisitionRequested, CreateDataAcquisitionRequested>();
    builder.Services.AddTransient<ICreateLinkBearerToken, CreateLinkBearerToken>();
    builder.Services.AddTransient<IRefreshSigningKey, RefreshSigningKey>();

    // Add YARP (reverse proxy)
    builder.Services.AddReverseProxy()
        .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

    // Add health checks
    builder.Services.AddHealthChecks();

    // Configure CORS
    var corsConfig = builder.Configuration.GetSection(LinkAdminConstants.AppSettingsSectionNames.CORS).Get<CorsConfig>();
    if(corsConfig != null)
    {
        builder.Services.AddCorsService(options =>
        {
            options.Environment = builder.Environment;
            options.PolicyName = corsConfig.PolicyName;   
            options.AllowedHeaders = corsConfig.AllowedHeaders;
            options.AllowedExposedHeaders = corsConfig.AllowedExposedHeaders;
            options.AllowedMethods = corsConfig.AllowedMethods;
            options.AllowedOrigins = corsConfig.AllowedOrigins;       
        });
    }
    else
    {
        throw new NullReferenceException("CORS Configuration was null.");
    }

    // Add swagger generation
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen(c =>
    {

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

    // Add open telemetry
    var telemetryConfig = builder.Configuration.GetSection(LinkAdminConstants.AppSettingsSectionNames.Telemetry).Get<TelemetryConfig>();
    if (telemetryConfig != null)
    {
        builder.Services.AddOpenTelemetryService(options => {
            options.Environment = builder.Environment;
            options.TelemetryCollectorEndpoint = telemetryConfig.TelemetryCollectorEndpoint;
            options.EnableRuntimeInstrumentation = telemetryConfig.EnableRuntimeInstrumentation;
        });
    }
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
    if (app.Configuration.GetValue<bool>(LinkAdminConstants.AppSettingsSectionNames.EnableSwagger))
    {
        var serviceInformation = app.Configuration.GetSection(LinkAdminConstants.AppSettingsSectionNames.ServiceInformation).Get<ServiceInformation>();
        app.UseSwagger();
        app.UseSwaggerUI(opts => {
            opts.SwaggerEndpoint("/swagger/v1/swagger.json", serviceInformation != null ? $"{serviceInformation.Name} - {serviceInformation.Version}" : "Link Admin API");
        });
    }

    app.UseRouting();
    var corsConfig = app.Configuration.GetSection(LinkAdminConstants.AppSettingsSectionNames.CORS).Get<CorsConfig>();
    app.UseCors(corsConfig?.PolicyName ?? CorsConfig.DefaultCorsPolicyName);
    app.UseAuthentication();
    app.UseMiddleware<UserScopeMiddleware>();
    app.UseAuthorization(); 

    // Register endpoints
    app.MapGet("/", (HttpContext ctx) => Results.Ok($"Welcome to {ServiceActivitySource.Instance.Name} version {ServiceActivitySource.Instance.Version}!")).AllowAnonymous();

    var apis = app.Services.GetServices<IApi>();
    foreach (var api in apis)
    {
        if(api is null) throw new InvalidProgramException("No Endpoints were registered.");
        api.RegisterEndpoints(app);        
    }

    app.MapReverseProxy().RequireAuthorization("AuthenticatedUser");

    // Map health check middleware
    app.MapHealthChecks("/health", new HealthCheckOptions
    {
        ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
    }).RequireCors("HealthCheckPolicy");    
}

#endregion



