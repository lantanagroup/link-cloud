using LantanaGroup.Link.Automation.Validation;

namespace LantanaGroup.Link.Automation.Helpers;

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
    public int? KafkaErrorCount { get; init; }
    public IReadOnlyCollection<MilestoneValidationOrchestrator.Milestone>? CompletedMilestones { get; init; }
    public List<MonitorIssue> Issues { get; init; } = [];
}

public sealed class TestMonitorState
{
    private readonly HashSet<MilestoneValidationOrchestrator.Milestone> _completedMilestones = [];
    private readonly List<MonitorIssue> _issues = [];

    public string FacilityId { get; private set; } = string.Empty;
    public string ReportId { get; private set; } = string.Empty;
    public int ExpectedPatientCount { get; private set; }
    public DateTime StartUtc { get; private set; }
    public int CycleCount { get; private set; }

    public bool HasCriticalFailure { get; set; }
    public TimeSpan StallDuration { get; set; }
    public string? StalledStage { get; set; }
    public int KafkaErrorCount { get; set; }

    public IReadOnlyCollection<MilestoneValidationOrchestrator.Milestone> CompletedMilestones => _completedMilestones;
    public IReadOnlyList<MonitorIssue> Issues => _issues;

    public void Start(string facilityId, string reportId, int expectedPatientCount)
    {
        FacilityId = facilityId;
        ReportId = reportId;
        ExpectedPatientCount = expectedPatientCount;
        StartUtc = DateTime.UtcNow;
        CycleCount = 0;
        HasCriticalFailure = false;
        StallDuration = TimeSpan.Zero;
        StalledStage = null;
        KafkaErrorCount = 0;
        _completedMilestones.Clear();
        _issues.Clear();
    }

    public void IncrementCycle() => CycleCount++;

    public void MergeMilestones(IReadOnlyCollection<MilestoneValidationOrchestrator.Milestone> milestones)
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
