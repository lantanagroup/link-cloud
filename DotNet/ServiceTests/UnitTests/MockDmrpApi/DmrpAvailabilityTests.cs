using FluentAssertions;
using LantanaGroup.Link.MockDmrpApi.Application.Middleware;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Moq;
using Xunit;

namespace UnitTests.MockDmrpApi;

/// <summary>
/// Covers the decision that keeps this stand-in out of production. Both the request
/// pipeline and startup consult it, so it is the whole of the service's protection.
/// </summary>
public class DmrpAvailabilityTests
{
    private static IHostEnvironment Environment(string environmentName)
    {
        var environment = new Mock<IHostEnvironment>();
        environment.SetupGet(e => e.EnvironmentName).Returns(environmentName);
        return environment.Object;
    }

    private static IConfiguration Configuration(string? enabled)
    {
        var values = new Dictionary<string, string?>();
        if (enabled is not null)
        {
            values[DmrpAvailability.EnabledConfigurationKey] = enabled;
        }

        return new ConfigurationBuilder().AddInMemoryCollection(values).Build();
    }

    // ------------------------------------------------------ the production block

    [Theory]
    [InlineData("true")]
    [InlineData("True")]
    [InlineData("false")]
    [InlineData(null)]
    public void IsEnabled_InProduction_IsAlwaysFalseWhateverTheConfigurationSays(string? configured)
    {
        // The test that matters. Azure App Configuration is appended last in the
        // configuration chain, so a row provisioned against a production label outranks
        // appsettings and environment variables. This closes that off: no configuration
        // source can turn the mock on in production.
        DmrpAvailability.IsEnabled(Environment("Production"), Configuration(configured))
            .Should().BeFalse();
    }

    [Fact]
    public void IsEnabled_InProduction_IsFalseEvenWhenEveryOtherSignalSaysOtherwise()
    {
        // Layered the way the real chain layers them: a base source standing in for
        // appsettings, and a second appended after it standing in for Azure App
        // Configuration, which is added last and so outranks everything before it.
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

        // Guards the test against going vacuous: the later source really does win, so the
        // environment block below is the only thing standing between this and a running mock.
        configuration[DmrpAvailability.EnabledConfigurationKey].Should().Be("true");

        DmrpAvailability.IsEnabled(Environment("Production"), configuration).Should().BeFalse();
    }

    // ------------------------------------------------- everywhere else, config wins

    [Theory]
    [InlineData("Development")]
    [InlineData("Docker")]
    [InlineData("Staging")]
    [InlineData("qa")]
    public void IsEnabled_OutsideProduction_FollowsConfiguration(string environmentName)
    {
        DmrpAvailability.IsEnabled(Environment(environmentName), Configuration("true")).Should().BeTrue();
        DmrpAvailability.IsEnabled(Environment(environmentName), Configuration("false")).Should().BeFalse();
    }

    [Fact]
    public void IsEnabled_OutsideProductionWithNoConfiguration_DefaultsToEnabled()
    {
        // A bare "dotnet run" should serve requests. Production is covered by the
        // environment check rather than by this default.
        DmrpAvailability.IsEnabled(Environment("Development"), Configuration(null)).Should().BeTrue();
    }

    [Fact]
    public void IsEnabled_RejectsNullArguments()
    {
        var nullEnvironment = () => DmrpAvailability.IsEnabled(null!, Configuration("true"));
        var nullConfiguration = () => DmrpAvailability.IsEnabled(Environment("Development"), null!);

        nullEnvironment.Should().Throw<ArgumentNullException>();
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
    [InlineData("/ps/annual")]
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
