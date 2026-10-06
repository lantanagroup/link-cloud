namespace LantanaGroup.Link.Shared.Application.Models.Telemetry
{
    /// <summary>
    /// Why a resource cache read was not served from the cache, tagged as
    /// <see cref="DiagnosticNames.CacheFallbackReason"/>.
    /// </summary>
    /// <remarks>
    /// The same three values MeasureEval emits, so a query for one reason returns comparable series
    /// from every service that reads the cache. Carried on <c>empty</c> reads as well as
    /// <c>fallback</c> ones: an empty result that followed a Redis outage is a result nothing should
    /// trust, where an empty result that followed a plain miss is routine.
    /// </remarks>
    public static class ResourceCacheFallbackReasons
    {
        /// <summary>
        /// The cache held no entry for the key, or held one with no usable resources in it.
        /// </summary>
        public const string Miss = "miss";

        /// <summary>
        /// The cache held an entry, but it held fewer resources than durable storage recorded, so it
        /// was recreated by a write after an eviction and is not the whole record.
        /// </summary>
        public const string Partial = "partial";

        /// <summary>
        /// The cache could not be reached, so whether it held the key is unknown.
        /// </summary>
        public const string Unavailable = "unavailable";
    }
}
