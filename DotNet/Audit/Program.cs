using Azure.Identity;
using HealthChecks.UI.Client;
using LantanaGroup.Link.Audit.Application.Interfaces;
using LantanaGroup.Link.Audit.Application.Services;
using LantanaGroup.Link.Audit.Domain.Managers;
using LantanaGroup.Link.Audit.Infrastructure;
using LantanaGroup.Link.Audit.Infrastructure.Health;
using LantanaGroup.Link.Audit.Infrastructure.Logging;
using LantanaGroup.Link.Audit.Infrastructure.Telemetry;
using LantanaGroup.Link.Audit.Listeners;
using LantanaGroup.Link.Audit.Persistance;
using LantanaGroup.Link.Audit.Persistance.Interceptors;
using LantanaGroup.Link.Audit.Persistance.Repositories;
using LantanaGroup.Link.Audit.Settings;
using LantanaGroup.Link.Shared.Application.Error.Handlers;
using LantanaGroup.Link.Shared.Application.Error.Interfaces;
using LantanaGroup.Link.Shared.Application.Extensions;
using LantanaGroup.Link.Shared.Application.Extensions.Security;
using LantanaGroup.Link.Shared.Application.Factories;
using LantanaGroup.Link.Shared.Application.Factory;
using LantanaGroup.Link.Shared.Application.Health;
using LantanaGroup.Link.Shared.Application.Interfaces;
using LantanaGroup.Link.Shared.Application.Listeners;
using LantanaGroup.Link.Shared.Application.Middleware;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using LantanaGroup.Link.Shared.Application.Models.Kafka;
using LantanaGroup.Link.Shared.Application.Services;
using LantanaGroup.Link.Shared.Application.Utilities;
using LantanaGroup.Link.Shared.Jobs;
using LantanaGroup.Link.Shared.Settings;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Compliance.Classification;
using Microsoft.Extensions.Compliance.Redaction;
using Microsoft.Extensions.Configuration.AzureAppConfiguration;
using Quartz;
using Quartz.Impl;
using Quartz.Spi;
using Serilog;
using Serilog.Enrichers.Span;
using Serilog.Settings.Configuration;
using System.Collections.Specialized;
using System.Reflection;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

RegisterServices(builder);
var app = builder.Build();
SetupMiddleware(app);

app.Run();

#region Register Services

static void RegisterServices(WebApplicationBuilder builder)
{
    // load external configuration source (if specified)
    builder.AddExternalConfiguration(AuditConstants.ServiceName);

    var serviceInformation = builder.Configuration.GetRequiredSection(AuditConstants.AppSettingsSectionNames.ServiceInformation).Get<ServiceInformation>();
    if (serviceInformation != null)
    {
        ServiceActivitySource.Initialize(serviceInformation);
    }
    else
    {
        throw new NullReferenceException("Service Information was null.");
    }

    //Add problem details
    builder.Services.AddProblemDetailsService(options =>
    {
        options.Environment = builder.Environment;
        options.ServiceName = AuditConstants.ServiceName;
        options.IncludeExceptionDetails = builder.Configuration.GetValue<bool>("ProblemDetails:IncludeExceptionDetails");
    });

    // Add services to the container. 
    builder.Services.Configure<KafkaConnection>(builder.Configuration.GetSection(KafkaConstants.SectionName));
    builder.Services.Configure<ServiceRegistry>(builder.Configuration.GetSection(ServiceRegistry.ConfigSectionName));
    builder.Services.Configure<ConsumerSettings>(builder.Configuration.GetRequiredSection(nameof(ConsumerSettings)));
    builder.Services.Configure<CorsSettings>(builder.Configuration.GetSection(ConfigurationConstants.AppSettings.CORS));
    builder.Services.Configure<LinkTokenServiceSettings>(builder.Configuration.GetSection(ConfigurationConstants.AppSettings.LinkTokenService));
    builder.Services.AddSingleton<TimeProvider>(TimeProvider.System);

    // Add kafka connection singleton
    var kafkaConnection = builder.Configuration.GetSection(KafkaConstants.SectionName).Get<KafkaConnection>();
    if (kafkaConnection is null) throw new NullReferenceException("Kafka Connection is required.");
    builder.Services.AddSingleton(kafkaConnection);

    //Add Managers
    builder.Services.AddScoped<IAuditManager, AuditManager>();

    //Add event processors
    builder.Services.AddTransient<IAuditEventProcessor, AuditEventProcessor>();

    //Add factories
    builder.Services.AddSingleton<InMemorySchedulerFactory>();
    builder.Services.AddKeyedSingleton<ISchedulerFactory>(ConfigurationConstants.RunTimeConstants.RetrySchedulerKeyedSingleton, (provider, key) => provider.GetRequiredService<InMemorySchedulerFactory>());
    
    builder.Services.AddTransient<IKafkaConsumerFactory<string, AuditEventMessage>, KafkaConsumerFactory<string, AuditEventMessage>>();
    builder.Services.AddTransient<IKafkaConsumerFactory<string, string>, KafkaConsumerFactory<string, string>>();
    builder.Services.AddTransient<IKafkaProducerFactory<string, AuditEventMessage>, KafkaProducerFactory<string, AuditEventMessage>>();
    builder.Services.AddTransient<IKafkaProducerFactory<string, string>, KafkaProducerFactory<string, string>>();    

    //Add event exception handlers
    builder.Services.AddTransient<IDeadLetterExceptionHandler<string, AuditEventMessage>, DeadLetterExceptionHandler<string, AuditEventMessage>>();
    builder.Services.AddTransient<IDeadLetterExceptionHandler<string, string>, DeadLetterExceptionHandler<string, string>>();
    builder.Services.AddTransient<ITransientExceptionHandler<string, AuditEventMessage>, TransientExceptionHandler<string, AuditEventMessage>>();
        

    //Add persistence interceptors
    builder.Services.AddSingleton<UpdateBaseEntityInterceptor>();

    //Add database context
    builder.Services.AddDbContext<AuditDbContext>((sp, options) => {

        var updateBaseEntityInterceptor = sp.GetRequiredService<UpdateBaseEntityInterceptor>();

        switch(builder.Configuration.GetValue<string>(AuditConstants.AppSettingsSectionNames.DatabaseProvider))
        {          
            case ConfigurationConstants.AppSettings.SqlServerDatabaseProvider:
                string? connectionString =
                    builder.Configuration.GetConnectionString(ConfigurationConstants.DatabaseConnections
                        .DatabaseConnection);
                
                if (string.IsNullOrEmpty(connectionString))
                    throw new InvalidOperationException("Database connection string is null or empty.");

                options
                    .UseSqlServer(connectionString)
                    .AddInterceptors(updateBaseEntityInterceptor);

                break;
            default:
                throw new InvalidOperationException("Database provider not supported.");
        }
    });       

    //Add repositories
    builder.Services.AddScoped<IAuditRepository, AuditLogRepository>();
    builder.Services.AddScoped<ISearchRepository, AuditLogSearchRepository>();

    //Add Hosted Services
    builder.Services.AddHostedService<AuditEventListener>();

    var consumerSettings = builder.Configuration.GetSection(nameof(ConsumerSettings)).Get<ConsumerSettings>();

    var quartzProps = new NameValueCollection
    {
        ["quartz.scheduler.instanceName"] = "AuditScheduler",
        ["quartz.scheduler.instanceId"] = "AUTO",
        ["quartz.jobStore.clustered"] = "true",
        ["quartz.jobStore.type"] = "Quartz.Impl.AdoJobStore.JobStoreTX, Quartz",
        ["quartz.jobStore.driverDelegateType"] = "Quartz.Impl.AdoJobStore.SqlServerDelegate, Quartz",
        ["quartz.jobStore.tablePrefix"] = "quartz.QRTZ_",
        ["quartz.jobStore.dataSource"] = "default",
        ["quartz.dataSource.default.connectionString"] = builder.Configuration.GetConnectionString(ConfigurationConstants.DatabaseConnections.DatabaseConnection),
        ["quartz.dataSource.default.provider"] = "SqlServer",
        ["quartz.threadPool.type"] = "Quartz.Simpl.SimpleThreadPool, Quartz",
        ["quartz.threadPool.threadCount"] = "5",
        ["quartz.jobStore.useProperties"] = "false",
        ["quartz.serializer.type"] = "json"
    };

    // Register main persistent scheduler factory
    builder.Services.AddSingleton<ISchedulerFactory>(new StdSchedulerFactory(quartzProps));
    builder.Services.AddKeyedSingleton(ConfigurationConstants.RunTimeConstants.RetrySchedulerKeyedSingleton, (provider, key) => provider.GetRequiredService<ISchedulerFactory>());

    if (consumerSettings != null && !consumerSettings.DisableRetryConsumer)
    {
        builder.Services.AddTransient<IRetryModelFactory, RetryModelFactory>();
        builder.Services.AddTransient<IJobFactory, QuartzJobFactory>();
        builder.Services.AddTransient<RetryJob>();

        builder.Services.AddSingleton(new RetryListenerSettings(AuditConstants.ServiceName, [KafkaTopic.AuditableEventOccurredRetry.GetStringValue()]));
        builder.Services.AddHostedService<RetryListener>();
        builder.Services.AddHostedService<RetryScheduleService>();
    }

    //Add health checks
    var kafkaHealthOptions = new KafkaHealthCheckConfiguration(kafkaConnection, AuditConstants.ServiceName).GetHealthCheckOptions();

    builder.Services.AddHealthChecks()
        .AddCheck<DatabaseHealthCheck>(HealthCheckType.Database.ToString())
        .AddKafka(kafkaHealthOptions, HealthCheckType.Kafka.ToString());

    //configure CORS
    builder.Services.AddLinkCorsService(options => {
        options.Environment = builder.Environment;
    });

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

    builder.Services.AddControllers(options => { options.ReturnHttpNotAcceptable = true; }).AddXmlDataContractSerializerFormatters();    

    // Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen(c =>
    {
        var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
        var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
        c.IncludeXmlComments(xmlPath);
        c.DocumentFilter<HealthChecksFilter>();
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
    var loggerOptions = new ConfigurationReaderOptions { SectionName = AuditConstants.AppSettingsSectionNames.Serilog };    
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
        options.ServiceName = AuditConstants.ServiceName;
        options.ServiceVersion = serviceInformation.Version; //TODO: Get version from assembly?                
    });

    builder.Services.AddSingleton<IAuditServiceMetrics, AuditServiceMetrics>();
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

    // Auto migrate database
    app.AutoMigrateEF<AuditDbContext>();
    
    app.UseRouting();
    app.UseCors(CorsSettings.DefaultCorsPolicyName);
    app.UseAuthentication();
    app.UseMiddleware<UserScopeMiddleware>();
    app.UseAuthorization();

    //map health check middleware and info endpoint
    app.MapHealthChecks("/health", new HealthCheckOptions { 
        ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
    });  
    app.MapInfo(Assembly.GetExecutingAssembly(), app.Configuration, "audit");

    app.UseEndpoints(endpoints => endpoints.MapControllers());
}

#endregion