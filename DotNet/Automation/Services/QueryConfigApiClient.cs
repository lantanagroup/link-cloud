using System.Net;
using LantanaGroup.Link.Automation.Configuration;
using LantanaGroup.Link.Automation.Helpers;
using Newtonsoft.Json.Linq;
using RestSharp;

namespace LantanaGroup.Link.Automation.Services;

public class QueryConfigApiClient
{
    private readonly RestClient _client;
    private readonly IAutomationOutput _output;
    private readonly AutomationConfig _config;

    public QueryConfigApiClient(RestClient client, IAutomationOutput output, AutomationConfig config)
    {
        _client = client;
        _output = output;
        _config = config;
    }

    public async Task CreateQueryConfigAsync(string facilityId)
    {
        _output.WriteLine("Creating query config...");
        var request = new RestRequest("data/fhirQueryConfiguration", Method.Post);
        var body = new JObject
        {
            ["FacilityId"] = facilityId,
            ["FhirServerBaseUrl"] = _config.InternalFhirServerBase,
            ["MaxConcurrentRequests"] = _config.FhirQuery.MaxConcurrentRequests,
            ["MaxRetries"] = 3
        };
        request.AddJsonBody(body.ToString(), "application/json");

        var response = await _client.ExecuteAsync(request);
        if (response.StatusCode != HttpStatusCode.Created)
            _output.WriteLine($"Expected HTTP 201 Created but received {response.StatusCode}: {response.Content}");
        AutomationInvariant.Require(response.StatusCode == HttpStatusCode.Created,
            $"Expected HTTP 201 Created but received {response.StatusCode}: {response.Content}");
    }

    public async Task CreateQueryPlanAsync(string facilityId, string? measureId, string ehrDescription)
    {
        _output.WriteLine("Creating query plan...");

        await PostQueryPlan(facilityId, measureId, ehrDescription, "Discharge");
        await PostQueryPlan(facilityId, measureId, ehrDescription, "Monthly");
    }

    private async Task PostQueryPlan(string facilityId, string? measureId, string ehrDescription, string type)
    {
        var body = QueryPlanBuilder.BuildQueryPlan(facilityId, measureId, ehrDescription, type);
        var request = new RestRequest($"data/{facilityId}/QueryPlan", Method.Post);
        request.AddJsonBody(body.ToString(), "application/json");

        var response = await _client.ExecuteAsync(request);
        if (response.StatusCode != HttpStatusCode.Created)
            _output.WriteLine($"Expected HTTP 201 Created for {type} query plan but received {response.StatusCode}: {response.Content}");
        AutomationInvariant.Require(response.StatusCode == HttpStatusCode.Created,
            $"Expected HTTP 201 Created for {type} query plan but received {response.StatusCode}: {response.Content}");
    }
}
