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
using System.IdentityModel.Tokens.Jwt;
using Microsoft.Identity.Client;
using Microsoft.Extensions.Azure;


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

    // Add Kafka Producer Factories
    builder.Services.AddSingleton<IKafkaProducerFactory<string, object>, KafkaProducerFactory<string, object>>();

    // Add Authentication
    List<string> authSchemes = [ LinkAdminConstants.AuthenticationSchemes.Cookie ];
    var authBuilder = builder.Services.AddAuthentication();
    authBuilder.AddCookie(LinkAdminConstants.AuthenticationSchemes.Cookie, options =>
        {
            options.Cookie.Name = LinkAdminConstants.AuthenticationSchemes.Cookie;
            options.Cookie.SameSite = SameSiteMode.Strict;
            options.ExpireTimeSpan = TimeSpan.FromMinutes(30);
        });

    if(builder.Configuration.GetValue<bool>("Authentication:Schemes:Jwt:Enabled"))
    {
        authSchemes.Add(LinkAdminConstants.AuthenticationSchemes.JwtBearerToken);

        authBuilder.AddJwTBearerAuthentication(options =>
        {            
            options.Environment = builder.Environment;
            options.Authority = builder.Configuration.GetValue<string>("Authentication:Schemes:Jwt:Authority");
            options.Audience = builder.Configuration.GetValue<string>("Authentication:Schemes:Jwt:Audience");
            options.NameClaimType = builder.Configuration.GetValue<string>("Authentication:Schemes:Jwt:NameClaimType");
            options.RoleClaimType = builder.Configuration.GetValue<string>("Authentication:Schemes:Jwt:RoleClaimType");       
        });
        //JwtSecurityTokenHandler.DefaultInboundClaimTypeMap.Clear();
        //authBuilder.Services.AddAuthentication()
        //    .AddJwtBearer(LinkAdminConstants.AuthenticationSchemes.JwtBearerToken, options =>
        //    {
        //        options.Authority = builder.Configuration.GetValue<string>("Authentication:Schemes:Jwt:Authority");
        //        options.Audience = builder.Configuration.GetValue<string>("Authentication:Schemes:Jwt:Audience");
        //        options.RequireHttpsMetadata = !builder.Environment.IsDevelopment();

        //        options.TokenValidationParameters = new()
        //        {
        //            NameClaimType = builder.Configuration.GetValue<string>("Authentication:Schemes:Jwt:NameClaimType"),
        //            RoleClaimType = builder.Configuration.GetValue<string>("Authentication:Schemes:Jwt:RoleClaimType"),
        //            //avoid jwt confustion attacks (ie: circumvent token signature checking)
        //            ValidTypes = builder.Configuration.GetValue<string[]>("Authentication:Schemes:Jwt:ValidTypes")
        //        };
        //    });
    }

    // Add Authorization
    builder.Services.AddAuthorization(builder =>
    {
        builder.AddPolicy("AuthenticatedUser", pb => {
            pb.RequireAuthenticatedUser()
                .AddAuthenticationSchemes([.. authSchemes]);         
        });
    });


    // Add Endpoints
    builder.Services.AddTransient<IApi, AuthEndpoints>();
    if (builder.Configuration.GetValue<bool>("EnableIntegrationFeature"))
    {
        builder.Services.AddTransient<IApi, IntegrationTestingEndpoints>();
    }    

    // Add fluent validation
    builder.Services.AddValidatorsFromAssemblyContaining(typeof(PatientEventValidator));

    // Add commands
    builder.Services.AddTransient<ICreatePatientEvent, CreatePatientEvent>();
    builder.Services.AddTransient<ICreateReportScheduled,  CreateReportScheduled>();
    builder.Services.AddTransient<ICreateDataAcquisitionRequested, CreateDataAcquisitionRequested>();

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
    //app.UseMiddleware<UserScopeMiddleware>();
    app.UseAuthorization(); 

    // Register endpoints
    var apis = app.Services.GetServices<IApi>();
    foreach (var api in apis)
    {
        if(api is null) throw new InvalidProgramException("No Endpoints were not found.");
        api.RegisterEndpoints(app);        
    }

    app.MapReverseProxy();

    // Map health check middleware
    app.MapHealthChecks("/health", new HealthCheckOptions
    {
        ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
    }).RequireCors("HealthCheckPolicy");
    
}

#endregion



