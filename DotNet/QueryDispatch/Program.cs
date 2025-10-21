using HealthChecks.UI.Client;
using LanatanGroup.Link.QueryDispatch.Jobs;
using LantanaGroup.Link.QueryDispatch.Application.Factory;
using LantanaGroup.Link.QueryDispatch.Application.Interfaces;
using LantanaGroup.Link.QueryDispatch.Application.Models;
using LantanaGroup.Link.QueryDispatch.Domain.Entities;
using LantanaGroup.Link.QueryDispatch.Listeners;
using LantanaGroup.Link.QueryDispatch.Presentation.Services;
using LantanaGroup.Link.Shared.Application.Error.Handlers;
using LantanaGroup.Link.Shared.Application.Error.Interfaces;
using LantanaGroup.Link.Shared.Application.Extensions;
using LantanaGroup.Link.Shared.Application.Extensions.Quartz;
using LantanaGroup.Link.Shared.Application.Extensions.Security;
using LantanaGroup.Link.Shared.Application.Factories;
using LantanaGroup.Link.Shared.Application.Health;
using LantanaGroup.Link.Shared.Application.Interfaces;
using LantanaGroup.Link.Shared.Application.Listeners;
using LantanaGroup.Link.Shared.Application.Middleware;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using LantanaGroup.Link.Shared.Application.Models.Kafka;
using LantanaGroup.Link.Shared.Application.Services;
using LantanaGroup.Link.Shared.Application.Utilities;
using LantanaGroup.Link.Shared.Domain.Repositories.Interfaces;
using LantanaGroup.Link.Shared.Jobs;
using LantanaGroup.Link.Shared.Settings;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.OpenApi.Models;
using QueryDispatch.Application.Extensions;
using QueryDispatch.Application.Interfaces;
using QueryDispatch.Application.Services;
using QueryDispatch.Application.Settings;
using QueryDispatch.Domain;
using QueryDispatch.Domain.Context;
using QueryDispatch.Domain.Managers;
using Serilog;
using Serilog.Enrichers.Span;
using Serilog.Exceptions;
using System.Diagnostics;
using System.Reflection;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);
builder.Configuration.AddStandardEnvironmentConfiguration();

// load external configuration source (if specified)
builder.AddExternalConfiguration(QueryDispatchConstants.ServiceName);

var assemblyVersion = Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? string.Empty;

var serviceInformation = builder.SetupServiceInformation(QueryDispatchConstants.ServiceName, assemblyVersion);

var kafkaConnection = builder.Configuration.GetSection(KafkaConstants.SectionName).Get<KafkaConnection>();
builder.Services.AddSingleton<KafkaConnection>(kafkaConnection);
builder.Services.Configure<ServiceRegistry>(builder.Configuration.GetSection(ServiceRegistry.ConfigSectionName));
builder.Services.Configure<CorsSettings>(builder.Configuration.GetSection(ConfigurationConstants.AppSettings.CORS));
builder.Services.Configure<LinkTokenServiceSettings>(builder.Configuration.GetSection(ConfigurationConstants.AppSettings.LinkTokenService));

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

//Add database context
builder.AddSQLServerEF<QueryDispatchDbContext>(true);

IConfigurationSection consumerSettingsSection = builder.Configuration.GetRequiredSection(nameof(ConsumerSettings));
builder.Services.Configure<ConsumerSettings>(consumerSettingsSection);
var consumerSettings = consumerSettingsSection.Get<ConsumerSettings>();

// Add services to the container.
builder.Services.AddControllers(options => { options.ReturnHttpNotAcceptable = true; }).AddXmlDataContractSerializerFormatters().AddJsonOptions(opt => opt.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddHttpClient();


//Register Kafka
builder.Services.RegisterKafka(kafkaConnection);


builder.Services.AddTransient<IRetryModelFactory, RetryModelFactory>();

builder.Services.AddTransient<IQueryDispatchFactory, QueryDispatchFactory>();
builder.Services.AddTransient<IQueryDispatchConfigurationFactory, QueryDispatchConfigurationFactory>();

//Add repos
builder.Services.AddTransient<IBaseEntityRepository<ScheduledReportEntity>, DataEntityRepository<ScheduledReportEntity>>();
builder.Services.AddTransient<IBaseEntityRepository<PatientDispatchEntity>, DataEntityRepository<PatientDispatchEntity>>();
builder.Services.AddTransient<IBaseEntityRepository<QueryDispatchConfigurationEntity>, DataEntityRepository<QueryDispatchConfigurationEntity>>();
builder.Services.AddTransient<IDatabase, Database>();


// Add Managers
builder.Services.AddTransient<IQueryDispatchConfigurationManager, QueryDispatchConfigurationManager>();
builder.Services.AddTransient<IPatientDispatchManager, PatientDispatchManager>();
builder.Services.AddTransient<IScheduledReportManager, ScheduledReportManager>();


//Excepation Handlers
builder.Services.AddSingleton(typeof(IExceptionLogger<>), typeof(ExceptionLogger<>));
builder.Services.AddSingleton(typeof(ITransientExceptionHandler<,,>), typeof(TransientExceptionHandler<,,>));
builder.Services.AddSingleton(typeof(IDeadLetterExceptionHandler<,,>), typeof(DeadLetterExceptionHandler<,,>));

//Add Services
builder.Services.AddTransient<ITenantApiService, TenantApiService>();

var connectionString = builder.Configuration.GetConnectionString(ConfigurationConstants.DatabaseConnections.DatabaseConnection);
builder.Services.RegisterQuartzDatabase(connectionString);

//Add Hosted Services
if (consumerSettings != null && !consumerSettings.DisableConsumer)
{
    builder.Services.AddHostedService<PatientEventListener>();
    builder.Services.AddHostedService<ReportScheduledEventListener>();
    builder.Services.AddHostedService<ScheduleService>();

}

if (consumerSettings != null && !consumerSettings.DisableRetryConsumer)
{
    builder.Services.AddSingleton(new RetryListenerSettings(serviceInformation.ServiceName, [KafkaTopic.ReportScheduledRetry.GetStringValue(), KafkaTopic.PatientEventRetry.GetStringValue()]));
    builder.Services.AddHostedService<RetryListener>();
    builder.Services.AddHostedService<RetryScheduleService>();
    builder.Services.AddSingleton<RetryJob>();
}

builder.Services.AddSingleton<QueryDispatchJob>();


//Add problem details
builder.Services.AddProblemDetails(options =>
{
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
            ctx.ProblemDetails.Extensions.Add("service", "QueryDispatch");
        }
        else
        {
            ctx.ProblemDetails.Extensions.Remove("exception");
        }

    };
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
    c.DocumentFilter<HealthChecksFilter>();
});

//Add health checks
var kafkaHealthOptions = new KafkaHealthCheckConfiguration(kafkaConnection, QueryDispatchConstants.ServiceName).GetHealthCheckOptions();

builder.Services.AddHealthChecks()
    .AddDbContextCheck<QueryDispatchDbContext>(HealthCheckType.Database.ToString())
    .AddKafka(kafkaHealthOptions, HealthCheckType.Kafka.ToString());

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

//Add CORS
builder.Services.AddLinkCorsService(options =>
{
    options.Environment = builder.Environment;
});

//Add telemetry if enabled
builder.Services.AddLinkTelemetry(builder.Configuration, options =>
{
    options.Environment = builder.Environment;
    options.ServiceName = QueryDispatchConstants.ServiceName;
    options.ServiceVersion = serviceInformation.Version; //TODO: Get version from assembly?                
});

builder.Services.AddSingleton<IQueryDispatchServiceMetrics, QueryDispatchServiceMetrics>();

var app = builder.Build();

// Configure the HTTP request pipeline.
SetupMiddleware(app);

app.Run();


static void SetupMiddleware(WebApplication app)
{
    // Configure the HTTP request pipeline.
    app.ConfigureSwagger();

    if (app.Environment.IsDevelopment())
    {
        app.UseDeveloperExceptionPage();
    }
    else
    {
        app.UseExceptionHandler();
    }

    //Run DB migrations
    app.AutoMigrateEF<QueryDispatchDbContext>();

    //map health check middleware and info endpoint   
    app.MapHealthChecks("/health", new HealthCheckOptions
    {
        ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
    });
    app.MapInfo(Assembly.GetExecutingAssembly(), app.Configuration, "querydispatch");

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

    app.UseEndpoints(endpoints => endpoints.MapControllers());
}
