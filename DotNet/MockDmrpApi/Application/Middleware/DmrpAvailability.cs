using LantanaGroup.Link.MockDmrpApi.Settings;

namespace LantanaGroup.Link.MockDmrpApi.Application.Middleware;

/// <summary>
/// Decides whether this stand-in is available to serve requests.
/// </summary>
/// <remarks>
/// One switch, and it fails closed: <c>MockDmrpApi:Enabled</c> must be present and true for
/// the mock to serve. An absent key means disabled.
/// <para>
/// The default is the whole of the production protection, so it is deliberately the
/// pessimistic one. An earlier version instead refused unconditionally when
/// <c>IHostEnvironment.IsProduction()</c> was true, which does not work here: every deployed
/// Link namespace runs with <c>ASPNETCORE_ENVIRONMENT=Production</c>, including dev, qa and
/// test, so that check disabled the mock in every environment it is actually deployed to
/// while protecting nothing that this default does not already protect (LEGLINK-1048).
/// </para>
/// <para>
/// What keeps the mock out of production is therefore twofold, and neither part depends on
/// the environment name: no <c>MockDmrpApi:Enabled</c> row is provisioned in a production
/// App Configuration store, and the image is not deployed to a production namespace at all
/// -- <c>Scripts/build_and_push_and_set.py</c> targets a <c>mock-dmrp-deploy</c> deployment,
/// which only the lower environments have.
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

    public static bool IsEnabled(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        // Parsed rather than read with GetValue<bool>, which throws on a value it cannot
        // convert. Program.cs calls this before the host is built, so a fat-fingered row --
        // "yes", "1", a stray space -- would crash-loop the pod instead of leaving it
        // dormant, which is the failure the 503 path exists to avoid. Anything that is not
        // a boolean true is off.
        return bool.TryParse(configuration[EnabledConfigurationKey], out var enabled) && enabled;
    }

    public static bool IsAlwaysAvailable(PathString path) =>
        AlwaysAvailablePaths.Any(allowed =>
            path.StartsWithSegments(allowed, StringComparison.OrdinalIgnoreCase));
}
