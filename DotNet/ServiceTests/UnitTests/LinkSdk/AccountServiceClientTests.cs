using LantanaGroup.Link.Sdk.Clients;
using LantanaGroup.Link.Shared.Application.Extensions.Security;
using LantanaGroup.Link.Shared.Application.Interfaces.Services.Security.Token;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using Microsoft.Extensions.Options;
using Moq;
using System.Text.Json;

namespace UnitTests.LinkSdk;

[Trait("Category", "UnitTests")]
public class AccountServiceClientTests
{
    [Fact]
    public async System.Threading.Tasks.Task SearchUsersAsync_CallsTheAccountUserSearch()
    {
        using var server = new OneShotServer("""{"records":[],"metadata":{"pageSize":10,"pageNumber":2,"totalCount":0,"totalPages":0}}""");
        using var client = CreateClient(server.BaseUrl);

        var callTask = client.SearchUsersAsync("ada", null, "Reader", null, false, true, 10, 2);
        var request = await server.WaitForRequestAsync();
        var result = await callTask;

        Assert.Equal("GET", request.Method);
        Assert.Equal("/api/account/users", request.Path);
        Assert.Contains("searchText=ada", request.Query, StringComparison.Ordinal);
        Assert.Contains("filterRoleBy=Reader", request.Query, StringComparison.Ordinal);
        Assert.Contains("includeDeactivatedUsers=false", request.Query, StringComparison.Ordinal);
        Assert.Contains("includeDeletedUsers=true", request.Query, StringComparison.Ordinal);
        Assert.Contains("pageNumber=2", request.Query, StringComparison.Ordinal);
        Assert.DoesNotContain("filterFacilityBy", request.Query, StringComparison.Ordinal);
        Assert.NotNull(result.Body);
        Assert.Empty(result.Body.Records);
    }

    [Fact]
    public async System.Threading.Tasks.Task CreateUserAsync_PostsTheAccountUser()
    {
        using var server = new OneShotServer("""{"id":"aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa","email":"ada@example.com"}""", 201);
        using var client = CreateClient(server.BaseUrl);

        var callTask = client.CreateUserAsync(new AccountUserApiModel
        {
            Username = "ada",
            Email = "ada@example.com",
            Roles = ["Reader"],
            UserClaims = []
        });
        var request = await server.WaitForRequestAsync();
        var result = await callTask;

        Assert.Equal("POST", request.Method);
        Assert.Equal("/api/account/user", request.Path);
        Assert.Equal("ada@example.com", GetProperty(request.Body, "Email"));
        Assert.Equal("ada@example.com", result.Body!.Email);
    }

    [Fact]
    public async System.Threading.Tasks.Task UpdateUserAsync_PutsTheAccountUser()
    {
        var id = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        using var server = new OneShotServer("", 204);
        using var client = CreateClient(server.BaseUrl);

        var callTask = client.UpdateUserAsync(id, new AccountUserApiModel { Id = id, Email = "ada@example.com", UserClaims = ["CanViewLogs"] });
        var request = await server.WaitForRequestAsync();
        var result = await callTask;

        Assert.Equal("PUT", request.Method);
        Assert.Equal($"/api/account/user/{id}", request.Path);
        Assert.Equal(204, result.StatusCode);
        using var document = JsonDocument.Parse(request.Body);
        Assert.Equal("CanViewLogs", document.RootElement.GetProperty("UserClaims")[0].GetString());
    }

    [Fact]
    public async System.Threading.Tasks.Task DeleteUserAsync_DeletesTheAccountUser()
    {
        var id = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        using var server = new OneShotServer("", 204);
        using var client = CreateClient(server.BaseUrl);

        var callTask = client.DeleteUserAsync(id);
        var request = await server.WaitForRequestAsync();
        var result = await callTask;

        Assert.Equal("DELETE", request.Method);
        Assert.Equal($"/api/account/user/{id}", request.Path);
        Assert.Equal(204, result.StatusCode);
    }

    [Fact]
    public async System.Threading.Tasks.Task GetRolesAsync_CallsTheRoleCollection()
    {
        using var server = new OneShotServer("""[{"id":"bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb","name":"Reader"}]""");
        using var client = CreateClient(server.BaseUrl);

        var callTask = client.GetRolesAsync();
        var request = await server.WaitForRequestAsync();
        var result = await callTask;

        Assert.Equal("GET", request.Method);
        Assert.Equal("/api/account/role", request.Path);
        Assert.Equal("Reader", result.Body![0].Name);
    }

    private static AccountServiceClient CreateClient(string baseUrl) => new(
        Options.Create(new ServiceRegistry { AccountServiceUrl = baseUrl }),
        Options.Create(new BackendAuthenticationServiceExtension.LinkBearerServiceOptions { AllowAnonymous = true }),
        Options.Create(new LinkTokenServiceSettings { SigningKey = "test" }),
        new Mock<ICreateSystemToken>().Object);

    private static string? GetProperty(string body, string propertyName)
    {
        using var document = JsonDocument.Parse(body);
        return document.RootElement.GetProperty(propertyName).GetString();
    }
}
