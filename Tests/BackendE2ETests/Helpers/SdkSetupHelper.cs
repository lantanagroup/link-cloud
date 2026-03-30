using Flurl.Http;
using LantanaGroup.Link.Automation.Configuration;
using LantanaGroup.Link.Automation.Helpers;
using LantanaGroup.Link.Shared.Application.Models.Integration.DataAcquisition;
using LantanaGroup.Link.Shared.Application.Models.Integration.Normalization;
using LantanaGroup.Link.Shared.Application.Models.Tenant;

namespace LantanaGroup.Link.Tests.E2ETests;

internal static class SdkSetupHelper
{
    public static async Task EnsureFacilityAsync(TestServices b, string facilityId, string? measureId)
    {
        try
        {
            await b.FacilityClient.GetAsync(facilityId);
            b.Output.WriteLine($"Facility '{facilityId}' already exists. Skipping create.");
            return;
        }
        catch (FlurlHttpException ex) when (ex.StatusCode == 404)
        {
        }

        await b.FacilityClient.CreateAsync(new FacilityModel
        {
            FacilityId = facilityId,
            FacilityName = facilityId,
            TimeZone = "America/Chicago",
            ScheduledReports = new TenantScheduledReportConfig
            {
                Monthly = measureId != null ? [measureId] : [],
                Daily = [],
                Weekly = []
            }
        });
    }

    public static async Task EnsureNormalizationConfigAsync(TestServices b, string facilityId)
    {
        try
        {
            var response = await b.NormalizationClient.SearchFacilityOperationsAsync(facilityId);
            if (response?.Records?.Count > 0)
            {
                b.Output.WriteLine($"Normalization config for facility '{facilityId}' already exists. Skipping create.");
                return;
            }
        }
        catch (FlurlHttpException ex) when (ex.StatusCode == 404)
        {
        }

        await b.NormalizationClient.CreateOperationAsync(new CreateNormalizationOperationRequestApiModel
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
        });
    }

    public static async Task EnsureQueryPlansAsync(TestServices b, string facilityId, string? measureId, string ehrDescription)
    {
        await EnsureQueryPlanAsync(b, facilityId, measureId, ehrDescription, "Discharge");
        await EnsureQueryPlanAsync(b, facilityId, measureId, ehrDescription, "Monthly");
    }

    public static async Task EnsureQueryConfigAsync(TestServices b, string facilityId)
    {
        try
        {
            await b.DataAcquisitionClient.CreateFhirQueryConfigurationAsync(new CreateFhirQueryConfigurationRequestApiModel
            {
                FacilityId = facilityId,
                FhirServerBaseUrl = b.AutomationCfg.InternalFhirServerBase,
                MaxConcurrentRequests = b.AutomationCfg.FhirQuery.MaxConcurrentRequests,
                MaxRetries = 3
            });
        }
        catch (FlurlHttpException ex)
        {
            if (await IsAlreadyExistsAsync(ex))
            {
                b.Output.WriteLine($"Query config for facility '{facilityId}' already exists. Skipping create.");
                return;
            }

            throw;
        }
    }

    public static async Task CleanupFacilityAsync(TestServices b, string facilityId)
    {
        b.Output.WriteLine("Cleaning up...");

        await TryDelete(async () => await b.NormalizationClient.DeleteFacilityOperationsAsync(facilityId), b.Output, "Normalization deletion");
        await TryDelete(async () => await b.DataAcquisitionClient.DeleteQueryPlanAsync(facilityId, "Discharge"), b.Output, "Discharge query plan deletion");
        await TryDelete(async () => await b.DataAcquisitionClient.DeleteQueryPlanAsync(facilityId, "Monthly"), b.Output, "Monthly query plan deletion");
        await TryDelete(async () => await b.DataAcquisitionClient.DeleteFhirQueryConfigurationAsync(facilityId), b.Output, "Query config deletion");
        await TryDelete(async () => await b.FacilityClient.DeleteAsync(facilityId), b.Output, "Facility deletion");
    }

    private static async Task EnsureQueryPlanAsync(TestServices b, string facilityId, string? measureId, string ehrDescription, string type)
    {
        var jBody = QueryPlanBuilder.BuildQueryPlan(facilityId, measureId, ehrDescription, type);
        var body = new CreateQueryPlanRequestApiModel
        {
            PlanName = jBody.Value<string>("PlanName"),
            FacilityId = jBody.Value<string>("FacilityId") ?? facilityId,
            EHRDescription = jBody.Value<string>("EHRDescription") ?? ehrDescription,
            LookBack = jBody.Value<string>("LookBack") ?? "P0D",
            Type = jBody.Value<string>("Type") ?? type,
            InitialQueries = jBody["InitialQueries"]?.ToObject<Dictionary<string, object>>() ?? new Dictionary<string, object>(),
            SupplementalQueries = jBody["SupplementalQueries"]?.ToObject<Dictionary<string, object>>() ?? new Dictionary<string, object>()
        };

        try
        {
            await b.DataAcquisitionClient.CreateQueryPlanAsync(facilityId, body);
        }
        catch (FlurlHttpException ex)
        {
            if (await IsAlreadyExistsAsync(ex))
            {
                b.Output.WriteLine($"{type} query plan for facility '{facilityId}' already exists. Skipping create.");
                return;
            }

            throw;
        }
    }

    private static async Task<bool> IsAlreadyExistsAsync(FlurlHttpException ex)
    {
        if (ex.StatusCode == 409)
            return true;

        if (ex.StatusCode != 400)
            return false;

        var body = await ex.GetResponseStringAsync();
        return body?.Contains("already exists", StringComparison.OrdinalIgnoreCase) == true;
    }

    private static async Task TryDelete(Func<Task> action, IAutomationOutput output, string opName)
    {
        try
        {
            await action();
        }
        catch (FlurlHttpException ex)
        {
            output.WriteLine($"{opName} failed: HTTP {ex.StatusCode}");
        }
    }
}
