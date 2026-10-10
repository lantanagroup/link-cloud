using System.Diagnostics;
using System.Security.Claims;
using System.Text;
using Confluent.Kafka;
using Confluent.Kafka.Admin;
using LantanaGroup.Link.LinkAdmin.BFF.Application.KafkaOps;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using LantanaGroup.Link.Shared.Application.Models.Kafka;
using Link.Authorization.Infrastructure;
using Link.Authorization.Permissions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace KafkaOps.Proof;

/// <summary>
/// Live proof against the box PLAINTEXT cluster (kafka-1, kafka-2, kafka-3).
/// Skipped unless KAFKA_BOX_PROOF=1 and KAFKA_BOOTSTRAP is set, so the isolated
/// proof kit (which also sets KAFKA_BOOTSTRAP) does not run this test.
/// </summary>
public class ReplicationFactorProofTests
{
    private const string Container = "silo-bug-investigator-kafka-kafka-1-1";
    private const string ProduceTopic = "ResourcesAcquired";
    private const long Throttle = 32_768;

    [BrokerRequiredFact]
    public async Task LiveCluster_PlansAndAppliesReplicationFactor_ThenProducesOneMessage()
    {
        var bootstrap = Environment.GetEnvironmentVariable("KAFKA_BOOTSTRAP");
        if (string.IsNullOrWhiteSpace(bootstrap))
            throw new InvalidOperationException("KAFKA_BOOTSTRAP is not set.");

        var topic = "rf-proof-" + Guid.NewGuid().ToString("N");
        var messageKey = "proof-" + Guid.NewGuid().ToString("N");
        var groupId = "rf-proof-read-" + Guid.NewGuid().ToString("N");
        var infra = new BoxKafkaInfra();
        using var gateway = new KafkaBrokerGateway(new KafkaConnection
        {
            BootstrapServers = [bootstrap],
            SaslProtocolEnabled = false
        });
        var service = ProofService(gateway, infra);
        using var admin = NewAdmin(bootstrap);
        IConsumer<string, string>? consumer = null;
        try
        {
            await admin.CreateTopicsAsync(
                [
                    new TopicSpecification
                    {
                        Name = topic,
                        NumPartitions = 2,
                        ReplicationFactor = 3,
                        Configs = new Dictionary<string, string> { ["min.insync.replicas"] = "2" }
                    }
                ],
                new CreateTopicsOptions { RequestTimeout = TimeSpan.FromSeconds(30) });
            await gateway.ProduceRecordAsync(topic, "seed", "one", [], CancellationToken.None);

            var plan = await service.PlanReplicationFactorAsync(topic, 2, Throttle, "", 1, 25, CancellationToken.None);
            Assert.True(plan.Accepted, plan.Summary);
            Assert.Equal(3, plan.CurrentFactor);
            Assert.Equal(2, plan.TargetFactor);
            Assert.Equal(2, plan.MinInSyncReplicas);
            Assert.True(plan.ChangedCount > 0, plan.Summary);

            var refused = await Assert.ThrowsAsync<KafkaOpsRejectedException>(() =>
                service.CreateReplicationFactorAsync(User(), topic, 2, Throttle, "prove the route", "wrong", "corr-rf", CancellationToken.None));
            Assert.Contains("topic name", refused.Message, StringComparison.Ordinal);
            Assert.Equal(0, infra.ApplyCalls);

            var created = await service.CreateReplicationFactorAsync(
                User(), topic, 2, Throttle, "prove the replication factor route", topic, "corr-rf-proof", CancellationToken.None);
            Assert.Equal(KafkaChangeKind.ReplicationFactor, created.Kind);
            Assert.Equal(KafkaChangeStatus.Converging, created.Status);
            Assert.Equal(3, created.BeforeReplicationFactor);
            Assert.Equal(2, created.TargetReplicationFactor);
            Assert.Equal(Throttle, created.ThrottleBytesPerSecond);
            Assert.Contains("--throttle " + Throttle.ToString(System.Globalization.CultureInfo.InvariantCulture), infra.LastScript, StringComparison.Ordinal);
            Assert.Contains(topic, infra.LastJson, StringComparison.Ordinal);
            Assert.False(created.ThrottleCleared);

            var deadline = DateTime.UtcNow.AddMinutes(2);
            var current = created;
            while (current.Status is KafkaChangeStatus.Converging or KafkaChangeStatus.Executing)
            {
                if (DateTime.UtcNow >= deadline)
                    break;
                await service.TrackAsync(CancellationToken.None);
                current = (await service.GetAsync(created.Id, CancellationToken.None))!;
                Console.WriteLine("rf " + current.Status + " " + current.Progress);
                if (current.Status is KafkaChangeStatus.Failed or KafkaChangeStatus.TimedOut)
                    break;
                await Task.Delay(TimeSpan.FromSeconds(2));
            }

            Assert.Equal(KafkaChangeStatus.Done, current.Status);
            Assert.True(current.ThrottleCleared);
            Assert.Contains(current.Steps, step => step == "Throttle cleared.");
            Assert.Contains("Throttle cleared", current.Progress, StringComparison.Ordinal);

            await Task.Delay(TimeSpan.FromSeconds(2));
            using (var check = NewAdmin(bootstrap))
            {
                var moved = check.GetMetadata(topic, TimeSpan.FromSeconds(15));
                var described = moved.Topics.Single(item => item.Topic == topic);
                Assert.False(described.Error.IsError, described.Error.Reason);
                Assert.All(described.Partitions, partition =>
                {
                    Assert.Equal(2, partition.Replicas.Length);
                    Assert.Equal(2, partition.InSyncReplicas.Length);
                });

                var kept = check.GetMetadata(ProduceTopic, TimeSpan.FromSeconds(15));
                Assert.All(kept.Topics.Single(item => item.Topic == ProduceTopic).Partitions, partition => Assert.Equal(3, partition.Replicas.Length));
            }

            var configs = await admin.DescribeConfigsAsync(
                [new ConfigResource { Type = ResourceType.Topic, Name = topic }],
                new DescribeConfigsOptions { RequestTimeout = TimeSpan.FromSeconds(15) });
            foreach (var result in configs)
            {
                foreach (var name in new[] { "leader.replication.throttled.replicas", "follower.replication.throttled.replicas" })
                {
                    if (result.Entries.TryGetValue(name, out var entry))
                        Assert.True(string.IsNullOrEmpty(entry.Value), name + " is still " + entry.Value);
                }
            }

            consumer = new ConsumerBuilder<string, string>(new ConsumerConfig
            {
                BootstrapServers = bootstrap,
                GroupId = groupId,
                ClientId = "rf-proof-read",
                EnableAutoCommit = false,
                EnableAutoOffsetStore = false,
                AllowAutoCreateTopics = false,
                AutoOffsetReset = AutoOffsetReset.Earliest
            }).Build();
            var partitions = admin.GetMetadata(ProduceTopic, TimeSpan.FromSeconds(15))
                .Topics.Single(item => item.Topic == ProduceTopic)
                .Partitions
                .Select(partition => new TopicPartition(ProduceTopic, partition.PartitionId))
                .ToList();
            consumer.Assign(partitions);
            var start = new Dictionary<int, Offset>();
            foreach (var partition in partitions)
            {
                var watermarks = consumer.QueryWatermarkOffsets(partition, TimeSpan.FromSeconds(15));
                start[partition.Partition.Value] = watermarks.High;
            }

            var produced = await service.ProduceMessageAsync(
                User(),
                ProduceTopic,
                "X-Note: live-proof",
                messageKey,
                "{\"proof\":true}",
                "prove the produce route",
                ProduceTopic,
                "corr-produce-proof",
                CancellationToken.None);
            Assert.Equal(KafkaChangeKind.Produce, produced.Kind);
            Assert.Equal(KafkaChangeStatus.Done, produced.Status);
            Assert.Equal("One message was produced.", produced.Progress);

            consumer.Assign(partitions.Select(partition => new TopicPartitionOffset(partition, start[partition.Partition.Value])).ToList());
            ConsumeResult<string, string>? match = null;
            var readUntil = DateTime.UtcNow.AddSeconds(20);
            while (match is null && DateTime.UtcNow < readUntil)
            {
                var result = consumer.Consume(TimeSpan.FromSeconds(1));
                if (result?.Message?.Key == messageKey)
                    match = result;
            }

            Assert.NotNull(match);
            Assert.Equal("{\"proof\":true}", match!.Message.Value);
            var note = match.Message.Headers.FirstOrDefault(header => header.Key == "X-Note");
            Assert.NotNull(note);
            Assert.Equal("live-proof", Encoding.UTF8.GetString(note!.GetValueBytes()));
        }
        finally
        {
            consumer?.Close();
            consumer?.Dispose();
            await BoxKafkaInfra.CleanupAsync(topic, groupId);
        }
    }

    private static IAdminClient NewAdmin(string bootstrap) =>
        new AdminClientBuilder(new AdminClientConfig
        {
            BootstrapServers = bootstrap,
            ClientId = "rf-proof-admin",
            TopicMetadataRefreshIntervalMs = 1_000,
            MetadataMaxAgeMs = 1_000
        }).Build();

    private static KafkaOpsService ProofService(KafkaBrokerGateway gateway, BoxKafkaInfra infra) =>
        new(
            gateway,
            new ProofCache(),
            Options.Create(new KafkaOpsOptions
            {
                RequireSecondApprover = false,
                MaxPartitionsPerTopic = 24,
                RateLimitMinutes = 30,
                CacheSeconds = 8,
                ScaleTimeoutSeconds = 150
            }),
            new ProofHost(),
            NullLogger<KafkaOpsService>.Instance,
            new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Authentication:EnableAnonymousAccess"] = "false"
            }).Build(),
            Array.Empty<IProducer<string, AuditEventMessage>>(),
            infra);

    private static ClaimsPrincipal User()
    {
        var identity = new ClaimsIdentity(
            [
                new Claim(ClaimTypes.Name, "rf-proof"),
                new Claim(LinkAuthorizationConstants.LinkSystemClaims.LinkPermissions, nameof(LinkSystemPermissions.CanManageKafkaTopics))
            ],
            "test",
            ClaimTypes.Name,
            ClaimTypes.Role);
        return new ClaimsPrincipal(identity);
    }

    private sealed class BrokerRequiredFactAttribute : FactAttribute
    {
        public BrokerRequiredFactAttribute()
        {
            if (!string.Equals(Environment.GetEnvironmentVariable("KAFKA_BOX_PROOF"), "1", StringComparison.Ordinal)
                || string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("KAFKA_BOOTSTRAP")))
                Skip = "KAFKA_BOX_PROOF=1 and KAFKA_BOOTSTRAP are required.";
        }
    }

    private sealed class ProofHost : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Development";
        public string ApplicationName { get; set; } = "proof";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    private sealed class ProofCache : LantanaGroup.Link.Shared.Application.Interfaces.ICacheService
    {
        private readonly Dictionary<string, object> _store = new(StringComparer.Ordinal);

        public Task<T> GetAsync<T>(string key, CancellationToken cancellationToken = default)
        {
            if (_store.TryGetValue(key, out var value) && value is T typed)
                return Task.FromResult(typed);
            return Task.FromResult(default(T)!);
        }

        public Task SetAsync<T>(string key, T value, TimeSpan expiration, ExpirationType expirationType = ExpirationType.Sliding, CancellationToken cancellationToken = default)
        {
            _store[key] = value!;
            return Task.CompletedTask;
        }

        public Task RemoveAsync(string key, CancellationToken cancellationToken = default)
        {
            _store.Remove(key);
            return Task.CompletedTask;
        }
    }

    private sealed class BoxKafkaInfra : IKafkaInfraProvider
    {
        public string Name => "LocalCompose";
        public bool Enabled => true;
        public string Detail => "The box Kafka cluster.";
        public int ApplyCalls { get; private set; }
        public string LastScript { get; private set; } = "";
        public string LastJson { get; private set; } = "";

        public Task ScaleGroupAsync(string groupId, int replicas, CancellationToken cancellationToken) =>
            Task.FromException(new KafkaOpsRejectedException("Scale is not part of this proof."));

        public Task AddBrokerAsync(CancellationToken cancellationToken) =>
            Task.FromException(new KafkaOpsRejectedException("Add broker is not part of this proof."));

        public Task RemoveBrokerAsync(int brokerId, CancellationToken cancellationToken) =>
            Task.FromException(new KafkaOpsRejectedException("Remove broker is not part of this proof."));

        public Task ApplyReassignmentAsync(string reassignmentJson, string rebalanceName, bool refresh, CancellationToken cancellationToken) =>
            ApplyReassignmentAsync(reassignmentJson, rebalanceName, 0, cancellationToken);

        public async Task ApplyReassignmentAsync(string reassignmentJson, string rebalanceName, long throttleBytesPerSecond, CancellationToken cancellationToken)
        {
            KafkaRebalanceNames.Require(rebalanceName);
            if (throttleBytesPerSecond < 0 || throttleBytesPerSecond > 1_073_741_824)
                throw new KafkaOpsRejectedException("The throttle is not valid.");
            var throttle = throttleBytesPerSecond > 0
                ? " --throttle " + throttleBytesPerSecond.ToString(System.Globalization.CultureInfo.InvariantCulture)
                : "";
            var file = "/tmp/" + rebalanceName + ".json";
            var script = "cat > " + file + " && /opt/kafka/bin/kafka-reassign-partitions.sh --bootstrap-server kafka-1:9092 --reassignment-json-file " + file + " --execute" + throttle;
            ApplyCalls++;
            LastScript = script;
            LastJson = reassignmentJson;
            var result = await DockerAsync(script, reassignmentJson, cancellationToken);
            if (result.ExitCode != 0)
                throw new KafkaOpsRejectedException("The reassignment was not submitted. " + Trim(result.Error + " " + result.Output));
        }

        public Task CancelReassignmentAsync(string rebalanceName, CancellationToken cancellationToken) =>
            Task.FromException(new KafkaOpsRejectedException("Cancel is not part of this proof."));

        public async Task ReleaseRebalanceAsync(string rebalanceName, CancellationToken cancellationToken)
        {
            KafkaRebalanceNames.Require(rebalanceName);
            var file = "/tmp/" + rebalanceName + ".json";
            var script = "/opt/kafka/bin/kafka-reassign-partitions.sh --bootstrap-server kafka-1:9092 --reassignment-json-file " + file + " --verify";
            var result = await DockerAsync(script, null, cancellationToken);
            var text = result.Output + " " + result.Error;
            if (result.ExitCode != 0 || text.Contains("still in progress", StringComparison.OrdinalIgnoreCase))
                throw new KafkaOpsRejectedException("The throttle was not cleared. " + Trim(text));
        }

        public async Task<ReassignmentListing> ListInFlightReassignmentsAsync(CancellationToken cancellationToken)
        {
            var result = await DockerAsync(
                "/opt/kafka/bin/kafka-reassign-partitions.sh --bootstrap-server kafka-1:9092 --list",
                null,
                cancellationToken);
            return ReassignmentListText.Read(result);
        }

        public static async Task CleanupAsync(string topic, string groupId)
        {
            try
            {
                await DockerAsync(
                    "/opt/kafka/bin/kafka-topics.sh --bootstrap-server kafka-1:9092 --delete --topic " + topic,
                    null,
                    CancellationToken.None);
            }
            catch (Exception ex)
            {
                Console.WriteLine("topic delete: " + ex.Message);
            }

            try
            {
                await DockerAsync(
                    "/opt/kafka/bin/kafka-consumer-groups.sh --bootstrap-server kafka-1:9092 --delete --group " + groupId,
                    null,
                    CancellationToken.None);
            }
            catch (Exception ex)
            {
                Console.WriteLine("group delete: " + ex.Message);
            }

            foreach (var broker in new[] { "1", "2", "3" })
            {
                try
                {
                    await DockerAsync(
                        "/opt/kafka/bin/kafka-configs.sh --bootstrap-server kafka-1:9092 --entity-type brokers --entity-name " + broker +
                        " --alter --delete-config leader.replication.throttled.rate,follower.replication.throttled.rate",
                        null,
                        CancellationToken.None);
                }
                catch (Exception ex)
                {
                    Console.WriteLine("throttle clear " + broker + ": " + ex.Message);
                }
            }
        }

        private static async Task<ProcessResult> DockerAsync(string script, string? stdin, CancellationToken cancellationToken)
        {
            var start = new ProcessStartInfo("docker")
            {
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false
            };
            start.ArgumentList.Add("exec");
            start.ArgumentList.Add("-i");
            start.ArgumentList.Add(Container);
            start.ArgumentList.Add("bash");
            start.ArgumentList.Add("-lc");
            start.ArgumentList.Add(script);
            using var process = Process.Start(start) ?? throw new InvalidOperationException("docker did not start.");
            var outputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
            var errorTask = process.StandardError.ReadToEndAsync(cancellationToken);
            if (!string.IsNullOrEmpty(stdin))
                await process.StandardInput.WriteAsync(stdin.AsMemory(), cancellationToken);
            process.StandardInput.Close();
            await process.WaitForExitAsync(cancellationToken);
            return new ProcessResult(process.ExitCode, await outputTask, await errorTask);
        }

        private static string Trim(string text)
        {
            var compact = (text ?? "").Replace('\n', ' ').Trim();
            return compact.Length <= 500 ? compact : compact[..500];
        }
    }
}
