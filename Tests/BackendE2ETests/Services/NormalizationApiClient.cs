using System.Net;
using RestSharp;
using Xunit;
using Xunit.Abstractions;

namespace LantanaGroup.Link.Tests.E2ETests.Services;

public class NormalizationApiClient(RestClient client, ITestOutputHelper output)
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
        Assert.True(response.StatusCode == HttpStatusCode.Created,
            $"Response was not 201 Created {response.StatusCode}: {response.Content}");
    }
}
