using LantanaGroup.Link.Audit.Settings;
using LantanaGroup.Link.Audit.Persistance;
using LantanaGroup.Link.Audit.Application.Interfaces;
using LantanaGroup.Link.Audit.Listeners;
using LantanaGroup.Link.Audit.Application.Commands;
using LantanaGroup.Link.Audit.Application.Factory;
using LantanaGroup.Link.Audit.Presentation.Services;
using LantanaGroup.Link.Audit.Application.Audit.Queries;
using LantanaGroup.Link.Audit.Infrastructure.AuditHelper;
using Serilog;
using Serilog.Exceptions;
using Serilog.Enrichers.Span;
using LantanaGroup.Link.Audit.Application.Models;
using LantanaGroup.Link.Audit.Infrastructure;
using LantanaGroup.Link.Audit.Infrastructure.Telemetry;
using System.Reflection;
using LantanaGroup.Link.Audit.Infrastructure.Health;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using HealthChecks.UI.Client;
using LantanaGroup.Link.Audit.Infrastructure.Extensions;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using LantanaGroup.Link.Shared.Application.Middleware;
using System.Diagnostics;
using Serilog.Settings.Configuration;
using Microsoft.Extensions.Configuration.AzureAppConfiguration;

var builder = WebApplication.CreateBuilder(args);

RegisterServices(builder);
var app = builder.Build();
SetupMiddleware(app);

app.Run();

#region Register Services

static void RegisterServices(WebApplicationBuilder builder)
{
    builder.Configuration.AddAzureAppConfiguration(options =>
    {
        options.Connect(builder.Configuration.GetConnectionString("AzureAppConfiguration"))
            // Load configuration values with no label
            .Select("Link:Audit*", LabelFilter.Null)
            // Override with any configuration values specific to current hosting env
            .Select("Link:Audit*", builder.Environment.EnvironmentName);
    });

    var serviceInformation = builder.Configuration.GetRequiredSection(AuditConstants.AppSettingsSectionNames.ServiceInformation).Get<ServiceInformation>();
    if (serviceInformation != null)
    {
        ServiceActivitySource.Initialize(serviceInformation);
        Counters.Initialize(serviceInformation);
    }
    else
    {
        throw new NullReferenceException("Service Information was null.");
    }

    //Add problem details
    builder.Services.AddProblemDetails(options => {
        options.CustomizeProblemDetails = ctx =>
        {            
            ctx.ProblemDetails.Detail = "An error occured in our API. Please use the trace id when requesting assistence.";
            if (!ctx.ProblemDetails.Extensions.ContainsKey("traceId"))
            {
                string? traceId = Activity.Current?.Id ?? ctx.HttpContext.TraceIdentifier;
                ctx.ProblemDetails.Extensions.Add(new KeyValuePair<string, object?>("traceId", traceId));
            }      

            if (builder.Environment.IsDevelopment())
            {
                ctx.ProblemDetails.Extensions.Add("service", "Audit");
            }
            else 
            {
                ctx.ProblemDetails.Extensions.Remove("exception");
            }
            
        };                        
    });

    // Add services to the container. 
    builder.Services.Configure<TempKafkaConnection>(builder.Configuration.GetRequiredSection(AuditConstants.AppSettingsSectionNames.Kafka));
    builder.Services.Configure<MongoConnection>(builder.Configuration.GetRequiredSection(AuditConstants.AppSettingsSectionNames.Mongo));
    builder.Services.AddTransient<IAuditHelper, AuditHelper>();

    //Add commands
    builder.Services.AddTransient<ICreateAuditEventCommand, CreateAuditEventCommand>();

    //Add queries
    builder.Services.AddTransient<IGetAuditEventQuery, GetAuditEventQuery>();
    builder.Services.AddTransient<IGetAllAuditEventsQuery, GetAllAuditEventsQuery>();
    builder.Services.AddTransient<IGetAuditEventListQuery, GetAuditEventListQuery>();

    //Add factories
    builder.Services.AddSingleton<IAuditFactory, AuditFactory>();
    builder.Services.AddTransient<IKafkaConsumerFactory, KafkaConsumerFactory>();

    //Add repositories
    builder.Services.AddSingleton<IAuditRepository, AuditMongoRepo>();

    //Add health checks
    builder.Services.AddHealthChecks()
        .AddCheck<DatabaseHealthCheck>("Database");

    //configure CORS
    builder.Services.AddCorsService(builder.Environment);

    //configure service api security    
    var idpConfig = builder.Configuration.GetSection(AuditConstants.AppSettingsSectionNames.IdentityProvider).Get<IdentityProviderConfig>();
    if (idpConfig != null)
    {
        builder.Services.AddAuthenticationService(idpConfig, builder.Environment);        
    }
    else
    {
        throw new NullReferenceException("Identity Provider Configuration was null.");
    }

    //builder.Services.AddAuthorizationService();

    // Additional configuration is required to successfully run gRPC on macOS.
    // For instructions on how to configure Kestrel and gRPC clients on macOS, visit https://go.microsoft.com/fwlink/?linkid=2099682
    builder.Services.AddGrpc();
    builder.Services.AddGrpcReflection();
    builder.Services.AddControllers(options => { options.ReturnHttpNotAcceptable = true; }).AddXmlDataContractSerializerFormatters();

    //Add Hosted Services
    builder.Services.AddHostedService<AuditEventListener>();

    // Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen(c =>
    {
        var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
        var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
        c.IncludeXmlComments(xmlPath);
    });

    // Logging using Serilog
    builder.Logging.AddSerilog();
    var loggerOptions = new ConfigurationReaderOptions { SectionName = "Serilog" };
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

    var telemetryConfig = builder.Configuration.GetSection(AuditConstants.AppSettingsSectionNames.Telemetry).Get<TelemetryConfig>();    
    if (telemetryConfig != null)
    {
        builder.Services.AddOpenTelemetryService(telemetryConfig, builder.Environment);        
    }
    else
    {
        //throw new NullReferenceException("Telemetry Configuration was null.");
    }     
}

#endregion

#region Set up middleware

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

    // Configure the HTTP request pipeline.
    if (app.Configuration.GetValue<bool>("EnableSwagger"))
    {
        var serviceInformation = app.Configuration.GetSection(AuditConstants.AppSettingsSectionNames.ServiceInformation).Get<ServiceInformation>();
        app.UseSwagger();
        app.UseSwaggerUI(opts => opts.SwaggerEndpoint("/swagger/v1/swagger.json", serviceInformation != null ? $"{serviceInformation.Name} - {serviceInformation.Version}" : "Link Audit Service"));
    }

    app.UseRouting();
    app.UseCors("CorsPolicy");
    app.UseAuthentication();
    app.UseMiddleware<UserScopeMiddleware>();
    app.UseAuthorization();

    //map health check middleware
    app.MapHealthChecks("/health", new HealthCheckOptions { 
        ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
    });

    app.UseEndpoints(endpoints => endpoints.MapControllers());   

    if (app.Configuration.GetValue<bool>("AllowReflection"))
    {
        app.MapGrpcReflectionService();
    }

    app.MapGrpcService<AuditService>();    
}

#endregion