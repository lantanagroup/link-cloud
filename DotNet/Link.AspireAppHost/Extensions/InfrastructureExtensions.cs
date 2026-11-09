using Azure.Storage.Blobs;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Link.AspireAppHost.Extensions;

/// <summary>
/// The containers every service depends on, and the addresses services use to reach them from the host.
/// </summary>
internal sealed record LinkInfrastructure(
    IResourceBuilder<SqlServerServerResource> Sql,
    IResourceBuilder<KafkaServerResource> Kafka,
    IResourceBuilder<ContainerResource> Mongo,
    IResourceBuilder<ContainerResource> Redis,
    IResourceBuilder<ContainerResource> Azurite,
    IResourceBuilder<ContainerResource> Loki,
    IResourceBuilder<ContainerResource> HapiFhir,
    IResourceBuilder<ContainerResource> KafkaRestProxy,
    IResourceBuilder<ContainerResource> Sftp,
    IResourceBuilder<ParameterResource> SftpPassword)
{
    /// <summary>The blob container internal artifacts and the resource cache live in.</summary>
    public const string InternalContainer = "internal";

    /// <summary>The blob container Submission copies a submission into. Copying it there is the submission.</summary>
    public const string ExternalContainer = "external";

    /// <summary>The blob container Automation.UI keeps imported and generated bundles in.</summary>
    public const string AutomationContainer = "link-automation-ui";

    /// <summary>The folder under <see cref="InternalContainer"/> the shared resource cache uses.</summary>
    public const string ResourceCacheBlobRoot = "resource-cache";

    public const string KafkaBootstrapServers = "localhost:19092";

    /// <summary>
    /// Redis runs without a password, as it does under dev-infrastructure. The address is 127.0.0.1, not localhost:
    /// Docker publishes the port on the IPv4 loopback only, localhost resolves to ::1 first on Windows, and
    /// StackExchange.Redis gives up on ::1 (ConnectTimeout) instead of falling back to IPv4. <c>allowAdmin=true</c>, as
    /// compose's connection strings have it: the acquisition worker's memory check runs <c>INFO memory</c>, which
    /// StackExchange.Redis refuses without it.
    /// </summary>
    public static readonly string RedisConnectionString = $"127.0.0.1:{Ports.Redis},abortConnect=false,allowAdmin=true";

    /// <summary>
    /// Directly to the one replica set member. Automation.UI uses transactions, which need a replica set,
    /// and a direct connection keeps clients from looking the member up by the name the set gives it.
    /// </summary>
    public static readonly string MongoConnectionString = $"mongodb://localhost:{Ports.Mongo}/?directConnection=true";

    /// <summary>
    /// Azurite's fixed development account. The key is the one Azurite documents for every installation
    /// and appears in <c>.env.example</c>; it protects nothing.
    /// </summary>
    public static readonly string BlobConnectionString =
        "DefaultEndpointsProtocol=http;AccountName=devstoreaccount1;" +
        "AccountKey=Eby8vdM02xNOcqFlqUwJPLlmEtlCDXJ1OUzFT50uSRZ6IFsuFq2UVErCz4I6tq/K1SZFPTOtr/KBHBeksoGMGw==;" + // trufflehog:ignore
        $"BlobEndpoint=http://127.0.0.1:{Ports.AzuriteBlob}/devstoreaccount1;";

    public static readonly string LokiUrl = $"http://localhost:{Ports.Loki}";

    public static readonly string HapiFhirBaseUrl = $"http://localhost:{Ports.HapiFhir}/fhir";

    public static readonly string KafkaRestProxyUrl = $"http://localhost:{Ports.KafkaRestProxy}";

    /// <summary>The SFTP server's one account. Its password is generated: <see cref="SftpPassword"/>.</summary>
    public const string SftpUser = "admin";

    /// <summary>
    /// The folder under the SFTP account's home that is writable and kept between runs. The home itself
    /// belongs to root, as the server's chroot requires, so uploads go here.
    /// </summary>
    public const string SftpDirectory = "data";

    /// <summary>The host a facility's SFTP configuration names to reach it, with <see cref="Ports.Sftp"/>.</summary>
    public const string SftpHost = "127.0.0.1";

    /// <summary>
    /// A database on the local SQL Server, created when SQL Server starts. The resource is named
    /// <c>db-&lt;name&gt;</c> because resource names share one namespace with the services.
    /// </summary>
    public IResourceBuilder<SqlServerDatabaseResource> Database(string name) => Sql.AddDatabase($"db-{name}", $"link-{name}");
}

internal static class InfrastructureExtensions
{
    private const string KafkaImage = "apache/kafka";
    private const string KafkaTag = "4.2.2";

    /// <summary>The topic Aspire's Kafka health check produces to. It is its default name.</summary>
    private const string HealthCheckTopic = "healthchecks-topic";

    public static LinkInfrastructure AddLinkInfrastructure(this IDistributedApplicationBuilder builder)
    {
        // SQL Server's password is generated on the first run and kept in this AppHost's user secrets.
        // Clients reach it on the port Docker publishes, not through Aspire's proxy: under load the
        // proxy has been seen to stop accepting connections and not recover (link-lbw).
        var sql = builder.AddSqlServer("sql", port: Ports.SqlServer)
            .WithEndpoint("tcp", endpoint => endpoint.IsProxied = false)
            .WithDataVolume("link-aspire-sql");

        var kafka = builder.AddLinkKafka();

        var mongo = builder.AddContainer("mongo", "mongo", "6-jammy")
            .WithArgs("--replSet", "rs0", "--bind_ip_all")
            .WithEndpoint(port: Ports.Mongo, targetPort: 27017, name: "tcp", isProxied: false)
            .WithVolume("link-aspire-mongo", "/data/db")
            .WithHealthCheck(AddMongoReplicaSetCheck(builder));

        // Redis is a cache, so nothing is kept between runs.
        var redis = builder.AddContainer("redis", "redis", "6.2-alpine")
            .WithArgs("redis-server", "--save", "", "--maxmemory", "256mb", "--maxmemory-policy", "allkeys-lru")
            .WithEndpoint(port: Ports.Redis, targetPort: 6379, name: "tcp", isProxied: false);

        var azurite = builder.AddContainer("azurite", "mcr.microsoft.com/azure-storage/azurite", "3.35.0")
            .WithArgs("azurite-blob", "--skipApiVersionCheck", "--blobHost", "0.0.0.0", "-l", "/data")
            .WithEndpoint(port: Ports.AzuriteBlob, targetPort: 10000, name: "blob", isProxied: false)
            .WithVolume("link-aspire-azurite", "/data")
            .WithHealthCheck(AddBlobContainersCheck(builder));

        // The Java services and the Serilog sink of every .NET service send logs here, so the logs can
        // be queried the same way as under docker-compose. The Aspire dashboard shows console output too.
        var loki = builder.AddContainer("loki", "grafana/loki", "3.3.1")
            .WithHttpEndpoint(port: Ports.Loki, targetPort: 3100, name: "http", isProxied: false);

        // The FHIR server facilities' query plans point at. Its H2 database lives inside the container,
        // so test data is loaded again on each run.
        var hapi = builder.AddContainer("fhir-server", "hapiproject/hapi", "v8.10.0-2")
            .WithHttpEndpoint(port: Ports.HapiFhir, targetPort: 8080, name: "http", isProxied: false)
            .WithEnvironment("hapi.fhir.fhir_version", "R4")
            .WithEnvironment("hapi.fhir.allow_multiple_delete", "true")
            .WithEnvironment("hapi.fhir.allow_external_references", "true")
            .WithEnvironment("hapi.fhir.allow_cascading_deletes", "true")
            .WithEnvironment("hapi.fhir.delete_enabled", "true")
            .WithEnvironment("hapi.fhir.expunge_enabled", "true")
            .WithEnvironment("hapi.fhir.enforce_referential_integrity_on_delete", "false")
            .WithEnvironment("hapi.fhir.enforce_referential_integrity_on_write", "false")
            .WithEnvironment("hapi.fhir.delete_expunge_enabled", "true")
            .WithEnvironment("hapi.fhir.reuse_cached_search_results_millis", "0")
            .WithEnvironment("hapi.fhir.subscription_enabled", "false")
            .WithEnvironment("hapi.fhir.client_id_strategy", "ANY")
            .WithEnvironment("hapi.fhir.server_id_strategy", "UUID")
            .WithEnvironment("spring.main.lazy-initialization", "true")
            .WithEnvironment("hapi.fhir.openapi_enabled", "false")
            .WithEnvironment("hapi.fhir.lastn_enabled", "false")
            .WithEnvironment("hapi.fhir.graphql_enabled", "false")
            .WithEnvironment("hapi.fhir.bulk_export_enabled", "false")
            .WithEnvironment("hapi.fhir.binary_storage_enabled", "false")
            .WithEnvironment("hapi.fhir.validation.requests_enabled", "false")
            .WithEnvironment("hapi.fhir.validation.responses_enabled", "false")
            .WithEnvironment("spring.datasource.url", "jdbc:h2:file:/tmp/hapi/h2");

        // Automation.UI drives Kafka through the REST proxy, as it does under docker-compose.
        var restProxy = builder.AddContainer("kafka-rest-proxy", "confluentinc/cp-kafka-rest", "7.4.0")
            .WithHttpEndpoint(port: Ports.KafkaRestProxy, targetPort: 8082, name: "http", isProxied: false)
            .WithEnvironment("KAFKA_REST_HOST_NAME", "kafka-rest-proxy")
            .WithEnvironment("KAFKA_REST_LISTENERS", "http://0.0.0.0:8082")
            .WithEnvironment("KAFKA_REST_KAFKA_CLUSTER_ID", "link-cluster")
            .WithEnvironment(context => context.EnvironmentVariables["KAFKA_REST_BOOTSTRAP_SERVERS"] =
                ReferenceExpression.Create($"{kafka.Resource.InternalEndpoint.Property(EndpointProperty.HostAndPort)}"))
            .WaitFor(kafka);

        var sftp = builder.AddLinkSftp();

        return new LinkInfrastructure(sql, kafka, mongo, redis, azurite, loki, hapi, restProxy, sftp.Container, sftp.Password);
    }

    /// <summary>
    /// The SFTP server compose runs, for facilities whose patient lists arrive as files. No service is
    /// pointed at it by configuration: a facility's SFTP configuration in DataAcquisition names it.
    /// </summary>
    /// <remarks>
    /// The account's password is generated on the first run and kept in this AppHost's user secrets,
    /// like SQL Server's. It has no special characters: the image reads the account from one
    /// colon-separated argument.
    /// <para />
    /// The image leaves a folder that already exists owned by root, and the volume keeps the data folder
    /// between runs, so <c>Scripts/sftp-setup.sh</c> hands it to the account on each start, as under compose.
    /// The script is copied in rather than bind-mounted: the image only runs scripts that are executable,
    /// and a bind mount carries the host's file mode (not executable in a Linux checkout) and line endings.
    /// </remarks>
    private static (IResourceBuilder<ContainerResource> Container, IResourceBuilder<ParameterResource> Password)
        AddLinkSftp(this IDistributedApplicationBuilder builder)
    {
        var password = builder.AddParameter("sftp-password",
            new GenerateParameterDefault { MinLength = 24, Special = false }, secret: true, persist: true);

        var setupScript = File.ReadAllText(Path.Combine(builder.AppHostDirectory, "..", "..", "Scripts", "sftp-setup.sh"))
            .Replace("\r\n", "\n");

        var container = builder.AddContainer("sftp", "atmoz/sftp")
            .WithArgs(ReferenceExpression.Create($"{LinkInfrastructure.SftpUser}:{password}:1001:1001:{LinkInfrastructure.SftpDirectory}"))
            .WithEndpoint(port: Ports.Sftp, targetPort: 22, name: "sftp", scheme: "sftp", isProxied: false)
            .WithVolume("link-aspire-sftp", $"/home/{LinkInfrastructure.SftpUser}/{LinkInfrastructure.SftpDirectory}")
            // The image has no /etc/sftp.d of its own, so the folder is created along with the script.
            .WithContainerFiles("/etc",
            [
                new ContainerDirectory
                {
                    Name = "sftp.d",
                    Entries =
                    [
                        new ContainerFile
                        {
                            Name = "sftp-setup.sh",
                            Contents = setupScript,
                            Mode = UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute |
                                   UnixFileMode.GroupRead | UnixFileMode.GroupExecute |
                                   UnixFileMode.OtherRead | UnixFileMode.OtherExecute
                        }
                    ]
                }
            ])
            .WithHealthCheck(AddSftpCheck(builder));

        return (container, password);
    }

    /// <summary>
    /// A single Kafka broker in KRaft mode, with the topics from <c>topics.txt</c> and Kafka UI.
    /// </summary>
    /// <remarks>
    /// Aspire's Kafka integration runs <c>confluentinc/confluent-local</c>, which bundles parts under a
    /// source-available license, so the official Apache image is used instead (as in link-lbw). That
    /// image drops its own single-node settings as soon as any setting is given, and Aspire gives the
    /// listeners, so the KRaft settings are set here.
    /// <para>
    /// Kafka keeps its data in the <c>link-aspire-kafka</c> volume, as compose keeps it in <c>kafka_data</c>, so topics,
    /// messages and consumer groups' committed offsets survive restarts. Most Link consumers set no
    /// <c>AutoOffsetReset</c>, so Kafka's default <c>latest</c> applies: a consumer group with no committed offset skips
    /// whatever is already on its topic. With nothing kept between runs every group was new on every start, and a
    /// message sent before its consumer joined was lost; Report replaying a stored report at startup lost its
    /// acquisition requests that way. Now only the very first run is exposed, as under compose.
    /// </para>
    /// <para>
    /// The image runs as a non-root user, and its default log directory (<c>/tmp/kraft-combined-logs</c>) does not exist
    /// in it, so a volume mounted there would be owned by root. <c>/var/lib/kafka/data</c> exists and belongs to that
    /// user, so a new volume mounted over it is writable. The image formats the storage on first start and accepts it
    /// as already formatted afterwards, which needs the fixed <c>CLUSTER_ID</c> below.
    /// </para>
    /// </remarks>
    private static IResourceBuilder<KafkaServerResource> AddLinkKafka(this IDistributedApplicationBuilder builder)
    {
        var kafka = builder.AddKafka("kafka", port: Ports.Kafka)
            .WithImage(KafkaImage, KafkaTag)
            .WithEnvironment("CLUSTER_ID", "bGluay1jbG91ZC1rYWZrYQ")
            .WithEnvironment("KAFKA_LOG_DIRS", "/var/lib/kafka/data")
            .WithVolume("link-aspire-kafka", "/var/lib/kafka/data")
            .WithEnvironment("KAFKA_NODE_ID", "1")
            .WithEnvironment("KAFKA_PROCESS_ROLES", "broker,controller")
            .WithEnvironment("KAFKA_CONTROLLER_QUORUM_VOTERS", "1@localhost:29093")
            .WithEnvironment("KAFKA_CONTROLLER_LISTENER_NAMES", "CONTROLLER")
            .WithEnvironment("KAFKA_INTER_BROKER_LISTENER_NAME", "PLAINTEXT")
            .WithEnvironment("KAFKA_OFFSETS_TOPIC_REPLICATION_FACTOR", "1")
            .WithEnvironment("KAFKA_TRANSACTION_STATE_LOG_REPLICATION_FACTOR", "1")
            .WithEnvironment("KAFKA_TRANSACTION_STATE_LOG_MIN_ISR", "1")
            .WithEnvironment("KAFKA_GROUP_INITIAL_REBALANCE_DELAY_MS", "0")
            // The same limits as the compose broker: events can carry up to 10 MiB.
            .WithEnvironment("KAFKA_MESSAGE_MAX_BYTES", "10485760")
            .WithEnvironment("KAFKA_REPLICA_FETCH_MAX_BYTES", "10485760")
            // A topic comes from topics.txt or not at all, so a mistyped name fails instead of making a topic.
            .WithEnvironment("KAFKA_AUTO_CREATE_TOPICS_ENABLE", "false")
            .WithKafkaUI(ui => ui.WithHostPort(Ports.KafkaUi));

        // Aspire marks Kafka healthy only once its own check can produce to HealthCheckTopic, and the
        // broker creates no topics itself. A one-shot kafka-init container, like compose's, creates the
        // missing topics from topics.txt with Kafka's own CLI, then HealthCheckTopic last, so Kafka turns
        // healthy only once every topic exists and services that wait for it start with their topics in
        // place. The .NET admin client was tried first and crashed the AppHost in native code
        // (rd_kafka_topic_result_name) while reading the creation results, on 2.15.0 and on 2.16.0.
        var topicsPath = Path.GetFullPath(Path.Combine(builder.AppHostDirectory, "..", "..", "topics.txt"));
        builder.AddContainer("kafka-init", KafkaImage, KafkaTag)
            .WithBindMount(topicsPath, "/topics.txt", isReadOnly: true)
            .WithEnvironment(context => context.EnvironmentVariables["BOOTSTRAP"] =
                ReferenceExpression.Create($"{kafka.Resource.InternalEndpoint.Property(EndpointProperty.HostAndPort)}"))
            .WithEntrypoint("/bin/sh")
            .WithArgs("-c", CreateTopicsScript)
            .WaitForStart(kafka);

        return kafka;
    }

    /// <summary>
    /// Waits for the broker, then creates every topic in <c>/topics.txt</c> (<c>name:partitions:replicas</c>
    /// per line) that does not exist, eight at a time, then <see cref="HealthCheckTopic"/>. There is one
    /// broker, so every topic gets one replica whatever the file asks for. POSIX sh, for the image's shell.
    /// </summary>
    private const string CreateTopicsScript = """
        set -e
        T=/opt/kafka/bin/kafka-topics.sh
        until existing=$($T --bootstrap-server "$BOOTSTRAP" --list 2>/dev/null); do
          echo "waiting for the broker at $BOOTSTRAP"; sleep 2
        done
        tr -d '\r' < /topics.txt | grep -v '^#' | grep -v '^[[:space:]]*$' | while IFS=: read -r name partitions replicas; do
          echo "$existing" | grep -qx "$name" || echo "$name ${partitions:-2}"
        done > /tmp/missing.txt
        count=$(wc -l < /tmp/missing.txt | tr -d ' ')
        xargs -r -n 2 -P 8 sh -c '/opt/kafka/bin/kafka-topics.sh --bootstrap-server "$BOOTSTRAP" --create --if-not-exists --topic "$0" --partitions "$1" --replication-factor 1 >/dev/null' < /tmp/missing.txt
        echo "$existing" | grep -qx healthchecks-topic || $T --bootstrap-server "$BOOTSTRAP" --create --if-not-exists --topic healthchecks-topic --partitions 1 --replication-factor 1 >/dev/null
        echo "Kafka has every topic in topics.txt ($count created)"
        """;

    /// <summary>
    /// Initiates the replica set on first start and reports healthy once this member is the primary.
    /// The member is named by its address inside the container; clients connect directly, so they
    /// never look that name up.
    /// </summary>
    private static string AddMongoReplicaSetCheck(IDistributedApplicationBuilder builder)
    {
        const string checkName = "mongo-replica-set";
        builder.Services.AddHealthChecks().AddAsyncCheck(checkName, async cancellationToken =>
        {
            try
            {
                var client = new MongoClient($"{LinkInfrastructure.MongoConnectionString}&serverSelectionTimeoutMS=2000&connectTimeoutMS=2000");
                var admin = client.GetDatabase("admin");
                BsonDocument status;
                try
                {
                    status = await admin.RunCommandAsync<BsonDocument>(new BsonDocument("replSetGetStatus", 1), cancellationToken: cancellationToken);
                }
                catch (MongoCommandException ex) when (ex.Code == 94) // NotYetInitialized
                {
                    var config = new BsonDocument
                    {
                        { "_id", "rs0" },
                        { "members", new BsonArray { new BsonDocument { { "_id", 0 }, { "host", "localhost:27017" } } } }
                    };
                    await admin.RunCommandAsync<BsonDocument>(new BsonDocument("replSetInitiate", config), cancellationToken: cancellationToken);
                    return HealthCheckResult.Unhealthy("Initiated replica set rs0; waiting for a primary");
                }

                return status.GetValue("myState", 0).ToInt32() == 1
                    ? HealthCheckResult.Healthy("Replica set rs0 has a primary")
                    : HealthCheckResult.Unhealthy("Replica set rs0 has no primary yet");
            }
            catch (Exception ex) when (ex is MongoException or TimeoutException)
            {
                return HealthCheckResult.Unhealthy("MongoDB is not answering yet", ex);
            }
        });
        return checkName;
    }

    /// <summary>
    /// Reports healthy once the SFTP server sends its SSH banner. The port alone answers earlier, while
    /// the container is still creating the account.
    /// </summary>
    private static string AddSftpCheck(IDistributedApplicationBuilder builder)
    {
        const string checkName = "sftp-banner";
        builder.Services.AddHealthChecks().AddAsyncCheck(checkName, async cancellationToken =>
        {
            try
            {
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                timeout.CancelAfter(TimeSpan.FromSeconds(3));
                using var client = new System.Net.Sockets.TcpClient();
                await client.ConnectAsync(LinkInfrastructure.SftpHost, Ports.Sftp, timeout.Token);
                var buffer = new byte[4];
                await client.GetStream().ReadExactlyAsync(buffer, timeout.Token);
                return System.Text.Encoding.ASCII.GetString(buffer) == "SSH-"
                    ? HealthCheckResult.Healthy("The SFTP server is accepting connections")
                    : HealthCheckResult.Unhealthy("The SFTP port answered with something other than SSH");
            }
            catch (Exception ex) when (ex is System.Net.Sockets.SocketException or IOException or OperationCanceledException)
            {
                return HealthCheckResult.Unhealthy("The SFTP server is not answering yet", ex);
            }
        });
        return checkName;
    }

    /// <summary>Creates the blob containers the services expect, and reports healthy once they exist.</summary>
    private static string AddBlobContainersCheck(IDistributedApplicationBuilder builder)
    {
        const string checkName = "blob-containers";
        builder.Services.AddHealthChecks().AddAsyncCheck(checkName, async cancellationToken =>
        {
            try
            {
                var service = new BlobServiceClient(LinkInfrastructure.BlobConnectionString);
                foreach (var name in new[] { LinkInfrastructure.InternalContainer, LinkInfrastructure.ExternalContainer, LinkInfrastructure.AutomationContainer })
                    await service.GetBlobContainerClient(name).CreateIfNotExistsAsync(cancellationToken: cancellationToken);
                return HealthCheckResult.Healthy("Blob containers exist");
            }
            catch (Exception ex) when (ex is Azure.RequestFailedException or HttpRequestException or AggregateException)
            {
                return HealthCheckResult.Unhealthy("Azurite is not answering yet", ex);
            }
        });
        return checkName;
    }
}
