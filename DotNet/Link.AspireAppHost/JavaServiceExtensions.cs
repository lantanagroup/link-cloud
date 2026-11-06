using System.Xml.Linq;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;

namespace Link.AspireAppHost;

/// <summary>
/// Builds the Java services with Maven and runs MeasureEval and Validation the way docker-compose
/// does: the <c>docker</c> Spring profile, with the hostnames it names swapped for this host's.
/// </summary>
internal static class JavaServiceExtensions
{
    public static (IResourceBuilder<ExecutableResource> MeasureEval, IResourceBuilder<ExecutableResource> Validation) AddJavaServices(
        this IDistributedApplicationBuilder builder,
        LinkInfrastructure infrastructure,
        IResourceBuilder<SqlServerDatabaseResource> validationDatabase)
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

        var measureEval = builder.AddJavaService("measureeval", Ports.MeasureEval, javaCommand, javaHome, javaDirectory, version, build)
            .WithEnvironment("spring.data.mongodb.uri", mongoUri)
            .WithEnvironment("spring.data.mongodb.database", "link-measureeval")
            .WithEnvironment("resource-cache.blob-storage.connection-string", LinkInfrastructure.BlobConnectionString)
            .WithEnvironment("resource-cache.blob-storage.blob-container-name", LinkInfrastructure.InternalContainer)
            .WithEnvironment("resource-cache.blob-storage.blob-root", LinkInfrastructure.ResourceCacheBlobRoot)
            .WithEnvironment("link.data-acquisition.base-url", DotNetServiceExtensions.Url(Ports.DataAcquisition))
            .WaitFor(infrastructure.Mongo);

        var validation = builder.AddJavaService("validation", Ports.Validation, javaCommand, javaHome, javaDirectory, version, build)
            .WithEnvironment("spring.datasource.url", validationJdbcUrl)
            .WithEnvironment("spring.datasource.username", "sa")
            .WithEnvironment("spring.datasource.password", infrastructure.Sql.Resource.PasswordParameter)
            .WithEnvironment("spring.kafka.producer.properties.max.request.size", "10485760")
            // Validation adds /api/terminology/fhir itself.
            .WithEnvironment("LINK_TERMINOLOGYSERVICEURL", DotNetServiceExtensions.Url(Ports.Terminology))
            .WaitFor(validationDatabase)
            .WithValidationMigrationCommand(javaDirectory, validationDatabase);

        return (measureEval, validation);
    }

    private static IResourceBuilder<ExecutableResource> AddJavaService(
        this IDistributedApplicationBuilder builder,
        string module,
        int port,
        string javaCommand,
        string? javaHome,
        string javaDirectory,
        string version,
        IResourceBuilder<ExecutableResource> build) =>
        builder.AddExecutable(module, javaCommand, Path.Combine(javaDirectory, module), "-jar", Path.Combine("target", $"{module}-{version}.jar"))
            .WithJavaHome(javaHome)
            .WithHttpEndpoint(port: port, isProxied: false)
            .WithEnvironment("SPRING_PROFILES_ACTIVE", "docker")
            .WithEnvironment("server.port", port.ToString())
            // The docker profile names kafka_b over SASL; this broker is local and plain.
            .WithEnvironment("spring.kafka.bootstrap-servers", LinkInfrastructure.KafkaBootstrapServers)
            .WithEnvironment("spring.kafka.properties.security.protocol", "PLAINTEXT")
            .WithEnvironment("spring.data.redis.host", "localhost")
            .WithEnvironment("spring.data.redis.port", Ports.Redis.ToString())
            .WithEnvironment("internal-blob-storage.connection-string", LinkInfrastructure.BlobConnectionString)
            .WithEnvironment("internal-blob-storage.blob-container-name", LinkInfrastructure.InternalContainer)
            .WithEnvironment("authentication.signing-key", DotNetServiceExtensions.LocalSigningKey)
            .WithEnvironment("link.report.base-url", DotNetServiceExtensions.Url(Ports.Report))
            .WithEnvironment("loki.url", LinkInfrastructure.LokiUrl)
            // The services read their collector address from telemetry.exporterEndpoint. Aspire's
            // WithOtlpExporter() is not used: it also sets OTEL_RESOURCE_ATTRIBUTES and
            // OTEL_EXPORTER_OTLP_HEADERS, which the OpenTelemetry Spring starter binds to Map properties
            // and fails to start on ("No converter found ... to type Map<String, String>").
            .WithEnvironment("TELEMETRY_EXPORTERENDPOINT", builder.Configuration["ASPIRE_DASHBOARD_OTLP_ENDPOINT_URL"] ?? "")
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
            executeCommand: async context =>
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
                    return CommandResults.Success();
                }
                catch (SqlException ex)
                {
                    logger.LogError("Applying the Validation migrations failed: {Error}", ex.Message);
                    return CommandResults.Failure("Applying the Validation migrations failed. The AppHost console has the details.");
                }
            },
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
