using HealthChecks.UI.Client;
using LantanaGroup.Link.Notification.Application.Extensions;
using LantanaGroup.Link.Notification.Application.Factory;
using LantanaGroup.Link.Notification.Application.Interfaces;
using LantanaGroup.Link.Notification.Application.Models;
using LantanaGroup.Link.Notification.Application.Notification.Commands;
using LantanaGroup.Link.Notification.Application.Notification.Queries;
using LantanaGroup.Link.Notification.Application.NotificationConfiguration.Commands;
using LantanaGroup.Link.Notification.Application.NotificationConfiguration.Queries;
using LantanaGroup.Link.Notification.Infrastructure;
using LantanaGroup.Link.Notification.Infrastructure.EmailService;
using LantanaGroup.Link.Notification.Infrastructure.Health;
using LantanaGroup.Link.Notification.Infrastructure.Telemetry;
using LantanaGroup.Link.Notification.Listeners;
using LantanaGroup.Link.Notification.Persistence;
using LantanaGroup.Link.Notification.Persistence.Notification;
using LantanaGroup.Link.Notification.Presentation.Services;
using LantanaGroup.Link.Notification.Settings;
using LantanaGroup.Link.Shared.Application.Middleware;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Serilog;
using Serilog.Enrichers.Span;
using Serilog.Exceptions;
using System.Diagnostics;
using System.Reflection;

var builder = WebApplication.CreateBuilder(args);

RegisterServices(builder);
var app = builder.Build();
SetupMiddleware(app);

app.Run();

#region Register Services

static void RegisterServices(WebApplicationBuilder builder)
{
    var serviceInformation = builder.Configuration.GetRequiredSection(NotificationConstants.AppSettingsSectionNames.ServiceInformation).Get<ServiceInformation>();
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
                ctx.ProblemDetails.Extensions.Add("service", "Notification");
            }
            else
            {
                ctx.ProblemDetails.Extensions.Remove("exception");
            }

        };
    });

    // Add services to the container. 
    builder.Services.Configure<KafkaConnection>(builder.Configuration.GetRequiredSection(NotificationConstants.AppSettingsSectionNames.Kafka));
    builder.Services.Configure<MongoConnection>(builder.Configuration.GetRequiredSection(NotificationConstants.AppSettingsSectionNames.Mongo));
    builder.Services.Configure<SmtpConnection>(builder.Configuration.GetRequiredSection(NotificationConstants.AppSettingsSectionNames.Smtp));
    builder.Services.Configure<Channels>(builder.Configuration.GetRequiredSection(NotificationConstants.AppSettingsSectionNames.Channels));

    // Additional configuration is required to successfully run gRPC on macOS.
    // For instructions on how to configure Kestrel and gRPC clients on macOS, visit https://go.microsoft.com/fwlink/?linkid=2099682
    builder.Services.AddGrpc();
    builder.Services.AddGrpcReflection();
    builder.Services.AddControllers(options => { options.ReturnHttpNotAcceptable = true; }).AddXmlDataContractSerializerFormatters();

    //Add commands
    builder.Services.AddTransient<ICreateFacilityConfigurationCommand, CreateFacilityConfigurationCommand>();
    builder.Services.AddTransient<IUpdateFacilityConfigurationCommand, UpdateFacilityConfigurationCommand>();
    builder.Services.AddTransient<IDeleteFacilityConfigurationCommand, DeleteFacilityConfigurationCommand>();
    builder.Services.AddTransient<ICreateNotificationCommand, CreateNotificationCommand>(); 
    builder.Services.AddTransient<ISendNotificationCommand, SendNotificationCommand>();    
    builder.Services.AddTransient<IValidateEmailAddressCommand, ValidateEmailAddressCommand>();
    builder.Services.AddTransient<ICreateAuditEventCommand, CreateAuditEventCommand>();

    //Add queries
    builder.Services.AddTransient<IGetFacilityConfigurationQuery, GetFacilityConfigurationQuery>();
    builder.Services.AddTransient<IFacilityConfigurationExistsQuery, FacilityConfigurationExistsQuery>();
    builder.Services.AddTransient<IGetFacilityConfigurationListQuery, GetFacilityConfigurationListQuery>();
    builder.Services.AddTransient<IGetNotificationConfigurationQuery, GetNotificationConfigurationQuery>();
    builder.Services.AddTransient<IGetNotificationQuery, GetNotificationQuery>();
    builder.Services.AddTransient<IGetFacilityNotificatonsQuery, GetFacilityNotificationsQuery>();
    builder.Services.AddTransient<IGetNotificationListQuery, GetNotificationListQuery>();

    //Add factories
    builder.Services.AddTransient<INotificationConfigurationFactory, NotificationConfigurationFactory>();
    builder.Services.AddTransient<INotificationFactory, NotificationFactory>();
    builder.Services.AddTransient<IKafkaProducerFactory, KafkaProducerFactory>();
    builder.Services.AddTransient<IKafkaConsumerFactory, KafkaConsumerFactory>();
    builder.Services.AddTransient<IAuditEventFactory, AuditEventFactory>();

    //Add repositories
    builder.Services.AddSingleton<INotificationConfigurationRepository, NotificationConfigMongoRepo>();
    builder.Services.AddSingleton<INotificationRepository, NotificationMongoRepo>();

    //Add health checks
    builder.Services.AddHealthChecks()
        .AddCheck<DatabaseHealthCheck>("Database");

    //Add Hosted Services
    builder.Services.AddHostedService<NotificationRequestedListener>();

    //Add infrastructure
    builder.Services.AddTransient<IEmailService, EmailService>();    

    //configure CORS
    builder.Services.AddCorsService(builder.Environment);

    //configure servive api security   
    var idpConfig = builder.Configuration.GetRequiredSection(NotificationConstants.AppSettingsSectionNames.IdentityProvider).Get<IdentityProviderConfig>();
    if (idpConfig != null)
    {
        builder.Services.AddAuthenticationService(idpConfig, builder.Environment);
    }
    else
    {
        throw new NullReferenceException("Identity Provider Configuration was null.");
    }

    //builder.Services.AddAuthorizationService();

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
    Log.Logger = new LoggerConfiguration()
                    .ReadFrom.Configuration(builder.Configuration)
                    .Filter.ByExcluding("RequestPath like '/health%'")
                    .Enrich.WithExceptionDetails()
                    .Enrich.FromLogContext()
                    .Enrich.WithSpan()
                    .Enrich.With<ActivityEnricher>()
                    .CreateLogger();

    //Serilog.Debugging.SelfLog.Enable(Console.Error);  

    var telemetryConfig = builder.Configuration.GetRequiredSection(NotificationConstants.AppSettingsSectionNames.Telemetry).Get<TelemetryConfig>();
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
        var serviceInformation = app.Configuration.GetSection(NotificationConstants.AppSettingsSectionNames.ServiceInformation).Get<ServiceInformation>();
        app.UseSwagger();
        app.UseSwaggerUI(opts => opts.SwaggerEndpoint("/swagger/v1/swagger.json", serviceInformation != null ? $"{serviceInformation.Name} - {serviceInformation.Version}" : "Link Audit Service"));
    }

    app.UseRouting();
    app.UseCors("CorsPolicy");
    app.UseAuthentication();
    app.UseMiddleware<UserScopeMiddleware>();
    app.UseAuthorization();

    //map health check middleware
    app.MapHealthChecks("/health", new HealthCheckOptions
    {
        ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
    });

    app.UseEndpoints(endpoints => endpoints.MapControllers());

    if (app.Configuration.GetValue<bool>("AllowReflection"))
    {
        app.MapGrpcReflectionService();
    }

    app.MapGrpcService<NotificationService>();
}

#endregion
