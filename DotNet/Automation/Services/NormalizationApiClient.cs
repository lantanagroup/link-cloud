using System.Net;
using LantanaGroup.Link.Automation.Helpers;
using LantanaGroup.Link.Shared.Application.Models.Integration.Normalization;
using LantanaGroup.Link.Sdk.Clients;

namespace LantanaGroup.Link.Automation.Services;

public class NormalizationApiClient(NormalizationServiceClient normalizationClient, IAutomationOutput output)
{
    public async Task CreateConfigAsync(string facilityId)
    {
        if (await NormalizationConfigExistsAsync(facilityId))
        {
            output.WriteLine($"Normalization config for facility '{facilityId}' already exists. Skipping create.");
            return;
        }

        output.WriteLine("Creating normalization config...");

        var body = new CreateNormalizationOperationRequestApiModel
        {
            ResourceTypes = ["Location"],
            FacilityId = facilityId,
            Operation = new CreateNormalizationOperationDetailsApiModel
            {
                OperationType = "CopyProperty",
                Name = "Copy Location Identifier to Type",
                Description = "A Test Operation",
                SourceFhirPath = "identifier.value",
                TargetFhirPath = "type[0].coding.code"
            },
            Description = "Copy Location Identifier to Code",
            VendorIds = []
        };

        var status = await normalizationClient.CreateOperationAsync(body);
        AutomationInvariant.Require(status == HttpStatusCode.Created,
            $"Response was not 201 Created {status}");
    }

    private async Task<bool> NormalizationConfigExistsAsync(string facilityId)
    {
        var (status, response) = await normalizationClient.SearchFacilityOperationsAsync(facilityId);

        if (status == HttpStatusCode.NotFound)
            return false;

        AutomationInvariant.Require(status == HttpStatusCode.OK,
            $"Unexpected status while checking normalization config existence for '{facilityId}': {status}");

        return response?.Records?.Count > 0;
    }
}
