using LantanaGroup.Link.Shared.Application.Models.Kafka;
using Link.UI.Services;
using Microsoft.AspNetCore.Mvc;

namespace Link.UI.Controllers;

/// <summary>
/// One small endpoint per Kafka overview widget, so other dashboards can embed a widget with
/// data-au-refresh-url. Each call makes the same four bounded reads the Overview makes.
/// </summary>
[Route("Operations/Kafka/widgets")]
public sealed class KafkaWidgetsController : Controller
{
    public static readonly IReadOnlyDictionary<string, string> Partials = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["pipeline"] = "Kafka/_KafkaPipeline",
        ["brokers"] = "Kafka/_KafkaBrokerHealth",
        ["leaders"] = "Kafka/_KafkaLeaderDonut",
        ["errors"] = "Kafka/_KafkaErrorRetry",
        ["throughput"] = "Kafka/_KafkaThroughput",
        ["summary"] = "Kafka/_KafkaHealthSummary"
    };

    private readonly KafkaOpsClient _kafka;

    public KafkaWidgetsController(KafkaOpsClient kafka) => _kafka = kafka;

    [HttpGet("{name}")]
    public async Task<IActionResult> Widget(string name, CancellationToken cancellationToken)
    {
        if (!Partials.TryGetValue(name, out var partial))
            return NotFound();
        var topics = await _kafka.GetTopicsAsync(cancellationToken);
        var groups = await _kafka.GetGroupsAsync(false, cancellationToken);
        var cluster = await _kafka.GetClusterAsync(cancellationToken);
        var history = await _kafka.GetHistoryAsync(KafkaDashboard.TopN, cancellationToken);
        var model = KafkaDashboardRules.Build(
            topics.Value ?? new KafkaTopicsResponse { Error = topics.Error },
            groups.Value ?? new KafkaGroupsResponse { Error = groups.Error },
            cluster.Value ?? new ClusterSnapshot { Error = cluster.Error },
            history.Value ?? new KafkaHistoryResponse { Error = history.Error });
        Response.Headers.CacheControl = "no-store";
        return PartialView(partial, model);
    }
}
