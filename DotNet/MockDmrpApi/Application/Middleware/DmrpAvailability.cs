using LantanaGroup.Link.MockDmrpApi.Settings;
using Microsoft.Extensions.Hosting;

namespace LantanaGroup.Link.MockDmrpApi.Application.Middleware;

/// <summary>
/// Decides whether this stand-in is available to serve requests.
/// </summary>
/// <remarks>
/// Two layers, and the outer one is absolute. Production is never allowed to run the mock,
/// whatever any configuration source says; everywhere else,
/// <c>MockDmrpApi:Enabled</c> decides.
/// <para>
/// The environment check exists because Azure App Configuration is appended last in the
/// configuration chain, so a row provisioned against a production label would silently
/// outrank appsettings and environment variables. That failure would be invisible -- a
/// running mock looks exactly like a healthy service -- so it is closed off here rather
/// than left to configuration hygiene alone.
/// </para>
/// <para>
/// Both the request pipeline and startup consult this, so a disabled deployment cannot end
/// up serving traffic but skipping migrations, or the reverse.
/// </para>
/// </remarks>
public static class DmrpAvailability
{
    public const string EnabledConfigurationKey =
        $"{DmrpApiSettings.ConfigSectionName}:{nameof(DmrpApiSettings.Enabled)}";

    /// <summary>Paths that answer even when the mock is disabled.</summary>
    /// <remarks>
    /// Health has to stay up or the container reports unhealthy and restarts, which looks
    /// like an outage rather than a deliberately dormant service. The info route is there
    /// so an operator can confirm which build is deployed without enabling anything.
    /// </remarks>
    public static readonly string[] AlwaysAvailablePaths = ["/health", "/api/mock-dmrp/info"];

    public static bool IsEnabled(IHostEnvironment environment, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(environment);
        ArgumentNullException.ThrowIfNull(configuration);

        if (environment.IsProduction())
        {
            return false;
        }

        return configuration.GetValue(EnabledConfigurationKey, true);
    }

    public static bool IsAlwaysAvailable(PathString path) =>
        AlwaysAvailablePaths.Any(allowed =>
            path.StartsWithSegments(allowed, StringComparison.OrdinalIgnoreCase));
}
