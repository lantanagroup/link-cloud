using System.Net;
using LantanaGroup.Link.Automation.Helpers;
using LantanaGroup.Link.Sdk.Clients;
using LantanaGroup.Link.Shared.Application.Models.Tenant;

namespace LantanaGroup.Link.Automation.Services;

public class FacilityApiClient(
    FacilityServiceClient facilityClient,
    NormalizationServiceClient normalizationClient,
    DataAcquisitionServiceClient dataAcqClient,
    IAutomationOutput output)
{
    public async Task<HttpStatusCode> CreateAsync(string facilityId, string? measure)
    {
        output.WriteLine("Creating facility...");

        var existingFacilityStatus = await facilityClient.GetAsync(facilityId);
        if (existingFacilityStatus == HttpStatusCode.OK)
        {
            output.WriteLine($"Facility '{facilityId}' already exists. Skipping create.");
            return existingFacilityStatus;
        }

        var responseStatus = await SendCreateRequestAsync(facilityId, measure);

        if (responseStatus == HttpStatusCode.BadRequest)
        {
            existingFacilityStatus = await facilityClient.GetAsync(facilityId);
            if (existingFacilityStatus == HttpStatusCode.OK)
            {
                output.WriteLine($"Facility '{facilityId}' already exists (detected after create attempt). Skipping create.");
                return existingFacilityStatus;
            }

            output.WriteLine("Facility creation returned BadRequest — attempting cleanup and retry.");
            await DeleteAsync(facilityId);
            responseStatus = await SendCreateRequestAsync(facilityId, measure);
        }

        AutomationInvariant.Require(responseStatus == HttpStatusCode.Created,
            $"Expected HTTP 201 Created for facility creation but got {responseStatus}");

        return responseStatus;
    }

    private async Task<HttpStatusCode> SendCreateRequestAsync(string facilityId, string? measure)
    {
        var body = new FacilityModel
        {
            FacilityId = facilityId,
            FacilityName = facilityId,
            TimeZone = "America/Chicago",
            ScheduledReports = new TenantScheduledReportConfig
            {
                Monthly = measure != null ? [measure] : [],
                Daily = [],
                Weekly = []
            }
        };

        return await facilityClient.CreateAsync(body);
    }

    public async Task DeleteAsync(string facilityId)
    {
        await Task.WhenAll(
            DeleteNormalizationAsync(facilityId),
            DeleteQueryPlanAsync(facilityId),
            DeleteQueryConfigAsync(facilityId)
        );

        output.WriteLine("Deleting facility...");
        var status = await facilityClient.DeleteAsync(facilityId);

        if (status != HttpStatusCode.NoContent)
            output.WriteLine($"Expected HTTP 204 No Content for facility deletion but received {status}");
    }

    private async Task DeleteNormalizationAsync(string facilityId)
    {
        output.WriteLine("Deleting facility normalization...");
        var status = await normalizationClient.DeleteFacilityOperationsAsync(facilityId);

        if (status != HttpStatusCode.NoContent)
            output.WriteLine($"Expected HTTP 204 No Content for normalization deletion but received {status}");
    }

    private async Task DeleteQueryPlanAsync(string facilityId)
    {
        output.WriteLine("Deleting facility discharge query plan...");
        var status = await dataAcqClient.DeleteQueryPlanAsync(facilityId, "Discharge");

        if (status != HttpStatusCode.Accepted)
            output.WriteLine($"Expected HTTP 202 Accepted for discharge query plan deletion but received {status}");

        output.WriteLine("Deleting facility monthly query plan...");
        status = await dataAcqClient.DeleteQueryPlanAsync(facilityId, "Monthly");

        if (status != HttpStatusCode.Accepted)
            output.WriteLine($"Expected HTTP 202 Accepted for monthly query plan deletion but received {status}");
    }

    private async Task DeleteQueryConfigAsync(string facilityId)
    {
        output.WriteLine("Deleting facility query config...");
        var status = await dataAcqClient.DeleteFhirQueryConfigurationAsync(facilityId);

        if (status != HttpStatusCode.Accepted)
            output.WriteLine($"Expected HTTP 202 Accepted for query config deletion but received {status}");
    }
}
