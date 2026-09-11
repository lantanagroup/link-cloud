using FluentAssertions;
using LantanaGroup.Link.MockDmrpApi.Application.Middleware;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace UnitTests.MockDmrpApi;

/// <summary>
/// Covers the decision that keeps this stand-in out of production. Both the request
/// pipeline and startup consult it, so it is the whole of the service's protection.
/// </summary>
/// <remarks>
/// The environment name is deliberately not part of that decision. Every deployed Link
/// namespace runs with <c>ASPNETCORE_ENVIRONMENT=Production</c> -- dev, qa and test
/// included -- so an <c>IsProduction()</c> check disabled the mock everywhere it is
/// actually deployed (LEGLINK-1048). Failing closed on the configuration key protects the
/// same thing without depending on a name that does not distinguish environments here.
/// </remarks>
public class DmrpAvailabilityTests
{
    private static IConfiguration Configuration(string? enabled)
    {
        var values = new Dictionary<string, string?>();
        if (enabled is not null)
        {
            values[DmrpAvailability.EnabledConfigurationKey] = enabled;
        }

        return new ConfigurationBuilder().AddInMemoryCollection(values).Build();
    }

    // ------------------------------------------------------------ failing closed

    [Fact]
    public void IsEnabled_WithNoConfiguration_IsFalse()
    {
        // The test that matters. An environment that provisions no row -- which is what a
        // production store does -- gets a dormant service rather than a running mock.
        DmrpAvailability.IsEnabled(Configuration(null)).Should().BeFalse();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("yes")]
    [InlineData("1")]
    [InlineData("enabled")]
    public void IsEnabled_WithAValueThatIsNotABoolean_IsFalse(string configured)
    {
        // Worth pinning twice over. The default is the whole protection, so a fat-fingered
        // row must not slip past it -- and it must not throw either: this runs before the
        // host is built, so a value that cannot be parsed has to leave the service dormant
        // rather than crash-loop the pod.
        DmrpAvailability.IsEnabled(Configuration(configured)).Should().BeFalse();
    }

    // --------------------------------------------------------- configuration decides

    [Theory]
    [InlineData("true", true)]
    [InlineData("True", true)]
    [InlineData("TRUE", true)]
    [InlineData("false", false)]
    [InlineData("False", false)]
    public void IsEnabled_FollowsConfiguration(string configured, bool expected)
    {
        DmrpAvailability.IsEnabled(Configuration(configured)).Should().Be(expected);
    }

    [Fact]
    public void IsEnabled_TakesTheHighestRankedSource()
    {
        // Layered the way the real chain layers them: a base source standing in for
        // appsettings, and a second appended after it standing in for Azure App
        // Configuration, which is added last and so outranks everything before it. An
        // environment that wants the mock on provisions the row there, and it wins.
        //
        // A differently-cased key would not work as a second signal -- configuration keys are
        // case-insensitive, so it is the same key, and the in-memory provider rejects it as a
        // duplicate. Ranking sources is the only way one key arrives by two routes.
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [DmrpAvailability.EnabledConfigurationKey] = "false"
            })
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [DmrpAvailability.EnabledConfigurationKey] = "true"
            })
            .Build();

        DmrpAvailability.IsEnabled(configuration).Should().BeTrue();
    }

    [Fact]
    public void IsEnabled_RejectsNullConfiguration()
    {
        var nullConfiguration = () => DmrpAvailability.IsEnabled(null!);

        nullConfiguration.Should().Throw<ArgumentNullException>();
    }

    // ------------------------------------------------------------- allowed paths

    [Theory]
    [InlineData("/health")]
    [InlineData("/HEALTH")]
    [InlineData("/health/ready")]
    [InlineData("/api/mock-dmrp/info")]
    public void IsAlwaysAvailable_CoversHealthAndInfo(string path)
    {
        DmrpAvailability.IsAlwaysAvailable(new PathString(path)).Should().BeTrue();
    }

    [Theory]
    [InlineData("/msc")]
    [InlineData("/ps/annual/mrp")]
    // These four share the /api/mock-dmrp prefix with the info endpoint above and must
    // still be blocked -- only .../info is exempt, not the whole support surface.
    [InlineData("/api/mock-dmrp")]
    [InlineData("/api/mock-dmrp/entries")]
    [InlineData("/api/mock-dmrp/entries/search")]
    [InlineData("/api/mock-dmrp/oauth2/token")]
    [InlineData("/api/mock-dmrp/delay")]
    [InlineData("/swagger")]
    [InlineData("/healthy-looking-but-not-health")]
    public void IsAlwaysAvailable_DoesNotCoverTheApiSurface(string path)
    {
        DmrpAvailability.IsAlwaysAvailable(new PathString(path)).Should().BeFalse();
    }
}
