using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace LantanaGroup.Link.LinkAdmin.BFF.Application.Models.Health
{
    /// <summary>
    /// Maps a Spring Boot actuator status code onto the <see cref="HealthStatus"/> the dashboard renders.
    /// <para>
    /// DEGRADED is a custom Spring status the Java services return when a cache is unreachable but the
    /// durable store behind it is fine — the same condition <c>ResourceCacheHealthCheck</c> reports as
    /// <see cref="HealthStatus.Degraded"/> for the .NET services. Collapsing it to Unhealthy would put
    /// a survivable Redis outage in the same bucket as a lost database.
    /// </para>
    /// Anything unrecognised is Unhealthy: an unknown status is not a reason to claim health.
    /// </summary>
    public static class SpringHealthStatusMap
    {
        public static HealthStatus ToHealthStatus(string? status) =>
            status?.Trim().ToUpperInvariant() switch
            {
                "UP" => HealthStatus.Healthy,
                "DEGRADED" => HealthStatus.Degraded,
                _ => HealthStatus.Unhealthy
            };
    }
}
