using Azure.Identity;
using HealthChecks.UI.Client;
using LantanaGroup.Link.Notification.Application.Factory;
using LantanaGroup.Link.Notification.Application.Interfaces;
using LantanaGroup.Link.Notification.Application.Interfaces.Clients;
using LantanaGroup.Link.Notification.Application.Models;
using LantanaGroup.Link.Notification.Application.Notification.Commands;
using LantanaGroup.Link.Notification.Application.Notification.Queries;
using LantanaGroup.Link.Notification.Application.NotificationConfiguration.Commands;
using LantanaGroup.Link.Notification.Application.NotificationConfiguration.Queries;
using LantanaGroup.Link.Notification.Infrastructure;
using LantanaGroup.Link.Notification.Infrastructure.EmailService;
using LantanaGroup.Link.Notification.Infrastructure.Health;
using LantanaGroup.Link.Notification.Infrastructure.Logging;
using LantanaGroup.Link.Notification.Infrastructure.Telemetry;
using LantanaGroup.Link.Notification.Listeners;
using LantanaGroup.Link.Notification.Persistence;
using LantanaGroup.Link.Notification.Persistence.Interceptors;
using LantanaGroup.Link.Notification.Persistence.Repositories;
using LantanaGroup.Link.Notification.Presentation.Clients;
using LantanaGroup.Link.Notification.Settings;
using LantanaGroup.Link.Shared.Application.Extensions;
using LantanaGroup.Link.Shared.Application.Extensions.Security;
using LantanaGroup.Link.Shared.Application.Middleware;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using LantanaGroup.Link.Shared.Settings;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Compliance.Classification;
using Microsoft.Extensions.Compliance.Redaction;
using Microsoft.Extensions.Configuration.AzureAppConfiguration;
using Serilog;
using Serilog.Enrichers.Span;
using Serilog.Settings.Configuration;
using System.Diagnostics;
using System.Reflection;
using System.Text;
using LantanaGroup.Link.Shared.Application.Models;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.OpenApi.Models;
using LantanaGroup.Link.Shared.Application.Health;

var builder = WebApplication.CreateBuilder(args);

RegisterServices(builder);
var app = builder.Build();
SetupMiddleware(app);

app.Run();

#region Register Services

static void RegisterServices(WebApplicationBuilder builder)
{
    //load external configuration source if specified
    var externalConfigurationSource = builder.Configuration.GetSection(NotificationConstants.AppSettingsSectionNames.ExternalConfigurationSource).Get<string>();
    if (!string.IsNullOrEmpty(externalConfigurationSource))
    {
        switch (externalConfigurationSource)
        {
            case ("AzureAppConfiguration"):
                builder.Configuration.AddAzureAppConfiguration(options =>
                {
                    options.Connect(builder.Configuration.GetConnectionString("AzureAppConfiguration"))
                        .Select("*", LabelFilter.Null)
                        // Load configuration values for service name
                        .Select("*", NotificationConstants.ServiceName)
                        // Load configuration values for service name and environment
                        .Select("*", NotificationConstants.ServiceName + ":" + builder.Environment.EnvironmentName);

                    options.ConfigureKeyVault(kv =>
                    {
                        kv.SetCredential(new DefaultAzureCredential());
                    });

                });
                break;
        }
    }

    var serviceInformation = builder.Configuration.GetRequiredSection(NotificationConstants.AppSettingsSectionNames.ServiceInformation).Get<ServiceInformation>();
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
                ctx.ProblemDetails.Extensions.Add("service", "Notification");
            }
            else
            {
                ctx.ProblemDetails.Extensions.Remove("exception");
            }

        };
    });

    // Add services to the container. 
    builder.Services.Configure<ServiceRegistry>(builder.Configuration.GetSection(ServiceRegistry.ConfigSectionName));
    var kafkaConnection = builder.Configuration.GetSection(KafkaConstants.SectionName).Get<KafkaConnection>();
    builder.Services.AddSingleton<KafkaConnection>(kafkaConnection);
    builder.Services.Configure<SmtpConnection>(builder.Configuration.GetRequiredSection(NotificationConstants.AppSettingsSectionNames.Smtp));
    builder.Services.Configure<Channels>(builder.Configuration.GetRequiredSection(NotificationConstants.AppSettingsSectionNames.Channels));
    builder.Services.Configure<CorsSettings>(builder.Configuration.GetSection(ConfigurationConstants.AppSettings.CORS));
    builder.Services.Configure<LinkTokenServiceSettings>(builder.Configuration.GetSection(ConfigurationConstants.AppSettings.LinkTokenService));

    // Add HttpClients
    builder.Services.AddHttpClient<IFacilityClient, FacilityClient>();

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
    builder.Services.AddSingleton(new KafkaProducerFactory(kafkaConnection).CreateAuditEventProducer());

    builder.Services.AddTransient<IKafkaConsumerFactory, KafkaConsumerFactory>();
    builder.Services.AddTransient<IAuditEventFactory, AuditEventFactory>();

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
    builder.Services.AddDbContext<NotificationDbContext>((sp, options) => {
        
        var updateBaseEntityInterceptor = sp.GetService<UpdateBaseEntityInterceptor>()!;
        
        switch (builder.Configuration.GetValue<string>(NotificationConstants.AppSettingsSectionNames.DatabaseProvider))
        {
            case ConfigurationConstants.AppSettings.SqlServerDatabaseProvider:
                string? connectionString =
                    builder.Configuration.GetConnectionString(ConfigurationConstants.DatabaseConnections.DatabaseConnection);
                
                if (string.IsNullOrEmpty(connectionString))
                    throw new InvalidOperationException("Database connection string is null or empty.");
                
                options.UseSqlServer(connectionString)
                    .AddInterceptors(updateBaseEntityInterceptor);                    
                break;
            default:
                throw new InvalidOperationException("Database provider not supported.");
        }
    });

    //Add repositories
    builder.Services.AddScoped<INotificationConfigurationRepository, NotificationConfigurationRepository>();
    builder.Services.AddScoped<INotificationRepository, NotificationRepository>();

    //Add health checks
    var kafkaHealthOptions = new KafkaHealthCheckConfiguration(kafkaConnection, NotificationConstants.ServiceName).GetHealthCheckOptions();

    builder.Services.AddHealthChecks()
        .AddCheck<DatabaseHealthCheck>("Database")
        .AddKafka(kafkaHealthOptions);

    //Add Hosted Services
    builder.Services.AddHostedService<NotificationRequestedListener>();

    //Add infrastructure
    builder.Services.AddTransient<IEmailService, EmailService>();

    //configure CORS
    builder.Services.AddLinkCorsService(options => {
        options.Environment = builder.Environment;
    });    

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
    var loggerOptions = new ConfigurationReaderOptions { SectionName = NotificationConstants.AppSettingsSectionNames.Serilog };
    Log.Logger = new LoggerConfiguration()
                    .ReadFrom.Configuration(builder.Configuration, loggerOptions)
                    .Filter.ByExcluding("RequestPath like '/health%'")
                    .Filter.ByExcluding("RequestPath like '/swagger%'")
                    //.Enrich.WithExceptionDetails()
                    .Enrich.FromLogContext()
                    .Enrich.WithSpan()
                    .Enrich.With<ActivityEnricher>()
                    .CreateLogger();

    //Serilog.Debugging.SelfLog.Enable(Console.Error);  

    //Add telemetry if enabled
    builder.Services.AddLinkTelemetry(builder.Configuration, options =>
    {
        options.Environment = builder.Environment;
        options.ServiceName = NotificationConstants.ServiceName;
        options.ServiceVersion = serviceInformation.Version; //TODO: Get version from assembly?                
    });

    builder.Services.AddSingleton<INotificationServiceMetrics, NotificationServiceMetrics>();   
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

    app.MapGet("/api/notification/info", () => Results.Ok($"Welcome to {ServiceActivitySource.ServiceName} version {ServiceActivitySource.Instance.Version}!")).AllowAnonymous();

    //map health check middleware
    app.MapHealthChecks("/health", new HealthCheckOptions
    {
        ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
    }).RequireCors("HealthCheckPolicy");

    app.UseEndpoints(endpoints => endpoints.MapControllers());

    app.ConfigureSwagger();
}

#endregion
