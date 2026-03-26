using System.Net;
using RestSharp;
using Xunit;
using Xunit.Abstractions;

namespace LantanaGroup.Link.Tests.E2ETests.Services;

public class FacilityApiClient(RestClient client, ITestOutputHelper output)
{
    public async Task<RestResponse> CreateAsync(string facilityId, string? measure)
    {
        output.WriteLine("Creating facility...");

        var response = await SendCreateRequestAsync(facilityId, measure);

        // If the facility already exists (e.g., leftover from a previous failed run),
        // delete it and retry so the test can proceed cleanly.
        if (response.StatusCode == HttpStatusCode.BadRequest)
        {
            output.WriteLine($"Facility creation returned BadRequest — attempting cleanup and retry. Response: {response.Content}");
            await DeleteAsync(facilityId);
            response = await SendCreateRequestAsync(facilityId, measure);
        }

        Assert.True(response.StatusCode == HttpStatusCode.Created,
            $"Expected HTTP 201 Created for facility creation but got {response.StatusCode}: {response.Content}");

        return response;
    }

    private async Task<RestResponse> SendCreateRequestAsync(string facilityId, string? measure)
    {
        var request = new RestRequest("/Facility", Method.Post);
        request.AddHeader("Content-Type", "application/json");

        var body = new
        {
            FacilityId = facilityId,
            FacilityName = facilityId,
            TimeZone = "America/Chicago",
            ScheduledReports = new
            {
                monthly = measure != null ? new[] { measure } : Array.Empty<string>(),
                daily = Array.Empty<string>(),
                weekly = Array.Empty<string>()
            }
        };

        request.AddJsonBody(body);
        return await client.ExecuteAsync(request);
    }

    public async Task DeleteAsync(string facilityId)
    {
        await Task.WhenAll(
            DeleteNormalizationAsync(facilityId),
            DeleteQueryPlanAsync(facilityId),
            DeleteQueryConfigAsync(facilityId)
        );

        output.WriteLine("Deleting facility...");
        var request = new RestRequest($"/Facility/{facilityId}", Method.Delete);
        var response = await client.ExecuteAsync(request);

        if (response.StatusCode != HttpStatusCode.NoContent)
            output.WriteLine($"Expected HTTP 204 No Content for facility deletion but received {response.StatusCode}: {response.Content}");
    }

    private async Task DeleteNormalizationAsync(string facilityId)
    {
        output.WriteLine("Deleting facility normalization...");
        var request = new RestRequest($"/normalization/operations/facility/{facilityId}", Method.Delete);
        var response = await client.ExecuteAsync(request);

        if (response.StatusCode != HttpStatusCode.NoContent)
            output.WriteLine($"Expected HTTP 204 No Content for normalization deletion but received {response.StatusCode}: {response.Content}");
    }

    private async Task DeleteQueryPlanAsync(string facilityId)
    {
        output.WriteLine("Deleting facility discharge query plan...");
        var request = new RestRequest($"/data/{facilityId}/QueryPlan?type=Discharge", Method.Delete);
        var response = await client.ExecuteAsync(request);

        if (response.StatusCode != HttpStatusCode.Accepted)
            output.WriteLine($"Expected HTTP 202 Accepted for discharge query plan deletion but received {response.StatusCode}: {response.Content}");

        output.WriteLine("Deleting facility monthly query plan...");
        request = new RestRequest($"/data/{facilityId}/QueryPlan?type=Monthly", Method.Delete);
        response = await client.ExecuteAsync(request);

        if (response.StatusCode != HttpStatusCode.Accepted)
            output.WriteLine($"Expected HTTP 202 Accepted for monthly query plan deletion but received {response.StatusCode}: {response.Content}");
    }

    private async Task DeleteQueryConfigAsync(string facilityId)
    {
        output.WriteLine("Deleting facility query config...");
        var request = new RestRequest($"/data/{facilityId}/fhirQueryConfiguration", Method.Delete);
        var response = await client.ExecuteAsync(request);

        if (response.StatusCode != HttpStatusCode.Accepted)
            output.WriteLine($"Expected HTTP 202 Accepted for query config deletion but received {response.StatusCode}: {response.Content}");
    }
}
