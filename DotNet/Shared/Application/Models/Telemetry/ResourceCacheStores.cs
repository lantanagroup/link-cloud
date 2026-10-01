namespace LantanaGroup.Link.Shared.Application.Models.Telemetry
{
    /// <summary>
    /// Values for the <see cref="DiagnosticNames.CacheStore"/> tag.
    /// </summary>
    public static class ResourceCacheStores
    {
        /// <summary>The read cache, written inline.</summary>
        public const string Redis = "redis";

        /// <summary>Durable storage, written on the background queue.</summary>
        public const string Blob = "blob";
    }
}
