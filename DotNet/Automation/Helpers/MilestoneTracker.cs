namespace LantanaGroup.Automation.Helpers;

/// <summary>
/// Platform-agnostic interface for tracking milestones during a test run.
/// Platform-specific code implements concrete milestones and the checking logic.
/// </summary>
public interface IMilestoneTracker
{
    /// <summary>
    /// Runs a check cycle and updates milestone state.
    /// </summary>
    Task CheckAsync(string correlationId1, string correlationId2, CancellationToken cancellationToken = default);

    /// <summary>
    /// Whether a critical failure has been detected.
    /// </summary>
    bool HasCriticalFailure { get; }

    /// <summary>
    /// Names of completed milestones.
    /// </summary>
    IReadOnlyCollection<string> CompletedMilestones { get; }

    /// <summary>
    /// Issues detected during milestone tracking.
    /// </summary>
    IReadOnlyList<string> Issues { get; }

    /// <summary>
    /// Writes a human-readable summary of milestone state to the output.
    /// </summary>
    void WriteSummary(IAutomationOutput output);
}

/// <summary>
/// Base class for milestone trackers that provides common bookkeeping.
/// </summary>
public abstract class MilestoneTrackerBase : IMilestoneTracker
{
    protected readonly IAutomationOutput Output;
    private readonly HashSet<string> _completed = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<string> _issues = [];

    public bool HasCriticalFailure { get; protected set; }
    public IReadOnlyCollection<string> CompletedMilestones => _completed;
    public IReadOnlyList<string> Issues => _issues;

    protected MilestoneTrackerBase(IAutomationOutput output)
    {
        Output = output;
    }

    public abstract Task CheckAsync(string correlationId1, string correlationId2, CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks a milestone as completed. Returns true if the milestone was newly completed.
    /// </summary>
    protected bool CompleteMilestone(string milestoneName)
    {
        if (!_completed.Add(milestoneName))
            return false;

        Output.WriteLine($"[DIAG][Milestone] Reached: {milestoneName}");
        return true;
    }

    /// <summary>
    /// Records an issue. If <paramref name="critical"/> is true, sets <see cref="HasCriticalFailure"/>.
    /// Duplicate messages are ignored.
    /// </summary>
    protected void RecordIssue(string message, bool critical = false)
    {
        if (_issues.Contains(message, StringComparer.Ordinal))
            return;

        _issues.Add(message);
        Output.WriteLine($"[DIAG][Milestone] ISSUE: {message}");

        if (!critical) return;

        HasCriticalFailure = true;
        Output.WriteLine("[DIAG][Milestone] ISSUE marked CRITICAL");
    }

    public void WriteSummary(IAutomationOutput output)
    {
        var milestoneList = _completed.Count == 0
            ? "(none)"
            : string.Join(", ", _completed);

        output.WriteLine($"[DIAG][Milestone] Completed milestones: {milestoneList}");

        if (_issues.Count == 0) return;

        output.WriteLine($"[DIAG][Milestone] Recorded {_issues.Count} issue(s):");
        foreach (var issue in _issues.Take(10))
            output.WriteLine($"[DIAG][Milestone]   - {issue}");

        if (_issues.Count > 10)
            output.WriteLine($"[DIAG][Milestone]   - Additional issues omitted: {_issues.Count - 10}");
    }
}
