using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net.Sockets;
using System.Text;
using Azure;
using Azure.Storage.Blobs;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;

namespace Link.AspireAppHost.Extensions;

/// <summary>
/// The <b>Reset data</b> command on <c>sql</c>: empties what the services keep, so a run starts from nothing,
/// optionally keeping the Account database so the users and roles survive.
/// </summary>
/// <remarks>
/// It stops every running service that uses something being emptied, empties it, starts those services again
/// and runs the seed data. The services rebuild their own schemas as they start (EF migrations for the .NET
/// services, Hibernate for Validation), as on a first run, so nothing here knows a schema.
/// <para>
/// What is emptied: every SQL Server database but Account's (unless that is ticked off too), the Mongo databases,
/// the <c>internal</c> and <c>external</c> blob containers, and Redis. Optionally Automation UI's own data (its Mongo
/// database and blob container) and Kafka's topics. Kept: Keycloak's users, the SFTP folder, the local secrets file.
/// HAPI FHIR keeps nothing between runs anyway.
/// </para>
/// <para>
/// It sits on <c>sql</c> because that stays up throughout: the Admin BFF, where commands spanning several services
/// otherwise go, is one of the services it stops.
/// </para>
/// </remarks>
internal static class ResetExtensions
{
    /// <summary>Automation UI's Mongo database: <c>MongoDB:DatabaseName</c> in its appsettings, which this host does not override.</summary>
    private const string AutomationMongoDatabase = "botw-automation";

    /// <summary>The broker's own listener, as it is reached from inside its container.</summary>
    private const string KafkaInContainerBootstrap = "localhost:29092";

    /// <summary>The copy of <c>topics.txt</c> written into the broker's container to recreate the topics from.</summary>
    private const string TopicsFileInContainer = "/tmp/link-topics.txt";

    private static readonly TimeSpan StopTimeout = TimeSpan.FromMinutes(2);
    private static readonly TimeSpan HealthyTimeout = TimeSpan.FromMinutes(10);

    private sealed record Choices(bool KeepAccount, bool ClearAutomation, bool ClearKafka);

    public static IResourceBuilder<SqlServerServerResource> WithResetDataCommand(
        this IResourceBuilder<SqlServerServerResource> sql,
        LinkInfrastructure infrastructure,
        IResourceBuilder<SqlServerDatabaseResource> accountDatabase,
        bool keycloakMode)
    {
        var topicsFile = Path.GetFullPath(Path.Combine(sql.ApplicationBuilder.AppHostDirectory, "..", "..", "topics.txt"));
        var appHostDirectory = sql.ApplicationBuilder.AppHostDirectory;

        return sql.WithCommand(
            name: "reset-data",
            displayName: "Reset data",
            executeCommand: context => ResetAsync(context, infrastructure, accountDatabase.Resource, keycloakMode, topicsFile, appHostDirectory),
            commandOptions: new CommandOptions
            {
                Description = "Empty the services' databases, blob containers and caches, optionally keeping Account's users and roles, then seed again",
                UpdateState = context => context.ResourceSnapshot.HealthStatus is HealthStatus.Healthy
                    ? ResourceCommandState.Enabled
                    : ResourceCommandState.Disabled,
                IconName = "ArrowReset",
                IconVariant = IconVariant.Filled
            });
    }

    private static async Task<ExecuteCommandResult> ResetAsync(
        ExecuteCommandContext context,
        LinkInfrastructure infrastructure,
        SqlServerDatabaseResource accountDatabase,
        bool keycloakMode,
        string topicsFile,
        string appHostDirectory)
    {
        var interaction = context.Services.GetRequiredService<IInteractionService>();
        if (!interaction.IsAvailable)
            return CommandResults.Failure("The dashboard is needed to confirm a reset.");

        var choices = await AskAsync(interaction, keycloakMode, context.CancellationToken);
        if (choices is null)
            return CommandResults.Canceled();

        return await CommandNotifications.RunWithNotificationsAsync(context, "Reset data",
            "Stopping the services, emptying their data, and starting them again. This takes a few minutes.", async () =>
        {
            var logger = context.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Link.ResetData");
            await using var states = ResourceStates.Watch(context.Services.GetRequiredService<ResourceNotificationService>());
            var commands = context.Services.GetRequiredService<ResourceCommandService>();
            var model = context.Services.GetRequiredService<DistributedApplicationModel>();
            var cancellationToken = context.CancellationToken;

            // 1. Stop what uses the data. Nothing is emptied unless every one of them has stopped.
            var candidates = ServicesToStop(model, infrastructure, accountDatabase, choices).ToList();
            List<IResource> services;
            try
            {
                await states.WaitForAsync(candidates.Append(infrastructure.Kafka.Resource), cancellationToken);
                // Only what is running now: a service never started (AutoStart off) or stopped by hand stays that way.
                services = candidates.Where(service => states.Of(service).Any(IsRunning)).ToList();
                logger.LogInformation("Reset: stopping {Services}", string.Join(", ", services.Select(s => s.Name)));
                await StopAsync(services, commands, states, cancellationToken);
            }
            catch (Exception ex) when (ex is InvalidOperationException or TimeoutException)
            {
                logger.LogError("Reset: stopping the services failed, so nothing was emptied: {Error}", ex.Message);
                return CommandResults.Failure($"Stopping the services failed, so nothing was emptied: {ex.Message} Start them again from the dashboard.");
            }

            // 2. Empty the stores. A failure stops here; the services stay stopped so the cause can be looked at.
            var emptied = new List<string>();
            try
            {
                emptied.Add(await EmptySqlAsync(infrastructure, choices.KeepAccount ? accountDatabase.DatabaseName : null, logger, cancellationToken));
                emptied.Add(await EmptyMongoAsync(choices.ClearAutomation, logger, cancellationToken));
                emptied.Add(await EmptyBlobsAsync(choices.ClearAutomation, logger, cancellationToken));
                emptied.Add(await FlushRedisAsync(logger, cancellationToken));
                if (choices.ClearKafka)
                    emptied.Add(await ResetKafkaAsync(infrastructure, states, topicsFile, logger, cancellationToken));
            }
            catch (Exception ex) when (ex is SqlException or MongoException or RequestFailedException or IOException
                                           or SocketException or InvalidOperationException or TimeoutException)
            {
                logger.LogError("Reset: emptying the data failed: {Error}", ex.Message);
                return CommandResults.Failure(
                    $"Emptying the data failed part way: {ex.Message} The services are still stopped. Fix the cause and run Reset data again.");
            }

            // 3. Start them again, each once what it waits for is healthy, then seed.
            try
            {
                logger.LogInformation("Reset: starting {Services}", string.Join(", ", services.Select(s => s.Name)));
                await StartAsync(services, commands, states, cancellationToken);
            }
            catch (Exception ex) when (ex is InvalidOperationException or TimeoutException)
            {
                logger.LogError("Reset: starting the services again failed: {Error}", ex.Message);
                return CommandResults.Failure($"The data was emptied, but starting the services again failed: {ex.Message}");
            }

            var (seedFailed, seedSummary) = await SeedDataExtensions.SeedAllAsync(appHostDirectory, logger, cancellationToken);
            var summary = $"{string.Join(" ", emptied)} {seedSummary} Run Create test facility on admin-bff to set a facility up again.";
            return seedFailed ? CommandResults.Failure(summary) : CommandResults.Success(summary);
        });
    }

    private static async Task<Choices?> AskAsync(IInteractionService interaction, bool keycloakMode, CancellationToken cancellationToken)
    {
        List<InteractionInput> inputs =
        [
            new()
            {
                Name = "keepAccount",
                Label = "Keep Account (users and roles)",
                InputType = InputType.Boolean,
                Value = "true",
                Description = keycloakMode
                    ? "Untick to empty it too. Keycloak keeps its users either way, so they could sign in but would have no Account user until Create user is run for each."
                    : "Untick to empty it too."
            },
            new()
            {
                Name = "clearAutomation",
                Label = "Also clear Automation UI data",
                InputType = InputType.Boolean,
                Value = "false",
                Description = "Its imported and generated bundles and its runs (Mongo database and blob container)."
            },
            new()
            {
                Name = "clearKafka",
                Label = "Also clear Kafka",
                InputType = InputType.Boolean,
                Value = "false",
                Description = "Deletes and recreates every topic, so messages not yet consumed, retries included, are not delivered to the emptied services."
            }
        ];

        var result = await interaction.PromptInputsAsync(
            "Reset data",
            "This empties every service's database, the measures, the internal and external blob containers and Redis, " +
            "then starts the services again and seeds them. It can't be undone.",
            inputs,
            new InputsDialogInteractionOptions { PrimaryButtonText = "Reset" },
            cancellationToken);

        if (result.Canceled)
            return null;

        static bool Ticked(InteractionInput input) => bool.TryParse(input.Value, out var value) && value;
        return new Choices(
            KeepAccount: Ticked(result.Data["keepAccount"]),
            ClearAutomation: Ticked(result.Data["clearAutomation"]),
            ClearKafka: Ticked(result.Data["clearKafka"]));
    }

    // ---------------------------------------------------------------------------------------------------
    // Stopping and starting
    // ---------------------------------------------------------------------------------------------------

    /// <summary>
    /// The services that wait for something being emptied. Redis is not in the list: it is a cache, and flushing it
    /// under a running service only costs that service a few misses. Kafka is, when it is being cleared, so no consumer
    /// sits on a topic while it is deleted; Account is then stopped too, though its database is kept.
    /// </summary>
    private static IEnumerable<IResource> ServicesToStop(
        DistributedApplicationModel model, LinkInfrastructure infrastructure, SqlServerDatabaseResource accountDatabase, Choices choices)
    {
        var emptied = new HashSet<IResource>(model.Resources.OfType<SqlServerDatabaseResource>()
            .Where(database => !(choices.KeepAccount && database == accountDatabase)))
        {
            infrastructure.Mongo.Resource,
            infrastructure.Azurite.Resource
        };
        if (choices.ClearKafka)
            emptied.Add(infrastructure.Kafka.Resource);

        return model.Resources
            .Where(resource => resource is ProjectResource or ExecutableResource)
            .Where(resource => resource.Annotations.OfType<WaitAnnotation>().Any(wait => emptied.Contains(wait.Resource)));
    }

    private static async Task StopAsync(
        IReadOnlyList<IResource> services, ResourceCommandService commands, ResourceStates states, CancellationToken cancellationToken)
    {
        await Task.WhenAll(services.Select(async service =>
        {
            var result = await commands.ExecuteCommandAsync(service, KnownResourceCommands.StopCommand, cancellationToken);
            if (!result.Success)
                throw new InvalidOperationException($"{service.Name} did not stop: {result.Message}");
            await WaitUntilAsync(() => states.Of(service).All(IsStopped), StopTimeout, $"{service.Name} stopping", cancellationToken);
        }));
    }

    /// <summary>
    /// Starts each service once every service in the list it waits for is healthy (DataAcquisition before its worker,
    /// which does not migrate the database they share), then waits for it to turn healthy itself.
    /// </summary>
    private static async Task StartAsync(
        IReadOnlyList<IResource> services, ResourceCommandService commands, ResourceStates states, CancellationToken cancellationToken)
    {
        var restarting = services.ToHashSet();
        var started = new ConcurrentDictionary<IResource, Lazy<Task>>();

        Task Start(IResource service) => started.GetOrAdd(service, s => new Lazy<Task>(() => StartOne(s))).Value;

        async Task StartOne(IResource service)
        {
            foreach (var dependency in service.Annotations.OfType<WaitAnnotation>().Select(wait => wait.Resource).Where(restarting.Contains))
                await Start(dependency);

            var result = await commands.ExecuteCommandAsync(service, KnownResourceCommands.StartCommand, cancellationToken);
            if (!result.Success)
                throw new InvalidOperationException($"{service.Name} did not start: {result.Message}");
            await WaitUntilAsync(() => states.Of(service) is { Count: > 0 } instances && instances.All(IsHealthy), HealthyTimeout, $"{service.Name} turning healthy", cancellationToken);
        }

        await Task.WhenAll(services.Select(Start));
    }

    private static bool IsStopped(CustomResourceSnapshot? snapshot) =>
        snapshot?.State?.Text is not { } state || state == KnownResourceStates.NotStarted || KnownResourceStates.TerminalStates.Contains(state);

    private static bool IsRunning(CustomResourceSnapshot? snapshot) => !IsStopped(snapshot);

    private static bool IsHealthy(CustomResourceSnapshot? snapshot) =>
        snapshot?.State?.Text == KnownResourceStates.Running && snapshot.HealthStatus is HealthStatus.Healthy;

    private static async Task WaitUntilAsync(Func<bool> condition, TimeSpan timeout, string what, CancellationToken cancellationToken)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(timeout);
        try
        {
            while (!condition())
                await Task.Delay(TimeSpan.FromSeconds(1), deadline.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException($"Gave up waiting for {what} after {timeout.TotalMinutes:0} minutes.");
        }
    }

    // ---------------------------------------------------------------------------------------------------
    // The stores
    // ---------------------------------------------------------------------------------------------------

    /// <summary>
    /// Drops and creates each database again, empty. Creating it again matters: Aspire creates the databases only
    /// when SQL Server starts, EF's migrations would create one but Validation's Hibernate would not.
    /// </summary>
    private static async Task<string> EmptySqlAsync(LinkInfrastructure infrastructure, string? keep, ILogger logger, CancellationToken cancellationToken)
    {
        var connectionString = await infrastructure.Sql.Resource.GetConnectionStringAsync(cancellationToken)
            ?? throw new InvalidOperationException("SQL Server has no connection string yet.");
        var databases = infrastructure.Sql.Resource.Databases.Values
            .Where(name => !string.Equals(name, keep, StringComparison.OrdinalIgnoreCase))
            .Order(StringComparer.Ordinal)
            .ToList();

        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        foreach (var database in databases)
        {
            var quoted = "[" + database.Replace("]", "]]") + "]";
            // ROLLBACK IMMEDIATE closes whatever still holds a connection: a tool such as SSMS, or pooled connections
            // of the stopped services that SQL Server has not noticed are gone.
            await using var command = new SqlCommand(
                $"""
                IF DB_ID(@name) IS NOT NULL
                BEGIN
                    ALTER DATABASE {quoted} SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
                    DROP DATABASE {quoted};
                END;
                CREATE DATABASE {quoted};
                """, connection) { CommandTimeout = 300 };
            command.Parameters.AddWithValue("@name", database);
            await command.ExecuteNonQueryAsync(cancellationToken);
            logger.LogInformation("Reset: emptied the SQL Server database {Database}", database);
        }

        return keep is null
            ? $"Emptied {databases.Count} SQL Server databases."
            : $"Emptied {databases.Count} SQL Server databases and kept {keep}.";
    }

    /// <summary>Drops every database but Mongo's own, and Automation UI's unless that is being cleared too.</summary>
    private static async Task<string> EmptyMongoAsync(bool clearAutomation, ILogger logger, CancellationToken cancellationToken)
    {
        var client = new MongoClient(LinkInfrastructure.MongoConnectionString);
        var names = await (await client.ListDatabaseNamesAsync(cancellationToken)).ToListAsync(cancellationToken);
        var dropped = names
            .Where(name => name is not ("admin" or "local" or "config"))
            .Where(name => clearAutomation || name != AutomationMongoDatabase)
            .ToList();
        foreach (var name in dropped)
        {
            await client.DropDatabaseAsync(name, cancellationToken);
            logger.LogInformation("Reset: dropped the Mongo database {Database}", name);
        }
        return dropped.Count == 0 ? "Mongo had nothing to drop." : $"Dropped the Mongo databases {string.Join(", ", dropped)}.";
    }

    /// <summary>
    /// Deletes each container and creates it again, rather than deleting blob by blob: the resource cache can hold a
    /// great many. Creating it can be refused for a moment while the deletion finishes.
    /// </summary>
    private static async Task<string> EmptyBlobsAsync(bool clearAutomation, ILogger logger, CancellationToken cancellationToken)
    {
        var service = new BlobServiceClient(LinkInfrastructure.BlobConnectionString);
        var containers = clearAutomation
            ? new[] { LinkInfrastructure.InternalContainer, LinkInfrastructure.ExternalContainer, LinkInfrastructure.AutomationContainer }
            : new[] { LinkInfrastructure.InternalContainer, LinkInfrastructure.ExternalContainer };

        foreach (var name in containers)
        {
            var container = service.GetBlobContainerClient(name);
            await container.DeleteIfExistsAsync(cancellationToken: cancellationToken);
            for (var attempt = 1; ; attempt++)
            {
                try
                {
                    await container.CreateIfNotExistsAsync(cancellationToken: cancellationToken);
                    break;
                }
                catch (RequestFailedException ex) when (ex.ErrorCode == "ContainerBeingDeleted" && attempt < 30)
                {
                    await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
                }
            }
            logger.LogInformation("Reset: emptied the blob container {Container}", name);
        }
        return $"Emptied the blob containers {string.Join(", ", containers)}.";
    }

    /// <summary>
    /// <c>FLUSHALL</c>, sent as one of Redis's inline commands. This host has no Redis client of its own, and one
    /// command does not need one.
    /// </summary>
    private static async Task<string> FlushRedisAsync(ILogger logger, CancellationToken cancellationToken)
    {
        using var client = new TcpClient();
        await client.ConnectAsync("127.0.0.1", Ports.Redis, cancellationToken);
        var stream = client.GetStream();
        await stream.WriteAsync("FLUSHALL\r\n"u8.ToArray(), cancellationToken);

        var buffer = new byte[256];
        var read = await stream.ReadAsync(buffer, cancellationToken);
        var reply = Encoding.ASCII.GetString(buffer, 0, read).Trim();
        if (reply != "+OK")
            throw new InvalidOperationException($"Redis answered FLUSHALL with \"{reply}\".");

        logger.LogInformation("Reset: flushed Redis");
        return "Flushed Redis.";
    }

    /// <summary>
    /// Deletes every topic but Kafka's internal ones, waits for the deletion to finish, and creates them again from
    /// <c>topics.txt</c> with kafka-init's own script. It runs inside the broker's container with Kafka's own CLI,
    /// as kafka-init does: the .NET admin client crashed this host reading topic results (see
    /// <see cref="InfrastructureExtensions"/>). The committed offsets of a deleted topic go with it.
    /// </summary>
    private static async Task<string> ResetKafkaAsync(
        LinkInfrastructure infrastructure, ResourceStates states, string topicsFile, ILogger logger, CancellationToken cancellationToken)
    {
        var containerId = states.Of(infrastructure.Kafka.Resource)
            .Select(snapshot => snapshot.Properties.FirstOrDefault(p => p.Name == "container.id")?.Value as string)
            .FirstOrDefault(id => !string.IsNullOrEmpty(id))
            ?? throw new InvalidOperationException("Could not find Kafka's container.");

        var script = $"cat > {TopicsFileInContainer}\n{DeleteTopicsScript}\n{InfrastructureExtensions.CreateTopicsScript}";
        var runtime = Environment.GetEnvironmentVariable("ASPIRE_CONTAINER_RUNTIME")
                      ?? Environment.GetEnvironmentVariable("DOTNET_ASPIRE_CONTAINER_RUNTIME")
                      ?? "docker";

        var start = new ProcessStartInfo(runtime)
        {
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };
        foreach (var argument in new[]
                 {
                     "exec", "-i",
                     "-e", $"BOOTSTRAP={KafkaInContainerBootstrap}",
                     "-e", $"TOPICS_FILE={TopicsFileInContainer}",
                     containerId, "sh", "-c", script
                 })
            start.ArgumentList.Add(argument);

        using var process = Process.Start(start) ?? throw new InvalidOperationException($"Could not run {runtime}.");
        try
        {
            await process.StandardInput.WriteAsync((await File.ReadAllTextAsync(topicsFile, cancellationToken)).Replace("\r\n", "\n"));
            process.StandardInput.Close();
            var output = process.StandardOutput.ReadToEndAsync(cancellationToken);
            var error = process.StandardError.ReadToEndAsync(cancellationToken);
            await process.WaitForExitAsync(cancellationToken);

            foreach (var line in (await output).Split('\n', StringSplitOptions.RemoveEmptyEntries))
                logger.LogInformation("Reset (Kafka): {Line}", line);
            if (process.ExitCode != 0)
                throw new InvalidOperationException($"Resetting Kafka's topics failed ({process.ExitCode}): {(await error).Trim()}");
        }
        catch (OperationCanceledException)
        {
            process.Kill(entireProcessTree: true);
            throw;
        }

        return "Deleted and recreated Kafka's topics.";
    }

    /// <summary>
    /// Deletes every topic whose name does not start with an underscore (Kafka's own), then waits up to two minutes
    /// for them to be gone, since creating a topic still being deleted fails. POSIX sh, for the image's shell.
    /// </summary>
    private const string DeleteTopicsScript = """
        set -e
        T=/opt/kafka/bin/kafka-topics.sh
        if [ -n "$($T --bootstrap-server "$BOOTSTRAP" --list | grep -v '^__' || true)" ]; then
          $T --bootstrap-server "$BOOTSTRAP" --delete --topic '[^_].*'
        fi
        i=0
        while [ -n "$($T --bootstrap-server "$BOOTSTRAP" --list | grep -v '^__' || true)" ]; do
          i=$((i + 1))
          if [ "$i" -gt 60 ]; then echo "topics are still being deleted after two minutes" >&2; exit 1; fi
          sleep 2
        done
        echo "Deleted every topic"
        """;

    /// <summary>
    /// The latest state of every resource instance while the reset runs, from <see cref="ResourceNotificationService.WatchAsync"/>,
    /// which reports each replica separately and starts with the state every resource is in now. Aspire's own lookup by
    /// instance name needs the replicas' generated names, which it does not make public.
    /// </summary>
    private sealed class ResourceStates : IAsyncDisposable
    {
        private readonly ConcurrentDictionary<string, (IResource Resource, CustomResourceSnapshot Snapshot)> _latest = new();
        private readonly CancellationTokenSource _stop = new();
        private Task _watching = Task.CompletedTask;

        public static ResourceStates Watch(ResourceNotificationService notifications)
        {
            var states = new ResourceStates();
            states._watching = Task.Run(async () =>
            {
                try
                {
                    await foreach (var update in notifications.WatchAsync(states._stop.Token))
                        states._latest[update.ResourceId] = (update.Resource, update.Snapshot);
                }
                catch (OperationCanceledException)
                {
                    // Disposed.
                }
            });
            return states;
        }

        /// <summary>The latest snapshot of each instance of <paramref name="resource"/>: one, or one per replica.</summary>
        public IReadOnlyList<CustomResourceSnapshot> Of(IResource resource) =>
            _latest.Values.Where(entry => entry.Resource == resource).Select(entry => entry.Snapshot).ToList();

        /// <summary>Waits until every one of <paramref name="resources"/> has reported a state.</summary>
        public Task WaitForAsync(IEnumerable<IResource> resources, CancellationToken cancellationToken)
        {
            var wanted = resources.ToList();
            return WaitUntilAsync(() => wanted.All(resource => Of(resource).Count > 0), TimeSpan.FromSeconds(30),
                "the dashboard to report every service's state", cancellationToken);
        }

        public async ValueTask DisposeAsync()
        {
            await _stop.CancelAsync();
            await _watching;
            _stop.Dispose();
        }
    }
}
