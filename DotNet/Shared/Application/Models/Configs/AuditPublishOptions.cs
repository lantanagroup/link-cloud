namespace LantanaGroup.Link.Shared.Application.Models.Configs
{
    /// <summary>
    /// How hard the direct publisher tries before an audit event is written off.
    /// </summary>
    /// <remarks>
    /// The publish happens while the response is already decided but not yet returned, so the
    /// caller waits at most <see cref="ResponseTimeout"/> for it. A broker that is down will not
    /// come back inside a request, and holding the caller longer only turns an audit problem into a
    /// timed-out write the caller may repeat. The outbox feature removes the trade-off entirely.
    /// </remarks>
    public sealed class AuditPublishOptions
    {
        /// <summary>
        /// The longest the request waits for the broker to confirm the event, across every attempt.
        /// Past this the request returns and delivery carries on in the background: the producer
        /// keeps the message queued, so a broker that recovers still receives it, and one that does
        /// not is counted and logged when the producer finally gives up.
        /// </summary>
        public TimeSpan ResponseTimeout { get; set; } = TimeSpan.FromSeconds(3);

        /// <summary>
        /// Total attempts, including the first, for a produce that fails outright within the
        /// response timeout. One means no retry. A produce that is merely slow is never retried,
        /// because the first copy is still queued and a second would duplicate the event.
        /// </summary>
        public int MaxAttempts { get; set; } = 3;

        /// <summary>
        /// How long to wait after the first failed attempt.
        /// </summary>
        public TimeSpan InitialBackoff { get; set; } = TimeSpan.FromMilliseconds(200);

        /// <summary>
        /// What each wait is multiplied by for the attempt after it.
        /// </summary>
        public double BackoffMultiplier { get; set; } = 3;

        /// <summary>
        /// The wait after the given attempt number, the first attempt being 1.
        /// </summary>
        public TimeSpan Backoff(int attempt)
        {
            if (attempt < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(attempt), attempt, "Attempts are numbered from 1.");
            }

            var multiplier = Math.Pow(BackoffMultiplier <= 0 ? 1 : BackoffMultiplier, attempt - 1);
            return TimeSpan.FromMilliseconds(InitialBackoff.TotalMilliseconds * multiplier);
        }
    }
}
