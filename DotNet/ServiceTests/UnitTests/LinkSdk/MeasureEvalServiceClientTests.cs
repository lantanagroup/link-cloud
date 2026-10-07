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

    private static MeasureEvalServiceClient CreateClient(string baseUrl) => new(
        Options.Create(new ServiceRegistry { MeasureServiceUrl = baseUrl }),
        Options.Create(new BackendAuthenticationServiceExtension.LinkBearerServiceOptions { AllowAnonymous = true }),
        Options.Create(new LinkTokenServiceSettings { SigningKey = "test" }),
        new Mock<ICreateSystemToken>().Object);
}
