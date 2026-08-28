using System.Reflection;
using System.Security.Claims;
using System.Security.Cryptography.X509Certificates;
using HealthChecks.UI.Client;
using LantanaGroup.Link.Nhsn.App.Bff.Application.Interfaces.Services;
using LantanaGroup.Link.Nhsn.App.Bff.Application.Services.Acknowledgements;
using LantanaGroup.Link.Nhsn.App.Bff.Application.Services.FacilityAdministration;
using LantanaGroup.Link.Nhsn.App.Bff.Application.Services.Onboarding;
using LantanaGroup.Link.Nhsn.App.Bff.Application.Services.PatientsOfInterest;
using LantanaGroup.Link.Nhsn.App.Bff.Application.Services.Reference;
using LantanaGroup.Link.Nhsn.App.Bff.Application.Services.Session;
using LantanaGroup.Link.Nhsn.App.Bff.Infrastructure.Errors;
using LantanaGroup.Link.Nhsn.App.Bff.Infrastructure.Link.DependencyInjection;
using LantanaGroup.Link.Nhsn.App.Bff.Persistence;
using LantanaGroup.Link.Nhsn.App.Bff.Persistence.Seed;
using LantanaGroup.Link.Nhsn.App.Bff.Presentation.Endpoints;
using LantanaGroup.Link.Nhsn.App.Bff.Settings;
using LantanaGroup.Link.Shared.Application.Extensions;
using LantanaGroup.Link.Shared.Application.Extensions.Security;
using LantanaGroup.Link.Sdk.DependencyInjection;
using LantanaGroup.Link.Shared.Application.Health;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using LantanaGroup.Link.Shared.Settings;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.Text.Json;
using System.Text.Json.Serialization;
using Serilog;
using Serilog.Enrichers.Span;

var builder = WebApplication.CreateBuilder(args);
builder.Configuration.AddStandardEnvironmentConfiguration();

RegisterServices(builder);
var app = builder.Build();
SetupMiddleware(app);

app.Run();

static void RegisterServices(WebApplicationBuilder builder)
{
    builder.AddExternalConfiguration(NhsnAppConstants.ServiceName);
    ValidateConfiguration(builder);

    var assemblyVersion = Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? string.Empty;
    var serviceInformation = builder.SetupServiceInformation(NhsnAppConstants.ServiceName, assemblyVersion);
    ServiceActivitySource.Initialize(serviceInformation);

    builder.Services.AddProblemDetailsService(options =>
    {
        options.Environment = builder.Environment;
        options.ServiceName = serviceInformation.ServiceName;
        options.IncludeExceptionDetails = builder.Configuration.GetValue<bool>("ProblemDetails:IncludeExceptionDetails");
    });

    builder.Services.Configure<CorsSettings>(builder.Configuration.GetSection(ConfigurationConstants.AppSettings.CORS));
    builder.Services.AddLinkCorsService(options =>
    {
        options.Environment = builder.Environment;
    });
    builder.Services.Configure<NhsnJwtSettings>(builder.Configuration.GetRequiredSection(NhsnJwtSettings.SectionName));

    builder.Services.Configure<ServiceRegistry>(builder.Configuration.GetSection(ServiceRegistry.ConfigSectionName));
    builder.Services.Configure<LinkTokenServiceSettings>(builder.Configuration.GetSection(ConfigurationConstants.AppSettings.LinkTokenService));
    builder.Services.AddLinkSdk();

    builder.Services.AddDbContext<NhsnAppDbContext>(options =>
    {
        var connectionString = builder.Configuration.GetConnectionString(ConfigurationConstants.DatabaseConnections.DatabaseConnection)
                              ?? throw new InvalidOperationException("DatabaseConnection is required for NHSN-App-BFF.");
        options.UseSqlServer(connectionString);
    });

    var jwtSettings = builder.Configuration.GetRequiredSection(NhsnJwtSettings.SectionName).Get<NhsnJwtSettings>()
                     ?? throw new InvalidOperationException("NhsnJwt configuration is required.");
    var validationParameters = CreateValidationParameters(jwtSettings);
    var signingKeyId = validationParameters.IssuerSigningKey?.KeyId ?? "<none>";

    builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(options =>
        {
            options.RequireHttpsMetadata = !builder.Environment.IsDevelopment();
            options.MapInboundClaims = false;
            options.TokenValidationParameters = validationParameters;
            options.Events = new JwtBearerEvents
            {
                OnAuthenticationFailed = context =>
                {
                    var logger = context.HttpContext.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger("NhsnJwtAuth");
                    logger.LogWarning(
                        context.Exception,
                        "JWT authentication failed. Path={Path}; Scheme={Scheme}; Issuer={Issuer}; Audience={Audience}; SigningKeyId={SigningKeyId}; AuthHeaderPresent={AuthHeaderPresent}",
                        context.HttpContext.Request.Path,
                        context.Scheme.Name,
                        jwtSettings.Issuer,
                        string.IsNullOrWhiteSpace(jwtSettings.Audience) ? "<none>" : jwtSettings.Audience,
                        signingKeyId,
                        context.Request.Headers.ContainsKey("Authorization"));
                    return Task.CompletedTask;
                },
                OnChallenge = context =>
                {
                    if (TryHandleTokenRenewalRedirect(context, jwtSettings))
                    {
                        return Task.CompletedTask;
                    }

                    var logger = context.HttpContext.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger("NhsnJwtAuth");
                    logger.LogWarning(
                        "JWT challenge triggered. Path={Path}; Error={Error}; ErrorDescription={ErrorDescription}; SigningKeyId={SigningKeyId}; AuthHeaderPresent={AuthHeaderPresent}",
                        context.HttpContext.Request.Path,
                        context.Error,
                        context.ErrorDescription,
                        signingKeyId,
                        context.Request.Headers.ContainsKey("Authorization"));
                    return Task.CompletedTask;
                },
                OnTokenValidated = context =>
                {
                    EnforceMaximumTokenAge(context, jwtSettings);

                    var logger = context.HttpContext.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger("NhsnJwtAuth");
                    var subject = context.Principal?.FindFirstValue(jwtSettings.UserIdClaimType)
                                  ?? context.Principal?.FindFirstValue(jwtSettings.EmailClaimType)
                                  ?? context.Principal?.Identity?.Name
                                  ?? "<unknown>";
                    logger.LogInformation(
                        "JWT token validated successfully. Path={Path}; Subject={Subject}; ClaimsCount={ClaimsCount}; SigningKeyId={SigningKeyId}",
                        context.HttpContext.Request.Path,
                        subject,
                        context.Principal?.Claims.Count() ?? 0,
                        signingKeyId);
                    return Task.CompletedTask;
                }
            };
        });

    builder.Services.AddAuthorizationBuilder()
        .AddPolicy("AuthenticatedUser", policy => policy.RequireAuthenticatedUser());

    builder.Services.AddLinkGateways(builder.Configuration);
    builder.Services.AddExceptionHandler<FacilityWriteLockExceptionHandler>();
    builder.Services.AddExceptionHandler<LinkServiceExceptionHandler>();

    builder.Services.AddLinkTelemetry(builder.Configuration, options =>
    {
        options.Environment = builder.Environment;
        options.ServiceName = NhsnAppConstants.ServiceName;
        options.ServiceVersion = ServiceActivitySource.Instance.Version;
    });

    builder.Services.AddHttpContextAccessor();
    builder.Services.AddMemoryCache();
    builder.Services.AddScoped<INhsnUserContext, NhsnUserContext>();
    builder.Services.AddScoped<IUserInfoService, UserInfoService>();
    builder.Services.AddScoped<IOnboardingDraftStore, OnboardingDraftStore>();
    builder.Services.AddScoped<IOnboardingReadService, OnboardingReadService>();
    builder.Services.AddScoped<IOnboardingWriteService, OnboardingWriteService>();
    builder.Services.AddScoped<IAcknowledgementService, AcknowledgementService>();
    builder.Services.AddSingleton<IReferenceDataService, ReferenceDataService>();
    builder.Services.AddScoped<IPatientsOfInterestService, PatientsOfInterestService>();
    builder.Services.Configure<OnboardingReadSettings>(builder.Configuration.GetSection(OnboardingReadSettings.SectionName));
    builder.Services.AddScoped<IFacilityAdministrationService, FacilityAdministrationService>();
    builder.Services.AddScoped<ILocalizationResourceService, LocalizationResourceService>();
    builder.Services.Configure<LocalizationSettings>(builder.Configuration.GetSection(LocalizationSettings.SectionName));

    builder.Services.AddTransient<IApi, UserInfoEndpoints>();
    builder.Services.AddTransient<IApi, FacilityAdministrationEndpoints>();
    builder.Services.AddTransient<IApi, FhirServerEndpoints>();
    builder.Services.AddTransient<IApi, LocalizationEndpoints>();
    builder.Services.AddTransient<IApi, StaticAssetEndpoints>();
    builder.Services.AddTransient<IApi, OnboardingEndpoints>();
    builder.Services.AddTransient<IApi, ReferenceEndpoints>();
    builder.Services.AddTransient<IApi, PatientsOfInterestEndpoints>();
    builder.Services.AddHealthChecks().AddDbContextCheck<NhsnAppDbContext>(name: "database");
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen(options =>
    {
        options.SwaggerDoc("v1", new OpenApiInfo
        {
            Version = "v1",
            Title = "NHSN App BFF"
        });

        options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
        {
            Description = "JWT Authorization header using the Bearer scheme. Enter 'Bearer' followed by your token.",
            Name = "Authorization",
            Type = SecuritySchemeType.ApiKey,
            In = ParameterLocation.Header
        });

        options.AddSecurityRequirement(new OpenApiSecurityRequirement
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

        options.DocumentFilter<HealthChecksFilter>();
    });

    // camelCase on every route, both directions. Stated explicitly rather than left to the
    // framework default so a new endpoint cannot pick the other convention by accident.
    // Browsers cache nhsn-link.js independently of BFF releases, so once a bundle ships a casing
    // change breaks every cached copy until it refreshes - which is why this is settled now, while
    // nothing is deployed.
    builder.Services.Configure<JsonOptions>(opt =>
    {
        opt.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;

        // Enums travel as their names, not their ordinals. A numeric status is opaque to the UI and
        // silently reinterprets every stored value if a member is ever inserted. Names are left
        // PascalCase to match the unions the UI already declares - AccessState, EhrVendor,
        // OnboardingStatus are all "Allowed" / "Epic" / "NotStarted".
        opt.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });

    builder.Logging.AddSerilog();
    Log.Logger = new LoggerConfiguration()
        .ReadFrom.Configuration(builder.Configuration)
        .Filter.ByExcluding("RequestPath like '/health%'")
        .Filter.ByExcluding("RequestPath like '/swagger%'")
        .Enrich.FromLogContext()
        .Enrich.WithSpan()
        .CreateLogger();
}

static void SetupMiddleware(WebApplication app)
{
    app.AutoMigrateEF<NhsnAppDbContext>();
    using (var scope = app.Services.CreateScope())
    {
        var dbContext = scope.ServiceProvider.GetRequiredService<NhsnAppDbContext>();
        NhsnAppSeedData.SeedAsync(dbContext).GetAwaiter().GetResult();
    }

    if (app.Environment.IsDevelopment())
    {
        app.UseDeveloperExceptionPage();
    }
    else
    {
        app.UseExceptionHandler();
    }

    app.ConfigureSwagger();
    app.UseRouting();
    app.UseCors(CorsSettings.DefaultCorsPolicyName);
    app.UseAuthentication();
    app.UseAuthorization();

    var apis = app.Services.GetServices<IApi>();
    foreach (var api in apis)
    {
        api.RegisterEndpoints(app);
    }

    app.MapHealthChecks("/health", new HealthCheckOptions
    {
        ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
    }).RequireCors("HealthCheckPolicy");
    app.MapInfo(Assembly.GetExecutingAssembly(), app.Configuration, "nhsn-app-bff");
}

static TokenValidationParameters CreateValidationParameters(NhsnJwtSettings settings)
{
    if (string.IsNullOrWhiteSpace(settings.PublicCertificatePem))
    {
        throw new InvalidOperationException("NhsnJwt:PublicCertificatePem must be configured.");
    }

    if (string.IsNullOrWhiteSpace(settings.Issuer))
    {
        throw new InvalidOperationException("NhsnJwt:Issuer must be configured.");
    }

    var certificate = X509Certificate2.CreateFromPem(settings.PublicCertificatePem);
    var issuerSigningKey = CreateIssuerSigningKey(certificate);

    return new TokenValidationParameters
    {
        TryAllIssuerSigningKeys = true,
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = issuerSigningKey,
        ValidAlgorithms = [SecurityAlgorithms.EcdsaSha256],
        ValidateIssuer = true,
        ValidIssuer = settings.Issuer,
        ValidateAudience = !string.IsNullOrWhiteSpace(settings.Audience),
        ValidAudience = string.IsNullOrWhiteSpace(settings.Audience) ? null : settings.Audience,
        ValidateLifetime = true,
        ClockSkew = TimeSpan.FromMinutes(2),
        NameClaimType = settings.NameClaimType,
        RoleClaimType = ClaimTypes.Role
    };
}

static void EnforceMaximumTokenAge(TokenValidatedContext context, NhsnJwtSettings settings)
{
    if (!settings.MaxTokenAgeMinutes.HasValue || settings.MaxTokenAgeMinutes.Value <= 0)
    {
        return;
    }

    var iatValue = context.Principal?.FindFirstValue("iat");
    if (string.IsNullOrWhiteSpace(iatValue) || !long.TryParse(iatValue, out var issuedAtUnixSeconds))
    {
        context.Fail(new SecurityTokenValidationException("JWT is missing a valid iat claim required for maximum token age validation."));
        return;
    }

    var issuedAt = DateTimeOffset.FromUnixTimeSeconds(issuedAtUnixSeconds);
    var maxTokenAge = TimeSpan.FromMinutes(settings.MaxTokenAgeMinutes.Value);
    if (DateTimeOffset.UtcNow - issuedAt > maxTokenAge)
    {
        context.Fail(new SecurityTokenValidationException("JWT exceeds the configured maximum token age and must be refreshed."));
    }
}

static bool TryHandleTokenRenewalRedirect(JwtBearerChallengeContext context, NhsnJwtSettings settings)
{
    if (string.IsNullOrWhiteSpace(settings.ExpiredTokenRedirectUrl))
    {
        return false;
    }

    if (!ShouldRedirectForTokenRenewal(context.AuthenticateFailure))
    {
        return false;
    }

    var redirectUrl = BuildTokenRenewalRedirectUrl(settings.ExpiredTokenRedirectUrl, context.Request);
    context.HandleResponse();
    context.Response.StatusCode = StatusCodes.Status401Unauthorized;
    context.Response.Headers.Append("Location", redirectUrl);
    context.Response.Headers.Append("X-Expired-Token-Redirect-Url", redirectUrl);
    return true;
}

static bool ShouldRedirectForTokenRenewal(Exception? exception)
{
    if (exception is null)
    {
        return false;
    }

    return exception is SecurityTokenExpiredException
           || exception is SecurityTokenValidationException;
}

static string BuildTokenRenewalRedirectUrl(string configuredUrl, HttpRequest request)
{
    var currentUrl = $"{request.Scheme}://{request.Host}{request.PathBase}{request.Path}{request.QueryString}";
    var encodedCurrentUrl = Uri.EscapeDataString(currentUrl);

    if (configuredUrl.Contains("{redirectUrl}", StringComparison.OrdinalIgnoreCase))
    {
        return configuredUrl.Replace("{redirectUrl}", encodedCurrentUrl, StringComparison.OrdinalIgnoreCase);
    }

    var separator = configuredUrl.Contains('?') ? '&' : '?';
    return $"{configuredUrl}{separator}redirectUrl={encodedCurrentUrl}";
}

static SecurityKey CreateIssuerSigningKey(X509Certificate2 certificate)
{
    var ecdsa = certificate.GetECDsaPublicKey();
    if (ecdsa is not null)
    {
        return new ECDsaSecurityKey(ecdsa)
        {
            KeyId = certificate.Thumbprint
        };
    }

    var rsa = certificate.GetRSAPublicKey();
    if (rsa is not null)
    {
        return new RsaSecurityKey(rsa)
        {
            KeyId = certificate.Thumbprint
        };
    }

    throw new InvalidOperationException("NhsnJwt:PublicCertificatePem must contain an RSA or ECDSA public key.");
}

// Fails the process at startup when configuration is missing or unsafe for the target environment.
//
// Validate the ServiceRegistry strings directly. Never resolve a Link client or read a *ApiUrl
// property to do this: TenantServiceApiUrl's getter dereferences ServiceRegistry.TenantService,
// declared null!, so that throws NullReferenceException from inside the check itself.
static void ValidateConfiguration(WebApplicationBuilder builder)
{
    // Docker is exempt alongside Development: the compose stack deliberately runs with
    // AllowAllOrigins and no JWT audience.
    if (builder.Environment.IsDevelopment() || builder.Environment.IsEnvironment("Docker"))
    {
        return;
    }

    var errors = new List<string>();
    var serviceRegistry = builder.Configuration.GetSection(ServiceRegistry.ConfigSectionName);

    // Tenant is a nested object, not a flat key like the other services' *ServiceUrl names.
    if (string.IsNullOrWhiteSpace(serviceRegistry["TenantService:TenantServiceUrl"]))
    {
        errors.Add("ServiceRegistry:TenantService:TenantServiceUrl must be configured.");
    }

    // Only services this build actually calls are validated, so a deployment isn't blocked on
    // configuration for a phase that hasn't shipped.
    if (string.IsNullOrWhiteSpace(serviceRegistry["DataAcquisitionServiceUrl"]))
    {
        errors.Add("ServiceRegistry:DataAcquisitionServiceUrl must be configured.");
    }

    if (errors.Count > 0)
    {
        throw new InvalidOperationException("Invalid configuration:" + Environment.NewLine + " - " + string.Join(Environment.NewLine + " - ", errors));
    }
}
