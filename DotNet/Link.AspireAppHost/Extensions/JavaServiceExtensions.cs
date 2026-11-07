using System.Xml.Linq;
using Link.AspireAppHost.Security;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;

namespace Link.AspireAppHost.Extensions;

/// <summary>
/// Builds the Java services with Maven and runs MeasureEval and Validation the way docker-compose
/// does: the <c>docker</c> Spring profile, with the hostnames it names swapped for this host's.
/// </summary>
internal static class JavaServiceExtensions
{
    public static (JavaService MeasureEval, JavaService Validation) AddJavaServices(
        this IDistributedApplicationBuilder builder,
        LinkInfrastructure infrastructure,
        IResourceBuilder<SqlServerDatabaseResource> validationDatabase,
        bool requireToken,
        LinkKeycloak? keycloak)
    {
        var javaDirectory = Path.GetFullPath(Path.Combine(builder.AppHostDirectory, "..", "..", "Java"));
        var javaHome = FindJavaHome(builder.Configuration);
        var javaCommand = javaHome is null ? "java" : Path.Combine(javaHome, "bin", OperatingSystem.IsWindows() ? "java.exe" : "java");
        var version = ReadProjectVersion(javaDirectory);

        // One build for both services. Maven is quick when nothing changed. On Windows mvn is a batch
        // file, which only cmd can start.
        var mavenArgs = new[] { "-q", "-B", "-f", Path.Combine(javaDirectory, "pom.xml"), "package", "-DskipTests", "-pl", "measureeval,validation", "-am" };
        var maven = builder.Configuration["Java:Maven"] ?? "mvn";
        var build = (OperatingSystem.IsWindows()
                ? builder.AddExecutable("java-build", "cmd", javaDirectory, ["/c", maven, .. mavenArgs])
                : builder.AddExecutable("java-build", maven, javaDirectory, mavenArgs))
            .WithJavaHome(javaHome);

        // Built first: an interpolated string passed straight to WithEnvironment becomes a reference expression.
        var mongoUri = $"mongodb://localhost:{Ports.Mongo}/link-measureeval?directConnection=true&serverSelectionTimeoutMS=4000";
        // No credentials in the URL: the password is passed separately as spring.datasource.password.
        var validationJdbcUrl =
            $"jdbc:sqlserver://localhost:{Ports.SqlServer};databaseName={validationDatabase.Resource.DatabaseName};encrypt=true;trustServerCertificate=true"; // trufflehog:ignore

        var measureEval = builder.AddJavaService("measureeval", Ports.MeasureEval, javaCommand, javaHome, javaDirectory, version, build, requireToken, copy => copy
            .WithEnvironment("spring.data.mongodb.uri", mongoUri)
            .WithEnvironment("spring.data.mongodb.database", "link-measureeval")
            .WithEnvironment("resource-cache.blob-storage.connection-string", LinkInfrastructure.BlobConnectionString)
            .WithEnvironment("resource-cache.blob-storage.blob-container-name", LinkInfrastructure.InternalContainer)
            .WithEnvironment("resource-cache.blob-storage.blob-root", LinkInfrastructure.ResourceCacheBlobRoot)
            .WithEnvironment("link.data-acquisition.base-url", DotNetServiceExtensions.Url(Ports.DataAcquisition))
            .WaitFor(infrastructure.Mongo));

        var validation = builder.AddJavaService("validation", Ports.Validation, javaCommand, javaHome, javaDirectory, version, build, requireToken, copy => copy
            .WithEnvironment("spring.datasource.url", validationJdbcUrl)
            .WithEnvironment("spring.datasource.username", "sa")
            .WithEnvironment("spring.datasource.password", infrastructure.Sql.Resource.PasswordParameter)
            .WithEnvironment("spring.kafka.producer.properties.max.request.size", "10485760")
            // The rubric Seed data publishes (SeedData/Validation). The shadow comparison evaluates every patient against
            // it, beside the legacy validation, which still decides pass or fail: vaas.bridge.enabled stays false.
            .WithEnvironment("vaas.bridge.rubric-id", SeedDataExtensions.ReadRubricIdentity(SeedDataExtensions.RubricFile(builder.AppHostDirectory)).Id)
            // Validation adds /api/terminology/fhir itself.
            .WithEnvironment("LINK_TERMINOLOGYSERVICEURL", DotNetServiceExtensions.Url(Ports.Terminology))
            .WaitFor(validationDatabase));
        // A dashboard command, so on the first copy only: the copies share one database.
        validation.Primary.WithValidationMigrationCommand(javaDirectory, validationDatabase);

        if (requireToken && keycloak is not null)
        {
            measureEval.WaitFor(keycloak.Container);
            validation.WaitFor(keycloak.Container);
        }

        return (measureEval, validation);
    }

    /// <summary>
    /// Adds a Java service as <c>Replicas:&lt;module&gt;</c> copies, one by default, each given the same
    /// settings by <paramref name="configure"/>.
    /// </summary>
    /// <remarks>
    /// Aspire's replicas are for .NET projects only, so each copy is a resource of its own: the first is
    /// named <paramref name="module"/> and holds the fixed port behind Aspire's proxy, as the .NET services
    /// do; the others are <c>&lt;module&gt;-2</c> and so on, on ports Aspire picks. Every HTTP call goes to
    /// the first copy, since a proxy cannot be shared between resources. What the copies add is consumers:
    /// they share the service's Kafka consumer groups and split the topics' partitions between them.
    /// </remarks>
    private static JavaService AddJavaService(
        this IDistributedApplicationBuilder builder,
        string module,
        int port,
        string javaCommand,
        string? javaHome,
        string javaDirectory,
        string version,
        IResourceBuilder<ExecutableResource> build,
        bool requireToken,
        Func<IResourceBuilder<ExecutableResource>, IResourceBuilder<ExecutableResource>> configure)
    {
        var count = builder.ReplicaCount(module);
        var copies = new List<IResourceBuilder<ExecutableResource>>();
        for (var copy = 1; copy <= count; copy++)
        {
            var name = copy == 1 ? module : $"{module}-{copy}";
            var resource = builder.AddExecutable(name, javaCommand,
                    Path.Combine(javaDirectory, module), "-jar", Path.Combine("target", $"{module}-{version}.jar"))
                // The name logback appends to every Loki line ([instance=validation-2]).
                .WithEnvironment("LINK_SERVICE_INSTANCE", name);
            // Aspire passes the port it picked for the copy in server.port. The first copy's sits behind the
            // proxy, which holds the fixed port.
            resource = copy == 1
                ? resource.WithHttpEndpoint(port: port, env: "server.port")
                : resource.WithHttpEndpoint(env: "server.port", isProxied: false);
            copies.Add(configure(resource.WithJavaSettings(javaHome, build, requireToken)));
        }

        return new JavaService(copies);
    }

    /// <summary>The settings every copy of every Java service gets.</summary>
    private static IResourceBuilder<ExecutableResource> WithJavaSettings(
        this IResourceBuilder<ExecutableResource> resource,
        string? javaHome,
        IResourceBuilder<ExecutableResource> build,
        bool requireToken) =>
        resource
            .WithJavaHome(javaHome)
            .WithEnvironment("SPRING_PROFILES_ACTIVE", "docker")
            // The docker profile names kafka_b over SASL; this broker is local and plain.
            .WithEnvironment("spring.kafka.bootstrap-servers", LinkInfrastructure.KafkaBootstrapServers)
            .WithEnvironment("spring.kafka.properties.security.protocol", "PLAINTEXT")
            // IPv4, as for the .NET services: the port is published on 127.0.0.1 only.
            .WithEnvironment("spring.data.redis.host", "127.0.0.1")
            .WithEnvironment("spring.data.redis.port", Ports.Redis.ToString())
            .WithEnvironment("internal-blob-storage.connection-string", LinkInfrastructure.BlobConnectionString)
            .WithEnvironment("internal-blob-storage.blob-container-name", LinkInfrastructure.InternalContainer)
            .WithEnvironment("authentication.signing-key", DotNetServiceExtensions.LocalSigningKey)
            // The docker profile serves requests anonymously. When it does not, incoming tokens are checked
            // against the signing key, and the tokens these services send Report must carry the issuer the
            // .NET services accept (authentication.authority defaults to https://localhost:7004).
            .WithEnvironment("authentication.anonymous", requireToken ? "false" : "true")
            .WithEnvironment("authentication.authority", LinkSystemToken.Issuer)
            .WithEnvironment("link.report.base-url", DotNetServiceExtensions.Url(Ports.Report))
            .WithEnvironment("loki.url", LinkInfrastructure.LokiUrl)
            // The services read their collector address from telemetry.exporterEndpoint. Aspire's
            // WithOtlpExporter() is not used: it also sets OTEL_RESOURCE_ATTRIBUTES and
            // OTEL_EXPORTER_OTLP_HEADERS, which the OpenTelemetry Spring starter binds to Map properties
            // and fails to start on ("No converter found ... to type Map<String, String>").
            .WithEnvironment("TELEMETRY_EXPORTERENDPOINT", resource.ApplicationBuilder.Configuration["ASPIRE_DASHBOARD_OTLP_ENDPOINT_URL"] ?? "")
            .WithHttpHealthCheck("/health")
            .WaitForCompletion(build);

    private static IResourceBuilder<ExecutableResource> WithJavaHome(this IResourceBuilder<ExecutableResource> resource, string? javaHome) =>
        javaHome is null ? resource : resource.WithEnvironment("JAVA_HOME", javaHome);

    /// <summary>
    /// The JDK from the AppHost's <c>Java:Home</c> setting, then <c>JAVA_HOME</c>, else whatever <c>java</c>
    /// is on <c>PATH</c>. The services target Java 17 and are run on 21 in development; a newer default
    /// JDK can fail the build, which is why the setting exists.
    /// </summary>
    private static string? FindJavaHome(IConfiguration configuration)
    {
        var configured = configuration["Java:Home"];
        if (!string.IsNullOrWhiteSpace(configured))
            return configured.TrimEnd('\\', '/');
        var environment = Environment.GetEnvironmentVariable("JAVA_HOME");
        return string.IsNullOrWhiteSpace(environment) ? null : environment.TrimEnd('\\', '/');
    }

    /// <summary>The version in <c>Java/pom.xml</c>, which names the jars Maven builds.</summary>
    private static string ReadProjectVersion(string javaDirectory)
    {
        var pom = XDocument.Load(Path.Combine(javaDirectory, "pom.xml"));
        XNamespace ns = "http://maven.apache.org/POM/4.0.0";
        return pom.Root?.Element(ns + "version")?.Value
            ?? throw new InvalidOperationException("Java/pom.xml has no version.");
    }

    /// <summary>
    /// A dashboard command that applies Validation's <c>database/migrations/V*.sql</c> scripts in name
    /// order, as the deployment pipeline does. Every script is guarded, so running them again changes
    /// nothing. The docker profile also has Hibernate update the schema at startup, as under compose;
    /// this command gives the same indexes and constraints a deployed database has.
    /// </summary>
    private static IResourceBuilder<ExecutableResource> WithValidationMigrationCommand(
        this IResourceBuilder<ExecutableResource> validation,
        string javaDirectory,
        IResourceBuilder<SqlServerDatabaseResource> database)
    {
        var scripts = Path.Combine(javaDirectory, "validation", "src", "main", "resources", "database", "migrations");
        return validation.WithCommand(
            name: "validation-migrations",
            displayName: "Apply Validation migrations",
            executeCommand: context => CommandNotifications.RunWithNotificationsAsync(context, "Apply Validation migrations",
                "Applying Validation's database migrations.", async () =>
            {
                var logger = context.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Link.ValidationMigrations");
                var connectionString = await database.Resource.ConnectionStringExpression.GetValueAsync(context.CancellationToken);
                try
                {
                    await using var connection = new SqlConnection(connectionString);
                    await connection.OpenAsync(context.CancellationToken);
                    foreach (var script in Directory.GetFiles(scripts, "V*.sql").Order(StringComparer.Ordinal))
                    {
                        logger.LogInformation("Applying {Script}", Path.GetFileName(script));
                        await using var command = new SqlCommand(await File.ReadAllTextAsync(script, context.CancellationToken), connection) { CommandTimeout = 300 };
                        await command.ExecuteNonQueryAsync(context.CancellationToken);
                    }
                    return CommandResults.Success("Validation's migrations are applied.");
                }
                catch (SqlException ex)
                {
                    logger.LogError("Applying the Validation migrations failed: {Error}", ex.Message);
                    return CommandResults.Failure("Applying the Validation migrations failed. The AppHost console has the details.");
                }
            }),
            commandOptions: new CommandOptions
            {
                UpdateState = context => context.ResourceSnapshot.HealthStatus is HealthStatus.Healthy
                    ? ResourceCommandState.Enabled
                    : ResourceCommandState.Disabled,
                IconName = "DatabaseArrowUp",
                IconVariant = IconVariant.Filled
            });
    }
}

/// <summary>
/// The copies of one Java service (see <c>Replicas</c>). <see cref="Primary"/> is the one HTTP calls reach and
/// dashboard commands belong to; a dependency every copy needs is added with <see cref="WaitFor{T}"/>.
/// </summary>
internal sealed class JavaService(IReadOnlyList<IResourceBuilder<ExecutableResource>> copies)
{
    public IResourceBuilder<ExecutableResource> Primary => copies[0];

    public IReadOnlyList<IResourceBuilder<ExecutableResource>> Copies => copies;

    public JavaService WaitFor<T>(IResourceBuilder<T> dependency) where T : class, IResource
    {
        foreach (var copy in copies)
            copy.WaitFor(dependency);
        return this;
    }
}
