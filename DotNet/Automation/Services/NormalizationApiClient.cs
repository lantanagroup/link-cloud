using System.Net;
using LantanaGroup.Link.Automation.Helpers;
using RestSharp;

namespace LantanaGroup.Link.Automation.Services;

public class NormalizationApiClient(RestClient client, IAutomationOutput output)
{
    public async Task CreateConfigAsync(string facilityId)
    {
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
}
