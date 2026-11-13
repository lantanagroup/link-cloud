using LantanaGroup.Link.Sdk.Clients;
using LantanaGroup.Link.Shared.Application.Extensions.Security;
using LantanaGroup.Link.Shared.Application.Interfaces.Services.Security.Token;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;
using Task = System.Threading.Tasks.Task;

namespace UnitTests.LinkSdk;

[Trait("Category", "UnitTests")]
public class MeasureEvalServiceClientTests
{
    [Fact]
    public async Task GetRelatedArtifactsAsync_UsesTheMeasureRoute()
    {
        using var http = new FakeHttpBoundary("[]");
        using var client = CreateClient(http.BaseUrl);

        await client.GetRelatedArtifactsAsync("NHSN-1");
        var request = http.SingleRequest();

        Assert.Equal("GET", request.Method);
        Assert.Equal("/api/measureeval/measure-definition/NHSN-1/relatedArtifact", request.Path);
    }

    [Fact]
    public async Task GetMeasureCqlAsync_UsesTheLibraryRouteAndOptionalRange()
    {
        using var http = new FakeHttpBoundary("define x: 1");
        using var client = CreateClient(http.BaseUrl);

        await client.GetMeasureCqlAsync("NHSN-1", "NHSN", "37:1-38:22");
        var request = http.SingleRequest();

        Assert.Equal("GET", request.Method);
        Assert.Equal("/api/measureeval/measure-definition/NHSN-1/NHSN/$cql", request.Path);
        Assert.Contains("range=", request.Query);
    }

    [Fact]
    public async Task EvaluateMeasureAsync_PostsParametersAndDebug()
    {
        using var http = new FakeHttpBoundary("{\"resourceType\":\"MeasureReport\"}");
        using var client = CreateClient(http.BaseUrl);

        await client.EvaluateMeasureAsync("NHSN-1", "{\"resourceType\":\"Parameters\"}", "groups");
        var request = http.SingleRequest();

        Assert.Equal("POST", request.Method);
        Assert.Equal("/api/measureeval/measure-definition/NHSN-1/$evaluate", request.Path);
        Assert.Contains("debug=groups", request.Query);
        Assert.Contains("\"resourceType\":\"Parameters\"", request.Body);
    }

    private static MeasureEvalServiceClient CreateClient(string baseUrl) => new(
        Options.Create(new ServiceRegistry { MeasureServiceUrl = baseUrl }),
        Options.Create(new BackendAuthenticationServiceExtension.LinkBearerServiceOptions { AllowAnonymous = true }),
        Options.Create(new LinkTokenServiceSettings { SigningKey = "test" }),
        new Mock<ICreateSystemToken>().Object);
}
