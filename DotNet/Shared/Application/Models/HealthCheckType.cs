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
        Cache = 300,
        Service = 400,
    }
}
