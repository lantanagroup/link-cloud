using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace LantanaGroup.Link.LinkAdmin.BFF.Application.KafkaOps;

public interface IProcessRunner
{
    Task<ProcessResult> RunAsync(string fileName, IReadOnlyList<string> arguments, string? standardInput, CancellationToken cancellationToken);
}

public sealed record ProcessResult(int ExitCode, string Output, string Error);

public interface IKubernetesResourceClient
{
    Task<string?> GetAsync(string apiVersion, string plural, string namespaceName, string name, CancellationToken cancellationToken);
    Task<IReadOnlyList<string>> ListAsync(string apiVersion, string plural, string namespaceName, CancellationToken cancellationToken);
    Task ApplyAsync(string apiVersion, string plural, string namespaceName, string name, string body, CancellationToken cancellationToken);
    Task DeleteAsync(string apiVersion, string plural, string namespaceName, string name, CancellationToken cancellationToken);
}

public interface IKafkaInfraProvider
{
    string Name { get; }
    bool Enabled { get; }
    string Detail { get; }
    Task ScaleGroupAsync(string groupId, int replicas, CancellationToken cancellationToken);
    Task AddBrokerAsync(CancellationToken cancellationToken);
    Task RemoveBrokerAsync(int brokerId, CancellationToken cancellationToken);
    Task ApplyReassignmentAsync(string reassignmentJson, string rebalanceName, bool refresh, CancellationToken cancellationToken);
    Task CancelReassignmentAsync(string rebalanceName, CancellationToken cancellationToken);
    Task ReleaseRebalanceAsync(string rebalanceName, CancellationToken cancellationToken);
    Task<ReassignmentListing> ListInFlightReassignmentsAsync(CancellationToken cancellationToken);
}

public static class KafkaRebalanceNames
{
    public static string For(Guid id) => "link-ops-" + id.ToString("N");

    public static void Require(string? name)
    {
        const string prefix = "link-ops-";
        if (string.IsNullOrWhiteSpace(name) || name.Length != prefix.Length + 32 || !name.StartsWith(prefix, StringComparison.Ordinal))
            throw new KafkaOpsRejectedException("The rebalance name is invalid.");
        foreach (var ch in name.AsSpan(prefix.Length))
        {
            if (ch is not (>= '0' and <= '9' or >= 'a' and <= 'f'))
                throw new KafkaOpsRejectedException("The rebalance name is invalid.");
        }
    }
}

public sealed class ProcessRunner : IProcessRunner
{
    public async Task<ProcessResult> RunAsync(string fileName, IReadOnlyList<string> arguments, string? standardInput, CancellationToken cancellationToken)
    {
        var start = new ProcessStartInfo
        {
            FileName = fileName,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = standardInput is not null,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        foreach (var argument in arguments)
            start.ArgumentList.Add(argument);

        using var process = Process.Start(start) ?? throw new KafkaOpsRejectedException("The infrastructure command could not be started.");
        if (standardInput is not null)
        {
            await process.StandardInput.WriteAsync(standardInput.AsMemory(), cancellationToken);
            process.StandardInput.Close();
        }

        var output = await process.StandardOutput.ReadToEndAsync(cancellationToken);
        var error = await process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);
        return new ProcessResult(process.ExitCode, output, error);
    }
}

public sealed class UnconfiguredKubernetesClient : IKubernetesResourceClient
{
    private const string NotConfigured = "Kubernetes is not configured. DevOps must enable the Strimzi provider before brokers or replicas can change.";

    public Task<string?> GetAsync(string apiVersion, string plural, string namespaceName, string name, CancellationToken cancellationToken) =>
        throw new KafkaOpsRejectedException(NotConfigured);

    public Task<IReadOnlyList<string>> ListAsync(string apiVersion, string plural, string namespaceName, CancellationToken cancellationToken) =>
        throw new KafkaOpsRejectedException(NotConfigured);

    public Task ApplyAsync(string apiVersion, string plural, string namespaceName, string name, string body, CancellationToken cancellationToken) =>
        throw new KafkaOpsRejectedException(NotConfigured);

    public Task DeleteAsync(string apiVersion, string plural, string namespaceName, string name, CancellationToken cancellationToken) =>
        throw new KafkaOpsRejectedException(NotConfigured);
}

public sealed class DisabledKafkaInfraProvider : IKafkaInfraProvider
{
    public DisabledKafkaInfraProvider(string detail)
    {
        Detail = detail;
    }

    public string Name => "Disabled";
    public bool Enabled => false;
    public string Detail { get; }

    public Task ScaleGroupAsync(string groupId, int replicas, CancellationToken cancellationToken) => Refuse();
    public Task AddBrokerAsync(CancellationToken cancellationToken) => Refuse();
    public Task RemoveBrokerAsync(int brokerId, CancellationToken cancellationToken) => Refuse();
    public Task ApplyReassignmentAsync(string reassignmentJson, string rebalanceName, bool refresh, CancellationToken cancellationToken) => Refuse();
    public Task CancelReassignmentAsync(string rebalanceName, CancellationToken cancellationToken) => Refuse();
    public Task ReleaseRebalanceAsync(string rebalanceName, CancellationToken cancellationToken) => Task.CompletedTask;

    public Task<ReassignmentListing> ListInFlightReassignmentsAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        // No query is added. A reassignment started outside this console stays invisible, so the answer is unknown.
        return Task.FromResult(new ReassignmentListing());
    }

    private Task Refuse() => throw new KafkaOpsRejectedException(Detail);
}

public sealed class LocalComposeKafkaInfraProvider : IKafkaInfraProvider
{
    private readonly IProcessRunner _process;
    private readonly KafkaOpsOptions _options;

    public LocalComposeKafkaInfraProvider(IProcessRunner process, KafkaOpsOptions options)
    {
        _process = process;
        _options = options;
    }

    public string Name => "LocalCompose";
    public bool Enabled => true;
    public string Detail => "Local compose project " + _options.ComposeProject + ".";

    public Task ScaleGroupAsync(string groupId, int replicas, CancellationToken cancellationToken)
    {
        RequireToken(groupId, "group");
        if (replicas is < 0 or > 100)
            throw new KafkaOpsRejectedException("The replica count must be from 0 to 100.");
        var service = DeploymentName(groupId) ?? _options.ConsumerService;
        RequireToken(service, "service");
        return ComposeAsync(cancellationToken, null, "up", "-d", "--scale", service + "=" + replicas, "--no-recreate", service);
    }

    public Task AddBrokerAsync(CancellationToken cancellationToken)
    {
        var profile = TokenOrDefault(_options.ExtraBrokerProfile, "extra-broker", "profile");
        var service = TokenOrDefault(_options.ExtraBrokerService, "broker-3", "service");
        return ComposeAsync(cancellationToken, null, "--profile", profile, "up", "-d", service);
    }

    public Task RemoveBrokerAsync(int brokerId, CancellationToken cancellationToken)
    {
        var prefix = TokenOrDefault(_options.BrokerServicePrefix, "broker-", "prefix");
        return ComposeAsync(cancellationToken, null, "stop", prefix + brokerId);
    }

    public Task ApplyReassignmentAsync(string reassignmentJson, string rebalanceName, bool refresh, CancellationToken cancellationToken)
    {
        KafkaRebalanceNames.Require(rebalanceName);
        var throttle = _options.ReassignmentThrottleBytesPerSecond > 0
            ? " --throttle " + _options.ReassignmentThrottleBytesPerSecond
            : "";
        var file = "/tmp/" + rebalanceName + ".json";
        var script = "cat > " + file + " && /opt/kafka/bin/kafka-reassign-partitions.sh --bootstrap-server " +
                     _options.BrokerService + ":9092 --reassignment-json-file " + file + " --execute" + throttle;
        return ComposeAsync(cancellationToken, reassignmentJson, "exec", "-T", _options.BrokerService, "bash", "-lc", script);
    }

    public Task CancelReassignmentAsync(string rebalanceName, CancellationToken cancellationToken)
    {
        KafkaRebalanceNames.Require(rebalanceName);
        var file = "/tmp/" + rebalanceName + ".json";
        var script = "/opt/kafka/bin/kafka-reassign-partitions.sh --bootstrap-server " + _options.BrokerService +
                     ":9092 --reassignment-json-file " + file + " --cancel";
        return ComposeAsync(cancellationToken, null, "exec", "-T", _options.BrokerService, "bash", "-lc", script);
    }

    public Task ReleaseRebalanceAsync(string rebalanceName, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(rebalanceName))
            return Task.CompletedTask;
        KafkaRebalanceNames.Require(rebalanceName);
        RequireToken(_options.BrokerService, "service");
        var file = "/tmp/" + rebalanceName + ".json";
        var script = "if [ -f " + file + " ]; then /opt/kafka/bin/kafka-reassign-partitions.sh --bootstrap-server " +
                     _options.BrokerService + ":9092 --reassignment-json-file " + file + " --verify; fi";
        return ComposeAsync(cancellationToken, null, "exec", "-T", _options.BrokerService, "bash", "-lc", script);
    }

    public async Task<ReassignmentListing> ListInFlightReassignmentsAsync(CancellationToken cancellationToken)
    {
        try
        {
            RequireToken(_options.BrokerService, "service");
            var script = "/opt/kafka/bin/kafka-reassign-partitions.sh --bootstrap-server " +
                         _options.BrokerService + ":9092 --list";
            var result = await RunComposeAsync(cancellationToken, null, "exec", "-T", _options.BrokerService, "bash", "-lc", script);
            return ReassignmentListText.Read(result);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return new ReassignmentListing();
        }
    }

    private async Task ComposeAsync(CancellationToken cancellationToken, string? stdin, params string[] args)
    {
        var result = await RunComposeAsync(cancellationToken, stdin, args);
        if (result.ExitCode != 0)
            throw new KafkaOpsRejectedException("The local compose command failed. " + Trim(result.Error) + " " + Trim(result.Output));
    }

    private async Task<ProcessResult> RunComposeAsync(CancellationToken cancellationToken, string? stdin, params string[] args)
    {
        RequireToken(_options.ComposeProject, "project");
        var arguments = new List<string> { "compose", "-p", _options.ComposeProject };
        foreach (var path in ComposeFiles())
            arguments.AddRange(["-f", path]);
        arguments.AddRange(args);
        return await _process.RunAsync("docker", arguments, stdin, cancellationToken);
    }

    private string? DeploymentName(string groupId)
    {
        foreach (var pair in (_options.ConsumerDeployments ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var parts = pair.Split('=', 2, StringSplitOptions.TrimEntries);
            if (parts.Length == 2 && string.Equals(parts[0], groupId, StringComparison.Ordinal))
                return parts[1];
        }

        return null;
    }

    private IEnumerable<string> ComposeFiles()
    {
        if (string.IsNullOrWhiteSpace(_options.ComposeFile))
            yield break;

        foreach (var segment in _options.ComposeFile.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            RequirePath(segment);
            yield return segment;
        }
    }

    private static string TokenOrDefault(string? value, string fallback, string name)
    {
        var chosen = string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
        RequireToken(chosen, name);
        return chosen;
    }

    private static void RequireToken(string? value, string name)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > 80 || !value.All(ch => char.IsLetterOrDigit(ch) || ch is '-' or '_' or '.'))
            throw new KafkaOpsRejectedException("The " + name + " name is invalid.");
    }

    private static void RequirePath(string value)
    {
        if (value.Length == 0 || value.Length > 260 || !value.All(ch => char.IsLetterOrDigit(ch) || ch is '-' or '_' or '.' or ':' or '\\' or '/'))
            throw new KafkaOpsRejectedException("The compose file path is invalid.");
    }

    private static string Trim(string value) =>
        value.Length <= 400 ? value.Trim() : value.Trim()[..400];
}

public sealed class StrimziKafkaInfraProvider : IKafkaInfraProvider
{
    private readonly IKubernetesResourceClient _kubernetes;
    private readonly KafkaOpsOptions _options;

    public StrimziKafkaInfraProvider(IKubernetesResourceClient kubernetes, KafkaOpsOptions options)
    {
        _kubernetes = kubernetes;
        _options = options;
    }

    public string Name => "Strimzi";
    public bool Enabled => true;
    public string Detail => "Strimzi resources in namespace " + _options.KubernetesNamespace + ".";

    public async Task ScaleGroupAsync(string groupId, int replicas, CancellationToken cancellationToken)
    {
        var deployment = DeploymentName(groupId);
        if (deployment is null)
            throw new KafkaOpsRejectedException("No Deployment is mapped for group " + groupId + ". Set KafkaOps:ConsumerDeployments.");
        await _kubernetes.ApplyAsync(
            "apps/v1",
            "deployments",
            Namespace(),
            deployment,
            JsonSerializer.Serialize(new { spec = new { replicas } }),
            cancellationToken);
    }

    public async Task AddBrokerAsync(CancellationToken cancellationToken)
    {
        var pool = Pool();
        var current = await ReadReplicasAsync(pool, cancellationToken);
        await _kubernetes.ApplyAsync(
            "kafka.strimzi.io/v1beta2",
            "kafkanodepools",
            Namespace(),
            pool,
            JsonSerializer.Serialize(new { spec = new { replicas = current + 1 } }),
            cancellationToken);
    }

    public async Task RemoveBrokerAsync(int brokerId, CancellationToken cancellationToken)
    {
        var pool = Pool();
        var current = await ReadReplicasAsync(pool, cancellationToken);
        if (current <= 1)
            throw new KafkaOpsRejectedException("The node pool cannot be scaled below one broker.");
        var body = JsonSerializer.Serialize(new
        {
            metadata = new
            {
                annotations = new Dictionary<string, string>
                {
                    ["strimzi.io/remove-node-ids"] = "[" + brokerId + "]"
                }
            },
            spec = new { replicas = current - 1 }
        });
        await _kubernetes.ApplyAsync(
            "kafka.strimzi.io/v1beta2",
            "kafkanodepools",
            Namespace(),
            pool,
            body,
            cancellationToken);
    }

    public async Task ApplyReassignmentAsync(string reassignmentJson, string rebalanceName, bool refresh, CancellationToken cancellationToken)
    {
        KafkaRebalanceNames.Require(rebalanceName);
        var existing = await _kubernetes.GetAsync("kafka.strimzi.io/v1beta2", "kafkarebalances", Namespace(), rebalanceName, cancellationToken);
        if (existing is not null && !refresh)
            throw new KafkaOpsRejectedException("KafkaRebalance " + rebalanceName + " already exists.");

        var leaving = LeavingBroker(reassignmentJson);
        var mode = leaving is null ? "add-brokers" : "remove-brokers";
        var brokers = leaving is null ? ArrivingBrokers(reassignmentJson) : new[] { leaving.Value };
        var annotation = refresh ? "refresh" : "approve";
        var body = JsonSerializer.Serialize(new
        {
            apiVersion = "kafka.strimzi.io/v1beta2",
            kind = "KafkaRebalance",
            metadata = new
            {
                name = rebalanceName,
                annotations = new Dictionary<string, string> { ["strimzi.io/rebalance"] = annotation }
            },
            spec = new { mode, brokers }
        });
        await _kubernetes.ApplyAsync("kafka.strimzi.io/v1beta2", "kafkarebalances", Namespace(), rebalanceName, body, cancellationToken);
    }

    public Task CancelReassignmentAsync(string rebalanceName, CancellationToken cancellationToken)
    {
        KafkaRebalanceNames.Require(rebalanceName);
        return _kubernetes.DeleteAsync("kafka.strimzi.io/v1beta2", "kafkarebalances", Namespace(), rebalanceName, cancellationToken);
    }

    public async Task ReleaseRebalanceAsync(string rebalanceName, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(rebalanceName))
            return;
        KafkaRebalanceNames.Require(rebalanceName);
        var existing = await _kubernetes.GetAsync("kafka.strimzi.io/v1beta2", "kafkarebalances", Namespace(), rebalanceName, cancellationToken);
        if (existing is null)
            return;
        await _kubernetes.DeleteAsync("kafka.strimzi.io/v1beta2", "kafkarebalances", Namespace(), rebalanceName, cancellationToken);
    }

    public async Task<ReassignmentListing> ListInFlightReassignmentsAsync(CancellationToken cancellationToken)
    {
        try
        {
            var bodies = await _kubernetes.ListAsync("kafka.strimzi.io/v1beta2", "kafkarebalances", Namespace(), cancellationToken);
            var names = new List<string>();
            foreach (var body in bodies)
            {
                if (!KafkaRebalanceInFlight(body, out var name))
                    continue;
                if (!names.Contains(name, StringComparer.Ordinal))
                    names.Add(name);
            }

            return new ReassignmentListing { Known = true, Topics = names };
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return new ReassignmentListing();
        }
    }

    private static bool KafkaRebalanceInFlight(string body, out string name)
    {
        name = "";
        using var document = JsonDocument.Parse(string.IsNullOrWhiteSpace(body) ? throw new JsonException("KafkaRebalance body is empty.") : body);
        var root = document.RootElement;
        if (root.TryGetProperty("metadata", out var metadata) && metadata.TryGetProperty("name", out var nameElement))
            name = nameElement.GetString() ?? "";
        if (string.IsNullOrWhiteSpace(name))
            throw new JsonException("KafkaRebalance has no name.");

        var active = ConditionTrue(root, "Rebalancing") || ConditionTrue(root, "PendingProposal") || ConditionTrue(root, "ProposalReady");
        if (!root.TryGetProperty("status", out _) || active)
            return true;
        var finished = ConditionTrue(root, "Ready") || ConditionTrue(root, "Stopped");
        return !finished;
    }

    private static bool ConditionTrue(JsonElement root, string type)
    {
        if (!root.TryGetProperty("status", out var status) || status.ValueKind != JsonValueKind.Object)
            return false;
        if (!status.TryGetProperty("conditions", out var conditions) || conditions.ValueKind != JsonValueKind.Array)
            return false;
        foreach (var condition in conditions.EnumerateArray())
        {
            if (!condition.TryGetProperty("type", out var typeElement))
                continue;
            if (!string.Equals(typeElement.GetString(), type, StringComparison.OrdinalIgnoreCase))
                continue;
            if (condition.TryGetProperty("status", out var statusElement)
                && string.Equals(statusElement.GetString(), "True", StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private async Task<int> ReadReplicasAsync(string pool, CancellationToken cancellationToken)
    {
        var json = await _kubernetes.GetAsync("kafka.strimzi.io/v1beta2", "kafkanodepools", Namespace(), pool, cancellationToken);
        if (string.IsNullOrWhiteSpace(json))
            throw new KafkaOpsRejectedException("KafkaNodePool " + pool + " was not found.");
        using var document = JsonDocument.Parse(json);
        if (!document.RootElement.TryGetProperty("spec", out var spec) || !spec.TryGetProperty("replicas", out var replicas))
            throw new KafkaOpsRejectedException("KafkaNodePool " + pool + " has no spec.replicas.");
        return replicas.GetInt32();
    }

    private string Namespace()
    {
        var value = string.IsNullOrWhiteSpace(_options.KubernetesNamespace) ? "kafka" : _options.KubernetesNamespace.Trim();
        if (!value.All(ch => char.IsLetterOrDigit(ch) || ch == '-'))
            throw new KafkaOpsRejectedException("The Kubernetes namespace is invalid.");
        return value;
    }

    private string Pool()
    {
        if (string.IsNullOrWhiteSpace(_options.KafkaNodePool))
            throw new KafkaOpsRejectedException("KafkaOps:KafkaNodePool is required for the Strimzi provider.");
        return _options.KafkaNodePool.Trim();
    }

    private string? DeploymentName(string groupId)
    {
        foreach (var pair in (_options.ConsumerDeployments ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var parts = pair.Split('=', 2, StringSplitOptions.TrimEntries);
            if (parts.Length == 2 && string.Equals(parts[0], groupId, StringComparison.Ordinal))
                return parts[1];
        }

        return null;
    }

    private static int? LeavingBroker(string json)
    {
        using var document = JsonDocument.Parse(string.IsNullOrWhiteSpace(json) ? "{}" : json);
        if (!document.RootElement.TryGetProperty("leavingBroker", out var leaving))
            return null;
        return leaving.GetInt32();
    }

    private static int[] ArrivingBrokers(string json)
    {
        using var document = JsonDocument.Parse(string.IsNullOrWhiteSpace(json) ? "{}" : json);
        if (!document.RootElement.TryGetProperty("arrivingBroker", out var arriving))
            return [];
        return [arriving.GetInt32()];
    }
}

public static class ReassignmentListText
{
    public static ReassignmentListing Read(ProcessResult result)
    {
        if (result.ExitCode != 0)
            return new ReassignmentListing();

        var text = ((result.Output ?? "") + "\n" + (result.Error ?? "")).Trim();
        if (text.Length == 0)
            return new ReassignmentListing();
        if (text.Contains("no partition reassignment", StringComparison.OrdinalIgnoreCase))
            return new ReassignmentListing { Known = true };

        var topics = new List<string>();
        var partitions = new List<string>();
        var sawPartitions = false;
        var start = text.IndexOf('{');
        var end = text.LastIndexOf('}');
        if (start >= 0 && end > start)
        {
            try
            {
                using var document = JsonDocument.Parse(text.Substring(start, end - start + 1));
                if (document.RootElement.TryGetProperty("partitions", out var entries) && entries.ValueKind == JsonValueKind.Array)
                {
                    sawPartitions = true;
                    foreach (var entry in entries.EnumerateArray())
                    {
                        if (entry.TryGetProperty("topic", out var topic) && topic.ValueKind == JsonValueKind.String)
                        {
                            var name = topic.GetString();
                            Add(topics, name);
                            if (entry.TryGetProperty("partition", out var number) && number.TryGetInt32(out var id))
                                AddKey(partitions, name, id);
                        }
                    }
                }
            }
            catch (JsonException)
            {
                sawPartitions = false;
            }
        }

        foreach (Match match in Regex.Matches(text, @"(?m)^(.+)-(\d+)\s*:\s*replicas\s*:", RegexOptions.CultureInvariant))
        {
            var name = match.Groups[1].Value.Trim();
            Add(topics, name);
            if (int.TryParse(match.Groups[2].Value, out var id))
                AddKey(partitions, name, id);
        }

        if (topics.Count > 0 || partitions.Count > 0)
            return new ReassignmentListing { Known = true, Topics = topics, Partitions = partitions };
        if (sawPartitions)
            return new ReassignmentListing { Known = true };
        return new ReassignmentListing();
    }

    private static void Add(List<string> topics, string? topic)
    {
        if (string.IsNullOrWhiteSpace(topic) || topics.Contains(topic, StringComparer.Ordinal))
            return;
        topics.Add(topic);
    }

    private static void AddKey(List<string> partitions, string? topic, int partition)
    {
        if (string.IsNullOrWhiteSpace(topic))
            return;
        var key = topic + "\n" + partition;
        if (!partitions.Contains(key, StringComparer.Ordinal))
            partitions.Add(key);
    }
}

public static class KafkaInfraProviderFactory
{
    public static IKafkaInfraProvider Create(KafkaOpsOptions options, bool production, IProcessRunner process, IKubernetesResourceClient kubernetes)
    {
        var name = (options.InfraProvider ?? "Disabled").Trim();
        if (name.Equals("LocalCompose", StringComparison.OrdinalIgnoreCase))
        {
            if (production)
                return new DisabledKafkaInfraProvider("Local compose is refused on a Production host.");
            if (string.IsNullOrWhiteSpace(options.ComposeProject))
                return new DisabledKafkaInfraProvider("KafkaOps:ComposeProject is required before local compose actions can run.");
            return new LocalComposeKafkaInfraProvider(process, options);
        }

        if (name.Equals("Strimzi", StringComparison.OrdinalIgnoreCase))
            return new StrimziKafkaInfraProvider(kubernetes, options);

        return new DisabledKafkaInfraProvider("Broker and replica changes are disabled. DevOps must set KafkaOps:InfraProvider before those actions can run.");
    }
}

public static class ReassignmentJson
{
    public static string ForMoves(IEnumerable<ReplicaMove> moves, int? leavingBroker, int? arrivingBroker)
    {
        var partitions = moves.Select(move => new
        {
            topic = move.Topic,
            partition = move.Partition,
            replicas = move.Replicas
        });
        var payload = new Dictionary<string, object?>
        {
            ["version"] = 1,
            ["partitions"] = partitions
        };
        if (leavingBroker is not null)
            payload["leavingBroker"] = leavingBroker;
        if (arrivingBroker is not null)
            payload["arrivingBroker"] = arrivingBroker;
        return JsonSerializer.Serialize(payload);
    }
}
