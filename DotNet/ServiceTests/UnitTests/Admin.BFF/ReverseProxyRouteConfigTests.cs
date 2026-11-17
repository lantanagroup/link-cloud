using FluentAssertions;
using Microsoft.Extensions.Configuration;

namespace UnitTests.Admin.BFF;

/// <summary>
/// Pins the BFF's reverse-proxy routes to Data Acquisition in the shipped appsettings files. The Admin UI
/// only reaches Data Acquisition through these routes, so a missing or renamed one is a 404 in the browser.
/// </summary>
/// <remarks>
/// The files are linked into the test output by ServiceTests.csproj under Resources/Admin.BFF.
/// </remarks>
[Trait("Category", "UnitTests")]
public class ReverseProxyRouteConfigTests
{
    private const string DataAcquisitionCluster = "DataAcquisitionService";

    public static TheoryData<string> SettingsFiles => new()
    {
        "appsettings.json",
        "appsettings.Development.json"
    };

    [Theory]
    [MemberData(nameof(SettingsFiles))]
    public void Routes_DataAcquisitionRoute_ForwardsCanonicalPrefixToDataAcquisition(string file)
    {
        // Named rather than numbered on purpose: QA and QA2 override route1-route14 in App Configuration, so a
        // numbered route here could be silently replaced in those environments.
        var route = LoadRoutes(file).GetSection("dataAcquisition");

        route.Exists().Should().BeTrue("the Admin UI calls /api/data-acquisition through this route");
        route["Match:Path"].Should().Be("api/data-acquisition/{**catch-all}");
        route["ClusterId"].Should().Be(DataAcquisitionCluster);
    }

    [Theory]
    [MemberData(nameof(SettingsFiles))]
    public void Routes_LegacyRoute_StillForwardsOldPrefixToDataAcquisition(string file)
    {
        // Remove this test together with route4 when the /api/data alias is retired.
        var route = LoadRoutes(file).GetSection("route4");

        route.Exists().Should().BeTrue("callers not yet moved off /api/data still go through this route");
        route["Match:Path"].Should().Be("api/data/{**catch-all}");
        route["ClusterId"].Should().Be(DataAcquisitionCluster);
    }

    [Theory]
    [MemberData(nameof(SettingsFiles))]
    public void Routes_DataAcquisitionRoute_RequiresSamePolicyAsLegacyRoute(string file)
    {
        // The new route must not be a weaker way into the same endpoints.
        var routes = LoadRoutes(file);
        var policy = routes["dataAcquisition:AuthorizationPolicy"];

        policy.Should().NotBeNullOrWhiteSpace();
        policy.Should().Be(routes["route4:AuthorizationPolicy"]);
    }

    [Theory]
    [MemberData(nameof(SettingsFiles))]
    public void Clusters_DataAcquisitionCluster_IsDefined(string file)
    {
        var configuration = Load(file);

        configuration.GetSection($"ReverseProxy:Clusters:{DataAcquisitionCluster}").Exists().Should().BeTrue();
    }

    private static IConfigurationSection LoadRoutes(string file)
    {
        return Load(file).GetSection("ReverseProxy:Routes");
    }

    private static IConfiguration Load(string file)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Resources", "Admin.BFF", file);

        return new ConfigurationBuilder()
            .AddJsonFile(path, optional: false)
            .Build();
    }
}
