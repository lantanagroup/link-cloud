namespace LantanaGroup.Link.Shared.Application.Models
{
    public enum HealthCheckType
    {
        /// <summary>
        /// No longer registers a check: a database row names its provider, which a member name
        /// cannot spell. Use <see cref="HealthCheckNames.SqlServerDatabase"/> instead.
        /// </summary>
        Database = 100,
        Kafka = 200,
        /// <summary>
        /// No longer registers a check: cache rows name their store, and the two kinds are not the
        /// same check. Use <see cref="HealthCheckNames.RedisCache"/> for the service cache or
        /// <see cref="HealthCheckNames.ResourceCache"/> for the resource cache.
        /// </summary>
        Cache = 300,
        Service = 400,
    }
}
