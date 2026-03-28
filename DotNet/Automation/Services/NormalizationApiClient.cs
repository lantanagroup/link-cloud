using System.Net;
using System.Text.Json;
using LantanaGroup.Link.Automation.Helpers;
using RestSharp;

namespace LantanaGroup.Link.Automation.Services;

public class NormalizationApiClient(RestClient client, IAutomationOutput output)
{
    public async Task CreateConfigAsync(string facilityId)
    {
        if (await NormalizationConfigExistsAsync(facilityId))
        {
            output.WriteLine($"Normalization config for facility '{facilityId}' already exists. Skipping create.");
            return;
        }

        output.WriteLine("Creating normalization config...");
        var request = new RestRequest("normalization/Operations", Method.Post);

        var body = new
        {
            ResourceTypes = new[] { "Location" },
            FacilityId = facilityId,
            Operation = new
            {
                OperationType = "CopyProperty",
                Name = "Copy Location Identifier to Type",
                Description = "A Test Operation",
                SourceFhirPath = "identifier.value",
                TargetFhirPath = "type[0].coding.code"
            },
            Description = "Copy Location Identifier to Code",
            VendorIds = Array.Empty<string>()
        };

        request.AddJsonBody(body);

        var response = await client.ExecuteAsync(request);
        AutomationInvariant.Require(response.StatusCode == HttpStatusCode.Created,
            $"Response was not 201 Created {response.StatusCode}: {response.Content}");
    }

    private async Task<bool> NormalizationConfigExistsAsync(string facilityId)
    {
        var request = new RestRequest($"normalization/Operations/facility/{facilityId}", Method.Get);
        request.AddQueryParameter("includeDisabled", "true");
        request.AddQueryParameter("pageSize", "1");
        request.AddQueryParameter("pageNumber", "1");

        var response = await client.ExecuteAsync(request);

        if (response.StatusCode == HttpStatusCode.NotFound)
            return false;

        AutomationInvariant.Require(response.StatusCode == HttpStatusCode.OK,
            $"Unexpected status while checking normalization config existence for '{facilityId}': {response.StatusCode} {response.Content}");

        if (string.IsNullOrWhiteSpace(response.Content))
            return false;

        try
        {
            using var doc = JsonDocument.Parse(response.Content);
            if (!doc.RootElement.TryGetProperty("records", out var records) || records.ValueKind != JsonValueKind.Array)
                return false;

            return records.GetArrayLength() > 0;
        }
        catch
        {
            return false;
        }
    }
}
