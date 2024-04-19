using LantanaGroup.Link.Audit.Settings;
using LantanaGroup.Link.Audit.Application.Interfaces;
using LantanaGroup.Link.Audit.Listeners;
using LantanaGroup.Link.Audit.Application.Commands;
using LantanaGroup.Link.Audit.Application.Factory;
using LantanaGroup.Link.Audit.Application.Audit.Queries;
using LantanaGroup.Link.Audit.Infrastructure.AuditHelper;
using Serilog;
using Serilog.Exceptions;
using Serilog.Enrichers.Span;
using LantanaGroup.Link.Audit.Application.Models;
using LantanaGroup.Link.Audit.Infrastructure;
using System.Reflection;
using LantanaGroup.Link.Audit.Infrastructure.Health;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using HealthChecks.UI.Client;
using LantanaGroup.Link.Audit.Infrastructure.Extensions;
using LantanaGroup.Link.Shared.Application.Middleware;
using System.Diagnostics;
using Serilog.Settings.Configuration;
using Microsoft.Extensions.Configuration.AzureAppConfiguration;
using Azure.Identity;
using Microsoft.Extensions.Compliance.Redaction;
using LantanaGroup.Link.Audit.Infrastructure.Logging;
using Microsoft.Extensions.Compliance.Classification;
using System.Text;
using LantanaGroup.Link.Audit.Persistance.Repositories;
using LantanaGroup.Link.Audit.Persistance;
using Microsoft.EntityFrameworkCore;
using LantanaGroup.Link.Audit.Persistance.Interceptors;
using LantanaGroup.Link.Shared.Application.Extensions;
using LantanaGroup.Link.Shared.Settings;
using LantanaGroup.Link.Audit.Infrastructure.Telemetry;
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
    var externalConfigurationSource = builder.Configuration.GetSection(AuditConstants.AppSettingsSectionNames.ExternalConfigurationSource).Get<string>();
    if(!string.IsNullOrEmpty(externalConfigurationSource))
    {
        switch (externalConfigurationSource)
        { 
            case("AzureAppConfiguration"):
                builder.Configuration.AddAzureAppConfiguration(options =>
                {
                    options.Connect(builder.Configuration.GetConnectionString("AzureAppConfiguration"))
                        // Load configuration values with no label
                        .Select("*", LabelFilter.Null)
                        // Load configuration values for service name
                        .Select("*", AuditConstants.ServiceName)
                        // Load configuration values for service name and environment
                        .Select("*", AuditConstants.ServiceName + ":" + builder.Environment.EnvironmentName);

                    options.ConfigureKeyVault(kv =>
                    {
                        kv.SetCredential(new DefaultAzureCredential());
                    });

                });
                break;
        }
    }   

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
    builder.Services.Configure<BrokerConnection>(builder.Configuration.GetRequiredSection(AuditConstants.AppSettingsSectionNames.Kafka));
    builder.Services.Configure<ServiceRegistry>(builder.Configuration.GetSection(ServiceRegistry.ConfigSectionName));
    builder.Services.AddTransient<IAuditHelper, AuditHelper>();
    builder.Services.AddSingleton<TimeProvider>(TimeProvider.System);

    //Add commands
    builder.Services.AddTransient<ICreateAuditEventCommand, CreateAuditEventCommand>();

    //Add queries
    builder.Services.AddTransient<IGetAuditEventQuery, GetAuditEventQuery>();
    builder.Services.AddTransient<IGetFacilityAuditEventsQuery, GetFacilityAuditEventsQuery>();
    builder.Services.AddTransient<IGetAuditEventListQuery, GetAuditEventListQuery>();

    //Add factories
    builder.Services.AddSingleton<IAuditFactory, AuditFactory>();
    builder.Services.AddTransient<IKafkaConsumerFactory, KafkaConsumerFactory>();

    //Add persistence interceptors
    builder.Services.AddSingleton<UpdateBaseEntityInterceptor>();

    //Add database context
    builder.Services.AddDbContext<AuditDbContext>((sp, options) => {

        var updateBaseEntityInterceptor = sp.GetRequiredService<UpdateBaseEntityInterceptor>();

        switch(builder.Configuration.GetValue<string>(AuditConstants.AppSettingsSectionNames.DatabaseProvider))
        {          
            case "SqlServer":
                string? connectionString = builder.Configuration.GetValue<string>(AuditConstants.AppSettingsSectionNames.DatabaseConnectionString);
                
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
    builder.Services.AddScoped<ISearchRepository, MsSqlAuditLogSearchRepository>();

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
                    .Enrich.WithExceptionDetails()
                    .Enrich.FromLogContext()
                    .Enrich.WithSpan()                  
                    .Enrich.With<ActivityEnricher>()
                    .CreateLogger();

    //Serilog.Debugging.SelfLog.Enable(Console.Error);  

    //Add telemetry if enabled
    if(builder.Configuration.GetValue<bool>($"{ConfigurationConstants.AppSettings.Telemetry}:EnableTelemetry"))
    {
        builder.Services.AddLinkTelemetry(builder.Configuration, options =>
        {
            options.Environment = builder.Environment;
            options.ServiceName = AuditConstants.ServiceName;
            options.ServiceVersion = serviceInformation.Version; //TODO: Get version from assembly?
            options.InstrumentEntityFramework = true;
        });

        builder.Services.AddSingleton<IAuditServiceMetrics, AuditServiceMetrics>();
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
    if (app.Configuration.GetValue<bool>(AuditConstants.AppSettingsSectionNames.EnableSwagger))
    {
        var serviceInformation = app.Configuration.GetSection(AuditConstants.AppSettingsSectionNames.ServiceInformation).Get<ServiceInformation>();
        app.UseSwagger();
        app.UseSwaggerUI(opts => opts.SwaggerEndpoint("/swagger/v1/swagger.json", serviceInformation != null ? $"{serviceInformation.Name} - {serviceInformation.Version}" : "Link Audit Service"));
    }

    //TODO: Discuss migrations rather than ensure created
    // Ensure database created (temporary), not for production
    using (var scope = app.Services.CreateScope())
    {
        var context = scope.ServiceProvider.GetRequiredService<AuditDbContext>();
        context.Database.EnsureCreated();
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
     
}

#endregion