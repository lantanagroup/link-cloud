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
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [DmrpAvailability.EnabledConfigurationKey] = "true",
                ["MockDmrpApi:Enabled "] = "true"
            })
            .Build();

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
    [InlineData("/dmrp/mock")]
    [InlineData("/dmrp/mock/search")]
    [InlineData("/dmrp/mock/reporting-plans")]
    [InlineData("/dmrp/mock/oauth2/token")]
    [InlineData("/dmrp/mock/auth-test")]
    [InlineData("/swagger")]
    [InlineData("/healthy-looking-but-not-health")]
    public void IsAlwaysAvailable_DoesNotCoverTheApiSurface(string path)
    {
        DmrpAvailability.IsAlwaysAvailable(new PathString(path)).Should().BeFalse();
    }
}
