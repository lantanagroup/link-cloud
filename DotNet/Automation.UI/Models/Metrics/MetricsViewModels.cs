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
    public int ScenarioVersion { get; set; } = 1;
    public string? SetupSummary { get; set; }
    public double? PatientsPerMinute { get; set; }
}

public sealed class MetricsRunDetailViewModel : MetricsRunListItem
{
    /// <summary>
    /// False when the operational run (logs, diagnostics) was deleted.
    /// The metrics snapshot can still be viewed.
    /// </summary>
    public bool RunAvailable { get; set; }

    public string? BenchmarkKey { get; set; }
    public int PatientCount { get; set; }
    public double? ResourcesPerSecond { get; set; }
    public string? ThetisGitSha { get; set; }
    public int Seed { get; set; }
    public long GenerationDurationMs { get; set; }
    public string? ScenarioFingerprint { get; set; }
    public Dictionary<string, StageSnapshot> Stages { get; set; } = new(StringComparer.Ordinal);
    public IReadOnlyList<ProcessUtilizationItemView> ProcessUtilization { get; set; } = [];
    public IReadOnlyList<ApiLatencyItemView> ApiLatency { get; set; } = [];
    public IReadOnlyList<ApiRouteLatencyItemView> SlowestApiRoutes { get; set; } = [];
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

public sealed class ProcessUtilizationItemView
{
    public string Key { get; set; } = "";
    public string Name { get; set; } = "";
    public string Hint { get; set; } = "";
    public bool Unavailable { get; set; }
    public double? AvgCpuCores { get; set; }
    public double? PeakCpuCores { get; set; }
    public double? AvgCpuPercent { get; set; }
    public double? PeakCpuPercent { get; set; }
    public double? AvgMemoryBytes { get; set; }
    public double? PeakMemoryBytes { get; set; }
}

public sealed class ApiLatencyItemView
{
    public string Key { get; set; } = "";
    public string Name { get; set; } = "";
    public bool Unavailable { get; set; }
    public long Count { get; set; }
    public double? P50Ms { get; set; }
    public double? P95Ms { get; set; }
    public double? P99Ms { get; set; }
    public long ErrorCount { get; set; }
}

public sealed class ApiRouteLatencyItemView
{
    public string Service { get; set; } = "";
    public string Method { get; set; } = "";
    public string Route { get; set; } = "";
    public double P95Ms { get; set; }
    public double Count { get; set; }
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
    public double? PatientsPerMinute { get; set; }
    public int ScenarioVersion { get; set; } = 1;
}

public sealed class MetricsServiceHealthItem
{
    public string Key { get; set; } = "";
    public string Name { get; set; } = "";
    public string Hint { get; set; } = "";
    public double? SlowMs { get; set; }
    public bool Unavailable { get; set; }
}

public sealed class MetricsScenarioCardViewModel
{
    public Guid ScenarioId { get; set; }
    public string Name { get; set; } = "";
    public string? SetupSummary { get; set; }
    public int RunCount { get; set; }
    public int ScenarioVersion { get; set; } = 1;
    public bool VersionChanged { get; set; }
    public string Outcome { get; set; } = "";
    public double LastE2eSeconds { get; set; }
    public double? LastPatientsPerMinute { get; set; }
    public bool LastStagesUnavailable { get; set; }
    public bool GotSlower { get; set; }
    public DateTimeOffset? LastFinishedAt { get; set; }
    public Guid? LastRunId { get; set; }
    public IReadOnlyList<double> Sparkline { get; set; } = [];
}

public sealed class MetricsDashboardViewModel
{
    public double LastRunE2eSeconds { get; set; }
    public double? LastRunPatientsPerMinute { get; set; }
    public bool LastRunStagesUnavailable { get; set; }
    public int RegressionFlagCount { get; set; }
    public double? FleetPatientsPerMinute { get; set; }
    public int ScenarioCount { get; set; }
    public int RecentRunCount { get; set; }
    public IReadOnlyList<MetricsServiceHealthItem> Services { get; set; } = [];
    public IReadOnlyList<MetricsScenarioCardViewModel> ScenarioCards { get; set; } = [];
    public IReadOnlyList<MetricsRunListItem> Runs { get; set; } = [];
    public PaginationMetadata Metadata { get; set; } = new();
    public IReadOnlyList<TestScenarioDefinition> MetricsScenarios { get; set; } = [];
    public IReadOnlyList<MetricsDurationPoint> DurationTrend { get; set; } = [];
}

public sealed class MetricsScenarioHistoryViewModel
{
    public Guid ScenarioId { get; set; }
    public string Name { get; set; } = "";
    public string? SetupSummary { get; set; }
    public int CurrentVersion { get; set; } = 1;
    public bool HasVersionChange { get; set; }
    public IReadOnlyList<MetricsDurationPoint> DurationTrend { get; set; } = [];
    public IReadOnlyList<MetricsRunListItem> Runs { get; set; } = [];
}

public sealed class MetricsCompareViewModel
{
    public MetricsRunDetailViewModel Left { get; set; } = new();
    public MetricsRunDetailViewModel Right { get; set; } = new();
}
