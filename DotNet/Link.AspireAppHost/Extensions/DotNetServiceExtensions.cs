using Link.AspireAppHost.Security;

namespace Link.AspireAppHost.Extensions;

/// <summary>
/// Runs a .NET service the way docker-compose runs it, with the hostnames swapped for this host's.
/// </summary>
/// <remarks>
/// Each service runs in the <c>Docker</c> environment, so it reads <c>appsettings.Docker.json</c>, the
/// settings CI already exercises: local secret management, anonymous access, Redis-backed caches. The
/// environment variables below replace only the values in those files that name a compose host
/// (<c>kafka_b</c>, <c>mssql</c>, <c>redis_cache</c>, <c>loki</c>, a service's container name) and the
/// values compose itself passes. Environment variables outrank appsettings files. User secrets are not
/// loaded outside Development, so nothing on a developer's machine changes what runs here.
/// </remarks>
internal static class DotNetServiceExtensions
{
    /// <summary>The signing key compose gives every service for Link's own bearer tokens. It protects nothing.</summary>
    public const string LocalSigningKey = "local-dev-link-bearer-signing-key-2026-automation-adminbff-shared-secret-min-64-bytes";

    /// <summary>The token authority compose uses.</summary>
    public const string LocalTokenAuthority = "LinkServiceAdmin";

    public static string Url(int port) => $"http://localhost:{port}";

    public static IResourceBuilder<ProjectResource> AddLinkService<TProject>(
        this IDistributedApplicationBuilder builder, string name, int port, string healthPath = "/health")
        where TProject : IProjectMetadata, new() =>
        // No launch profile: the port is this host's, and launchSettings.json stays the IDE's.
        builder.AddProject<TProject>(name, launchProfileName: null)
            .WithHttpEndpoint(port: port, isProxied: false)
            .WithEnvironment("ASPNETCORE_ENVIRONMENT", "Docker")
            .WithEnvironment("DOTNET_ENVIRONMENT", "Docker")
            // appsettings.Docker.json names a Kestrel endpoint, which outranks ASPNETCORE_URLS.
            .WithEnvironment("Kestrel__Endpoints__http__Url", Url(port))
            .WithEnvironment("Serilog__WriteTo__0__Args__uri", LinkInfrastructure.LokiUrl)
            .WithDashboardTelemetry()
            .WithHttpHealthCheck(healthPath);

    /// <summary>
    /// Sends the service's OpenTelemetry to the Aspire dashboard. The services read their collector
    /// address from <c>Telemetry:OtelCollectorEndpoint</c>, not the standard variable Aspire sets, so the
    /// standard one is copied across. Aspire's callback that sets it runs before this one.
    /// </summary>
    private static IResourceBuilder<ProjectResource> WithDashboardTelemetry(this IResourceBuilder<ProjectResource> service) =>
        service
            .WithEnvironment("Telemetry__EnableTelemetry", "true")
            .WithEnvironment("Telemetry__EnableOtelCollector", "true")
            .WithEnvironment(context =>
            {
                if (context.EnvironmentVariables.TryGetValue("OTEL_EXPORTER_OTLP_ENDPOINT", out var endpoint))
                    context.EnvironmentVariables["Telemetry__OtelCollectorEndpoint"] = endpoint;
            });

    public static IResourceBuilder<ProjectResource> WithKafka(this IResourceBuilder<ProjectResource> service, LinkInfrastructure infrastructure) =>
        service
            .WithEnvironment("KafkaConnection__BootstrapServers__0", LinkInfrastructure.KafkaBootstrapServers)
            .WithEnvironment("KafkaConnection__SaslProtocolEnabled", "false")
            .WaitFor(infrastructure.Kafka);

    /// <summary>The service's own database, which it migrates when it starts, as under compose.</summary>
    public static IResourceBuilder<ProjectResource> WithDatabase(
        this IResourceBuilder<ProjectResource> service, IResourceBuilder<SqlServerDatabaseResource> database, bool autoMigrate = true) =>
        service
            .WithEnvironment("ConnectionStrings__DatabaseConnection", database)
            .WithEnvironment("DatabaseProvider", "SqlServer")
            .WithEnvironment("AutoMigrate", autoMigrate ? "true" : "false")
            .WaitFor(database);

    public static IResourceBuilder<ProjectResource> WithRedis(this IResourceBuilder<ProjectResource> service, LinkInfrastructure infrastructure) =>
        service
            .WithEnvironment("ConnectionStrings__Redis", LinkInfrastructure.RedisConnectionString)
            .WithEnvironment("Redis__Password", "")
            .WaitFor(infrastructure.Redis);

    /// <summary>The Redis and blob halves of the shared FHIR resource cache.</summary>
    public static IResourceBuilder<ProjectResource> WithResourceCache(this IResourceBuilder<ProjectResource> service, LinkInfrastructure infrastructure) =>
        service
            .WithEnvironment("ResourceCache__CacheImplementation", "Hybrid")
            .WithEnvironment("ResourceCache__Redis__ConnectionString", LinkInfrastructure.RedisConnectionString)
            .WithEnvironment("ResourceCache__Redis__Password", "")
            .WithEnvironment("ResourceCache__Redis__MaxMemoryBytes", "268435456")
            .WithEnvironment("ResourceCache__BlobStorage__ConnectionString", LinkInfrastructure.BlobConnectionString)
            .WithEnvironment("ResourceCache__BlobStorage__BlobContainerName", LinkInfrastructure.InternalContainer)
            .WithEnvironment("ResourceCache__BlobStorage__BlobRoot", LinkInfrastructure.ResourceCacheBlobRoot)
            .WaitFor(infrastructure.Redis)
            .WaitFor(infrastructure.Azurite);

    public static IResourceBuilder<ProjectResource> WithInternalBlobStorage(this IResourceBuilder<ProjectResource> service, LinkInfrastructure infrastructure) =>
        service
            .WithEnvironment("InternalBlobStorage__ConnectionString", LinkInfrastructure.BlobConnectionString)
            .WithEnvironment("InternalBlobStorage__BlobContainerName", LinkInfrastructure.InternalContainer)
            .WaitFor(infrastructure.Azurite);

    /// <summary>Points the service at Tenant, with the values appsettings.Docker.json uses.</summary>
    public static IResourceBuilder<ProjectResource> WithTenantService(this IResourceBuilder<ProjectResource> service, string pathSuffix = "") =>
        service.WithEnvironment("ServiceRegistry__TenantService__TenantServiceUrl", Url(Ports.Tenant) + pathSuffix);

    /// <summary>
    /// Link's own service authentication: the shared local signing key and token issuer, and whether the
    /// service requires a Link bearer token.
    /// </summary>
    /// <param name="service">The service.</param>
    /// <param name="requireToken">
    /// False serves requests anonymously, as compose does. True requires a valid Link bearer token, as in a
    /// deployed environment: the Admin BFF attaches one to everything it forwards, and the services create
    /// system tokens for the calls they make to each other. True in Auth:Mode Keycloak.
    /// </param>
    public static IResourceBuilder<ProjectResource> WithLocalAuthentication(
        this IResourceBuilder<ProjectResource> service, bool requireToken = false, LinkKeycloak? keycloak = null)
    {
        service
            .WithEnvironment("Authentication__EnableAnonymousAccess", requireToken ? "false" : "true")
            // The issuer tokens are created with, and the issuer the services accept. With tokens required, the
            // shared validation treats the second as an OpenID Connect authority: it must be HTTPS outside
            // Development, and its metadata is fetched to validate a token. Keycloak's realm is both, so in
            // Auth:Mode Keycloak that is the issuer; otherwise it is compose's LinkServiceAdmin.
            .WithEnvironment("LinkTokenService__Authority", LinkSystemToken.Issuer)
            .WithEnvironment("Authentication__Schemas__LinkBearer__Authority", LinkSystemToken.Issuer)
            .WithEnvironment("LinkTokenService__SigningKey", LocalSigningKey);

        // The first token a service validates fetches the authority's metadata, so it must be answering.
        if (requireToken && keycloak is not null)
            service.WaitFor(keycloak.Container);
        return service;
    }
}
