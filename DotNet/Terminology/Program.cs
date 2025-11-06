using System.Reflection;
using HealthChecks.UI.Client;
using LantanaGroup.Link.Shared.Application.Extensions;
using LantanaGroup.Link.Shared.Application.Extensions.Security;
using LantanaGroup.Link.Shared.Application.Middleware;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using LantanaGroup.Link.Terminology.Application.Formatters;
using LantanaGroup.Link.Terminology.Application.Settings;
using LantanaGroup.Link.Terminology.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.OpenApi.Models;
using Serilog;
using Serilog.Enrichers.Span;
using Serilog.Exceptions;
using Serilog.Settings.Configuration;

static void RegisterServices(WebApplicationBuilder builder)
{
    // load external configuration source (if specified)
    builder.AddExternalConfiguration(TerminologyConstants.ServiceName);
    
    var serviceInformation = builder.Configuration.GetRequiredSection(TerminologyConstants.AppSettingsSectionNames.ServiceInformation).Get<ServiceInformation>();
    
    builder.Services.AddHttpClient();

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

    builder.Services.AddControllers(options =>
    {
        options.ModelBinderProviders.Insert(0, new FhirModelBinderProvider());
        options.OutputFormatters.Insert(0, new FhirOutputFormatter());
    });

    builder.Services.AddHealthChecks();

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

        c.EnableAnnotations();
        //c.SwaggerDoc("v1", new OpenApiInfo { Title = "Link Terminology", Version = "3.1.0" });
        
        var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
        var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
        c.IncludeXmlComments(xmlPath);
    });

    ConfigureLogging(builder);

    //Add CORS
    builder.Services.AddLinkCorsService(options => { 
        options.Environment = builder.Environment;
    });            

    //Add telemetry if enabled
    builder.Services.AddLinkTelemetry(builder.Configuration, options =>
    {
        options.Environment = builder.Environment;
        options.ServiceName = TerminologyConstants.ServiceName;
        options.ServiceVersion = serviceInformation?.Version ?? Assembly.GetExecutingAssembly().GetName().Version?.ToString();                
    });
    
    builder.Services.AddMemoryCache();
    builder.Services.AddSingleton<CodeGroupCacheService>();
    builder.Services.AddSingleton<FhirService>();

    builder.Services.AddOptions<TerminologyConfig>()
        .Bind(builder.Configuration.GetSection(TerminologyConstants.AppSettingsSectionNames.Terminology))
        .Validate(cfg => !string.IsNullOrWhiteSpace(cfg.Path), "Terminology:Path is required")
        .Validate(cfg => Directory.Exists(cfg.Path), "Terminology:Path does not exist")
        .ValidateOnStart();

    builder.Services.AddHostedService<Startup>();
}

static void ConfigureLogging(WebApplicationBuilder builder)
{
    // Logging using Serilog
    builder.Logging.AddSerilog();
    var loggerOptions = new ConfigurationReaderOptions { SectionName = TerminologyConstants.AppSettingsSectionNames.Serilog };
    Log.Logger = new LoggerConfiguration()
        .ReadFrom.Configuration(builder.Configuration, loggerOptions)
        .Filter.ByExcluding("RequestPath like '/health%'")
        .Filter.ByExcluding("RequestPath like '/swagger%'")
        .Enrich.WithExceptionDetails()
        .Enrich.FromLogContext()
        .Enrich.WithSpan()
        .Enrich.With<ActivityEnricher>()
        .CreateLogger();
            
    Serilog.Debugging.SelfLog.Enable(Console.Error);
}

static void SetupMiddleware(WebApplication app)
{
    // Configure the HTTP request pipeline.
    app.ConfigureSwagger();

    //map health check middleware
    app.MapHealthChecks("/health", new HealthCheckOptions
    {
        ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
    });
    
    app.UseRouting();            
    app.UseCors(CorsSettings.DefaultCorsPolicyName);
    
    var allowAnonymousAccess = app.Configuration.GetValue<bool>("Authentication:EnableAnonymousAccess");
    if (!allowAnonymousAccess)
    {
        app.UseAuthentication();
        app.UseMiddleware<UserScopeMiddleware>();
    }
    app.UseAuthorization();
    
    app.MapControllers();
    
    app.MapInfo(Assembly.GetExecutingAssembly(), app.Configuration, "terminology");
}

var builder = WebApplication.CreateBuilder(args);

RegisterServices(builder);
var app = builder.Build();
SetupMiddleware(app);
app.Run();