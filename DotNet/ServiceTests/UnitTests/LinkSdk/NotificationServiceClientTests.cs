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
public class NotificationServiceClientTests
{
    [Fact]
    public void Constructor_WithoutUrl_NamesNotification()
    {
        var error = Assert.Throws<InvalidOperationException>(() => CreateClient(null));
        Assert.Contains("Notification", error.Message);
    }

    [Fact]
    public async Task SearchNotificationsAsync_FiltersByFacility()
    {
        using var http = new FakeHttpBoundary("{\"records\":[],\"metadata\":{\"pageSize\":10,\"pageNumber\":1,\"totalCount\":0,\"totalPages\":0}}");
        using var client = CreateClient(http.BaseUrl);

        await client.SearchNotificationsAsync(facilityId: "link-ui", searchText: "ready", sortBy: "CreatedOn", sortOrder: "Descending");
        var request = http.SingleRequest();

        Assert.Equal("GET", request.Method);
        Assert.Equal("/api/notification", request.Path);
        Assert.Contains("filterFacilityBy=link-ui", request.Query);
        Assert.DoesNotContain("facilityId=", request.Query);
        Assert.Contains("searchText=ready", request.Query);
    }

    [Fact]
    public async Task Configuration_UsesConfigurationRoutes()
    {
        using var http = new FakeHttpBoundary("{}", 204);
        using var client = CreateClient(http.BaseUrl);
        var id = "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee";

        await client.GetConfigurationAsync(id);
        var get = http.SingleRequest();
        Assert.Equal("GET", get.Method);
        Assert.Equal("/api/notification/configuration/" + id, get.Path);
    }

    [Fact]
    public async Task DeleteConfigurationAsync_DeletesById()
    {
        using var http = new FakeHttpBoundary(string.Empty, 204);
        using var client = CreateClient(http.BaseUrl);

        await client.DeleteConfigurationAsync("bbbbbbbb-bbbb-cccc-dddd-eeeeeeeeeeee");
        var request = http.SingleRequest();

        Assert.Equal("DELETE", request.Method);
        Assert.Equal("/api/notification/configuration/bbbbbbbb-bbbb-cccc-dddd-eeeeeeeeeeee", request.Path);
    }

    [Fact]
    public async Task CreateNotificationAsync_PostsTheAdminBody()
    {
        using var http = new FakeHttpBoundary("{\"id\":\"8d8c6e5a-1b2c-4d3e-9f70-1234567890ab\"}", 201);
        using var client = CreateClient(http.BaseUrl);

        await client.CreateNotificationAsync(new NotificationMessageApiModel
        {
            NotificationType = "Test Notification",
            Subject = "Hello",
            Body = "Body",
            Recipients = ["a@example.com"],
            Bcc = []
        });
        var request = http.SingleRequest();

        Assert.Equal("POST", request.Method);
        Assert.Equal("/api/notification", request.Path);
        Assert.Contains("\"notificationType\":\"Test Notification\"", request.Body);
        Assert.Contains("\"recipients\":[\"a@example.com\"]", request.Body);
        Assert.Contains("\"bcc\":[]", request.Body);
        Assert.DoesNotContain("facilityId", request.Body);
        Assert.DoesNotContain("NotificationType", request.Body);
    }

    private static NotificationServiceClient CreateClient(string? baseUrl) => new(
        Options.Create(new ServiceRegistry { NotificationServiceUrl = baseUrl }),
        Options.Create(new BackendAuthenticationServiceExtension.LinkBearerServiceOptions { AllowAnonymous = true }),
        Options.Create(new LinkTokenServiceSettings { SigningKey = "test" }),
        new Mock<ICreateSystemToken>().Object);
}
