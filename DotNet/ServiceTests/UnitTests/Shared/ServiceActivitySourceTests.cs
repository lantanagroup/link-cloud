using LantanaGroup.Link.Shared.Application.Models;

namespace UnitTests.Shared;

/// <summary>
/// Covers the Initialize overload every service reaches through SetupServiceInformation (LEGLINK-1420).
/// A configured ServiceInformation section without a Version used to leave the source version empty.
/// ServiceConfigName is left null so these tests never move the ServiceName static under another test.
/// </summary>
[Trait("Category", "UnitTests")]
[Collection("ServiceActivitySource")]
public class ServiceActivitySourceTests
{
    [Fact]
    public void Initialize_ConfigVersionEmpty_FallsBackToAssemblyVersion()
    {
        var serviceInfo = new ServiceInformation { Version = string.Empty };

        ServiceActivitySource.Initialize("9.9.9-assembly", serviceInfo);

        Assert.Equal("9.9.9-assembly", ServiceActivitySource.Version);
        Assert.Equal("9.9.9-assembly", ServiceActivitySource.Instance.Version);
    }

    [Fact]
    public void Initialize_ConfigVersionWhitespace_FallsBackToAssemblyVersion()
    {
        var serviceInfo = new ServiceInformation { Version = "   " };

        ServiceActivitySource.Initialize("9.9.9-assembly", serviceInfo);

        Assert.Equal("9.9.9-assembly", ServiceActivitySource.Version);
    }

    [Fact]
    public void Initialize_ConfigVersionSet_UsesConfigVersion()
    {
        var serviceInfo = new ServiceInformation { Version = "1.2.3" };

        ServiceActivitySource.Initialize("9.9.9-assembly", serviceInfo);

        Assert.Equal("1.2.3", ServiceActivitySource.Version);
    }

    [Fact]
    public void Initialize_ProductVersionSet_IsCopied()
    {
        var serviceInfo = new ServiceInformation
        {
            Version = "1.2.3",
            ProductVersion = "0.9.0"
        };

        ServiceActivitySource.Initialize("9.9.9-assembly", serviceInfo);

        Assert.Equal("0.9.0", ServiceActivitySource.ProductVersion);
    }

    [Fact]
    public void Initialize_NullServiceInfo_UsesAssemblyVersion()
    {
        ServiceActivitySource.Initialize("9.9.9-assembly", null);

        Assert.Equal("9.9.9-assembly", ServiceActivitySource.Version);
        Assert.Equal(string.Empty, ServiceActivitySource.ProductVersion);
    }
}
