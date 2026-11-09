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
    public bool AutomationCreatedFacility { get; set; }
    public string? ReportId { get; set; }
    public AutomationRunStatus Status { get; set; } = AutomationRunStatus.Queued;
    public string? Error { get; set; }
    public List<string> FailureDetails { get; } = [];
    public List<string> Logs { get; } = [];
    /// <summary>
    /// Appends still writing this run's log to the store. Publish waits on
    /// <see cref="LogAppendIdle"/> when this is greater than zero.
    /// </summary>
    public int PendingLogAppends;
    /// <summary>Appends that did not land in the store. A non-empty store can still be missing those lines.</summary>
    public int FailedLogAppends;
    /// <summary>True after the in-memory log has dropped older lines.</summary>
    public bool LogTrimmed;
    public TaskCompletionSource? LogAppendIdle;
    /// <summary>Tail of the per-run persist chain. Appends run one at a time so store sequence follows write order.</summary>
    public Task? LogPersistChain;
    public CancellationTokenSource RunCancellation { get; } = new();
    public bool CancelRequested { get; set; }
    public int TestRailPublished;
    public List<TestRailRunReference> TestRailRuns { get; } = [];
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
