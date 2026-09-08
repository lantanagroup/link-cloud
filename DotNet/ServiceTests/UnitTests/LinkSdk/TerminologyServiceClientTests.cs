using LantanaGroup.Link.Sdk.Clients;
using LantanaGroup.Link.Shared.Application.Extensions.Security;
using LantanaGroup.Link.Shared.Application.Interfaces.Services.Security.Token;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using Microsoft.Extensions.Options;
using Moq;
using Task = System.Threading.Tasks.Task;

namespace UnitTests.LinkSdk;

[Trait("Category", "UnitTests")]
public class TerminologyServiceClientTests
{
    [Fact]
    public async Task ExpandValueSetAsync_ByUrl_CallsExpandRoute()
    {
        using var server = new OneShotServer("{\"resourceType\":\"ValueSet\"}");
        using var client = CreateClient(server.BaseUrl);

        var callTask = client.ExpandValueSetAsync(url: "http://example.org/vs/encounter-type");
        var request = await server.WaitForRequestAsync();
        var result = await callTask;

        Assert.Equal("GET", request.Method);
        Assert.Equal("/api/terminology/fhir/ValueSet/$expand", request.Path);
        Assert.Contains("url=http", request.Query);
        Assert.Contains("ValueSet", result.Body);
    }

    [Fact]
    public async Task ExpandValueSetAsync_ById_CallsIdScopedExpandRoute()
    {
        using var server = new OneShotServer("{}");
        using var client = CreateClient(server.BaseUrl);

        var callTask = client.ExpandValueSetAsync(id: "vs-1");
        var request = await server.WaitForRequestAsync();
        await callTask;

        Assert.Equal("GET", request.Method);
        Assert.Equal("/api/terminology/fhir/ValueSet/vs-1/$expand", request.Path);
    }

    [Fact]
    public async Task GetValueSetsAsync_CallsValueSetRoute()
    {
        using var server = new OneShotServer("{\"resourceType\":\"Bundle\"}");
        using var client = CreateClient(server.BaseUrl);

        var callTask = client.GetValueSetsAsync(url: "http://example.org/vs/encounter-type");
        var request = await server.WaitForRequestAsync();
        await callTask;

        Assert.Equal("GET", request.Method);
        Assert.Equal("/api/terminology/fhir/ValueSet", request.Path);
        Assert.Contains("url=http", request.Query);
    }

    [Fact]
    public async Task LookupCodeInCodeSystemAsync_CallsLookupRouteWithQuery()
    {
        using var server = new OneShotServer("{\"resourceType\":\"Parameters\"}");
        using var client = CreateClient(server.BaseUrl);

        var callTask = client.LookupCodeInCodeSystemAsync(system: "http://loinc.org", code: "1234-5");
        var request = await server.WaitForRequestAsync();
        await callTask;

        Assert.Equal("GET", request.Method);
        Assert.Equal("/api/terminology/fhir/CodeSystem/$lookup", request.Path);
        Assert.Contains("system=http", request.Query);
        Assert.Contains("code=1234-5", request.Query);
    }

    private static TerminologyServiceClient CreateClient(string baseUrl) => new(
        Options.Create(new ServiceRegistry { TerminologyServiceUrl = baseUrl }),
        Options.Create(new BackendAuthenticationServiceExtension.LinkBearerServiceOptions { AllowAnonymous = true }),
        Options.Create(new LinkTokenServiceSettings { SigningKey = "test" }),
        new Mock<ICreateSystemToken>().Object);
}
