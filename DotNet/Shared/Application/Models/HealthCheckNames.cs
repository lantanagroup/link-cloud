namespace LantanaGroup.Link.Shared.Application.Models
{
    /// <summary>
    /// The names health checks are registered under. A registration name reaches the Admin UI's
    /// Health Checks page verbatim, so these are the labels it renders per row.
    /// </summary>
    /// <remarks>
    /// <see cref="HealthCheckType"/> cannot carry a backend-specific name: its members supply the
    /// registration name through <c>ToString()</c>, and an enum member cannot spell one. A row names
    /// the backend it reached so that it can be compared against the Java services' rows rather than
    /// leaving the backend to be inferred from the service, and so that two rows reading alike are
    /// checking the same thing.
    /// </remarks>
    public static class HealthCheckNames
    {
        /// <summary>
        /// A database check against SQL Server, which every .NET service persists to through EF Core.
        /// MeasureEval's MongoDB row is named by its own indicator on the Java side.
        /// </summary>
        public const string SqlServerDatabase = "Database (SQL Server)";

        /// <summary>
        /// The general-purpose service cache, reached through <c>ICacheService</c> and backed by Redis.
        /// </summary>
        public const string RedisCache = "Cache (Redis)";

        /// <summary>
        /// The resource cache, which holds FHIR resource bodies and is backed by Redis, Azure Blob
        /// Storage, or both depending on <c>ResourceCacheSettings.CacheImplementation</c>. Deliberately
        /// distinct from <see cref="RedisCache"/>: a different store, checked by a different probe, and
        /// the same mechanism MeasureEval reports under this name on the Java side. Naming it "Cache
        /// (Redis)" would say a service caches in Redis when it may be configured for blob storage.
        /// </summary>
        public const string ResourceCache = "Resource Cache (Redis/ABS)";
    }
}
