using System.Net;
using LantanaGroup.Link.Tests.E2ETests.Helpers;
using Newtonsoft.Json.Linq;
using RestSharp;
using Xunit;
using Xunit.Abstractions;

namespace LantanaGroup.Link.Tests.E2ETests.Services;

public class QueryConfigApiClient(RestClient client, ITestOutputHelper output)
{
    public async Task CreateQueryConfigAsync(string facilityId)
    {
        output.WriteLine("Creating query config...");
        var request = new RestRequest("data/fhirQueryConfiguration", Method.Post);
        var body = new JObject
        {
            ["FacilityId"] = facilityId,
            ["FhirServerBaseUrl"] = TestConfig.InternalFhirServerBase,
            ["MaxConcurrentRequests"] = TestConfig.FhirQueryConfig.MaxConcurrentRequests,
            ["MaxRetries"] = 3
        };
        request.AddJsonBody(body.ToString(), "application/json");

        var response = await client.ExecuteAsync(request);
        if (response.StatusCode != HttpStatusCode.Created)
            output.WriteLine($"Expected HTTP 201 Created but received {response.StatusCode}: {response.Content}");
        Assert.True(response.StatusCode == HttpStatusCode.Created,
            $"Expected HTTP 201 Created but received {response.StatusCode}: {response.Content}");
    }

    public async Task CreateQueryPlanAsync(string facilityId, string? measureId, string ehrDescription)
    {
        output.WriteLine("Creating query plan...");

        await PostQueryPlan(facilityId, measureId, ehrDescription, "Discharge");
        await PostQueryPlan(facilityId, measureId, ehrDescription, "Monthly");
    }

    private async Task PostQueryPlan(string facilityId, string? measureId, string ehrDescription, string type)
    {
        var body = QueryPlanBuilder.BuildQueryPlan(facilityId, measureId, ehrDescription, type);
        var request = new RestRequest($"data/{facilityId}/QueryPlan", Method.Post);
        request.AddJsonBody(body.ToString(), "application/json");

        var response = await client.ExecuteAsync(request);
        if (response.StatusCode != HttpStatusCode.Created)
            output.WriteLine($"Expected HTTP 201 Created for {type} query plan but received {response.StatusCode}: {response.Content}");
        Assert.True(response.StatusCode == HttpStatusCode.Created,
            $"Expected HTTP 201 Created for {type} query plan but received {response.StatusCode}: {response.Content}");
    }
}
