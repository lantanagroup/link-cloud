using System.Reflection;
using System.Security.Claims;
using System.Security.Cryptography.X509Certificates;
using HealthChecks.UI.Client;
using LantanaGroup.Link.Nhsn.App.Bff.Application.Interfaces;
using LantanaGroup.Link.Nhsn.App.Bff.Application.Services;
using LantanaGroup.Link.Nhsn.App.Bff.Persistence;
using LantanaGroup.Link.Nhsn.App.Bff.Persistence.Seed;
using LantanaGroup.Link.Nhsn.App.Bff.Presentation.Endpoints;
using LantanaGroup.Link.Nhsn.App.Bff.Settings;
using LantanaGroup.Link.Shared.Application.Extensions;
using LantanaGroup.Link.Shared.Application.Extensions.Security;
using LantanaGroup.Link.Shared.Application.Health;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using LantanaGroup.Link.Shared.Settings;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
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

    var assemblyVersion = Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? string.Empty;
    var serviceInformation = builder.SetupServiceInformation(NhsnAppConstants.ServiceName, assemblyVersion);

    builder.Services.AddProblemDetailsService(options =>
    {
        options.Environment = builder.Environment;
        options.ServiceName = serviceInformation.ServiceName;
        options.IncludeExceptionDetails = builder.Configuration.GetValue<bool>("ProblemDetails:IncludeExceptionDetails");
    });

    builder.Services.Configure<CorsSettings>(builder.Configuration.GetSection(ConfigurationConstants.AppSettings.CORS));
    builder.Services.Configure<NhsnJwtSettings>(builder.Configuration.GetRequiredSection(NhsnJwtSettings.SectionName));

    builder.Services.AddDbContext<NhsnAppDbContext>(options =>
    {
        var connectionString = builder.Configuration.GetConnectionString(ConfigurationConstants.DatabaseConnections.DatabaseConnection)
                              ?? throw new InvalidOperationException("DatabaseConnection is required for NHSN-App-BFF.");
        options.UseSqlServer(connectionString);
    });

    var jwtSettings = builder.Configuration.GetRequiredSection(NhsnJwtSettings.SectionName).Get<NhsnJwtSettings>()
                     ?? throw new InvalidOperationException("NhsnJwt configuration is required.");

    var allowAnonymousAccess = builder.Configuration.GetValue<bool>("Authentication:EnableAnonymousAccess");

    if (!allowAnonymousAccess)
    {
        builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.RequireHttpsMetadata = !builder.Environment.IsDevelopment();
                options.MapInboundClaims = false;
                options.TokenValidationParameters = CreateValidationParameters(jwtSettings);
            });

        builder.Services.AddAuthorizationBuilder()
            .AddPolicy("AuthenticatedUser", policy => policy.RequireAuthenticatedUser());
    }
    else
    {
        builder.Services.AddAuthorizationBuilder()
            .AddPolicy("AuthenticatedUser", policy => policy.RequireAssertion(_ => true));
    }

    builder.Services.AddScoped<IUserInfoService, UserInfoService>();

    builder.Services.AddTransient<IApi, UserInfoEndpoints>();
    builder.Services.AddTransient<IApi, SimulationEndpoints>();

    builder.Services.AddHealthChecks().AddDbContextCheck<NhsnAppDbContext>("Database");

    builder.Services.AddLinkCorsService(options =>
    {
        options.Environment = builder.Environment;
    });

    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen(options =>
    {
        options.SwaggerDoc("v1", new OpenApiInfo
        {
            Title = "NHSN App BFF",
            Version = "v1",
            Description = "Backend-for-frontend for the NHSN App integration framework."
        });

        if (!allowAnonymousAccess)
        {
            options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
            {
                Description = "Authorization using JWT",
                Name = "Authorization",
                Type = SecuritySchemeType.Http,
                BearerFormat = "JWT",
                In = ParameterLocation.Header,
                Scheme = JwtBearerDefaults.AuthenticationScheme
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
        }

        options.DocumentFilter<HealthChecksFilter>();
    });

    builder.Services.Configure<JsonOptions>(opt => opt.SerializerOptions.PropertyNamingPolicy = null);

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

    if (!app.Configuration.GetValue<bool>("Authentication:EnableAnonymousAccess"))
    {
        app.UseAuthentication();
    }
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

    var certificate = X509Certificate2.CreateFromPem(settings.PublicCertificatePem);
    return new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new X509SecurityKey(certificate),
        ValidateIssuer = !string.IsNullOrWhiteSpace(settings.Issuer),
        ValidIssuer = string.IsNullOrWhiteSpace(settings.Issuer) ? null : settings.Issuer,
        ValidateAudience = !string.IsNullOrWhiteSpace(settings.Audience),
        ValidAudience = string.IsNullOrWhiteSpace(settings.Audience) ? null : settings.Audience,
        ValidateLifetime = true,
        ClockSkew = TimeSpan.FromMinutes(2),
        NameClaimType = settings.NameClaimType,
        RoleClaimType = ClaimTypes.Role
    };
}