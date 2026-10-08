using LantanaGroup.Link.Sdk.Clients;
using LantanaGroup.Link.Sdk.DependencyInjection;
using LantanaGroup.Link.Shared.Application.Extensions.Security;
using LantanaGroup.Link.Shared.Application.Interfaces.Services.Security.Token;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using LantanaGroup.Link.Shared.Application.Models.Integration.MockDmrp;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Moq;
using System.Text.Json;

namespace UnitTests.LinkSdk;

/// <summary>
/// The Mock DMRP API's support surface, reached at the mock's root address rather than through a
/// service registry entry.
/// </summary>
[Trait("Category", "UnitTests")]
public class MockDmrpServiceClientTests
{
    private const string Token = "link-system-token";
    private const string EmptyPage = "{\"records\":[]," +
                                     "\"metadata\":{\"pageSize\":1,\"pageNumber\":1," +
                                     "\"totalCount\":0,\"totalPages\":0}}";

    /// <summary>
    /// The info route is how a caller decides whether the host is the mock at all, so it is asked
    /// before anything proves the host is one. A Link token sent there could land with the real DMRP.
    /// </summary>
    [Fact]
    public async System.Threading.Tasks.Task GetInfoAsync_TokenConfigured_SendsNoAuthorization()
    {
        using var server = new OneShotServer("{\"serviceName\":\"Link Mock DMRP API\",\"version\":\"1.0.0\"}");
        using var client = CreateClient(server.BaseUrl, anonymous: false);

        var callTask = client.GetInfoAsync();
        var request = await server.WaitForRequestAsync();
        var response = await callTask;

        Assert.Equal("GET", request.Method);
        Assert.Equal("/api/mock-dmrp/info", request.Path);
        Assert.Null(request.Authorization);
        Assert.Equal("Link Mock DMRP API", response.Body?.ServiceName);
    }

    [Fact]
    public async System.Threading.Tasks.Task SearchEntriesAsync_TokenConfigured_SendsTheLinkToken()
    {
        using var server = new OneShotServer(EmptyPage);
        using var client = CreateClient(server.BaseUrl, anonymous: false);

        var callTask = client.SearchEntriesAsync(pageSize: 1);
        var request = await server.WaitForRequestAsync();
        await callTask;

        Assert.Equal($"Bearer {Token}", request.Authorization);
    }

    [Fact]
    public async System.Threading.Tasks.Task SearchEntriesAsync_FiltersGiven_SendsThemToTheSearchRoute()
    {
        using var server = new OneShotServer(EmptyPage);
        using var client = CreateClient(server.BaseUrl);

        var callTask = client.SearchEntriesAsync(facilityId: "100", component: "MSC", measure: "HOB",
            reportingMonth: 10, reportingYear: 2026, pageSize: 100, pageNumber: 2);
        var request = await server.WaitForRequestAsync();
        await callTask;

        Assert.Equal("GET", request.Method);
        Assert.Equal("/api/mock-dmrp/entries/search", request.Path);
        Assert.Contains("facilityId=100", request.Query);
        Assert.Contains("component=MSC", request.Query);
        Assert.Contains("measure=HOB", request.Query);
        Assert.Contains("reportingMonth=10", request.Query);
        Assert.Contains("reportingYear=2026", request.Query);
        Assert.Contains("pageSize=100", request.Query);
        Assert.Contains("pageNumber=2", request.Query);
    }

    /// <summary>
    /// The mock sanitizes and compares every filter it is sent, so an empty one would match only
    /// entries with an empty value rather than every entry.
    /// </summary>
    [Fact]
    public async System.Threading.Tasks.Task SearchEntriesAsync_FiltersOmitted_LeavesThemOffTheRequest()
    {
        using var server = new OneShotServer(EmptyPage);
        using var client = CreateClient(server.BaseUrl);

        var callTask = client.SearchEntriesAsync(facilityId: "100");
        var request = await server.WaitForRequestAsync();
        await callTask;

        Assert.DoesNotContain("component=", request.Query);
        Assert.DoesNotContain("measure=", request.Query);
        Assert.DoesNotContain("reportingMonth=", request.Query);
        Assert.DoesNotContain("reportingYear=", request.Query);
    }

    [Fact]
    public async System.Threading.Tasks.Task SearchEntriesAsync_EntriesReturned_ReadsThePage()
    {
        const string page = "{\"records\":[{\"id\":\"6f1c\",\"facilityId\":\"100\",\"component\":\"MSC\"," +
                            "\"measure\":\"HOB\",\"reportingMonth\":10,\"reportingYear\":2026," +
                            "\"isReporting\":\"Y\"}]," +
                            "\"metadata\":{\"pageSize\":10,\"pageNumber\":1,\"totalCount\":1,\"totalPages\":1}}";
        using var server = new OneShotServer(page);
        using var client = CreateClient(server.BaseUrl);

        var callTask = client.SearchEntriesAsync(facilityId: "100");
        await server.WaitForRequestAsync();
        var response = await callTask;

        var entry = Assert.Single(response.Body!.Records);
        Assert.Equal("6f1c", entry.Id);
        Assert.Equal("HOB", entry.Measure);
        Assert.Equal(10, entry.ReportingMonth);
        Assert.Equal(1, response.Body.Metadata.TotalCount);
    }

    [Fact]
    public async System.Threading.Tasks.Task CreateEntryAsync_PostsTheEntry()
    {
        using var server = new OneShotServer("{\"id\":\"6f1c\",\"facilityId\":\"100\"}", statusCode: 201);
        using var client = CreateClient(server.BaseUrl);

        var callTask = client.CreateEntryAsync(new MockDmrpEntryRequest
        {
            FacilityId = "100",
            Component = "MSC",
            Measure = "HOB",
            ReportingMonth = 10,
            ReportingYear = 2026
        });
        var request = await server.WaitForRequestAsync();
        var response = await callTask;

        Assert.Equal("POST", request.Method);
        Assert.Equal("/api/mock-dmrp/entries", request.Path);
        var sent = JsonSerializer.Deserialize<MockDmrpEntryRequest>(request.Body,
            new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        Assert.Equal("100", sent.FacilityId);
        Assert.Equal("HOB", sent.Measure);
        Assert.Equal("Y", sent.IsReporting);
        Assert.Equal(201, response.StatusCode);
        Assert.Equal("6f1c", response.Body?.Id);
    }

    [Fact]
    public async System.Threading.Tasks.Task DeleteEntryAsync_DeletesThatEntryOnly()
    {
        using var server = new OneShotServer(string.Empty, statusCode: 204);
        using var client = CreateClient(server.BaseUrl);

        var callTask = client.DeleteEntryAsync("6f1c");
        var request = await server.WaitForRequestAsync();
        await callTask;

        Assert.Equal("DELETE", request.Method);
        Assert.Equal("/api/mock-dmrp/entries/6f1c", request.Path);
    }

    /// <summary>
    /// <c>DELETE /api/mock-dmrp/entries</c> with no id deletes every entry the mock holds, for every
    /// facility. A blank id must never reach the wire.
    /// </summary>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async System.Threading.Tasks.Task DeleteEntryAsync_BlankId_ThrowsWithoutSending(string id)
    {
        using var client = CreateClient("http://127.0.0.1:9");

        await Assert.ThrowsAsync<ArgumentException>(() => client.DeleteEntryAsync(id));
    }

    [Fact]
    public async System.Threading.Tasks.Task DeleteEntriesForFacilityAsync_DeletesThatFacilityOnly()
    {
        using var server = new OneShotServer(string.Empty, statusCode: 204);
        using var client = CreateClient(server.BaseUrl);

        var callTask = client.DeleteEntriesForFacilityAsync("100");
        var request = await server.WaitForRequestAsync();
        await callTask;

        Assert.Equal("DELETE", request.Method);
        Assert.Equal("/api/mock-dmrp/facilities/100/entries", request.Path);
    }

    /// <summary>
    /// A facility id is caller data, so a slash in one must stay inside its segment rather than
    /// reshape the route.
    /// </summary>
    [Fact]
    public async System.Threading.Tasks.Task DeleteEntriesForFacilityAsync_IdWithSlash_KeepsItInOneSegment()
    {
        using var server = new OneShotServer(string.Empty, statusCode: 204);
        using var client = CreateClient(server.BaseUrl);

        var callTask = client.DeleteEntriesForFacilityAsync("a/b");
        var request = await server.WaitForRequestAsync();
        await callTask;

        Assert.Equal("/api/mock-dmrp/facilities/a%2Fb/entries", request.Path);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async System.Threading.Tasks.Task DeleteEntriesForFacilityAsync_BlankFacility_ThrowsWithoutSending(
        string facilityId)
    {
        using var client = CreateClient("http://127.0.0.1:9");

        await Assert.ThrowsAsync<ArgumentException>(() => client.DeleteEntriesForFacilityAsync(facilityId));
    }

    /// <summary>
    /// Callers hold the mock's address as its root, sometimes with a trailing slash, and the client
    /// adds the <c>/api</c> prefix itself.
    /// </summary>
    [Fact]
    public async System.Threading.Tasks.Task SearchEntriesAsync_BaseUrlWithTrailingSlash_ReachesTheSameRoute()
    {
        using var server = new OneShotServer(EmptyPage);
        using var client = CreateClient(server.BaseUrl + "/");

        var callTask = client.SearchEntriesAsync();
        var request = await server.WaitForRequestAsync();
        await callTask;

        Assert.Equal("/api/mock-dmrp/entries/search", request.Path);
    }

    [Fact]
    public async System.Threading.Tasks.Task SearchEntriesAsync_MockDisabled_ReturnsTheStatusInsteadOfThrowing()
    {
        using var server = new OneShotServer("{\"title\":\"Mock DMRP API is disabled\"}", statusCode: 503);
        using var client = CreateClient(server.BaseUrl);

        var callTask = client.SearchEntriesAsync(pageSize: 1);
        await server.WaitForRequestAsync();
        var response = await callTask;

        Assert.Equal(503, response.StatusCode);
        Assert.False(response.IsSuccessStatusCode);
    }

    [Fact]
    public void AddMockDmrpServiceClient_AlreadyRegistered_KeepsTheFirstRegistration()
    {
        var existing = new Mock<IMockDmrpServiceClient>().Object;
        var services = new ServiceCollection();
        services.AddSingleton(existing);

        services.AddMockDmrpServiceClient(_ => "http://127.0.0.1:9");

        using var provider = services.BuildServiceProvider();
        Assert.Same(existing, provider.GetRequiredService<IMockDmrpServiceClient>());
    }

    private static MockDmrpServiceClient CreateClient(string baseUrl, bool anonymous = true)
    {
        var tokenService = new Mock<ICreateSystemToken>();
        tokenService
            .Setup(t => t.ExecuteAsync(It.IsAny<string>(), It.IsAny<int>()))
            .ReturnsAsync(Token);

        var bearerOptions = new BackendAuthenticationServiceExtension.LinkBearerServiceOptions
        {
            AllowAnonymous = anonymous
        };

        return new MockDmrpServiceClient(
            baseUrl,
            Options.Create(bearerOptions),
            Options.Create(new LinkTokenServiceSettings { SigningKey = "test" }),
            tokenService.Object);
    }
}
