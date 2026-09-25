using LantanaGroup.Link.Sdk.Clients;
using LantanaGroup.Link.Shared.Application.Extensions.Security;
using LantanaGroup.Link.Shared.Application.Interfaces.Services.Security.Token;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using LantanaGroup.Link.Shared.Application.Models.Integration.Normalization;
using Microsoft.Extensions.Options;
using Moq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using Task = System.Threading.Tasks.Task;

namespace UnitTests.LinkSdk;

[Trait("Category", "UnitTests")]
public class NormalizationServiceClientTests
{
    [Fact]
    public async Task GetFacilityLocationsAsync_CallsFacilityLocationsEndpoint()
    {
        using var server = new OneShotServer("{\"records\":[{\"locationId\":\"location-1\"}]}");
        using var client = CreateClient(server.BaseUrl);

        var callTask = client.GetFacilityLocationsAsync("facility-1");
        var request = await server.WaitForRequestAsync();
        var result = await callTask;

        Assert.Equal("GET", request.Method);
        Assert.Equal("/api/normalization/facility-locations/facilities/facility-1/locations", request.Path);
        Assert.Equal("location-1", result.Body!.Records[0].LocationId);
    }

    [Fact]
    public async Task GetFacilityLocationAsync_CallsLocationEndpoint()
    {
        using var server = new OneShotServer("{\"facilityId\":\"facility-1\",\"locationId\":\"location-1\"}");
        using var client = CreateClient(server.BaseUrl);

        var callTask = client.GetFacilityLocationAsync("facility-1", "location-1");
        var request = await server.WaitForRequestAsync();
        var result = await callTask;

        Assert.Equal("GET", request.Method);
        Assert.Equal("/api/normalization/facility-locations/facilities/facility-1/locations/location-1", request.Path);
        Assert.Equal("location-1", result.Body!.LocationId);
    }

    [Fact]
    public async Task CreateFacilityLocationAsync_PostsLocationToFacilityEndpoint()
    {
        using var server = new OneShotServer("{\"facilityId\":\"facility-1\",\"locationId\":\"location-1\"}");
        using var client = CreateClient(server.BaseUrl);

        var callTask = client.CreateFacilityLocationAsync("facility-1", new CreateFacilityLocationRequestApiModel
        {
            LocationId = "location-1",
            LocationName = "Main location"
        });
        var request = await server.WaitForRequestAsync();
        await callTask;

        Assert.Equal("POST", request.Method);
        Assert.Equal("/api/normalization/facility-locations/facilities/facility-1/locations", request.Path);
        Assert.Contains("\"LocationId\":\"location-1\"", request.Body);
        Assert.Contains("\"LocationName\":\"Main location\"", request.Body);
    }

    [Fact]
    public async Task SearchFacilityLocationLocalCodeMappingsAsync_SendsSpecifiedFilters()
    {
        var hslocId = Guid.NewGuid();
        using var server = new OneShotServer("{\"records\":[]}");
        using var client = CreateClient(server.BaseUrl);

        var callTask = client.SearchFacilityLocationLocalCodeMappingsAsync(new SearchFacilityLocationLocalCodeMappingsRequestApiModel
        {
            Id = "mapping-1",
            FacilityId = "facility-1",
            LocationId = "location-1",
            LocalCodeSystem = "urn:oid:1.2.3",
            LocalCode = "local-code",
            HSLOCId = hslocId,
            Unmapped = true,
            PageSize = 25,
            PageNumber = 2
        });
        var request = await server.WaitForRequestAsync();
        await callTask;

        Assert.Equal("GET", request.Method);
        Assert.Equal("/api/normalization/hsloc-mappings/search", request.Path);
        Assert.Contains("id=mapping-1", request.Query);
        Assert.Contains("facilityId=facility-1", request.Query);
        Assert.Contains("locationId=location-1", request.Query);
        Assert.Contains("localCodeSystem=urn%3Aoid%3A1.2.3", request.Query);
        Assert.Contains("localCode=local-code", request.Query);
        Assert.Contains($"HSLOCId={hslocId}", request.Query);
        Assert.Contains("unmapped=True", request.Query);
        Assert.Contains("pageSize=25", request.Query);
        Assert.Contains("pageNumber=2", request.Query);
    }

    [Theory]
    [MemberData(nameof(MappingEndpointCalls))]
    public async Task FacilityLocationLocalCodeMappingMethods_CallControllerRoutes(
        string expectedMethod,
        string expectedPath,
        Func<NormalizationServiceClient, Task> invoke)
    {
        using var server = new OneShotServer("{}");
        using var client = CreateClient(server.BaseUrl);

        var callTask = invoke(client);
        var request = await server.WaitForRequestAsync();
        await callTask;

        Assert.Equal(expectedMethod, request.Method);
        Assert.Equal(expectedPath, request.Path);
        if (expectedMethod is "POST" or "PUT")
        {
            Assert.Contains("\"LocalCode\":\"local-code\"", request.Body);
        }
    }

    public static IEnumerable<object[]> MappingEndpointCalls()
    {
        yield return ["GET", "/api/normalization/hsloc-mappings/mapping-1", new Func<NormalizationServiceClient, Task>(async client =>
        {
            await client.GetFacilityLocationLocalCodeMappingAsync("mapping-1");
        })];
        yield return ["POST", "/api/normalization/hsloc-mappings/facilities/facility-1", new Func<NormalizationServiceClient, Task>(async client =>
        {
            await client.CreateFacilityLocationLocalCodeMappingAsync("facility-1", new CreateFacilityLocationLocalCodeMappingRequestApiModel
            {
                LocationId = "location-1",
                LocalCodeSystem = "urn:oid:1.2.3",
                LocalCode = "local-code"
            });
        })];
        yield return ["PUT", "/api/normalization/hsloc-mappings/mapping-1", new Func<NormalizationServiceClient, Task>(async client =>
        {
            await client.UpdateFacilityLocationLocalCodeMappingAsync("mapping-1", new UpdateFacilityLocationLocalCodeMappingRequestApiModel
            {
                LocalCodeSystem = "urn:oid:1.2.3",
                LocalCode = "local-code"
            });
        })];
        yield return ["DELETE", "/api/normalization/hsloc-mappings/mapping-1", new Func<NormalizationServiceClient, Task>(async client =>
        {
            await client.DeleteFacilityLocationLocalCodeMappingAsync("mapping-1");
        })];
        yield return ["DELETE", "/api/normalization/hsloc-mappings/facilities/facility-1", new Func<NormalizationServiceClient, Task>(async client =>
        {
            await client.DeleteFacilityLocationLocalCodeMappingsForFacilityAsync("facility-1");
        })];
    }

    [Fact]
    public async Task GetHslocCodesAsync_CallsCodeSetEndpoint()
    {
        using var server = new OneShotServer("[{\"hslocCode\":\"1025-6\",\"isActive\":true}]");
        using var client = CreateClient(server.BaseUrl);

        var callTask = client.GetHslocCodesAsync();
        var request = await server.WaitForRequestAsync();
        var result = await callTask;

        Assert.Equal("GET", request.Method);
        Assert.Equal("/api/normalization/HSLOC", request.Path);
        Assert.Contains("includeInactive=False", request.Query);
        Assert.Equal("1025-6", result.Body![0].HSLOCCode);
        Assert.True(result.Body[0].IsActive);
    }

    [Fact]
    public async Task GetHslocCodesAsync_IncludeInactive_SendsQueryFlag()
    {
        using var server = new OneShotServer("[]");
        using var client = CreateClient(server.BaseUrl);

        var callTask = client.GetHslocCodesAsync(includeInactive: true);
        var request = await server.WaitForRequestAsync();
        await callTask;

        Assert.Contains("includeInactive=True", request.Query);
    }

    [Fact]
    public async Task UpdateHslocCodesAsync_PutsMultipartCsv()
    {
        using var server = new OneShotServer(string.Empty, 204);
        using var client = CreateClient(server.BaseUrl);
        using var csv = new MemoryStream(Encoding.UTF8.GetBytes("CDCCode,ShortDescription,HSLOCCode,LongDescription\nIN:ACUTE:CC:T,Trauma Critical Care,1025-6,Trauma\n"));

        var callTask = client.UpdateHslocCodesAsync("2022", "2023", csv, "codes.csv");
        var request = await server.WaitForRequestAsync();
        await callTask;

        Assert.Equal("PUT", request.Method);
        Assert.Equal("/api/normalization/HSLOC", request.Path);
        Assert.Contains("OldVersion", request.Body);
        Assert.Contains("2022", request.Body);
        Assert.Contains("NewVersion", request.Body);
        Assert.Contains("2023", request.Body);
        Assert.Contains("CsvFile", request.Body);
        Assert.Contains("1025-6", request.Body);
    }

    [Fact]
    public async Task DeleteAllHslocCodesAsync_DeletesCodeSetEndpoint()
    {
        using var http = new FakeHttpBoundary(string.Empty, 204);
        using var client = CreateClient(http.BaseUrl);

        var result = await client.DeleteAllHslocCodesAsync();
        var request = http.SingleRequest();

        Assert.Equal("DELETE", request.Method);
        Assert.Equal("/api/normalization/HSLOC", request.Path);
        Assert.Equal(204, result.StatusCode);
    }

    private static NormalizationServiceClient CreateClient(string baseUrl) => new(
        Options.Create(new ServiceRegistry { NormalizationServiceUrl = baseUrl }),
        Options.Create(new BackendAuthenticationServiceExtension.LinkBearerServiceOptions { AllowAnonymous = true }),
        Options.Create(new LinkTokenServiceSettings { SigningKey = "test" }),
        new Mock<ICreateSystemToken>().Object);

}