using System.Net;
using System.Text.Json;
using Flurl.Http;
using Flurl.Http.Configuration;
using Flurl.Http.Testing;
using LantanaGroup.Link.Onboarding.Mcp;
using LantanaGroup.Link.Sdk.Clients;
using LantanaGroup.Link.Shared.Application.Extensions.Security;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using Microsoft.Extensions.Options;
using Xunit;

namespace Onboarding.Chat.Tests;

public sealed class OnboardingSdkTests
{
    private sealed class Handler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            send(request, cancellationToken);
    }

    [Theory]
    [InlineData("data")]
    [InlineData("data-acquisition")]
    public async Task ConfiguredDataAcquisitionClientUsesFullServiceUrlAndToken(string route)
    {
        using var client = new DataAcquisitionServiceClient(Transport(async (request, token) =>
        {
            Assert.Equal($"/api/{route}/fhirQueryConfiguration", request.RequestUri!.AbsolutePath);
            Assert.Equal("Bearer test-token", request.Headers.Authorization?.ToString());
            using var json = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(token));
            Assert.Equal("facility", json.RootElement.GetProperty("facilityId").GetString());
            return new(HttpStatusCode.Accepted) { Content = new StringContent("{}") };
        }, $"http://localhost/api/{route}/"));

        var response = await client.UpdateFhirQueryConfigurationAsync(new
        {
            facilityId = "facility", authentication = new { password = "private" }
        });

        Assert.Equal(202, response.StatusCode);
        Assert.Null(response.RequestBody);
    }

    [Fact]
    public async Task ServiceRegistryClientRetainsStandardRoute()
    {
        using var http = new HttpTest();
        http.RespondWith("{}", 200);
        using var client = new DataAcquisitionServiceClient(
            Options.Create(new ServiceRegistry { DataAcquisitionServiceUrl = "http://localhost" }),
            Options.Create(new BackendAuthenticationServiceExtension.LinkBearerServiceOptions { AllowAnonymous = true }),
            Options.Create(new LinkTokenServiceSettings()), null!);

        await client.GetFhirQueryConfigurationAsync("facility");

        http.ShouldHaveCalled("http://localhost/api/data-acquisition/facility/fhirQueryConfiguration").Times(1);
    }

    [Theory]
    [InlineData("create")]
    [InlineData("vendors")]
    [InlineData("read")]
    [InlineData("save")]
    [InlineData("validate")]
    public async Task CallerCancellationDuringSdkRequestPropagates(string operation)
    {
        using var cancellation = new CancellationTokenSource();
        Task<HttpResponseMessage> Cancel(HttpRequestMessage request, CancellationToken token)
        {
            cancellation.Cancel();
            token.ThrowIfCancellationRequested();
            throw new InvalidOperationException("The SDK did not forward cancellation.");
        }
        using var facilityClient = new FacilityServiceClient(Transport(Cancel));
        using var acquisitionClient = new DataAcquisitionServiceClient(Transport(Cancel));
        var acquisition = new DataAcquisitionConfigurationService(facilityClient, acquisitionClient);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
        {
            switch (operation)
            {
                case "create":
                    await new CreateFacilityService(facilityClient).CreateAsync("facility", "Facility", "America/New_York", "", cancellation.Token);
                    break;
                case "vendors":
                    await new TenantVendorService(facilityClient).GetAsync(cancellation.Token);
                    break;
                case "read":
                    await acquisition.GetAsync("facility", cancellation.Token);
                    break;
                case "save":
                    await acquisition.SaveAsync("facility", "https://ehr.example/fhir", -1, -1, "", "", cancellation.Token);
                    break;
                case "validate":
                    await acquisition.ValidateConnectionAsync("https://ehr.example/fhir", cancellation.Token);
                    break;
            }
        });
    }

    [Fact]
    public async Task MissingConnectionFlagIsUnavailableRatherThanDefaultFalse()
    {
        using var facilityClient = new FacilityServiceClient(Transport((_, _) => throw new InvalidOperationException()));
        using var acquisitionClient = new DataAcquisitionServiceClient(Transport((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{}") })));
        var service = new DataAcquisitionConfigurationService(facilityClient, acquisitionClient);

        var result = await service.ValidateConnectionAsync("https://ehr.example/fhir", default);

        Assert.Equal("unavailable", result.Status);
        Assert.Null(result.IsConnected);
    }

    private static IFlurlClient Transport(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send,
        string baseUrl = "http://localhost/api/")
    {
        var client = new FlurlClient(new HttpClient(new Handler(send)) { BaseAddress = new Uri(baseUrl) });
        client.Settings.JsonSerializer = new DefaultJsonSerializer(new JsonSerializerOptions(JsonSerializerDefaults.Web));
        client.WithOAuthBearerToken("test-token");
        return client;
    }
}
