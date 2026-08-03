using LantanaGroup.Link.Automation.Link.Configuration;
using LantanaGroup.Link.Sdk.Clients;
using LantanaGroup.Link.Shared.Application.Enums;
using LantanaGroup.Link.Shared.Application.Models.Integration.DataAcquisition;
using LantanaGroup.Link.Shared.Application.Models.Integration.Census;
using LantanaGroup.Link.Shared.Application.Models.Integration.Normalization;
using LantanaGroup.Link.Shared.Application.Models.Integration.QueryDispatch;
using LantanaGroup.Link.Shared.Application.Models.Tenant;
using LantanaGroup.Link.Shared.Application.Services.Security;
using System.Net;

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
        if (existing.IsSuccessStatusCode && existing.Body != null)
        {
            output.WriteLine($"Facility '{facilityId}' already exists. Skipping create.");
            await WaitForFacilityReadConsistencyAsync(facilityClient, output, facilityId);
            return;
        }

        var createResponse = await facilityClient.CreateAsync(new FacilityModel
        {
            FacilityId = facilityId,
            FacilityName = facilityId,
            TimeZone = "America/Chicago",
            Vendor = new VendorModel
            {
                Name = "Epic"
            },
            ScheduledReports = new TenantScheduledReportConfig
            {
                Monthly = measureIds.ToArray(),
                Daily = [],
                Weekly = []
            }
        });

        if (!createResponse.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"Failed to create facility '{facilityId}'. HTTP {createResponse.StatusCode}: {createResponse.RawBody ?? "(no body)"}");
        }

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
            if (facility.IsSuccessStatusCode && facility.Body != null)
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
            if (response.IsSuccessStatusCode && response.Body?.Records?.Count > 0)
            {
                output.WriteLine($"Normalization config for facility '{facilityId}' already exists. Skipping create.");
                return;
            }

            var normResp = await normalizationClient.CreateOperationAsync(new CreateNormalizationOperationRequestApiModel
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

            if (!normResp.IsSuccessStatusCode)
                throw new InvalidOperationException(
                    $"Failed to create normalization operation for facility '{facilityId}'. HTTP {normResp.StatusCode}: {normResp.RawBody ?? "(no body)"}");
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
        List<string> measureIds,
        string ehrDescription,
        QueryPlanInput? externalQueryPlan = null)
    {
        // Query plans are keyed by (facilityId, type) — not per measure.
        // Create each plan type once using the first measure as the plan name.
        //
        // The type MUST match the ReportableEvent-to-Frequency mapping the DataAcquisition
        // service uses to select a plan (ReportableEventToQueryPlanTypeFactory):
        //   - Discharge: the QueryDispatch discharge path sends ReportableEvent=Discharge for
        //     patients discharged inside the report window (Frequency.Discharge).
        //   - Daily/Monthly: the End-of-Report-Period job (Report.DataAcquisitionRequestedProducer)
        //     derives the event from schedule.Frequency (EOD/EOM). Keep both plans present so
        //     scheduled scenarios can run against either ACH Daily or ACH Monthly measures
        //     without configuration-missing failures.
        var planName = measureIds.Count > 0 ? measureIds[0] : null;
        await EnsureQueryPlanAsync(dataAcqClient, output, facilityId, planName, ehrDescription, "Discharge", externalQueryPlan);
        await EnsureQueryPlanAsync(dataAcqClient, output, facilityId, planName, ehrDescription, "Daily", externalQueryPlan);
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

        if (!created.IsSuccessStatusCode)
        {
            if (created.StatusCode == (int)HttpStatusCode.Conflict)
            {
                output.WriteLine($"Query config for facility '{facilityId}' already exists. Skipping create.");
                return;
            }

            throw new InvalidOperationException(
                $"Failed to create query config for facility '{facilityId}'. HTTP {created.StatusCode}: {created.RawBody ?? "(no body)"}");
        }
    }

    public static async Task EnsureQueryDispatchConfigAsync(
        IQueryDispatchServiceClient queryDispatchClient,
        IAutomationOutput output,
        string facilityId)
    {
        var qdResp = await queryDispatchClient.UpsertQueryDispatchConfigurationAsync(
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

        if (!qdResp.IsSuccessStatusCode)
            throw new InvalidOperationException(
                $"Failed to upsert query dispatch config for facility '{facilityId}'. HTTP {qdResp.StatusCode}: {qdResp.RawBody ?? "(no body)"}");

        output.WriteLine($"Ensured query dispatch config for facility '{facilityId}'.");
    }

    public static async Task EnsureCensusConfigAsync(
        ICensusServiceClient censusClient,
        IAutomationOutput output,
        string facilityId,
        string scheduledTrigger = "0 0/5 * * * ?",
        bool enabled = true)
    {
        var existing = await censusClient.GetCensusConfigAsync(facilityId);
        if (existing.IsSuccessStatusCode && existing.Body != null)
        {
            if (string.Equals(existing.Body.ScheduledTrigger, scheduledTrigger, StringComparison.Ordinal)
                && existing.Body.Enabled == enabled)
            {
                output.WriteLine($"Census config for facility '{facilityId}' already exists. Skipping create.");
                return;
            }

            var updateResp = await censusClient.UpdateCensusConfigAsync(facilityId, new CensusConfigApiModel
            {
                FacilityId = facilityId,
                ScheduledTrigger = scheduledTrigger,
                Enabled = enabled
            });

            if (!updateResp.IsSuccessStatusCode)
                throw new InvalidOperationException(
                    $"Failed to update census config for facility '{facilityId}'. HTTP {updateResp.StatusCode}: {updateResp.RawBody ?? "(no body)"}");

            output.WriteLine($"Updated census config for facility '{facilityId}'.");
            return;
        }

        var createResp = await censusClient.CreateCensusConfigAsync(new CensusConfigApiModel
        {
            FacilityId = facilityId,
            ScheduledTrigger = scheduledTrigger,
            Enabled = enabled
        });

        if (!createResp.IsSuccessStatusCode)
            throw new InvalidOperationException(
                $"Failed to create census config for facility '{facilityId}'. HTTP {createResp.StatusCode}: {createResp.RawBody ?? "(no body)"}");

        output.WriteLine($"Created census config for facility '{facilityId}'.");
    }

    public static async Task EnsureFhirListConfigAsync(
        IDataAcquisitionServiceClient dataAcqClient,
        AutomationConfig config,
        IAutomationOutput output,
        string facilityId)
    {
        var existing = await dataAcqClient.GetFhirListConfigurationAsync(facilityId);
        if (existing.IsSuccessStatusCode)
        {
            output.WriteLine($"FHIR list config for facility '{facilityId}' already exists. Skipping create.");
            return;
        }

        // Create all 6 required list combinations (Admit/Discharge x 3 timeframes)
        var listConfigs = new[]
        {
            new { Status = "Admit", TimeFrame = "LessThan24Hours", FhirId = $"census-{facilityId}-admit-lt24" },
            new { Status = "Admit", TimeFrame = "Between24To48Hours", FhirId = $"census-{facilityId}-admit-24to48" },
            new { Status = "Admit", TimeFrame = "MoreThan48Hours", FhirId = $"census-{facilityId}-admit-gt48" },
            new { Status = "Discharge", TimeFrame = "LessThan24Hours", FhirId = $"census-{facilityId}-discharge-lt24" },
            new { Status = "Discharge", TimeFrame = "Between24To48Hours", FhirId = $"census-{facilityId}-discharge-24to48" },
            new { Status = "Discharge", TimeFrame = "MoreThan48Hours", FhirId = $"census-{facilityId}-discharge-gt48" }
        };

        var request = new
        {
            FacilityId = facilityId,
            FhirBaseServerUrl = config.FacilityFhirServerBase,
            EHRPatientLists = listConfigs.Select(c => new
            {
                c.Status,
                c.TimeFrame,
                c.FhirId
            }).ToList()
        };

        var createResp = await dataAcqClient.CreateFhirListConfigurationAsync(request);
        if (!createResp.IsSuccessStatusCode)
        {
            if (createResp.StatusCode == (int)HttpStatusCode.Conflict)
            {
                output.WriteLine($"FHIR list config for facility '{facilityId}' already exists (conflict). Continuing.");
                return;
            }

            throw new InvalidOperationException(
                $"Failed to create FHIR list config for facility '{facilityId}'. HTTP {createResp.StatusCode}: {createResp.RawBody ?? "(no body)"}");
        }

        output.WriteLine($"Created FHIR list config for facility '{facilityId}'.");
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
        await dataAcqClient.DeleteQueryPlanAsync(facilityId, "Daily");
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

        var createdPlan = await dataAcqClient.CreateQueryPlanAsync(facilityId, body);
        if (!createdPlan.IsSuccessStatusCode)
        {
            if (createdPlan.StatusCode == (int)HttpStatusCode.Conflict)
            {
                output.WriteLine($"{type} query plan for facility '{facilityId}' already exists. Skipping create.");
                return;
            }

            throw new InvalidOperationException(
                $"Failed to create {type} query plan for facility '{facilityId}'. HTTP {createdPlan.StatusCode}: {createdPlan.RawBody ?? "(no body)"}");
        }
    }
}