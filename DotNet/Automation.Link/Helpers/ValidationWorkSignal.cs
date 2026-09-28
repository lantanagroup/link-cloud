namespace LantanaGroup.Link.Automation.Link.Helpers;

/// <summary>
/// Remembers that validation is actually working. Pending patients alone do not
/// count: the first snapshot is a baseline. A later count change or a validation
/// activity line means the queue is live, and it stays live until no patients
/// remain pending. There is no expiry. Cancel is how an operator stops it.
/// </summary>
public sealed class ValidationWorkSignal
{
    private bool _baselineSet;
    private int _lastPending = -1;
    private int _lastResolved = -1;
    private bool _workObserved;

    public int PendingValidation { get; private set; }

    /// <summary>
    /// True while patients are still pending validation and this run has already
    /// seen that work move or the validation service report activity.
    /// </summary>
    public bool IsOngoing => PendingValidation > 0 && _workObserved;

    public void NoteActivity() => _workObserved = true;

    public void ObserveCounts(int pendingValidation, int passedValidation, int failedValidation)
    {
        var resolved = passedValidation + failedValidation;
        if (_baselineSet && (pendingValidation != _lastPending || resolved != _lastResolved))
            _workObserved = true;

        _baselineSet = true;
        _lastPending = pendingValidation;
        _lastResolved = resolved;
        PendingValidation = pendingValidation;
    }
}
