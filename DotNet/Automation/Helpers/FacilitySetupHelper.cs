using LantanaGroup.Link.Automation.Configuration;
using LantanaGroup.Link.Sdk.Clients;
using LantanaGroup.Link.Shared.Application.Enums;
using LantanaGroup.Link.Shared.Application.Models.Integration.DataAcquisition;
using LantanaGroup.Link.Shared.Application.Models.Integration.Normalization;
using LantanaGroup.Link.Shared.Application.Models.Tenant;

namespace LantanaGroup.Link.Automation.Helpers;

public static class FacilitySetupHelper
{
    public static async Task EnsureFacilityAsync(
        IFacilityServiceClient facilityClient,
        IAutomationOutput output,
        string facilityId,
        string? measureId)
    {
        var existing = await facilityClient.GetAsync(facilityId);
        if (existing != null)
        {
            output.WriteLine($"Facility '{facilityId}' already exists. Skipping create.");
            return;
        }

        await facilityClient.CreateAsync(new FacilityModel
        {
            FacilityId = facilityId,
            FacilityName = facilityId,
            TimeZone = "America/Chicago",
            Vendor = Vendor.Epic,
            ScheduledReports = new TenantScheduledReportConfig
            {
                Monthly = measureId != null ? [measureId] : [],
                Daily = [],
                Weekly = []
            }
        });
    }

    public static async Task EnsureNormalizationConfigAsync(
        INormalizationServiceClient normalizationClient,
        IAutomationOutput output,
        string facilityId)
    {
        var response = await normalizationClient.SearchFacilityOperationsAsync(facilityId);
        if (response?.Records?.Count > 0)
        {
            output.WriteLine($"Normalization config for facility '{facilityId}' already exists. Skipping create.");
            return;
        }

        await normalizationClient.CreateOperationAsync(new CreateNormalizationOperationRequestApiModel
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

    public static async Task EnsureQueryPlansAsync(
        IDataAcquisitionServiceClient dataAcqClient,
        IAutomationOutput output,
        string facilityId,
        string? measureId,
        string ehrDescription)
    {
        await EnsureQueryPlanAsync(dataAcqClient, output, facilityId, measureId, ehrDescription, "Discharge");
        await EnsureQueryPlanAsync(dataAcqClient, output, facilityId, measureId, ehrDescription, "Monthly");
    }

    public static async Task EnsureQueryConfigAsync(
        IDataAcquisitionServiceClient dataAcqClient,
        AutomationConfig config,
        IAutomationOutput output,
        string facilityId)
    {
        var created = await dataAcqClient.CreateFhirQueryConfigurationAsync(new CreateFhirQueryConfigurationRequestApiModel
        {
            FacilityId = facilityId,
            FhirServerBaseUrl = config.InternalFhirServerBase,
            MaxConcurrentRequests = config.FhirQuery.MaxConcurrentRequests,
            MaxRetries = 3
        });

        if (!created)
            output.WriteLine($"Query config for facility '{facilityId}' already exists. Skipping create.");
    }

    public static async Task CleanupFacilityAsync(
        IFacilityServiceClient facilityClient,
        INormalizationServiceClient normalizationClient,
        IDataAcquisitionServiceClient dataAcqClient,
        IAutomationOutput output,
        string facilityId)
    {
        await normalizationClient.DeleteFacilityOperationsAsync(facilityId);
        await dataAcqClient.DeleteQueryPlanAsync(facilityId, "Discharge");
        await dataAcqClient.DeleteQueryPlanAsync(facilityId, "Monthly");
        await dataAcqClient.DeleteFhirQueryConfigurationAsync(facilityId);
        await facilityClient.DeleteAsync(facilityId);
        output.WriteLine("Facility cleanup complete.");
    }

    private static async Task EnsureQueryPlanAsync(
        IDataAcquisitionServiceClient dataAcqClient,
        IAutomationOutput output,
        string facilityId,
        string? measureId,
        string ehrDescription,
        string type)
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

        var created = await dataAcqClient.CreateQueryPlanAsync(facilityId, body);
        if (!created)
            output.WriteLine($"{type} query plan for facility '{facilityId}' already exists. Skipping create.");
    }
}
