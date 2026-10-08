using System.Text.Json;

namespace LantanaGroup.Link.LinkAdmin.BFF.Application.KafkaOps;

public interface IKafkaWorkloadControl
{
    bool ManualChecklist { get; }
    bool TryMap(string workload, out string target);
    Task<int?> ReadReplicasAsync(string workload, CancellationToken cancellationToken);
    Task ScaleAsync(string workload, int replicas, CancellationToken cancellationToken);
    Task<bool> TopicOperatorOwnsAsync(string topic, CancellationToken cancellationToken);
}

/// <summary>
/// Stops and starts producer and consumer workloads. Disabled refuses.
/// Strimzi manual checklist records the request and waits for the operator.
/// </summary>
public sealed class KafkaWorkloadControl : IKafkaWorkloadControl
{
    private readonly KafkaOpsOptions _options;
    private readonly IProcessRunner _process;
    private readonly IKubernetesResourceClient _kubernetes;
    private readonly Dictionary<string, string> _workloads;

    public KafkaWorkloadControl(KafkaOpsOptions options, IProcessRunner process, IKubernetesResourceClient kubernetes)
    {
        _options = options;
        _process = process;
        _kubernetes = kubernetes;
        _workloads = Parse(options.Workloads);
    }

    public bool ManualChecklist =>
        _options.ManualChecklist && string.Equals(_options.InfraProvider, "Strimzi", StringComparison.OrdinalIgnoreCase);

    public bool TryMap(string workload, out string target) => _workloads.TryGetValue(workload, out target!);

    public async Task<int?> ReadReplicasAsync(string workload, CancellationToken cancellationToken)
    {
        if (!TryMap(workload, out var target))
            return null;
        if (IsLocal())
            return await ComposeCountAsync(target, cancellationToken);
        if (IsStrimzi() && !ManualChecklist)
            return await DeploymentReplicasAsync(target, cancellationToken);
        return null;
    }

    public async Task ScaleAsync(string workload, int replicas, CancellationToken cancellationToken)
    {
        if (replicas is < 0 or > 100)
            throw new KafkaOpsRejectedException("The replica count must be from 0 to 100.");
        if (string.Equals(_options.InfraProvider, "Disabled", StringComparison.OrdinalIgnoreCase) || !_options.InfraProvider.Equals("LocalCompose", StringComparison.OrdinalIgnoreCase) && !_options.InfraProvider.Equals("Strimzi", StringComparison.OrdinalIgnoreCase))
            throw new KafkaOpsRejectedException("The infrastructure provider cannot stop or start workloads. Disabled refuses because the replica state is unknown.");
        if (ManualChecklist)
            return;
        if (!TryMap(workload, out var target))
            throw new KafkaOpsRejectedException("No workload target is configured for " + workload + ". Set KafkaOps:Workloads.");
        if (IsLocal())
        {
            var result = await ComposeAsync(cancellationToken, "up", "-d", "--scale", target + "=" + replicas, "--no-recreate", target);
            if (result.ExitCode != 0)
                throw new KafkaOpsRejectedException("The local compose scale failed. " + Trim(result.Error));
            return;
        }

        await _kubernetes.ApplyAsync(
            "apps/v1",
            "deployments",
            Namespace(),
            target,
            JsonSerializer.Serialize(new { spec = new { replicas } }),
            cancellationToken);
    }

    public async Task<bool> TopicOperatorOwnsAsync(string topic, CancellationToken cancellationToken)
    {
        if (_options.TopicOperatorManagesLinkTopics)
            return true;
        if (!IsStrimzi())
            return false;
        var names = await _kubernetes.ListAsync("kafka.strimzi.io/v1beta2", "kafkatopics", Namespace(), cancellationToken);
        return names.Any(name => string.Equals(name, topic, StringComparison.Ordinal));
    }

    private bool IsLocal() => string.Equals(_options.InfraProvider, "LocalCompose", StringComparison.OrdinalIgnoreCase);
    private bool IsStrimzi() => string.Equals(_options.InfraProvider, "Strimzi", StringComparison.OrdinalIgnoreCase);

    private async Task<int?> ComposeCountAsync(string service, CancellationToken cancellationToken)
    {
        var result = await ComposeAsync(cancellationToken, "ps", "-q", "--status", "running", service);
        if (result.ExitCode != 0)
            return null;
        return result.Output.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Length;
    }

    private async Task<int?> DeploymentReplicasAsync(string name, CancellationToken cancellationToken)
    {
        var body = await _kubernetes.GetAsync("apps/v1", "deployments", Namespace(), name, cancellationToken);
        if (string.IsNullOrWhiteSpace(body))
            return null;
        using var doc = JsonDocument.Parse(body);
        if (doc.RootElement.TryGetProperty("spec", out var spec) && spec.TryGetProperty("replicas", out var replicas))
            return replicas.GetInt32();
        return null;
    }

    private async Task<ProcessResult> ComposeAsync(CancellationToken cancellationToken, params string[] args)
    {
        RequireToken(_options.ComposeProject, "project");
        var arguments = new List<string> { "compose", "-p", _options.ComposeProject };
        if (!string.IsNullOrWhiteSpace(_options.ComposeFile))
        {
            foreach (var segment in _options.ComposeFile.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                RequirePath(segment);
                arguments.Add("-f");
                arguments.Add(segment);
            }
        }

        arguments.AddRange(args);
        return await _process.RunAsync("docker", arguments, null, cancellationToken);
    }

    private string Namespace()
    {
        var name = string.IsNullOrWhiteSpace(_options.WorkloadNamespace) ? _options.KubernetesNamespace : _options.WorkloadNamespace;
        RequireToken(name, "namespace");
        return name;
    }

    private static Dictionary<string, string> Parse(string? text)
    {
        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var pair in (text ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var parts = pair.Split('=', 2, StringSplitOptions.TrimEntries);
            if (parts.Length == 2 && parts[0].Length > 0 && parts[1].Length > 0)
                map[parts[0]] = parts[1];
        }

        return map;
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

    private static string Trim(string value) => value.Length <= 400 ? value.Trim() : value.Trim()[..400];
}
