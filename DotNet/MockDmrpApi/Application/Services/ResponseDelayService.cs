namespace LantanaGroup.Link.MockDmrpApi.Application.Services;

/// <inheritdoc cref="IResponseDelayService"/>
public class ResponseDelayService : IResponseDelayService
{
    private readonly TimeProvider _timeProvider;

    // Volatile rather than locked: every contract request reads this and writes are rare, so
    // a lock on the read path would be the more expensive choice. Reference assignment is
    // atomic and ResponseDelay is immutable, so a reader sees either the old delay or the new
    // one, never a half-applied pair of fields.
    private volatile ResponseDelay _current = ResponseDelay.None;

    public ResponseDelayService(TimeProvider? timeProvider = null)
    {
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public ResponseDelay Current => _current;

    public ResponseDelay Set(int milliseconds)
    {
        if (milliseconds < 0 || milliseconds > ResponseDelay.MaxMilliseconds)
        {
            throw new ArgumentOutOfRangeException(
                nameof(milliseconds), milliseconds,
                $"A delay must be between 0 and {ResponseDelay.MaxMilliseconds} milliseconds.");
        }

        _current = milliseconds == 0
            ? ResponseDelay.None
            : new ResponseDelay(milliseconds, _timeProvider.GetUtcNow());

        return _current;
    }

    public ResponseDelay Clear()
    {
        _current = ResponseDelay.None;
        return _current;
    }

    public async Task ApplyAsync(CancellationToken cancellationToken)
    {
        // Read once. Clearing the delay mid-wait does not shorten a wait already under way,
        // which is the honest behaviour -- an upstream that has already begun a slow response
        // does not speed up because someone changed a setting.
        var delay = _current;

        if (!delay.IsActive)
        {
            return;
        }

        await Task.Delay(TimeSpan.FromMilliseconds(delay.Milliseconds), _timeProvider, cancellationToken);
    }
}
