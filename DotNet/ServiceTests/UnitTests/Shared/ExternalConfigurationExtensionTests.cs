using LantanaGroup.Link.Shared.Application.Extensions;
using LantanaGroup.Link.Shared.Application.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace UnitTests.Shared;

/// <summary>
/// Covers the version fallback SetupServiceInformation applies to the ServiceInformation singleton it
/// registers (LEGLINK-1420). Shares the ServiceActivitySource collection because the call also
/// re-initializes that static.
/// </summary>
[Trait("Category", "UnitTests")]
[Collection("ServiceActivitySource")]
public class ExternalConfigurationExtensionTests
{
    private const string AssemblyVersion = "9.9.9-assembly";

    [Fact]
    public void SetupServiceInformation_ConfigVersionSet_RegistersConfigVersion()
    {
        var serviceInformation = SetupWithVersion("1.2.3");

        Assert.Equal("1.2.3", serviceInformation.Version);
    }

    [Fact]
    public void SetupServiceInformation_ConfigVersionMissing_RegistersAssemblyVersion()
    {
        var serviceInformation = SetupWithVersion(null);

        Assert.Equal(AssemblyVersion, serviceInformation.Version);
    }

    [Fact]
    public void SetupServiceInformation_ConfigVersionEmpty_RegistersAssemblyVersion()
    {
        var serviceInformation = SetupWithVersion(string.Empty);

        Assert.Equal(AssemblyVersion, serviceInformation.Version);
    }

    [Fact]
    public void SetupServiceInformation_ConfigVersionWhitespace_RegistersAssemblyVersion()
    {
        var serviceInformation = SetupWithVersion("   ");

        Assert.Equal(AssemblyVersion, serviceInformation.Version);
    }

    /// <summary>
    /// Binds a ServiceInformation section from in-memory configuration, the way a service's
    /// appsettings.json would supply it, and resolves the singleton SetupServiceInformation registered.
    /// </summary>
    private static ServiceInformation SetupWithVersion(string? version)
    {
        var settings = new Dictionary<string, string?>
        {
            ["ServiceInformation:ServiceName"] = "Unit Test Service"
        };

        if (version != null)
        {
            settings["ServiceInformation:Version"] = version;
        }

        var builder = Host.CreateEmptyApplicationBuilder(new HostApplicationBuilderSettings());
        builder.Configuration.AddInMemoryCollection(settings);

        builder.SetupServiceInformation("UnitTest", AssemblyVersion);

        using var provider = builder.Services.BuildServiceProvider();
        return provider.GetRequiredService<ServiceInformation>();
    }
}
