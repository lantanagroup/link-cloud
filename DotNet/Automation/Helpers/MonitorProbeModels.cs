namespace LantanaGroup.Automation.Helpers;

public enum MonitorIssueSeverity
{
    Info,
    Warning,
    Critical
}

public sealed record MonitorIssue(
    string Key,
    string Source,
    string Message,
    MonitorIssueSeverity Severity,
    DateTime TimestampUtc);

public sealed class MonitorProbeResult
{
    public static readonly MonitorProbeResult Empty = new();

    public bool? HasCriticalFailure { get; init; }
    public TimeSpan? StallDuration { get; init; }
    public string? StalledStage { get; init; }
    public int? MessageBusErrorCount { get; init; }
    public IReadOnlyCollection<string>? CompletedMilestones { get; init; }
    public DateTime? LastProgressUtc { get; init; }
    public int? AcquisitionResourcesAcquired { get; init; }
    public bool? AcquisitionInFlight { get; init; }
    public List<MonitorIssue> Issues { get; init; } = [];
}

public sealed class TestMonitorState
{
    private readonly HashSet<string> _completedMilestones = [];
    private readonly List<MonitorIssue> _issues = [];

    public string CorrelationId1 { get; private set; } = string.Empty;
    public string CorrelationId2 { get; private set; } = string.Empty;
    public int ExpectedItemCount { get; private set; }
    public DateTime StartUtc { get; private set; }
    public int CycleCount { get; private set; }

    public bool HasCriticalFailure { get; set; }
    public TimeSpan StallDuration { get; set; }
    public string? StalledStage { get; set; }
    public int MessageBusErrorCount { get; set; }
    public DateTime LastProgressUtc { get; set; }
    public int AcquisitionResourcesAcquired { get; set; }
    public bool AcquisitionInFlight { get; set; }

    public IReadOnlyCollection<string> CompletedMilestones => _completedMilestones;
    public IReadOnlyList<MonitorIssue> Issues => _issues;

    public void Start(string correlationId1, string correlationId2, int expectedItemCount)
    {
        CorrelationId1 = correlationId1;
        CorrelationId2 = correlationId2;
        ExpectedItemCount = expectedItemCount;
        StartUtc = DateTime.UtcNow;
        CycleCount = 0;
        HasCriticalFailure = false;
        StallDuration = TimeSpan.Zero;
        StalledStage = null;
        MessageBusErrorCount = 0;
        LastProgressUtc = default;
        AcquisitionResourcesAcquired = 0;
        AcquisitionInFlight = false;
        _completedMilestones.Clear();
        _issues.Clear();
    }

    public void IncrementCycle() => CycleCount++;

    public void MergeMilestones(IReadOnlyCollection<string> milestones)
    {
        foreach (var milestone in milestones)
            _completedMilestones.Add(milestone);
    }

    public void AddIssue(MonitorIssue issue) => _issues.Add(issue);
}

public interface IBackgroundMonitorProbe
{
    string Name { get; }
    TimeSpan Interval { get; }
    Task<MonitorProbeResult> ExecuteAsync(TestMonitorState state, CancellationToken cancellationToken);
}
