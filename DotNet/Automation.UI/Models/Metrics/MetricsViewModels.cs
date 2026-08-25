using LantanaGroup.Link.Shared.Application.Models.Responses;

namespace Automation.UI.Models.Metrics;

public class MetricsRunListItem
{
    public Guid RunId { get; set; }
    public Guid? ScenarioId { get; set; }
    public string ScenarioName { get; set; } = "";
    public string Outcome { get; set; } = "";
    public double E2eDurationSeconds { get; set; }
    public bool BenchmarkPass { get; set; } = true;
    public bool StagesUnavailable { get; set; }
    public DateTimeOffset? FinishedAt { get; set; }
}

public sealed class MetricsRunDetailViewModel : MetricsRunListItem
{
    public string? BenchmarkKey { get; set; }
    public int PatientCount { get; set; }
    public double? PatientsPerMinute { get; set; }
    public double? ResourcesPerSecond { get; set; }
    public string? ThetisGitSha { get; set; }
    public int Seed { get; set; }
    public Dictionary<string, StageSnapshot> Stages { get; set; } = new(StringComparer.Ordinal);
    public IReadOnlyList<string> BenchmarkViolations { get; set; } = [];
    public IReadOnlyList<string> RegressionFlags { get; set; } = [];
    public Guid? PreviousRunId { get; set; }
    public IReadOnlyList<ValidatorOutcomeSnapshotView> Validators { get; set; } = [];
}

public sealed class StageSnapshot
{
    public bool Unavailable { get; set; }
    public long Count { get; set; }
    public double? P50Ms { get; set; }
    public double? P95Ms { get; set; }
    public double? P99Ms { get; set; }
    public long ErrorCount { get; set; }
}

public sealed class ValidatorOutcomeSnapshotView
{
    public string Name { get; set; } = string.Empty;
    public string Outcome { get; set; } = string.Empty;
    public int IssueCount { get; set; }
}

public sealed class MetricsDurationPoint
{
    public Guid RunId { get; set; }
    public DateTimeOffset FinishedAt { get; set; }
    public double E2eDurationSeconds { get; set; }
}

public sealed class MetricsDashboardViewModel
{
    public double LastRunE2eSeconds { get; set; }
    public double? LastRunPatientsPerMinute { get; set; }
    public bool LastRunStagesUnavailable { get; set; }
    public int RegressionFlagCount { get; set; }
    public IReadOnlyList<MetricsRunListItem> Runs { get; set; } = [];
    public PaginationMetadata Metadata { get; set; } = new();
    public IReadOnlyList<TestScenarioDefinition> MetricsScenarios { get; set; } = [];
    public IReadOnlyList<MetricsDurationPoint> DurationTrend { get; set; } = [];
}
