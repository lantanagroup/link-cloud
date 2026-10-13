namespace LantanaGroup.Link.LinkAdmin.BFF.Application.Models.Health;

/// <summary>
/// How long the dashboard waits for one service to answer <c>/health</c>.
/// </summary>
/// <remarks>
/// Configurable because it is a budget rather than a constant. A service's own health endpoint costs
/// the sum of its indicators -- Spring evaluates them sequentially on the request thread -- so the
/// deadline that is generous today stops being generous the next time a backend is added. When it is
/// exceeded the dashboard shows the service Unhealthy with a single entry saying the check timed out,
/// which is honest but less useful than the report the service was about to produce.
/// <para>
/// The default matches the value this replaced, so nothing changes until an environment raises it.
/// </para>
/// </remarks>
public class ServiceHealthCheckSettings
{
    public const string ConfigSectionName = "ServiceHealthCheck";

    private const int DefaultTimeoutSeconds = 5;

    public int TimeoutSeconds { get; set; } = DefaultTimeoutSeconds;

    /// <summary>
    /// The configured deadline, falling back to the default for a value that cannot be one -- a
    /// zero or negative timeout would cancel every check before it was sent and report every
    /// service as timed out.
    /// </summary>
    public TimeSpan Timeout =>
        TimeSpan.FromSeconds(TimeoutSeconds > 0 ? TimeoutSeconds : DefaultTimeoutSeconds);
}
