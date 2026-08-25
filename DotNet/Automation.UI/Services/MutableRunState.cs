using Automation.UI.Models;
using LantanaGroup.Automation;
using LantanaGroup.Link.Automation.Link.Models;

namespace Automation.UI.Services;

/// <summary>
/// In-memory state for a single in-flight automation run. Owned by
/// <see cref="AutomationRunManager"/>'s <c>_runs</c> dictionary; mutated by the
/// run pipeline (<see cref="RunExecutor"/>) and the lifecycle/cancellation
/// helpers on the manager.
///
/// Internal so the manager and executor can share the type without exposing
/// run-pipeline plumbing on the public API of <see cref="AutomationRunManager"/>.
/// All cross-thread reads/writes of the mutable fields go through
/// <see cref="Sync"/>.
/// </summary>
internal sealed class MutableRunState(
    Guid runId,
    Guid? scenarioId,
    AutomationScenarioKind scenario,
    ResolvedRunOptions options,
    string? runNameOverride,
    string? runConfigurationJson)
{
    public object Sync { get; } = new();
    public Guid RunId { get; } = runId;
    public Guid? ScenarioId { get; } = scenarioId;
    public AutomationScenarioKind Scenario { get; } = scenario;
    public ResolvedRunOptions Options { get; } = options;
    public string? RunNameOverride { get; } = runNameOverride;
    public string? RunConfigurationJson { get; } = runConfigurationJson;
    public DateTimeOffset CreatedAt { get; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? StartedAt { get; set; }
    public DateTimeOffset? FinishedAt { get; set; }
    public string? FacilityId { get; set; }
    public string? ReportId { get; set; }
    public AutomationRunStatus Status { get; set; } = AutomationRunStatus.Queued;
    public string? Error { get; set; }
    public List<string> Logs { get; } = [];
    public CancellationTokenSource RunCancellation { get; } = new();
    public bool CancelRequested { get; set; }
    public int TestRailPublished;
    public Task? ExecutionTask { get; set; }
    public FhirDataLoader? FhirDataLoader { get; set; }
    public Guid? GeneratedTemplateCacheVersionId { get; set; }
    public int? GeneratedTemplateCacheVersionNumber { get; set; }
    public string? GeneratedTemplateCacheScenarioKey { get; set; }
    public string? GeneratedTemplateSetHash { get; set; }
    public DateTimeOffset? LiveWindowStartUtc { get; set; }
    public DateTimeOffset? LiveWindowEndUtc { get; set; }
    public IReadOnlyList<string> LiveExpectedPopulation { get; set; } = [];
}
