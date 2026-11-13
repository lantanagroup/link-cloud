using LantanaGroup.Link.Sdk.Clients;
using LantanaGroup.Link.Shared.Application.Extensions.Security;
using LantanaGroup.Link.Shared.Application.Interfaces.Services.Security.Token;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using Microsoft.Extensions.Options;
using Moq;

namespace UnitTests.LinkSdk;

[Trait("Category", "UnitTests")]
public class AuditServiceClientTests
{
    [Fact]
    public async System.Threading.Tasks.Task SearchAsync_CallsAuditWithFilters()
    {
        using var http = new FakeHttpBoundary("""{"records":[],"metadata":null}""");
        using var client = CreateClient(http.BaseUrl);

        await client.SearchAsync(
            searchText: "patient",
            facility: "f1",
            service: "Report",
            action: "Submit",
            sortBy: "CreatedOn",
            sortOrder: "Descending",
            pageNumber: 2,
            pageSize: 20);
        var request = http.SingleRequest();

        Assert.Equal("GET", request.Method);
        Assert.Equal("/api/audit", request.Path);
        Assert.Contains("searchText=patient", request.Query);
        Assert.Contains("facility=f1", request.Query);
        Assert.Contains("service=Report", request.Query);
        Assert.Contains("action=Submit", request.Query);
        Assert.Contains("sortBy=CreatedOn", request.Query);
        Assert.Contains("pageNumber=2", request.Query);
        Assert.Contains("pageSize=20", request.Query);
    }

    [Fact]
    public async System.Threading.Tasks.Task GetAsync_CallsAuditById()
    {
        var id = Guid.Parse("33333333-3333-3333-3333-333333333333");
        using var http = new FakeHttpBoundary($$"""{"id":"{{id}}","action":"Create"}""");
        using var client = CreateClient(http.BaseUrl);

        var result = await client.GetAsync(id);
        var request = http.SingleRequest();

        Assert.Equal("GET", request.Method);
        Assert.Equal($"/api/audit/{id}", request.Path);
        Assert.Equal("Create", result.Body!.Action);
    }

    private static AuditServiceClient CreateClient(string baseUrl)
    {
        var serviceRegistry = Options.Create(new ServiceRegistry
        {
            AuditServiceUrl = baseUrl
        });
        var bearerOptions = Options.Create(new BackendAuthenticationServiceExtension.LinkBearerServiceOptions
        {
            AllowAnonymous = true
        });
        var tokenSettings = Options.Create(new LinkTokenServiceSettings
        {
            SigningKey = "test"
        });
        var tokenService = new Mock<ICreateSystemToken>();
        return new AuditServiceClient(serviceRegistry, bearerOptions, tokenSettings, tokenService.Object);
    }
}
