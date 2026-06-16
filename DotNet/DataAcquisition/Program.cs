using HealthChecks.UI.Client;
using Hl7.Fhir.Model;
using Hl7.Fhir.Serialization;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Interfaces;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Serializers;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Services;
using LantanaGroup.Link.DataAcquisition.Domain.Extensions;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Context;
using LantanaGroup.Link.DataAcquisition.Domain.Settings;
using LantanaGroup.Link.DataAcquisition.Jobs;
using LantanaGroup.Link.DataAcquisition.Listeners;
using LantanaGroup.Link.Shared.Application.Extensions;
using LantanaGroup.Link.Shared.Application.Extensions.Security;
using LantanaGroup.Link.Shared.Application.Factories;
using LantanaGroup.Link.Shared.Application.Health;
using LantanaGroup.Link.Shared.Application.Interfaces;
using LantanaGroup.Link.Shared.Application.Listeners;
using LantanaGroup.Link.Shared.Application.Middleware;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using LantanaGroup.Link.Shared.Application.Services;
using LantanaGroup.Link.Shared.Application.Utilities;
using LantanaGroup.Link.Shared.Settings;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.OpenApi.Models;
using System.Reflection;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.DataProtection;

using Microsoft.AspNetCore.Antiforgery;

var builder = WebApplication.CreateBuilder(args);
builder.Configuration.AddStandardEnvironmentConfiguration();

RegisterServices(builder);
var app = builder.Build();
SetupMiddleware(app);

app.Run();

#region Register Services

static void RegisterServices(WebApplicationBuilder builder)
{
    builder.RegisterAll(DataAcquisitionConstants.ServiceName, configureRedis: true);

    var consumerSettings = builder.Configuration.GetRequiredSection(nameof(ConsumerSettings)).Get<ConsumerSettings>();

    // Add Data Protection
    builder.Services.AddDataProtection()
        .SetApplicationName(builder.Configuration.GetValue<string>("DataProtection:KeyRing") ?? "Link");

    // Configure antiforgery for browser-based callers (Admin.UI via YARP).
    // Bearer-authenticated callers (LinkSdk) bypass validation via ValidateAntiForgeryOrBearerTokenAttribute.
    builder.Services.AddAntiforgery(options =>
    {
        options.HeaderName = "X-Link-AntiForgery";
    });

    builder.Services.Configure<AcquisitionJobSettings>(builder.Configuration.GetSection(AcquisitionJobSettings.SectionName));
    builder.Services.Configure<TailMessageRecoveryJobSettings>(builder.Configuration.GetSection(TailMessageRecoveryJobSettings.SectionName));


    builder.Services.AddTransient<IRetryModelFactory, RetryModelFactory>();

    // Add services to the container.
    // Additional configuration is required to successfully run gRPC on macOS.
    // For instructions on how to configure Kestrel and gRPC clients on macOS, visit https://go.microsoft.com/fwlink/?linkid=2099682
    builder.Services.AddControllers(
        options => options.SuppressImplicitRequiredAttributeForNonNullableReferenceTypes = true
        ).AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
        options.JsonSerializerOptions.PropertyNameCaseInsensitive = true;
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
        options.JsonSerializerOptions.Converters.Add(new QueryConfigConverter());
        options.JsonSerializerOptions.Converters.Add(new ParameterConverter());
        options.JsonSerializerOptions.Converters.Add(new TimeSpanConverter());
        options.JsonSerializerOptions.ForFhir(ModelInfo.ModelInspector);
    });

    //Add Hosted Services
    builder.Services.AddHostedService<AcquisitionProcessingScheduleService>();

    if (!(consumerSettings?.DisableConsumer ?? false))
    {
        builder.Services.AddHostedService<DataAcquisitionRequestedListener>();
        builder.Services.AddHostedService<PatientCensusScheduledListener>();
    }

    if (!(consumerSettings?.DisableRetryConsumer ?? false))
    {
        builder.Services.AddSingleton(new RetryListenerSettings(DataAcquisitionConstants.ServiceName, [KafkaTopic.DataAcquisitionRequestedRetry.GetStringValue(), KafkaTopic.PatientCensusScheduledRetry.GetStringValue()]));
        builder.Services.AddHostedService<RetryListener>();
        builder.Services.AddHostedService<RetryScheduleService>();
        builder.Services.AddTransient<IRetryModelFactory, RetryModelFactory>();
    }

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

    //Add CORS
    builder.Services.AddLinkCorsService(options =>
    {
        options.Environment = builder.Environment;
    });

    //Add Health Check
    var kafkaConnection = builder.Configuration.GetRequiredSection(KafkaConstants.SectionName).Get<KafkaConnection>();
    var kafkaHealthOptions = new KafkaHealthCheckConfiguration(kafkaConnection, DataAcquisitionConstants.ServiceName).GetHealthCheckOptions();

    builder.Services.AddHealthChecks()
        .AddDbContextCheck<DataAcquisitionDbContext>(HealthCheckType.Database.ToString())
        .AddCheck<ResourceCacheHealthCheck>(HealthCheckType.Cache.ToString())
        .AddKafka(kafkaHealthOptions, HealthCheckType.Kafka.ToString());

    builder.Services.AddSingleton(TimeProvider.System);
    builder.Services.AddSingleton<IDataAcquisitionServiceMetrics, DataAcquisitionServiceMetrics>();
}

#endregion

#region Set up middleware

static void SetupMiddleware(WebApplication app)
{
    app.ConfigureSwagger();

    app.AutoMigrateEF<DataAcquisitionDbContext>();

    if (app.Environment.IsDevelopment())
    {
        app.UseDeveloperExceptionPage();
    }
    else
    {
        app.UseExceptionHandler();
    }

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

    app.MapControllers();

    // Antiforgery token endpoint for browser-based callers (Admin.UI via YARP proxy).
    // Returns the request token in the response body and sets the cookie token automatically.
    app.MapGet("/api/data/antiforgery-token", (IAntiforgery antiforgery, HttpContext context) =>
    {
        var tokens = antiforgery.GetAndStoreTokens(context);
        return Results.Ok(new { token = tokens.RequestToken, headerName = "X-Link-AntiForgery" });
    }).RequireAuthorization();

    //map health check middleware and info endpoint
    app.MapHealthChecks("/health", new HealthCheckOptions
    {
        ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse,
    });
    app.MapInfo(Assembly.GetExecutingAssembly(), app.Configuration, "data");
}

#endregion
