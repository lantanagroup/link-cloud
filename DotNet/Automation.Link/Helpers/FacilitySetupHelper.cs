using LantanaGroup.Link.Automation.Link.Configuration;
using LantanaGroup.Link.Sdk.Clients;
using LantanaGroup.Link.Shared.Application.Enums;
using LantanaGroup.Link.Shared.Application.Models.Integration.DataAcquisition;
using LantanaGroup.Link.Shared.Application.Models.Integration.Normalization;
using LantanaGroup.Link.Shared.Application.Models.Integration.QueryDispatch;
using LantanaGroup.Link.Shared.Application.Models.Tenant;
using LantanaGroup.Link.Shared.Application.Services.Security;

namespace LantanaGroup.Link.Automation.Link.Helpers;

public static class FacilitySetupHelper
{
    public static async Task EnsureFacilityAsync(
        IFacilityServiceClient facilityClient,
        IAutomationOutput output,
        string facilityId,
        string? measureId)
    {
        await EnsureFacilityAsync(facilityClient, output, facilityId,
            measureId != null ? [measureId] : []);
    }

    public static async Task EnsureFacilityAsync(
        IFacilityServiceClient facilityClient,
        IAutomationOutput output,
        string facilityId,
        List<string> measureIds)
    {
        var existing = await facilityClient.GetAsync(facilityId);
        if (existing != null)
        {
            output.WriteLine($"Facility '{facilityId}' already exists. Skipping create.");
            await WaitForFacilityReadConsistencyAsync(facilityClient, output, facilityId);
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
                Monthly = measureIds.ToArray(),
                Daily = [],
                Weekly = []
            }
        });

        await WaitForFacilityReadConsistencyAsync(facilityClient, output, facilityId);
    }

    private static async Task WaitForFacilityReadConsistencyAsync(
        IFacilityServiceClient facilityClient,
        IAutomationOutput output,
        string facilityId,
        CancellationToken cancellationToken = default)
    {
        var timeout = TimeSpan.FromSeconds(20);
        var started = DateTime.UtcNow;

        while (DateTime.UtcNow - started < timeout)
        {
            var facility = await facilityClient.GetAsync(facilityId, cancellationToken);
            if (facility != null)
                return;

            await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
        }

        throw new InvalidOperationException(
            $"Facility '{facilityId}' was created but could not be read back from Tenant service within {timeout.TotalSeconds:F0}s.");
    }

    public static async Task EnsureNormalizationConfigAsync(
        INormalizationServiceClient normalizationClient,
        IAutomationOutput output,
        string facilityId)
    {
        try
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
        catch (Exception ex)
        {
            output.WriteLine($"CreateOperationAsync failed for facility '{facilityId.SanitizeForLog()}': {ex.GetType().Name}");
            throw;
        }
    }

    public static async Task EnsureQueryPlansAsync(
        IDataAcquisitionServiceClient dataAcqClient,
        IAutomationOutput output,
        string facilityId,
        string? measureId,
        string ehrDescription)
    {
        await EnsureQueryPlansAsync(dataAcqClient, output, facilityId,
            measureId != null ? [measureId] : [], ehrDescription);
    }

    public static async Task EnsureQueryPlansAsync(
        IDataAcquisitionServiceClient dataAcqClient,
        IAutomationOutput output,
        string facilityId,
        List<string> measureIds,
        string ehrDescription,
        QueryPlanInput? externalQueryPlan = null)
    {
        // Query plans are keyed by (facilityId, type) — not per measure.
        // Create each plan type once using the first measure as the plan name.
        var planName = measureIds.Count > 0 ? measureIds[0] : null;
        await EnsureQueryPlanAsync(dataAcqClient, output, facilityId, planName, ehrDescription, "Discharge", externalQueryPlan);
        await EnsureQueryPlanAsync(dataAcqClient, output, facilityId, planName, ehrDescription, "Monthly", externalQueryPlan);
    }

    public static async Task EnsureQueryConfigAsync(
        IDataAcquisitionServiceClient dataAcqClient,
        AutomationConfig config,
        IAutomationOutput output,
        string facilityId)
    {
        // Keep concurrency high enough to avoid single-request bottlenecks,
        // but cap it to reduce downstream service saturation in large volume runs.
        var effectiveMaxConcurrentRequests = Math.Clamp(config.FhirQuery.MaxConcurrentRequests, 1, 8);

        var created = await dataAcqClient.CreateFhirQueryConfigurationAsync(new CreateFhirQueryConfigurationRequestApiModel
        {
            FacilityId = facilityId,
            FhirServerBaseUrl = config.FacilityFhirServerBase,
            MaxConcurrentRequests = effectiveMaxConcurrentRequests,
            MaxRetries = 3,
            MinAcquisitionPullTime = config.FhirQuery.MinAcquisitionPullTime,
            MaxAcquisitionPullTime = config.FhirQuery.MaxAcquisitionPullTime,
            TimeZone = config.FhirQuery.TimeZone
        });

        if (!created)
            output.WriteLine($"Query config for facility '{facilityId}' already exists. Skipping create.");
    }

    public static async Task EnsureQueryDispatchConfigAsync(
        IQueryDispatchServiceClient queryDispatchClient,
        IAutomationOutput output,
        string facilityId)
    {
        await queryDispatchClient.UpsertQueryDispatchConfigurationAsync(
            facilityId,
            new QueryDispatchConfigurationApiModel
            {
                FacilityId = facilityId,
                DispatchSchedules =
                [
                    new DispatchScheduleApiModel
                    {
                        Event = "Discharge",
                        Duration = "PT0S"
                    }
                ]
            });

        output.WriteLine($"Ensured query dispatch config for facility '{facilityId}'.");
    }

    public static async Task CleanupQueryDispatchConfigAsync(
        IQueryDispatchServiceClient queryDispatchClient,
        IAutomationOutput output,
        string facilityId)
    {
        await queryDispatchClient.DeleteQueryDispatchConfigurationAsync(facilityId);
        output.WriteLine($"Query dispatch config cleanup complete for facility '{facilityId}'.");
    }

    public static async Task CleanupFacilityAsync(
        IFacilityServiceClient facilityClient,
        INormalizationServiceClient normalizationClient,
        IDataAcquisitionServiceClient dataAcqClient,
        IQueryDispatchServiceClient queryDispatchClient,
        IAutomationOutput output,
        string facilityId)
    {
        await queryDispatchClient.DeleteQueryDispatchConfigurationAsync(facilityId);
        await normalizationClient.DeleteFacilityOperationsAsync(facilityId);
        await dataAcqClient.DeleteQueryPlanAsync(facilityId, "Discharge");
        await dataAcqClient.DeleteQueryPlanAsync(facilityId, "Monthly");
        await dataAcqClient.DeleteFhirQueryConfigurationAsync(facilityId);
        await facilityClient.DeleteAsync(facilityId);
        output.WriteLine("Facility cleanup complete.");
    }

    public static async Task SoftDeleteRunDataAsync(
        IReportServiceClient reportClient,
        IDataAcquisitionServiceClient dataAcqClient,
        IQueryDispatchServiceClient queryDispatchClient,
        IAutomationOutput output,
        string facilityId,
        string reportId)
    {
        if (string.IsNullOrWhiteSpace(facilityId) || string.IsNullOrWhiteSpace(reportId))
            return;

        await reportClient.SoftDeleteScheduleAsync(reportId);
        await dataAcqClient.SoftDeleteLogsByFacilityAsync(facilityId);
        await queryDispatchClient.DeleteQueryDispatchConfigurationAsync(facilityId);

        output.WriteLine($"Soft-deleted report '{reportId}', DA logs, and query dispatch config for facility '{facilityId}'.");
    }

    private static async Task EnsureQueryPlanAsync(
        IDataAcquisitionServiceClient dataAcqClient,
        IAutomationOutput output,
        string facilityId,
        string? measureId,
        string ehrDescription,
        string type,
        QueryPlanInput? externalQueryPlan = null)
    {
        var jBody = QueryPlanBuilder.BuildQueryPlan(facilityId, measureId, ehrDescription, type, externalQueryPlan);
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