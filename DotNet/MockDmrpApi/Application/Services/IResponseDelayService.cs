namespace LantanaGroup.Link.MockDmrpApi.Application.Services;

/// <summary>
/// An artificial delay applied to the contract endpoints, for exercising a caller's timeout
/// and retry behaviour.
/// </summary>
/// <param name="Milliseconds">How long each contract request is held. Zero means no delay.</param>
/// <param name="ConfiguredOn">When the delay was set, or null when none is configured.</param>
public sealed record ResponseDelay(int Milliseconds, DateTimeOffset? ConfiguredOn)
{
    /// <summary>
    /// The ceiling on a configurable delay.
    /// </summary>
    /// <remarks>
    /// Five minutes is comfortably longer than any client timeout worth testing, and short
    /// enough that a mistyped value cannot make the contract surface unusable until someone
    /// restarts the service. There is no way to wait out a delay that outlives the test that
    /// set it.
    /// </remarks>
    public const int MaxMilliseconds = 300_000;

    public static readonly ResponseDelay None = new(0, null);

    public bool IsActive => Milliseconds > 0;
}

/// <summary>
/// Holds the currently configured artificial delay.
/// </summary>
/// <remarks>
/// Deliberately in memory and never persisted. The delay describes what a test is doing right
/// now, not how the service is configured, so surviving a restart would be a bug rather than
/// a feature -- a forgotten delay would outlive the run that set it and confuse the next one.
/// A restart always returns the service to answering immediately.
/// <para>
/// Registered as a singleton, read on every contract request and written rarely.
/// </para>
/// </remarks>
public interface IResponseDelayService
{
    /// <summary>The delay in force. Never null; <see cref="ResponseDelay.None"/> when unset.</summary>
    ResponseDelay Current { get; }

    /// <summary>
    /// Sets the delay. A value of zero clears it, so a caller does not have to choose between
    /// this and <see cref="Clear"/> to turn one off.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">
    /// The value is negative or above <see cref="ResponseDelay.MaxMilliseconds"/>.
    /// </exception>
    ResponseDelay Set(int milliseconds);

    /// <summary>Removes any delay. Safe to call when none is configured.</summary>
    ResponseDelay Clear();

    /// <summary>
    /// Waits out the configured delay, or returns immediately when there is none.
    /// </summary>
    /// <remarks>
    /// The token is honoured so a caller that times out and disconnects releases its request
    /// rather than holding it for the full delay. Without that, a long delay plus concurrent
    /// callers would tie up requests that nobody is waiting for any more.
    /// </remarks>
    Task ApplyAsync(CancellationToken cancellationToken);
}
