using LantanaGroup.Link.Automation.Validation;

namespace LantanaGroup.Link.Automation.Helpers;

public sealed class LokiErrorProbe : IBackgroundMonitorProbe
{
    private readonly LokiScraper _lokiScraper;

    public LokiErrorProbe(LokiScraper lokiScraper, TimeSpan interval)
    {
        _lokiScraper = lokiScraper;
        Interval = interval;
    }

    public string Name => "LokiErrors";
    public TimeSpan Interval { get; }

    public async Task<MonitorProbeResult> ExecuteAsync(TestMonitorState state, CancellationToken cancellationToken)
    {
        await _lokiScraper.ScrapeErrorsAsync();
        return MonitorProbeResult.Empty;
    }
}

public sealed class KafkaErrorProbe : IBackgroundMonitorProbe
{
    private readonly KafkaErrorMonitor _kafkaMonitor;
    private int _lastObservedCount;

    public KafkaErrorProbe(KafkaErrorMonitor kafkaMonitor, TimeSpan interval)
    {
        _kafkaMonitor = kafkaMonitor;
        Interval = interval;
    }

    public string Name => "KafkaErrors";
    public TimeSpan Interval { get; }

    public Task<MonitorProbeResult> ExecuteAsync(TestMonitorState state, CancellationToken cancellationToken)
    {
        var count = _kafkaMonitor.CapturedErrors.Count;

        if (count <= 0)
            return Task.FromResult(new MonitorProbeResult { KafkaErrorCount = 0 });

        var issues = new List<MonitorIssue>();
        if (count > _lastObservedCount)
        {
            issues.Add(new MonitorIssue(
                Key: $"kafka-errors-{count}",
                Source: Name,
                Message: $"Kafka error/retry messages detected: {count}",
                Severity: MonitorIssueSeverity.Critical,
                TimestampUtc: DateTime.UtcNow));
        }

        _lastObservedCount = count;

        return Task.FromResult(new MonitorProbeResult
        {
            KafkaErrorCount = count,
            HasCriticalFailure = true,
            Issues = issues
        });
    }
}

public sealed class ProgressProbe : IBackgroundMonitorProbe
{
    private readonly ProgressMonitor _progressMonitor;

    public ProgressProbe(ProgressMonitor progressMonitor, TimeSpan interval)
    {
        _progressMonitor = progressMonitor;
        Interval = interval;
    }

    public string Name => "PipelineProgress";
    public TimeSpan Interval { get; }

    public async Task<MonitorProbeResult> ExecuteAsync(TestMonitorState state, CancellationToken cancellationToken)
    {
        var critical = await _progressMonitor.CheckProgressAsync(state.FacilityId, state.ReportId);

        var issues = new List<MonitorIssue>();
        if (critical)
        {
            issues.Add(new MonitorIssue(
                Key: "progress-critical",
                Source: Name,
                Message: "Critical pipeline state detected by progress monitor.",
                Severity: MonitorIssueSeverity.Critical,
                TimestampUtc: DateTime.UtcNow));
        }

        return new MonitorProbeResult
        {
            HasCriticalFailure = critical,
            StalledStage = _progressMonitor.StalledStage,
            StallDuration = _progressMonitor.StallDuration,
            Issues = issues
        };
    }
}

public sealed class MilestoneProbe : IBackgroundMonitorProbe
{
    private readonly MilestoneValidationOrchestrator _milestones;
    private readonly HashSet<string> _seenIssues = [];

    public MilestoneProbe(MilestoneValidationOrchestrator milestones, TimeSpan interval)
    {
        _milestones = milestones;
        Interval = interval;
    }

    public string Name => "Milestones";
    public TimeSpan Interval { get; }

    public async Task<MonitorProbeResult> ExecuteAsync(TestMonitorState state, CancellationToken cancellationToken)
    {
        await _milestones.CheckAsync(state.FacilityId, state.ReportId);

        var newIssues = new List<MonitorIssue>();
        foreach (var issue in _milestones.Issues)
        {
            if (!_seenIssues.Add(issue))
                continue;

            newIssues.Add(new MonitorIssue(
                Key: $"milestone-{issue}",
                Source: Name,
                Message: issue,
                Severity: _milestones.HasCriticalFailure ? MonitorIssueSeverity.Critical : MonitorIssueSeverity.Warning,
                TimestampUtc: DateTime.UtcNow));
        }

        return new MonitorProbeResult
        {
            HasCriticalFailure = _milestones.HasCriticalFailure,
            CompletedMilestones = _milestones.CompletedMilestones,
            Issues = newIssues
        };
    }
}
