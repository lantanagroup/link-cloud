using LantanaGroup.Link.DemoApiGateway.settings;
using Serilog;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using System.IdentityModel.Tokens.Jwt;
using LantanaGroup.Link.DemoApiGateway.Application.Factory;
using Serilog.Exceptions;
using Serilog.Enrichers.Span;
using Hellang.Middleware.ProblemDetails;
using OpenTelemetry.Metrics;
using LantanaGroup.Link.DemoApiGateway.Infrastructure;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Confluent.Kafka.Extensions.OpenTelemetry;
using LantanaGroup.Link.DemoApiGateway.Application.Commands.CreateDataAcquisitionRequestedEvent;
using LantanaGroup.Link.DemoApiGateway.Application.Commands.CreatePatientEvent;
using LantanaGroup.Link.DemoApiGateway.Application.models.interfaces;
using LantanaGroup.Link.DemoApiGateway.Application.models;
using LantanaGroup.Link.DemoApiGateway.Services.Client;
using LantanaGroup.Link.DemoApiGateway.Application.Commands;
using System.Diagnostics;
using LantanaGroup.Link.Shared.Application.Middleware;
using LantanaGroup.Link.DemoApiGateway.Services.Client.DataAcquisition;
using LantanaGroup.Link.DemoApiGateway.Services.Client.Normalization;
using Microsoft.Extensions.Configuration.AzureAppConfiguration;
using Azure.Identity;
using Link.Authorization.Policies;
using Link.Authorization.Requirements;
using LantanaGroup.Link.Shared.Application.Models.Configs;

var builder = WebApplication.CreateBuilder(args);

RegisterServices(builder);
var app = builder.Build();
SetupMiddleware(app);

app.Run();

#region Register Services

static void RegisterServices(WebApplicationBuilder builder)
{
    //load external configuration source if specified
    var externalConfigurationSource = builder.Configuration.GetSection(GatewayConstants.AppSettingsSectionNames.ExternalConfigurationSource).Get<string>();
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
                            .Select("*", GatewayConstants.ServiceName)
                            // Load configuration values for service name and environment
                            .Select("*", GatewayConstants.ServiceName + ":" + builder.Environment.EnvironmentName);

                    options.ConfigureKeyVault(kv =>
                    {
                        kv.SetCredential(new DefaultAzureCredential());
                    });

                });
                break;
        }
    }

    var serviceInformation = builder.Configuration.GetSection(GatewayConstants.AppSettingsSectionNames.ServiceInformation).Get<ServiceInformation>();
    if (serviceInformation != null)
    {
        ServiceActivitySource.Initialize(serviceInformation);        
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
                ctx.ProblemDetails.Extensions.Add("service", "Demo API Gateway");
            }
            else
            {
                ctx.ProblemDetails.Extensions.Remove("exception");
            }

        };
    });

    builder.Services.Configure<ServiceRegistry>(builder.Configuration.GetSection(ServiceRegistry.ConfigSectionName));

    // Add services to the container
    builder.Services.AddHeaderPropagation(opts => {
        opts.Headers.Add("Authorization");
    }); 
    builder.Services.AddHttpClient<IAuditService, AuditService>().AddHeaderPropagation();
    builder.Services.AddHttpClient<INotificationService, NotificationService>().AddHeaderPropagation();
    builder.Services.AddHttpClient<ITenantService, TenantService>().AddHeaderPropagation();
    builder.Services.AddHttpClient<ICensusService, CensusService>().AddHeaderPropagation();
    builder.Services.AddHttpClient<IDataAcquisitionService, DataAcquisitionService>().AddHeaderPropagation();
    builder.Services.AddHttpClient<INormalizationService, NormalizationService>().AddHeaderPropagation();
    builder.Services.AddHttpClient<IReportService, ReportService>().AddHeaderPropagation();
    builder.Services.AddHttpClient<IMeasureDefinitionService, MeasureDefinitionService>().AddHeaderPropagation();

    //Commands
    builder.Services.AddTransient<ICreateReportScheduledCommand, CreateReportScheduledCommand>();
    builder.Services.AddTransient<ICreatePatientEventCommand, CreatePatientEventCommand>();
    builder.Services.AddTransient<ICreateDataAcquisitionRequestedEventCommand, CreateDataAcquisitionRequestedEventCommand>();
    
    //Factory
    builder.Services.AddTransient<IKafkaProducerFactory, KafkaProducerFactory>();

    builder.Services.AddCors(options =>
    {
        options.AddPolicy("CorsPolicy",
            builder => builder
                .AllowAnyMethod()
                .AllowCredentials()
                .SetIsOriginAllowed((host) => true) //lock this down, allows all atm
                .AllowAnyHeader());
    });

    //sync mapping between client and api gateway
    JwtSecurityTokenHandler.DefaultInboundClaimTypeMap.Clear();    
    var idpConfig = builder.Configuration.GetSection(GatewayConstants.AppSettingsSectionNames.IdentityProvider).Get<IdentityProviderConfig>() ?? throw new NullReferenceException("Identity Provider Configuration was null.");
    builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(options =>
        {
            options.Authority = idpConfig.Issuer; //gets the IDP metadata about endpoints and keys
            options.Audience = idpConfig.Audience;            
            if (builder.Environment.IsDevelopment()) {
                options.RequireHttpsMetadata = false;
            }            
            options.TokenValidationParameters = new()
            {
                NameClaimType = idpConfig.NameClaimType,
                RoleClaimType = idpConfig.RoleClaimType,  
                
                ValidTypes = idpConfig.ValidTypes //avoid jwt confustion attacks (ie: circumvent token signature checking)
            };
        });

    builder.Services.AddAuthorization(authorizationOptions => 
    {
        authorizationOptions.AddPolicy("UserCanViewAuditLogs", AuthorizationPolicies.CanViewAuditLogs());
        authorizationOptions.AddPolicy("CanCreateNotifiactionConfigurations", AuthorizationPolicies.CanCreateNotifiactionConfigurations());
        authorizationOptions.AddPolicy("CanUpdateNotifiactionConfigurations", AuthorizationPolicies.CanUpdateNotifiactionConfigurations());
        authorizationOptions.AddPolicy("CanDeleteNotifiactionConfigurations", AuthorizationPolicies.CanDeleteNotifiactionConfigurations());
        
        authorizationOptions.AddPolicy("ClientApplicationCanRead", policyBuilder =>
        {            
            policyBuilder.RequireScope("botwdemogatewayapi.read");
        });

        authorizationOptions.AddPolicy("ClientApplicationCanCreate", policyBuilder =>
        {
            policyBuilder.RequireScope("botwdemogatewayapi.write");
        }); 

        authorizationOptions.AddPolicy("ClientApplicationCanDelete", policyBuilder =>
        {
            policyBuilder.RequireScope("botwdemogatewayapi.delete");
        });

    });

    builder.Services.AddControllers(options => { options.ReturnHttpNotAcceptable = true; }).AddXmlDataContractSerializerFormatters();

    // Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen();

    // Logging using Serilog
    builder.Logging.AddSerilog();
    Log.Logger = new LoggerConfiguration()
                    .ReadFrom.Configuration(builder.Configuration)
                    .Filter.ByExcluding("RequestPath like '/health%'")
                    .Filter.ByExcluding("RequestPath like '/swagger%'")
                    .Enrich.WithExceptionDetails()
                    .Enrich.FromLogContext()
                    .Enrich.WithSpan()
                    .Enrich.With<ActivityEnricher>()
                    .CreateLogger();

    //Serilog.Debugging.SelfLog.Enable(Console.Error);  

    var telemetryConfig = builder.Configuration.GetSection(GatewayConstants.AppSettingsSectionNames.Telemetry).Get<TelemetryConfig>();
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
                        options.Filter = (httpContext) => httpContext.Request.Path != "/swagger"; //do not capture traces for the swagger endpoint
                    })
                    .AddConfluentKafkaInstrumentation()
                    .AddOtlpExporter(opts => { opts.Endpoint = new Uri(telemetryConfig.TelemetryCollectorEndpoint); }));

        otel.WithMetrics(metricsProviderBuilder =>
                metricsProviderBuilder
                    .AddAspNetCoreInstrumentation()
                    .AddRuntimeInstrumentation()                    
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
    if (app.Environment.IsDevelopment())
    {
        app.UseDeveloperExceptionPage();
    }
    else
    {
        app.UseExceptionHandler();
    }

    app.UseHeaderPropagation();

    // Configure the HTTP request pipeline.
    if (app.Configuration.GetValue<bool>("EnableSwagger"))
    {
        var serviceInformation = app.Configuration.GetSection(GatewayConstants.AppSettingsSectionNames.ServiceInformation).Get<ServiceInformation>();
        app.UseSwagger();
        app.UseSwaggerUI(opts => opts.SwaggerEndpoint("/swagger/v1/swagger.json", serviceInformation != null ? $"{serviceInformation.Name} - {serviceInformation.Version}" : "Demo API Gateway v1"));
    }

    app.UseRouting();
    app.UseCors("CorsPolicy");
    app.UseAuthentication();
    app.UseMiddleware<UserScopeMiddleware>();
    app.UseAuthorization();
    app.UseEndpoints(endpoints => endpoints.MapControllers());
}

#endregion