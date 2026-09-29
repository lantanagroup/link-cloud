namespace LantanaGroup.Link.Automation.Link.Helpers;

/// <summary>
/// Remembers that validation is actually working. The first snapshot is a baseline.
/// A later count change or a validation activity line keeps the queue open.
/// The hold ends when the queue drains, or when counts and activity have both
/// been quiet longer than <see cref="QuietLimit"/>. That quiet limit is long
/// enough to cover a missed Loki sample. It still stops a worker that has died.
/// A Loki line is recorded at its own timestamp, and only when that timestamp
/// is newer than the last one. Scraping the same line again does not move the
/// hold, and a line from the previous wave does not start the next one.
/// </summary>
public sealed class ValidationWorkSignal
{
    /// <summary>
    /// How long validation may sit still before the poll stops treating it as in progress.
    /// The census keep-alive tick is 5 minutes and validation samples can be a couple of
    /// minutes apart. Half an hour covers those gaps and still ends a wedged queue.
    /// </summary>
    public static readonly TimeSpan QuietLimit = TimeSpan.FromMinutes(30);

    private bool _baselineSet;
    private int _lastPending = -1;
    private int _lastResolved = -1;
    private bool _workObserved;
    private DateTime _lastWorkUtc;

    /// <summary>
    /// Newest Loki timestamp already applied. Not cleared when the queue drains,
    /// so a line still inside the lookback cannot re-arm the next wave.
    /// </summary>
    private DateTime _newestNotedUtc;

    public int PendingValidation { get; private set; }

    public bool IsOngoing => IsOngoingAt(DateTime.UtcNow);

    public bool IsOngoingAt(DateTime utcNow)
        => PendingValidation > 0
            && _workObserved
            && _lastWorkUtc != default
            && utcNow - _lastWorkUtc <= QuietLimit;

    public void NoteActivity() => NoteActivity(DateTime.UtcNow);

    /// <summary>
    /// Records one validation log. Returns false when <paramref name="utcNow"/>
    /// is missing or is not newer than the last noted log.
    /// </summary>
    public bool NoteActivity(DateTime utcNow)
    {
        if (utcNow == default || utcNow <= _newestNotedUtc)
            return false;

        _newestNotedUtc = utcNow;
        _workObserved = true;
        if (utcNow > _lastWorkUtc)
            _lastWorkUtc = utcNow;
        return true;
    }

    public void ObserveCounts(int pendingValidation, int passedValidation, int failedValidation)
        => ObserveCounts(pendingValidation, passedValidation, failedValidation, DateTime.UtcNow);

    public void ObserveCounts(int pendingValidation, int passedValidation, int failedValidation, DateTime utcNow)
    {
        if (pendingValidation == 0)
        {
            _workObserved = false;
            _lastWorkUtc = default;
            _baselineSet = false;
            _lastPending = -1;
            _lastResolved = -1;
            PendingValidation = 0;
            return;
        }

        if (!_baselineSet)
        {
            _workObserved = false;
            _lastWorkUtc = default;
        }

        var resolved = passedValidation + failedValidation;
        if (_baselineSet && (pendingValidation != _lastPending || resolved != _lastResolved))
        {
            _workObserved = true;
            _lastWorkUtc = utcNow;
        }

        _baselineSet = true;
        _lastPending = pendingValidation;
        _lastResolved = resolved;
        PendingValidation = pendingValidation;
    }

    /// <summary>
    /// A null report-entry body is cached as an empty list. That is not the queue draining.
    /// </summary>
    public static bool IsTransientEmptyEntryRead(int entryCount, int previouslySeenEntryCount)
        => entryCount == 0 && previouslySeenEntryCount > 0;
}
