namespace LantanaGroup.Link.Shared.Application.Models.Telemetry
{
    /// <summary>
    /// Values for the <see cref="DiagnosticNames.CacheOutcome"/> tag.
    /// </summary>
    public static class ResourceCacheOutcomes
    {
        /// <summary>The read cache held the resources; durable storage was not touched.</summary>
        public const string Hit = "hit";

        /// <summary>The cache did not hold them, so they came from durable storage.</summary>
        public const string Fallback = "fallback";

        /// <summary>Neither store held the key.</summary>
        public const string Empty = "empty";

        /// <summary>The write succeeded.</summary>
        public const string Ok = "ok";

        /// <summary>The write failed.</summary>
        public const string Failed = "failed";

        /// <summary>A write failed and was retried.</summary>
        public const string Retried = "retried";

        /// <summary>A write exhausted its retries; its key is not durable.</summary>
        public const string Exhausted = "exhausted";

        /// <summary>
        /// Shutdown stopped a write before it could finish retrying; its key is not durable.
        /// </summary>
        /// <remarks>
        /// Separate from <see cref="Exhausted"/>, which says storage rejected the write every time it
        /// was offered. This one says the write was never given its remaining attempts, so counting it
        /// as exhausted would report a storage problem on every deployment.
        /// </remarks>
        public const string Interrupted = "interrupted";
    }
}
