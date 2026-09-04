using LantanaGroup.Link.Sdk.Clients;
using LantanaGroup.Link.Shared.Application.Extensions.Security;
using LantanaGroup.Link.Shared.Application.Interfaces.Services.Security.Token;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using LantanaGroup.Link.Shared.Application.Models.Terminology;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace UnitTests.LinkSdk;

/// <summary>
/// Confirms the Terminology client reaches the route the service exposes and sends the query the caller
/// asked for.
/// </summary>
/// <remarks>
/// Terminology is the first service in this SDK with no client before it, so there is no prior shape to
/// fall back on if the URL is wrong. The omission case matters more than usual: the endpoint rejects a
/// blank codeSystem or valueSet rather than treating it as absent, so a client that sent empty strings
/// would fail every unscoped search.
/// </remarks>
[Trait("Category", "UnitTests")]
public class TerminologyServiceClientTests
{
    private const string EmptyPage = "{\"records\":[],\"metadata\":{\"pageSize\":20,\"pageNumber\":1,\"totalCount\":0,\"totalPages\":0}}";
    private const string Hsloc = "https://www.cdc.gov/nhsn/cdaportal/terminology/codesystem/hsloc.html";

    [Fact]
    public async System.Threading.Tasks.Task SearchCodesAsync_ReadsTheCodeSearchRoute()
    {
        using var server = new OneShotServer(EmptyPage);
        using var client = CreateClient(server.BaseUrl);

        var callTask = client.SearchCodesAsync(search: "burn");
        var request = await server.WaitForRequestAsync();
        await callTask;

        Assert.Equal("GET", request.Method);
        Assert.Equal("/api/terminology/codes", request.Path);
    }

    [Fact]
    public async System.Threading.Tasks.Task SearchCodesAsync_SendsTheFiltersItWasGiven()
    {
        using var server = new OneShotServer(EmptyPage);
        using var client = CreateClient(server.BaseUrl);

        var callTask = client.SearchCodesAsync(
            search: "burn", codeSystem: Hsloc, version: "1.0.0",
            excludeInactive: true, pageNumber: 2, pageSize: 50);
        var request = await server.WaitForRequestAsync();
        await callTask;

        Assert.Contains("search=burn", request.Query);
        Assert.Contains($"codeSystem={Uri.EscapeDataString(Hsloc)}", request.Query);
        Assert.Contains("version=1.0.0", request.Query);
        Assert.Contains("excludeInactive=True", request.Query);
        Assert.Contains("pageNumber=2", request.Query);
        Assert.Contains("pageSize=50", request.Query);
    }

    /// <summary>
    /// The canonical URI has to survive to the wire intact, escaped rather than mangled - its ":" and "/"
    /// are what the service matches the cached code group on.
    /// </summary>
    [Fact]
    public async System.Threading.Tasks.Task SearchCodesAsync_EscapesTheCanonicalUriWithoutAlteringIt()
    {
        using var server = new OneShotServer(EmptyPage);
        using var client = CreateClient(server.BaseUrl);

        var callTask = client.SearchCodesAsync(codeSystem: Hsloc);
        var request = await server.WaitForRequestAsync();
        await callTask;

        var sent = System.Web.HttpUtility.ParseQueryString(request.Query)["codeSystem"];
        Assert.Equal(Hsloc, sent);
    }

    /// <summary>
    /// A filter that was not given is left off the request rather than sent empty. The endpoint refuses a
    /// blank codeSystem or valueSet, so sending one would turn every unscoped search into a 400.
    /// </summary>
    [Fact]
    public async System.Threading.Tasks.Task SearchCodesAsync_OmitsFiltersThatWereNotGiven()
    {
        using var server = new OneShotServer(EmptyPage);
        using var client = CreateClient(server.BaseUrl);

        var callTask = client.SearchCodesAsync(search: "burn");
        var request = await server.WaitForRequestAsync();
        await callTask;

        Assert.DoesNotContain("codeSystem=", request.Query);
        Assert.DoesNotContain("valueSet=", request.Query);
        Assert.DoesNotContain("version=", request.Query);
    }

    [Fact]
    public async System.Threading.Tasks.Task SearchCodesAsync_UsesTheServerDefaultsWhenNoPagingIsAskedFor()
    {
        using var server = new OneShotServer(EmptyPage);
        using var client = CreateClient(server.BaseUrl);

        var callTask = client.SearchCodesAsync(search: "burn");
        var request = await server.WaitForRequestAsync();
        await callTask;

        Assert.Contains("pageNumber=1", request.Query);
        Assert.Contains("pageSize=20", request.Query);
        Assert.Contains("excludeInactive=False", request.Query);
    }

    /// <summary>
    /// The paged read model deserializes into the SDK's own types, including the string-valued status.
    /// </summary>
    [Fact]
    public async System.Threading.Tasks.Task SearchCodesAsync_DeserializesThePagedResult()
    {
        const string body = """
            {
              "records": [
                { "system": "https://example.org/cs", "code": "1026-4", "display": "Burn Critical Care", "status": "Inactive" }
              ],
              "metadata": { "pageSize": 20, "pageNumber": 1, "totalCount": 9, "totalPages": 1 }
            }
            """;

        using var server = new OneShotServer(body);
        using var client = CreateClient(server.BaseUrl);

        var callTask = client.SearchCodesAsync(search: "burn");
        await server.WaitForRequestAsync();
        var response = await callTask;

        Assert.True(response.IsSuccessStatusCode);
        Assert.NotNull(response.Body);

        var record = Assert.Single(response.Body.Records);
        Assert.Equal("1026-4", record.Code);
        Assert.Equal("Burn Critical Care", record.Display);
        Assert.Equal(CodeStatus.Inactive, record.Status);

        Assert.Equal(9, response.Body.Metadata.TotalCount);
    }

    private static TerminologyServiceClient CreateClient(string baseUrl)
    {
        return new TerminologyServiceClient(
            Options.Create(new ServiceRegistry { TerminologyServiceUrl = baseUrl }),
            Options.Create(new BackendAuthenticationServiceExtension.LinkBearerServiceOptions { AllowAnonymous = true }),
            Options.Create(new LinkTokenServiceSettings { SigningKey = "test" }),
            new Mock<ICreateSystemToken>().Object);
    }
}
