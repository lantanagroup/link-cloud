using LantanaGroup.Link.Shared.Application.Extensions;
using LantanaGroup.Link.Shared.Application.Models.Telemetry;
using OpenTelemetry.Resources;

namespace UnitTests.Shared;

/// <summary>
/// The test environments share one Prometheus. The environment attribute is what tells their series
/// apart, and the instance id is what keeps two processes from writing to the same series.
/// </summary>
[Trait("Category", "UnitTests")]
public class LinkTelemetryResourceTests
{
    [Fact]
    public void ConfigureLinkResource_EnvironmentSet_AddsDeploymentEnvironmentName()
    {
        var attributes = Build(deploymentEnvironment: "qa");

        Assert.Equal("qa", attributes[DiagnosticNames.DeploymentEnvironmentName]);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void ConfigureLinkResource_EnvironmentBlank_OmitsTheAttribute(string? deploymentEnvironment)
    {
        var attributes = Build(deploymentEnvironment);

        Assert.DoesNotContain(DiagnosticNames.DeploymentEnvironmentName, attributes.Keys);
    }

    [Fact]
    public void ConfigureLinkResource_AnyEnvironment_NamesTheServiceAndInstance()
    {
        var attributes = Build(deploymentEnvironment: "qa");

        Assert.Equal("Normalization", attributes["service.name"]);
        Assert.Equal("1.2.3", attributes["service.version"]);
        Assert.False(string.IsNullOrWhiteSpace(attributes["service.instance.id"] as string));
    }

    private static Dictionary<string, object> Build(string? deploymentEnvironment)
    {
        var options = new TelemetryServiceExtension.TelemetryServiceOptions
        {
            ServiceName = "Normalization",
            ServiceVersion = "1.2.3",
            DeploymentEnvironment = deploymentEnvironment
        };

        return TelemetryServiceExtension
            .ConfigureLinkResource(ResourceBuilder.CreateEmpty(), options)
            .Build()
            .Attributes
            .ToDictionary(a => a.Key, a => a.Value);
    }
}
