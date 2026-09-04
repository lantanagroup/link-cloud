using LantanaGroup.Link.Sdk.Clients;
using LantanaGroup.Link.Shared.Application.Extensions.Security;
using LantanaGroup.Link.Shared.Application.Interfaces.Services.Security.Token;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using LantanaGroup.Link.Shared.Application.Models.Tenant;
using Microsoft.Extensions.Options;
using Moq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

namespace UnitTests.LinkSdk;

[Trait("Category", "UnitTests")]
public class FacilityServiceClientTests
{
    [Fact]
    public async System.Threading.Tasks.Task CreateVendorAsync_PostsVendorAndDeserializesResponse()
    {
        var vendorId = Guid.NewGuid();
        using var server = new OneShotServer($"{{\"id\":\"{vendorId}\",\"name\":\"Acme\"}}");
        using var client = CreateClient(server.BaseUrl);

        var callTask = client.CreateVendorAsync(new CreateVendorModel { Name = "Acme" });
        var request = await server.WaitForRequestAsync();
        var result = await callTask;

        Assert.Equal("POST", request.Method);
        Assert.Equal("/api/Vendor", request.Path);
        Assert.Equal("Acme", GetProperty(request.Body, "Name"));
        Assert.Equal(vendorId, result.Body!.Id.GetValueOrDefault());
        Assert.Equal("Acme", result.Body.Name);
    }

    [Fact]
    public async System.Threading.Tasks.Task GetVendorAsync_CallsVendorEndpoint()
    {
        var vendorId = Guid.NewGuid();
        using var server = new OneShotServer($"{{\"id\":\"{vendorId}\",\"name\":\"Acme\"}}");
        using var client = CreateClient(server.BaseUrl);

        var callTask = client.GetVendorAsync(vendorId);
        var request = await server.WaitForRequestAsync();
        var result = await callTask;

        Assert.Equal("GET", request.Method);
        Assert.Equal($"/api/Vendor/{vendorId}", request.Path);
        Assert.Equal("Acme", result.Body!.Name);
    }

    [Fact]
    public async System.Threading.Tasks.Task GetVendorsAsync_CallsVendorCollectionEndpoint()
    {
        var vendorId = Guid.NewGuid();
        using var server = new OneShotServer($"[{{\"id\":\"{vendorId}\",\"name\":\"Acme\"}}]");
        using var client = CreateClient(server.BaseUrl);

        var callTask = client.GetVendorsAsync();
        var request = await server.WaitForRequestAsync();
        var result = await callTask;

        Assert.Equal("GET", request.Method);
        Assert.Equal("/api/Vendor", request.Path);
        Assert.Single(result.Body!);
        Assert.Equal("Acme", result.Body[0].Name);
    }

    [Fact]
    public async System.Threading.Tasks.Task UpdateVendorAsync_PutsVendorRequest()
    {
        var vendorId = Guid.NewGuid();
        using var server = new OneShotServer($"{{\"id\":\"{vendorId}\",\"name\":\"Updated\"}}");
        using var client = CreateClient(server.BaseUrl);

        var callTask = client.UpdateVendorAsync(vendorId, new UpdateVendorModel { Name = "Updated" });
        var request = await server.WaitForRequestAsync();
        var result = await callTask;

        Assert.Equal("PUT", request.Method);
        Assert.Equal($"/api/Vendor/{vendorId}", request.Path);
        Assert.Equal("Updated", GetProperty(request.Body, "Name"));
        Assert.Equal("Updated", result.Body!.Name);
    }

    [Fact]
    public async System.Threading.Tasks.Task DeleteVendorAsync_DeletesVendor()
    {
        var vendorId = Guid.NewGuid();
        using var server = new OneShotServer(string.Empty, 204);
        using var client = CreateClient(server.BaseUrl);

        var callTask = client.DeleteVendorAsync(vendorId);
        var request = await server.WaitForRequestAsync();
        var result = await callTask;

        Assert.Equal("DELETE", request.Method);
        Assert.Equal($"/api/Vendor/{vendorId}", request.Path);
        Assert.Equal(204, result.StatusCode);
    }

    [Fact]
    public async System.Threading.Tasks.Task CreateVendorVersionAsync_PostsVendorVersionAndDeserializesResponse()
    {
        var vendorId = Guid.NewGuid();
        var vendorVersionId = Guid.NewGuid();
        using var server = new OneShotServer($"{{\"id\":\"{vendorVersionId}\",\"vendorId\":\"{vendorId}\",\"version\":\"2026.1\"}}");
        using var client = CreateClient(server.BaseUrl);

        var callTask = client.CreateVendorVersionAsync(new CreateVendorVersionModel { VendorId = vendorId, Version = "2026.1" });
        var request = await server.WaitForRequestAsync();
        var result = await callTask;

        Assert.Equal("POST", request.Method);
        Assert.Equal("/api/VendorVersion", request.Path);
        Assert.Equal(vendorId.ToString(), GetProperty(request.Body, "VendorId"));
        Assert.Equal("2026.1", GetProperty(request.Body, "Version"));
        Assert.Equal(vendorVersionId, result.Body!.Id.GetValueOrDefault());
        Assert.Equal(vendorId, result.Body.VendorId.GetValueOrDefault());
    }

    [Fact]
    public async System.Threading.Tasks.Task GetVendorVersionAsync_CallsVendorVersionEndpoint()
    {
        var vendorId = Guid.NewGuid();
        var vendorVersionId = Guid.NewGuid();
        using var server = new OneShotServer($"{{\"id\":\"{vendorVersionId}\",\"vendorId\":\"{vendorId}\",\"version\":\"2026.1\"}}");
        using var client = CreateClient(server.BaseUrl);

        var callTask = client.GetVendorVersionAsync(vendorVersionId);
        var request = await server.WaitForRequestAsync();
        var result = await callTask;

        Assert.Equal("GET", request.Method);
        Assert.Equal($"/api/VendorVersion/{vendorVersionId}", request.Path);
        Assert.Equal("2026.1", result.Body!.Version);
    }

    [Fact]
    public async System.Threading.Tasks.Task GetVendorVersionsAsync_CallsVendorVersionCollectionEndpointWithoutFilter()
    {
        using var server = new OneShotServer("[]");
        using var client = CreateClient(server.BaseUrl);

        var callTask = client.GetVendorVersionsAsync();
        var request = await server.WaitForRequestAsync();
        var result = await callTask;

        Assert.Equal("GET", request.Method);
        Assert.Equal("/api/VendorVersion", request.Path);
        Assert.Equal(string.Empty, request.Query);
        Assert.Empty(result.Body!);
    }

    [Fact]
    public async System.Threading.Tasks.Task GetVendorVersionsAsync_UsesVendorIdFilter()
    {
        var vendorId = Guid.NewGuid();
        using var server = new OneShotServer("[]");
        using var client = CreateClient(server.BaseUrl);

        var callTask = client.GetVendorVersionsAsync(vendorId);
        var request = await server.WaitForRequestAsync();
        await callTask;

        Assert.Equal("GET", request.Method);
        Assert.Equal("/api/VendorVersion", request.Path);
        Assert.Equal($"?vendorId={vendorId}", request.Query);
    }

    [Fact]
    public async System.Threading.Tasks.Task UpdateVendorVersionAsync_PutsVendorVersionRequest()
    {
        var vendorId = Guid.NewGuid();
        var vendorVersionId = Guid.NewGuid();
        using var server = new OneShotServer($"{{\"id\":\"{vendorVersionId}\",\"vendorId\":\"{vendorId}\",\"version\":\"2026.2\"}}");
        using var client = CreateClient(server.BaseUrl);

        var callTask = client.UpdateVendorVersionAsync(vendorVersionId, new UpdateVendorVersionModel { Version = "2026.2" });
        var request = await server.WaitForRequestAsync();
        var result = await callTask;

        Assert.Equal("PUT", request.Method);
        Assert.Equal($"/api/VendorVersion/{vendorVersionId}", request.Path);
        Assert.Equal("2026.2", GetProperty(request.Body, "Version"));
        Assert.Equal("2026.2", result.Body!.Version);
    }

    [Fact]
    public async System.Threading.Tasks.Task DeleteVendorVersionAsync_DeletesVendorVersion()
    {
        var vendorVersionId = Guid.NewGuid();
        using var server = new OneShotServer(string.Empty, 204);
        using var client = CreateClient(server.BaseUrl);

        var callTask = client.DeleteVendorVersionAsync(vendorVersionId);
        var request = await server.WaitForRequestAsync();
        var result = await callTask;

        Assert.Equal("DELETE", request.Method);
        Assert.Equal($"/api/VendorVersion/{vendorVersionId}", request.Path);
        Assert.Equal(204, result.StatusCode);
    }

    private static FacilityServiceClient CreateClient(string baseUrl)
    {
        return new FacilityServiceClient(
            Options.Create(new ServiceRegistry
            {
                TenantService = new TenantServiceRegistration { TenantServiceUrl = baseUrl }
            }),
            CreateBearerOptions(),
            CreateTokenSettings(),
            new Mock<ICreateSystemToken>().Object);
    }

    private static IOptions<BackendAuthenticationServiceExtension.LinkBearerServiceOptions> CreateBearerOptions() =>
        Options.Create(new BackendAuthenticationServiceExtension.LinkBearerServiceOptions { AllowAnonymous = true });

    private static IOptions<LinkTokenServiceSettings> CreateTokenSettings() =>
        Options.Create(new LinkTokenServiceSettings { SigningKey = "test" });

    private static string? GetProperty(string body, string propertyName)
    {
        using var document = JsonDocument.Parse(body);
        return document.RootElement.GetProperty(propertyName).GetString();
    }

}