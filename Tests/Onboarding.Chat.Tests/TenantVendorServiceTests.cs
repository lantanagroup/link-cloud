using Xunit;
using System.Net;
using System.Text.Json;
using LantanaGroup.Link.Onboarding.Mcp;
using Microsoft.Extensions.Options;

namespace Onboarding.Chat.Tests;

public sealed class TenantVendorServiceTests
{
    private sealed class Handler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Assert.Equal("Bearer", request.Headers.Authorization?.Scheme);
            Assert.Equal("test-token", request.Headers.Authorization?.Parameter);
            return Task.FromResult(respond(request));
        }
    }

    private static TenantVendorService Service(Func<HttpRequestMessage, HttpResponseMessage> respond) =>
        new(new HttpClient(new Handler(respond)), Options.Create(new OnboardingOptions
        {
            TenantApiBaseUrl = "http://localhost:8080/api/", BearerToken = "test-token"
        }));

    private static HttpResponseMessage Json(string body) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json")
    };

    [Fact]
    public async Task GroupsVersionsAndExcludesAuthentication()
    {
        var vendor = Guid.NewGuid();
        var emptyVendor = Guid.NewGuid();
        var version = Guid.NewGuid();
        var service = Service(request => request.RequestUri!.AbsolutePath switch
        {
            "/api/Vendor" => Json($$$"""[{"id":"{{{vendor}}}","name":"Epic","authentication":{"secret":"sensitive"}},{"id":"{{{emptyVendor}}}","name":"Other"}]"""),
            "/api/VendorVersion" => Json($$$"""[{"id":"{{{version}}}","vendorId":"{{{vendor}}}","version":"default","authentication":{"secret":"sensitive"}}]"""),
            _ => throw new Exception("Unexpected request")
        });
        var result = await service.GetAsync(default);
        Assert.Equal("available", result.Status);
        Assert.Equal(2, result.Vendors.Length);
        Assert.Equal(new TenantVendorVersion(version, "default"), Assert.Single(result.Vendors[0].Versions));
        Assert.Empty(result.Vendors[1].Versions);
        Assert.DoesNotContain("sensitive", JsonSerializer.Serialize(result));
        Assert.DoesNotContain("Authentication", JsonSerializer.Serialize(result));
    }

    [Theory]
    [InlineData("[]", "available")]
    [InlineData("null", "unavailable")]
    [InlineData("invalid", "unavailable")]
    [InlineData("[null]", "unavailable")]
    public async Task DistinguishesEmptyFromInvalidCatalog(string body, string status)
    {
        var result = await Service(_ => Json(body)).GetAsync(default);
        Assert.Equal(status, result.Status);
        Assert.Empty(result.Vendors);
    }

    [Fact]
    public async Task DoesNotReturnPartialCatalogOnVersionFailure()
    {
        var service = Service(request => request.RequestUri!.AbsolutePath.EndsWith("Vendor")
            ? Json($$$"""[{"id":"{{{Guid.NewGuid()}}}","name":"Epic"}]""")
            : new(HttpStatusCode.Forbidden));
        var result = await service.GetAsync(default);
        Assert.Equal("unavailable", result.Status);
        Assert.Empty(result.Vendors);
    }

    [Fact]
    public async Task PropagatesCallerCancellation()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            Service(_ => throw new Exception("Should not request")).GetAsync(cancellation.Token));
    }
}
