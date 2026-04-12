namespace LantanaGroup.Automation.Helpers;

/// <summary>
/// Generic pipeline progress tracker with stall detection and progress bar rendering.
/// Platform-specific code supplies a delegate that computes the current progress state.
/// </summary>
public class ProgressTracker
{
    private readonly IAutomationOutput _output;
    private readonly int _totalUnits;
    private readonly Func<Task<ProgressSnapshot>> _computeProgress;
    private readonly int _barLength;

    private int _lastCompletedUnits = -1;
    private DateTime _lastProgressChange = DateTime.UtcNow;
    private string? _stalledStage;
    private string? _lastProgressBar;

    /// <summary>
    /// How long progress has been unchanged.
    /// </summary>
    public TimeSpan StallDuration => _lastCompletedUnits >= 0 && _stalledStage != null
        ? DateTime.UtcNow - _lastProgressChange
        : TimeSpan.Zero;

    /// <summary>
    /// A human-readable description of where progress appears stuck, or null.
    /// </summary>
    public string? StalledStage => _stalledStage;

    /// <summary>
    /// Creates a progress tracker.
    /// </summary>
    /// <param name="output">Output for progress bar rendering.</param>
    /// <param name="totalUnits">Total number of progress units (used for percentage calculation).</param>
    /// <param name="computeProgress">Async delegate that returns a current <see cref="ProgressSnapshot"/>.</param>
    /// <param name="barLength">Character width of the progress bar.</param>
    public ProgressTracker(
        IAutomationOutput output,
        int totalUnits,
        Func<Task<ProgressSnapshot>> computeProgress,
        int barLength = 30)
    {
        _output = output;
        _totalUnits = totalUnits;
        _computeProgress = computeProgress;
        _barLength = barLength;
    }

    /// <summary>
    /// Computes and renders the current progress.
    /// </summary>
    public async Task UpdateAsync()
    {
        try
        {
            var snapshot = await _computeProgress();
            PrintIfChanged(snapshot.CompletedUnits, snapshot.StageDetails);
        }
        catch (Exception ex)
        {
            _output.WriteLine($"[PROGRESS] Error computing progress: {ex.Message}");
        }
    }

    private void PrintIfChanged(int completedUnits, List<string> stageDetails)
    {
        if (completedUnits != _lastCompletedUnits)
        {
            _lastCompletedUnits = completedUnits;
            _lastProgressChange = DateTime.UtcNow;
            _stalledStage = null;
        }
        else if (_lastCompletedUnits >= 0)
        {
            _stalledStage = IdentifyStalledStage(stageDetails);
        }

        var percent = _totalUnits > 0 ? (int)((double)completedUnits / _totalUnits * 100) : 0;
        percent = Math.Clamp(percent, 0, 100);

        var filledLength = (int)(_barLength * percent / 100.0);
        var bar = new string('#', filledLength) + new string('-', _barLength - filledLength);
        var progressBar = $"[{bar}] {percent,3}%";

        if (progressBar == _lastProgressBar)
            return;

        var details = string.Join(" | ", stageDetails);
        _output.WriteLine($"[PROGRESS] {progressBar}  ({completedUnits}/{_totalUnits} units)  {details}");

        _lastProgressBar = progressBar;
    }

    private static string IdentifyStalledStage(List<string> stageDetails)
    {
        foreach (var detail in stageDetails)
        {
            var parts = detail.Split('=');
            if (parts.Length != 2) continue;

            var key = parts[0].Trim();
            var value = parts[1].Trim();

            if (value.Contains('/'))
            {
                var fractionParts = value.Split('/');
                if (fractionParts.Length == 2 &&
                    int.TryParse(fractionParts[0], out var current) &&
                    int.TryParse(fractionParts[1], out var expected) &&
                    current < expected)
                {
                    return key;
                }
            }
        }

        return "unknown";
    }
}

/// <summary>
/// A snapshot of progress returned by the compute delegate.
/// </summary>
public sealed class ProgressSnapshot
{
    /// <summary>
    /// Number of completed progress units.
    /// </summary>
    public int CompletedUnits { get; init; }

    /// <summary>
    /// Human-readable stage detail strings (e.g., "acq=5/10", "eval=3/10").
    /// These are used for stall detection and displayed in the progress bar.
    /// </summary>
    public List<string> StageDetails { get; init; } = [];
}
