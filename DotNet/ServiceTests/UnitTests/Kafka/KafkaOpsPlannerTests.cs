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
        await provider.ApplyReassignmentAsync("""{"leavingBroker":3,"partitions":[]}""", "link-ops-0123456789abcdef0123456789abcdef", false, CancellationToken.None);

        Assert.Contains("\"replicas\":4", kubernetes.Applied["kafkanodepools/link-brokers"], StringComparison.Ordinal);
        await provider.RemoveBrokerAsync(1, CancellationToken.None);
        var removed = kubernetes.Applied["kafkanodepools/link-brokers"];
        Assert.Contains("strimzi.io/remove-node-ids", removed, StringComparison.Ordinal);
        Assert.Contains("[1]", removed, StringComparison.Ordinal);
        Assert.Contains("\"replicas\":2", removed, StringComparison.Ordinal);
        Assert.Contains("\"replicas\":2", kubernetes.Applied["deployments/report"], StringComparison.Ordinal);
        Assert.Contains("remove-brokers", kubernetes.Applied["kafkarebalances/link-ops-0123456789abcdef0123456789abcdef"], StringComparison.Ordinal);
        Assert.Contains("\"approve\"", kubernetes.Applied["kafkarebalances/link-ops-0123456789abcdef0123456789abcdef"], StringComparison.Ordinal);
    }

    [Fact]
    public async Task StrimziRebalance_RefusesAnExistingName_RefreshesTheSameOne_AndDeletesIt()
    {
        var kubernetes = new RecordingKubernetes();
        var provider = new StrimziKafkaInfraProvider(kubernetes, new KafkaOpsOptions { KubernetesNamespace = "kafka", KafkaNodePool = "link-brokers" });
        const string first = "link-ops-aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
        const string second = "link-ops-bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";

        await provider.ApplyReassignmentAsync("""{"leavingBroker":3,"partitions":[]}""", first, false, CancellationToken.None);
        await provider.ApplyReassignmentAsync("""{"arrivingBroker":4,"partitions":[]}""", second, false, CancellationToken.None);
        Assert.NotEqual(first, second);
        Assert.True(kubernetes.Applied.ContainsKey("kafkarebalances/" + first));
        Assert.True(kubernetes.Applied.ContainsKey("kafkarebalances/" + second));

        kubernetes.Bodies["kafkarebalances/" + first] = kubernetes.Applied["kafkarebalances/" + first];
        var collision = await Assert.ThrowsAsync<KafkaOpsRejectedException>(() =>
            provider.ApplyReassignmentAsync("""{"leavingBroker":3,"partitions":[]}""", first, false, CancellationToken.None));
        Assert.Contains("already exists", collision.Message, StringComparison.Ordinal);

        await provider.ApplyReassignmentAsync("""{"leavingBroker":3,"partitions":[]}""", first, true, CancellationToken.None);
        Assert.Contains("\"refresh\"", kubernetes.Applied["kafkarebalances/" + first], StringComparison.Ordinal);
        Assert.DoesNotContain("kafkarebalances/link-ops-remove-brokers", string.Join(" ", kubernetes.Applied.Keys), StringComparison.Ordinal);

        await provider.ReleaseRebalanceAsync(first, CancellationToken.None);
        Assert.Contains("kafkarebalances/" + first, kubernetes.Deleted);
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

        await provider.ApplyReassignmentAsync("{}", "link-ops-0123456789abcdef0123456789abcdef", false, CancellationToken.None);
        Assert.Contains("exec", process.Arguments, StringComparison.Ordinal);
        Assert.Contains("-T", process.Arguments, StringComparison.Ordinal);
        Assert.Contains("broker-0", process.Arguments, StringComparison.Ordinal);
    }

    [Fact]
    public async Task LocalCompose_ListsReassignmentsOnTheSameExecChannel()
    {
        var process = new RecordingProcess();
        var provider = new LocalComposeKafkaInfraProvider(process, new KafkaOpsOptions
        {
            ComposeProject = "kafka-ops-proof",
            BrokerService = "broker-0"
        });

        process.Output = "No partition reassignments found.";
        var clear = await provider.ListInFlightReassignmentsAsync(CancellationToken.None);
        Assert.True(clear.Known);
        Assert.Empty(clear.Topics);
        Assert.Contains("exec", process.Arguments, StringComparison.Ordinal);
        Assert.Contains("-T", process.Arguments, StringComparison.Ordinal);
        Assert.Contains("broker-0", process.Arguments, StringComparison.Ordinal);
        Assert.Contains("kafka-reassign-partitions.sh", process.Arguments, StringComparison.Ordinal);
        Assert.Contains("--list", process.Arguments, StringComparison.Ordinal);
        Assert.DoesNotContain("--verify", process.Arguments, StringComparison.Ordinal);

        await provider.ReleaseRebalanceAsync("link-ops-0123456789abcdef0123456789abcdef", CancellationToken.None);
        Assert.Contains("--verify", process.Arguments, StringComparison.Ordinal);
        Assert.Contains("if [ -f /tmp/link-ops-0123456789abcdef0123456789abcdef.json ]", process.Arguments, StringComparison.Ordinal);
        Assert.Contains("broker-0:9092", process.Arguments, StringComparison.Ordinal);

        process.Output = "Current partition reassignments:\nops-proof-log-0: replicas: 1,2,3. adding: 3.\nops-proof-log-1: replicas: 0,1,2.";
        var busy = await provider.ListInFlightReassignmentsAsync(CancellationToken.None);
        Assert.True(busy.Known);
        Assert.Equal(["ops-proof-log"], busy.Topics);

        process.Output = "{\"version\":1,\"partitions\":[{\"topic\":\"ops-proof-members\",\"partition\":0,\"replicas\":[0,1]}]}";
        var json = await provider.ListInFlightReassignmentsAsync(CancellationToken.None);
        Assert.True(json.Known);
        Assert.Equal(["ops-proof-members"], json.Topics);

        process.Output = "{\"version\":1,\"partitions\":[]}";
        var emptyJson = await provider.ListInFlightReassignmentsAsync(CancellationToken.None);
        Assert.True(emptyJson.Known);
        Assert.Empty(emptyJson.Topics);

        process.Output = "";
        var silent = await provider.ListInFlightReassignmentsAsync(CancellationToken.None);
        Assert.False(silent.Known);

        process.Output = "No partition reassignments found.";
        process.ExitCode = 1;
        var failed = await provider.ListInFlightReassignmentsAsync(CancellationToken.None);
        Assert.False(failed.Known);

        process.ExitCode = 0;
        process.Output = "Error: the admin client timed out";
        var unrecognized = await provider.ListInFlightReassignmentsAsync(CancellationToken.None);
        Assert.False(unrecognized.Known);
    }

    [Fact]
    public async Task Strimzi_ListsRebalancesThatAreNotFinished()
    {
        var kubernetes = new RecordingKubernetes();
        var provider = new StrimziKafkaInfraProvider(kubernetes, new KafkaOpsOptions { KubernetesNamespace = "kafka" });
        kubernetes.Listed.Add(Rebalance("link-ops-active", "ProposalReady"));
        kubernetes.Listed.Add(Rebalance("link-ops-done", "Ready"));
        kubernetes.Listed.Add(Rebalance("link-ops-stopped", "Stopped"));
        kubernetes.Listed.Add(Rebalance("link-ops-both", "Ready", "Rebalancing"));
        kubernetes.Listed.Add("{\"metadata\":{\"name\":\"link-ops-new\"}}");

        var listing = await provider.ListInFlightReassignmentsAsync(CancellationToken.None);
        Assert.True(listing.Known);
        Assert.Equal(["link-ops-active", "link-ops-both", "link-ops-new"], listing.Topics);

        kubernetes.ListError = new InvalidOperationException("the list timed out");
        var unknown = await provider.ListInFlightReassignmentsAsync(CancellationToken.None);
        Assert.False(unknown.Known);
    }

    [Fact]
    public async Task Disabled_CannotConfirmReassignments()
    {
        var provider = new DisabledKafkaInfraProvider("Broker and replica changes are disabled.");
        var listing = await provider.ListInFlightReassignmentsAsync(CancellationToken.None);
        Assert.False(listing.Known);
        Assert.Empty(listing.Topics);
    }

    private static string Rebalance(string name, params string[] trueConditions)
    {
        var conditions = string.Join(",", trueConditions.Select(type => "{\"type\":\"" + type + "\",\"status\":\"True\"}"));
        return "{\"metadata\":{\"name\":\"" + name + "\"},\"status\":{\"conditions\":[" + conditions + "]}}";
    }

    private sealed class RecordingKubernetes : IKubernetesResourceClient
    {
        public Dictionary<string, string> Bodies { get; } = new(StringComparer.Ordinal);
        public Dictionary<string, string> Applied { get; } = new(StringComparer.Ordinal);
        public List<string> Deleted { get; } = [];

        public List<string> Listed { get; } = [];
        public Exception? ListError { get; set; }

        public Task<string?> GetAsync(string apiVersion, string plural, string namespaceName, string name, CancellationToken cancellationToken) =>
            Task.FromResult<string?>(Bodies.TryGetValue(plural + "/" + name, out var body) ? body : null);

        public Task<IReadOnlyList<string>> ListAsync(string apiVersion, string plural, string namespaceName, CancellationToken cancellationToken)
        {
            if (ListError is not null)
                throw ListError;
            return Task.FromResult<IReadOnlyList<string>>(Listed.ToList());
        }

        public Task ApplyAsync(string apiVersion, string plural, string namespaceName, string name, string body, CancellationToken cancellationToken)
        {
            Applied[plural + "/" + name] = body;
            return Task.CompletedTask;
        }

        public Task DeleteAsync(string apiVersion, string plural, string namespaceName, string name, CancellationToken cancellationToken)
        {
            Deleted.Add(plural + "/" + name);
            Bodies.Remove(plural + "/" + name);
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingProcess : IProcessRunner
    {
        public string Arguments { get; private set; } = "";
        public int ExitCode { get; set; }
        public string Output { get; set; } = "";
        public string Error { get; set; } = "";

        public Task<ProcessResult> RunAsync(string fileName, IReadOnlyList<string> arguments, string? standardInput, CancellationToken cancellationToken)
        {
            Arguments = fileName + " " + string.Join(" ", arguments);
            return Task.FromResult(new ProcessResult(ExitCode, Output, Error));
        }
    }
}
