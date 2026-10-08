using Task = System.Threading.Tasks.Task;
using LantanaGroup.Link.LinkAdmin.BFF.Application.KafkaOps;

namespace UnitTests.Kafka;

[Trait("Category", "UnitTests")]
public class KafkaOpsPlannerTests
{
    [Fact]
    public void ReplicaScale_BlocksCountsAboveThePartitionCeiling()
    {
        var allowed = ReplicaScalePlanner.Evaluate("Report", 1, 3, 3, 3, requireSecondApprover: false);
        Assert.True(allowed.Accepted);
        Assert.Equal(3, allowed.Ceiling);

        var idle = ReplicaScalePlanner.Evaluate("Report", 1, 3, 4, 3, requireSecondApprover: false);
        Assert.False(idle.Accepted);
        Assert.Contains(idle.Errors, error => error.Contains("sit idle", StringComparison.OrdinalIgnoreCase));

        var storedCap = ReplicaScalePlanner.Evaluate("Report", 1, 6, 5, 3, requireSecondApprover: true);
        Assert.False(storedCap.Accepted);
        Assert.Equal(3, storedCap.Ceiling);
        Assert.True(storedCap.SecondApproverRequired);
    }

    [Fact]
    public void Decommission_BlocksWhenReplicationFactorCannotBeKept()
    {
        var partitions = new List<BrokerPartitionFact>
        {
            new()
            {
                Topic = "ReportScheduled",
                Partition = 0,
                Leader = 1,
                Replicas = [1, 2, 3],
                MinInSyncReplicas = 2
            }
        };

        var blocked = BrokerMovePlanner.Decommission(1, [1, 2, 3], partitions);
        Assert.False(blocked.Accepted);
        Assert.Contains(blocked.Errors, error => error.Contains("replication factor", StringComparison.OrdinalIgnoreCase));

        var moved = BrokerMovePlanner.Decommission(1, [1, 2, 3, 4], partitions);
        Assert.True(moved.Accepted);
        Assert.Equal(4, moved.Moves[0].ToBroker);
        Assert.DoesNotContain(1, moved.Moves[0].Replicas);
        Assert.Equal(3, moved.Moves[0].Replicas.Count);
    }

    [Fact]
    public void Decommission_RefusesAControllerEligibleBroker()
    {
        var partitions = new List<BrokerPartitionFact>
        {
            new()
            {
                Topic = "ReadyToAcquire",
                Partition = 0,
                Leader = 1,
                Replicas = [1, 2, 3],
                MinInSyncReplicas = 2
            }
        };
        var brokers = new List<int> { 0, 1, 2, 3 };

        var controller = BrokerMovePlanner.Decommission(1, brokers, partitions, [0, 1, 2]);
        Assert.False(controller.Accepted);
        Assert.Contains(controller.Errors, error => error.Contains("controller", StringComparison.OrdinalIgnoreCase));

        var brokerOnly = BrokerMovePlanner.Decommission(3, brokers, partitions, [0, 1, 2]);
        Assert.True(brokerOnly.Accepted);
    }

    [Fact]
    public void Decommission_AllowsAnEmptyBroker()
    {
        var plan = BrokerMovePlanner.Decommission(9, [1, 9],
        [
            new BrokerPartitionFact { Topic = "ReportScheduled", Partition = 0, Leader = 1, Replicas = [1], MinInSyncReplicas = 1 }
        ]);

        Assert.True(plan.Accepted);
        Assert.True(plan.AlreadyEmpty);
    }

    [Fact]
    public void Spread_MovesAReplicaOntoTheNewBroker()
    {
        var plan = BrokerMovePlanner.SpreadOnto(4, [1, 2, 4],
        [
            new BrokerPartitionFact { Topic = "SubmitPayload", Partition = 0, Leader = 1, Replicas = [1], MinInSyncReplicas = 1 },
            new BrokerPartitionFact { Topic = "SubmitPayload", Partition = 1, Leader = 2, Replicas = [2], MinInSyncReplicas = 1 }
        ]);

        Assert.True(plan.Accepted);
        Assert.NotEmpty(plan.Moves);
        Assert.All(plan.Moves, move => Assert.Contains(4, move.Replicas));
    }

    [Fact]
    public async Task StrimziProvider_WritesNodePoolAndRebalance()
    {
        var kubernetes = new RecordingKubernetes();
        kubernetes.Bodies["kafkanodepools/link-brokers"] = """{"spec":{"replicas":3}}""";
        var provider = new StrimziKafkaInfraProvider(kubernetes, new KafkaOpsOptions
        {
            KubernetesNamespace = "kafka",
            KafkaNodePool = "link-brokers",
            ConsumerDeployments = "Report=report"
        });

        await provider.AddBrokerAsync(CancellationToken.None);
        await provider.ScaleGroupAsync("Report", 2, CancellationToken.None);
        await provider.ApplyReassignmentAsync("""{"leavingBroker":3,"partitions":[]}""", CancellationToken.None);

        Assert.Contains("\"replicas\":4", kubernetes.Applied["kafkanodepools/link-brokers"], StringComparison.Ordinal);
        await provider.RemoveBrokerAsync(1, CancellationToken.None);
        var removed = kubernetes.Applied["kafkanodepools/link-brokers"];
        Assert.Contains("strimzi.io/remove-node-ids", removed, StringComparison.Ordinal);
        Assert.Contains("[1]", removed, StringComparison.Ordinal);
        Assert.Contains("\"replicas\":2", removed, StringComparison.Ordinal);
        Assert.Contains("\"replicas\":2", kubernetes.Applied["deployments/report"], StringComparison.Ordinal);
        Assert.Contains("remove-brokers", kubernetes.Applied["kafkarebalances/link-ops-remove-brokers"], StringComparison.Ordinal);
        Assert.Contains("strimzi.io/rebalance", kubernetes.Applied["kafkarebalances/link-ops-remove-brokers"], StringComparison.Ordinal);
    }

    [Fact]
    public void LocalCompose_IsRefusedInProduction()
    {
        var provider = KafkaInfraProviderFactory.Create(
            new KafkaOpsOptions { InfraProvider = "LocalCompose", ComposeProject = "kafka-ops-proof" },
            production: true,
            new RecordingProcess(),
            new RecordingKubernetes());

        Assert.IsType<DisabledKafkaInfraProvider>(provider);
        Assert.False(provider.Enabled);
        Assert.Contains("Production", provider.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public async Task LocalCompose_ScalesWithoutAHostPort()
    {
        var process = new RecordingProcess();
        var provider = new LocalComposeKafkaInfraProvider(process, new KafkaOpsOptions
        {
            ComposeProject = "kafka-ops-proof",
            ConsumerService = "consumer"
        });

        await provider.ScaleGroupAsync("Report", 3, CancellationToken.None);

        Assert.Equal(0, process.ExitCode);
        Assert.Contains("kafka-ops-proof", process.Arguments);
        Assert.Contains("--scale", process.Arguments);
        Assert.Contains("consumer=3", process.Arguments);
        Assert.DoesNotContain("528", process.Arguments, StringComparison.Ordinal);
    }

    [Fact]
    public async Task LocalCompose_PassesTheComposeFile_AndTheConfiguredBrokerNames()
    {
        var process = new RecordingProcess();
        var provider = new LocalComposeKafkaInfraProvider(process, new KafkaOpsOptions
        {
            ComposeProject = "kafka-ops-proof",
            ComposeFile = @"C:\proof\compose.yml|C:\proof\compose.publish.yml",
            ExtraBrokerProfile = "extra-broker",
            ExtraBrokerService = "broker-3",
            BrokerServicePrefix = "broker-",
            BrokerService = "broker-0"
        });

        await provider.AddBrokerAsync(CancellationToken.None);
        Assert.Contains("-f", process.Arguments, StringComparison.Ordinal);
        Assert.Contains(@"C:\proof\compose.yml", process.Arguments, StringComparison.Ordinal);
        Assert.Contains("compose.publish.yml", process.Arguments, StringComparison.Ordinal);
        Assert.Contains("--profile", process.Arguments, StringComparison.Ordinal);
        Assert.Contains("extra-broker", process.Arguments, StringComparison.Ordinal);
        Assert.Contains("broker-3", process.Arguments, StringComparison.Ordinal);

        await provider.RemoveBrokerAsync(2, CancellationToken.None);
        Assert.Contains("stop", process.Arguments, StringComparison.Ordinal);
        Assert.Contains("broker-2", process.Arguments, StringComparison.Ordinal);

        await provider.ApplyReassignmentAsync("{}", CancellationToken.None);
        Assert.Contains("exec", process.Arguments, StringComparison.Ordinal);
        Assert.Contains("-T", process.Arguments, StringComparison.Ordinal);
        Assert.Contains("broker-0", process.Arguments, StringComparison.Ordinal);
    }

    private sealed class RecordingKubernetes : IKubernetesResourceClient
    {
        public Dictionary<string, string> Bodies { get; } = new(StringComparer.Ordinal);
        public Dictionary<string, string> Applied { get; } = new(StringComparer.Ordinal);

        public Task<string?> GetAsync(string apiVersion, string plural, string namespaceName, string name, CancellationToken cancellationToken) =>
            Task.FromResult<string?>(Bodies.TryGetValue(plural + "/" + name, out var body) ? body : null);

        public Task ApplyAsync(string apiVersion, string plural, string namespaceName, string name, string body, CancellationToken cancellationToken)
        {
            Applied[plural + "/" + name] = body;
            return Task.CompletedTask;
        }

        public Task DeleteAsync(string apiVersion, string plural, string namespaceName, string name, CancellationToken cancellationToken) =>
            Task.CompletedTask;
    }

    private sealed class RecordingProcess : IProcessRunner
    {
        public string Arguments { get; private set; } = "";
        public int ExitCode { get; private set; }

        public Task<ProcessResult> RunAsync(string fileName, IReadOnlyList<string> arguments, string? standardInput, CancellationToken cancellationToken)
        {
            Arguments = fileName + " " + string.Join(" ", arguments);
            return Task.FromResult(new ProcessResult(ExitCode, "", ""));
        }
    }
}
