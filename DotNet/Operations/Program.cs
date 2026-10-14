using System.Reflection;
using HealthChecks.UI.Client;
using LantanaGroup.Link.Operations.Application.Interfaces;
using LantanaGroup.Link.Operations.Application.KafkaOps;
using LantanaGroup.Link.Operations.Persistence;
using LantanaGroup.Link.Operations.Presentation.Endpoints;
using LantanaGroup.Link.Operations.Settings;
using LantanaGroup.Link.Shared.Application.Extensions;
using LantanaGroup.Link.Shared.Application.Extensions.Caching;
using LantanaGroup.Link.Shared.Application.Extensions.ExternalServices;
using LantanaGroup.Link.Shared.Application.Extensions.Security;
using LantanaGroup.Link.Shared.Application.Extensions.Telemetry;
using LantanaGroup.Link.Shared.Application.Factories;
using LantanaGroup.Link.Shared.Application.Health;
using LantanaGroup.Link.Shared.Application.Interfaces;
using LantanaGroup.Link.Shared.Application.Middleware;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using LantanaGroup.Link.Shared.Application.Models.Kafka;
using LantanaGroup.Link.Shared.KafkaAdmin.Protocol;
using LantanaGroup.Link.Shared.Settings;
using Link.Authorization.Policies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi.Models;
using Serilog;
using Serilog.Enrichers.Span;
using Serilog.Settings.Configuration;

var builder = WebApplication.CreateBuilder(args);
builder.Configuration.AddStandardEnvironmentConfiguration();

RegisterServices(builder);

var app = builder.Build();
SetupMiddleware(app);

app.Run();

static void RegisterServices(WebApplicationBuilder builder)
{
    builder.AddExternalConfiguration(OperationsConstants.ServiceName);

    builder.Logging.AddSerilog();
    Log.Logger = new LoggerConfiguration()
        .ReadFrom.Configuration(builder.Configuration, new ConfigurationReaderOptions { SectionName = OperationsConstants.AppSettingsSectionNames.Serilog })
        .Filter.ByExcluding("RequestPath like '/health%'")
        .Filter.ByExcluding("RequestPath like '/swagger%'")
        .Enrich.FromLogContext()
        .Enrich.WithSpan()
        .CreateLogger();

    var serviceInformation = builder.Configuration.GetRequiredSection(OperationsConstants.AppSettingsSectionNames.ServiceInformation).Get<ServiceInformation>()
        ?? throw new InvalidOperationException("Service Information is required.");
    serviceInformation.ServiceConfigName = OperationsConstants.ServiceName;
    builder.Services.AddSingleton(serviceInformation);
    ServiceActivitySource.Initialize(serviceInformation);

    builder.Services.AddProblemDetails();
    builder.Services.Configure<ServiceRegistry>(builder.Configuration.GetSection(ServiceRegistry.ConfigSectionName));
    builder.Services.Configure<LinkTokenServiceSettings>(builder.Configuration.GetSection(ConfigurationConstants.AppSettings.LinkTokenService));
    builder.Services.Configure<CorsSettings>(builder.Configuration.GetSection(ConfigurationConstants.AppSettings.CORS));

    var allowAnonymousAccess = builder.Configuration.GetValue<bool>("Authentication:EnableAnonymousAccess");

    // Kafka
    var kafkaConnection = builder.Configuration.GetSection(KafkaConstants.SectionName).Get<KafkaConnection>()
        ?? throw new InvalidOperationException("Kafka Connection is required.");
    builder.Services.AddSingleton(kafkaConnection);
    builder.Services.RegisterKafkaProducer<string, AuditEventMessage>(kafkaConnection, new Confluent.Kafka.ProducerConfig { AllowAutoCreateTopics = false });

    // Short-lived read caches (topic lists, snapshots, rate samples). Change requests are not kept here.
    var cacheType = builder.Configuration.GetValue<string>("Cache:Type");
    if (cacheType == "Redis")
    {
        builder.Services.AddRedisCache(options =>
        {
            options.Environment = builder.Environment;
            options.ConnectionString = builder.Configuration.GetConnectionString("Redis")
                ?? throw new InvalidOperationException("Redis Connection String is required.");
            options.Password = builder.Configuration.GetValue<string>("Redis:Password");
            if (builder.Configuration.GetValue<int>("Cache:Timeout") > 0)
                options.Timeout = builder.Configuration.GetValue<int>("Cache:Timeout");
        });
        builder.Services.AddSingleton<ICacheService, RedisCacheService>();
    }
    else
    {
        builder.Services.AddMemoryCache();
        builder.Services.AddSingleton<ICacheService, InMemoryCacheService>();
    }

    // Persistence: change requests, the single-executor lease and topic migrations.
    var provider = builder.Configuration.GetValue<string>(OperationsConstants.AppSettingsSectionNames.DatabaseProvider) ?? ConfigurationConstants.AppSettings.SqlServerDatabaseProvider;
    var environmentName = builder.Environment.EnvironmentName;
    var historyDays = builder.Configuration.GetSection(KafkaOpsOptions.SectionName).Get<KafkaOpsOptions>()?.HistoryRetentionDays ?? 30;
    var sql = !string.Equals(provider, OperationsConstants.InMemoryStore, StringComparison.OrdinalIgnoreCase);
    if (sql)
    {
        if (!string.Equals(provider, ConfigurationConstants.AppSettings.SqlServerDatabaseProvider, StringComparison.Ordinal))
            throw new InvalidOperationException("Database provider " + provider + " is not supported.");
        var connectionString = builder.Configuration.GetConnectionString(ConfigurationConstants.DatabaseConnections.DatabaseConnection);
        if (string.IsNullOrEmpty(connectionString))
            throw new InvalidOperationException("Database connection string is null or empty.");
        builder.Services.AddDbContextFactory<OperationsDbContext>(options =>
            options.UseSqlServer(connectionString, sqlOptions => sqlOptions.EnableRetryOnFailure()));
        builder.Services.AddSingleton<IChangeRequestStore>(sp => new SqlChangeRequestStore(sp.GetRequiredService<IDbContextFactory<OperationsDbContext>>(), environmentName, historyDays));
        builder.Services.AddSingleton<IKafkaOpsLease>(sp => new SqlExecutorLease(
            sp.GetRequiredService<IDbContextFactory<OperationsDbContext>>(), environmentName, logger: sp.GetRequiredService<ILogger<SqlExecutorLease>>()));
        builder.Services.AddSingleton<IMigrationStore>(sp => new SqlMigrationStore(sp.GetRequiredService<IDbContextFactory<OperationsDbContext>>(), environmentName));
    }
    else
    {
        Log.Logger.Warning("Kafka change requests are kept in memory. Run one replica only; nothing survives a restart.");
        builder.Services.AddSingleton<IChangeRequestStore, InMemoryChangeRequestStore>();
        builder.Services.AddSingleton<IKafkaOpsLease, InMemoryKafkaOpsLease>();
        builder.Services.AddSingleton<IMigrationStore, InMemoryMigrationStore>();
    }

    // Kafka operations console
    builder.Services.Configure<KafkaOpsOptions>(builder.Configuration.GetSection(KafkaOpsOptions.SectionName));
    builder.Services.Configure<KafkaOpsConnectionOptions>(builder.Configuration.GetSection(KafkaOpsConnectionOptions.SectionName));
    builder.Services.AddSingleton<IKafkaReassignmentAdmin>(_ => new KafkaAdminWire(KafkaWireSettings.From(kafkaConnection)));
    builder.Services.AddSingleton(sp => new KafkaBrokerGateway(kafkaConnection, sp.GetRequiredService<IKafkaReassignmentAdmin>()));
    builder.Services.AddSingleton<IKafkaBrokerGateway>(sp => sp.GetRequiredService<KafkaBrokerGateway>());
    builder.Services.AddSingleton<IKafkaThrottleConfig>(_ => new ConfluentThrottleConfig(kafkaConnection));
    builder.Services.AddSingleton<IKafkaPartitionMover>(sp => new KafkaNativePartitionMover(
        sp.GetRequiredService<IKafkaReassignmentAdmin>(),
        sp.GetRequiredService<IKafkaBrokerGateway>(),
        sp.GetRequiredService<IKafkaThrottleConfig>(),
        sp.GetRequiredService<IOptions<KafkaOpsOptions>>().Value.ReassignmentBatchPartitions));
    builder.Services.AddSingleton<IProcessRunner, ProcessRunner>();
    builder.Services.AddSingleton<IKubernetesResourceClient, UnconfiguredKubernetesClient>();
    builder.Services.AddSingleton<IKafkaInfraProvider>(sp => KafkaInfraProviderFactory.Create(
        sp.GetRequiredService<IOptions<KafkaOpsOptions>>().Value,
        sp.GetRequiredService<IHostEnvironment>().IsProduction(),
        sp.GetRequiredService<IProcessRunner>(),
        sp.GetRequiredService<IKubernetesResourceClient>()));
    builder.Services.AddSingleton<IMigrationHoldRegistry>(sp => new MigrationHoldRegistry(sp.GetRequiredService<IMigrationStore>()));
    builder.Services.AddSingleton<IKafkaMigrationAdmin>(sp => new KafkaMigrationAdmin(
        sp.GetRequiredService<KafkaConnection>(),
        sp.GetRequiredService<IOptions<KafkaOpsOptions>>().Value,
        sp.GetRequiredService<IOptions<KafkaOpsConnectionOptions>>().Value));
    builder.Services.AddSingleton<IKafkaWorkloadControl>(sp => new KafkaWorkloadControl(
        sp.GetRequiredService<IOptions<KafkaOpsOptions>>().Value,
        sp.GetRequiredService<IProcessRunner>(),
        sp.GetRequiredService<IKubernetesResourceClient>()));
    builder.Services.AddSingleton<IMigrationRuntime, MigrationRuntime>();
    builder.Services.AddSingleton<IKafkaOpsService, KafkaOpsService>();
    builder.Services.AddSingleton<IKafkaBrowseSessionFactory, ConfluentBrowseSessionFactory>();
    builder.Services.AddSingleton<IKafkaMessageBrowser, KafkaMessageBrowser>();
    builder.Services.AddHostedService<KafkaOpsWorker>();
    builder.Services.AddPipelineAbortRegistry(builder.Configuration);
    builder.Services.AddTransient<IApi, KafkaOpsEndpoints>();

    // Security. Identity arrives as the Link bearer token the Admin BFF mints from the signed-in user.
    builder.Services.AddLinkBearerServiceAuthentication(options =>
    {
        options.Environment = builder.Environment;
        options.AllowAnonymous = allowAnonymousAccess;
        options.Authority = builder.Configuration.GetValue<string>("Authentication:Schemas:LinkBearer:Authority");
        options.ValidateToken = builder.Configuration.GetValue<bool>("Authentication:Schemas:LinkBearer:ValidateToken");
        options.ProtectKey = builder.Configuration.GetValue<bool>("DataProtection:Enabled");
        options.SigningKey = builder.Configuration.GetValue<string>("LinkTokenService:SigningKey");
    });
    AddKafkaPolicies(builder.Services, allowAnonymousAccess);

    builder.Services.AddLinkCorsService(options => options.Environment = builder.Environment);

    // Health
    var kafkaHealthOptions = new KafkaHealthCheckConfiguration(kafkaConnection, OperationsConstants.ServiceName).GetHealthCheckOptions();
    var health = builder.Services.AddHealthChecks().AddKafka(kafkaHealthOptions);
    if (sql)
        health.AddCheck<DatabaseHealthCheck<OperationsDbContext>>("Database");

    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen(c =>
    {
        if (!allowAnonymousAccess)
        {
            c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
            {
                Description = "Authorization using JWT",
                Name = "Authorization",
                Type = SecuritySchemeType.Http,
                BearerFormat = "JWT",
                In = ParameterLocation.Header,
                Scheme = JwtBearerDefaults.AuthenticationScheme
            });
            c.AddSecurityRequirement(new OpenApiSecurityRequirement
            {
                {
                    new OpenApiSecurityScheme { Reference = new OpenApiReference { Id = "Bearer", Type = ReferenceType.SecurityScheme } },
                    new List<string>()
                }
            });
        }

        var xmlPath = Path.Combine(AppContext.BaseDirectory, $"{Assembly.GetExecutingAssembly().GetName().Name}.xml");
        if (File.Exists(xmlPath))
            c.IncludeXmlComments(xmlPath);
    });

    builder.Services.AddLinkTelemetry(builder.Configuration, options =>
    {
        options.Environment = builder.Environment;
        options.ServiceName = OperationsConstants.ServiceName;
        options.ServiceVersion = serviceInformation.Version;
    });
}

static void AddKafkaPolicies(IServiceCollection services, bool allowAnonymousAccess)
{
    var policies = services.AddAuthorizationBuilder();
    if (allowAnonymousAccess)
    {
        policies
            .AddPolicy(PolicyNames.CanViewInfrastructure, pb => pb.RequireAssertion(_ => true))
            .AddPolicy(PolicyNames.CanManageKafkaTopics, pb => pb.RequireAssertion(_ => true))
            .AddPolicy(PolicyNames.CanManageScaling, pb => pb.RequireAssertion(_ => true))
            .AddPolicy(PolicyNames.CanOperateKafka, pb => pb.RequireAssertion(_ => true))
            .AddPolicy(PolicyNames.CanMigrateKafkaTopics, pb => pb.RequireAssertion(_ => true));
        return;
    }

    policies
        .AddPolicy(PolicyNames.CanViewInfrastructure, AuthorizationPolicies.CanViewInfrastructure())
        .AddPolicy(PolicyNames.CanManageKafkaTopics, AuthorizationPolicies.CanManageKafkaTopics())
        .AddPolicy(PolicyNames.CanManageScaling, AuthorizationPolicies.CanManageScaling())
        .AddPolicy(PolicyNames.CanOperateKafka, AuthorizationPolicies.CanOperateKafka())
        .AddPolicy(PolicyNames.CanMigrateKafkaTopics, AuthorizationPolicies.CanMigrateKafkaTopics());
}

static void SetupMiddleware(WebApplication app)
{
    if (app.Environment.IsDevelopment())
        app.UseDeveloperExceptionPage();
    else
        app.UseExceptionHandler();

    if (!string.Equals(app.Configuration.GetValue<string>(OperationsConstants.AppSettingsSectionNames.DatabaseProvider), OperationsConstants.InMemoryStore, StringComparison.OrdinalIgnoreCase))
        app.AutoMigrateEF<OperationsDbContext>();

    app.ConfigureSwagger();
    app.UseRouting();
    app.UseCors(CorsSettings.DefaultCorsPolicyName);

    var allowAnonymousAccess = app.Configuration.GetValue<bool>("Authentication:EnableAnonymousAccess");
    if (!allowAnonymousAccess)
    {
        app.UseAuthentication();
        app.UseMiddleware<UserScopeMiddleware>();
    }

    app.UseAuthorization();

    foreach (var api in app.Services.GetServices<IApi>())
        api.RegisterEndpoints(app);

    app.MapGet("/api/operations/info", () => Results.Ok($"Welcome to {ServiceActivitySource.ServiceName} version {ServiceActivitySource.Instance.Version}!")).AllowAnonymous();
    app.MapHealthChecks("/health", new HealthCheckOptions
    {
        ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
    }).RequireCors("HealthCheckPolicy");
}

public partial class Program
{
}
