using Automation.UI.Models.ApiHealth;
using Automation.UI.Services.ApiHealth.Seeding;
using LantanaGroup.Link.Automation.Link.Configuration;
using LantanaGroup.Link.Sdk.ApiClient;
using LantanaGroup.Link.Sdk.Clients;
using LantanaGroup.Link.Shared.Application.Enums;
using LantanaGroup.Link.Shared.Application.Models.Integration.DataAcquisition;
using LantanaGroup.Link.Shared.Application.Models.Tenant;
using Microsoft.Extensions.Options;
using StepNames = Automation.UI.Services.ApiHealth.TestSuites.ApiEndPointLibrary.DataAcquisitionSteps;

namespace Automation.UI.Services.ApiHealth.TestSuites;

/// <summary>
/// Exercises DataAcquisition service CRUD operations via LinkSdk.
/// Each step is self-contained: prerequisite data (facility, config) is created
/// within the step that needs it and cleaned up after.
/// </summary>
public sealed class DataAcquisitionTestSuite : ServiceTestSuiteBase
{
    private readonly IDataAcquisitionServiceClient _client;
    private readonly IFacilityServiceClient _facilityClient;
    private readonly IApiHealthSeedContextAccessor _seedContext;
    private readonly IOptions<AutomationConfig> _automationConfig;

    public override string ServiceName => "DataAcquisition";
    public DataAcquisitionTestSuite(
        IDataAcquisitionServiceClient client,
        IFacilityServiceClient facilityClient,
        IApiHealthSeedContextAccessor seedContext,
        IOptions<AutomationConfig> automationConfig)
    {
        _client = client;
        _facilityClient = facilityClient;
        _seedContext = seedContext;
        _automationConfig = automationConfig;
    }

    public override IReadOnlyList<ApiHealthSeedRequirement> GetSeedRequirements() =>
    [
        ApiHealthSeedRequirement.ReportSchedule
    ];

    public override IReadOnlyList<ApiEndpointDefinition> GetEndpointDefinitions() =>
        ApiEndPointLibrary.GetServiceEndpoints(ServiceName);

    public override async Task<IReadOnlyList<ApiTestRunResult>> ExecuteAsync(CancellationToken ct = default)
    {
        var results = new List<ApiTestRunResult>();

        async Task AddSeededOrSkipAsync(
            bool canRun,
            string endpointName,
            int expectedStatusCode,
            Func<Task<LinkApiResponse>> action,
            string skipReason)
        {
            if (canRun)
                results.Add(await RunStepAsync(endpointName, expectedStatusCode, action, ct));
            else
                results.Add(SkipStepAsync(endpointName, skipReason));
        }

        async Task AddSeededOrSkipTypedAsync<T>(
            bool canRun,
            string endpointName,
            int expectedStatusCode,
            Func<Task<LinkApiResponse<T>>> action,
            string skipReason) where T : class
        {
            if (canRun)
                results.Add(await RunStepAsync(endpointName, expectedStatusCode, action, ct));
            else
                results.Add(SkipStepAsync(endpointName, skipReason));
        }

        var facilityId = $"ApiHealth-DA-{Guid.NewGuid():N}";
        var facilityCreated = false;
        var queryConfigCreated = false;
        var dischargePlanCreated = false;
        var fhirListCreated = false;

        try
        {
            var seededFacilityId = _seedContext.Current?.Report?.FacilityId;
            var seededReportId = _seedContext.Current?.Report?.ScheduleId;
            long? seededLogId = null;
            const string seedUnavailable = "Seeded report data was unavailable for DataAcquisition 2xx lifecycle checks.";

            if (!string.IsNullOrWhiteSpace(seededFacilityId) && !string.IsNullOrWhiteSpace(seededReportId))
            {
                var seededSearch = await _client.SearchAcquisitionLogsAsync(seededFacilityId, seededReportId, cancellationToken: ct);
                var hasSeededSearch = seededSearch.IsSuccessStatusCode;

                await AddSeededOrSkipTypedAsync(
                    hasSeededSearch,
                    StepNames.AcquisitionLogsGet200HasData,
                    200,
                    () => _client.SearchAcquisitionLogsAsync(seededFacilityId, seededReportId, cancellationToken: ct),
                    seedUnavailable);

                if (hasSeededSearch)
                {
                    var seededRecords = seededSearch.Body?.Records;
                    if (seededRecords is { Count: > 0 })
                        seededLogId = seededRecords[0].Id;
                }

                await AddSeededOrSkipTypedAsync(
                    hasSeededSearch,
                    StepNames.StatusCountsGet200HasData,
                    200,
                    () => _client.GetReportStatusCountsAsync(seededReportId, ct),
                    seedUnavailable);

                await AddSeededOrSkipTypedAsync(
                    hasSeededSearch,
                    StepNames.SummaryGet200HasData,
                    200,
                    () => _client.GetReportSummaryAsync(seededReportId, ct),
                    seedUnavailable);

                await AddSeededOrSkipTypedAsync(
                    hasSeededSearch,
                    StepNames.ResourceIdsGet200HasData,
                    200,
                    () => _client.GetAcquiredResourceIdsForReportAsync(seededFacilityId, seededReportId, ct),
                    seedUnavailable);
            }
            else
            {
                results.Add(SkipStepAsync(StepNames.AcquisitionLogsGet200HasData, seedUnavailable));
                results.Add(SkipStepAsync(StepNames.StatusCountsGet200HasData, seedUnavailable));
                results.Add(SkipStepAsync(StepNames.SummaryGet200HasData, seedUnavailable));
                results.Add(SkipStepAsync(StepNames.ResourceIdsGet200HasData, seedUnavailable));
            }

            await AddSeededOrSkipTypedAsync(
                seededLogId.HasValue,
                StepNames.GetLog200HasData,
                200,
                () => _client.GetAcquisitionLogByIdAsync(seededLogId!.Value, ct),
                seedUnavailable);

            await AddSeededOrSkipTypedAsync(
                seededLogId.HasValue,
                StepNames.GetNotes200HasData,
                200,
                () => _client.GetAcquisitionLogNotesAsync(seededLogId!.Value, ct),
                seedUnavailable);

            await AddSeededOrSkipTypedAsync(
                seededLogId.HasValue,
                StepNames.GetRefs200HasData,
                200,
                () => _client.GetReferenceResourcesForLogAsync(seededLogId!.Value, cancellationToken: ct),
                seedUnavailable);

            await AddSeededOrSkipAsync(
                seededLogId.HasValue,
                StepNames.Process202HasData,
                202,
                () => _client.ProcessAcquisitionLogAsync(seededLogId!.Value, ct),
                seedUnavailable);

            // Prerequisite: create facility (infrastructure, not tracked)
            await CreateFacilityAsync(facilityId, ct);
            facilityCreated = true;

            // Org-location configuration lifecycle checks
            results.Add(await RunStepAsync(StepNames.OrgLocationConfigGet200, 200, async () =>
                await _client.GetOrganizationLocationConfigurationsAsync(facilityId, ct), ct: ct));

            results.Add(await RunStepAsync(StepNames.OrgLocationConfigPost201, 201, async () =>
                await _client.CreateOrganizationLocationConfigurationAsync(
                    facilityId,
                    new CreateOrganizationLocationConfigurationApiModel
                    {
                        Description = "ApiHealth org-location config",
                        IsActive = true,
                        Conditions =
                        [
                            new CreateOrganizationLocationConditionApiModel
                            {
                                FhirPath = "identifier.where(system='http://example.org/fhir/sid/location').exists() or type.coding.where(system='https://www.cdc.gov/nhsn/cdaportal/terminology/codesystem/hsloc.html').exists()",
                                Priority = 1
                            }
                        ]
                    },
                    ct), ct: ct));

            results.Add(await RunStepAsync(StepNames.OrgLocationConfigGet200AfterPost, 200, async () =>
                await _client.GetOrganizationLocationConfigurationsAsync(facilityId, ct), ct: ct));

            results.Add(await RunStepAsync(StepNames.OrgLocationMappingsGet200, 200, async () =>
                await _client.GetOrganizationLocationMappingsAsync(facilityId, ct), ct: ct));

            results.Add(await RunStepAsync(StepNames.EncounterMappingsGet200, 200, async () =>
                await _client.GetEncounterMappingsAsync(facilityId, ct), ct: ct));

            // CREATE FHIR Query Config
            results.Add(await RunStepAsync(StepNames.FhirConfigPost201, 201, async () =>
            {
                var request = BuildFhirConfigRequest(facilityId);
                var resp = await _client.CreateFhirQueryConfigurationAsync(request, ct);
                if (resp.IsSuccessStatusCode) queryConfigCreated = true;
                return resp;
            }, ct: ct));

            // GET FHIR Query Config (depends on create above)
            results.Add(await RunStepAsync(StepNames.FhirConfigGet200, 200, async () =>
                await _client.GetFhirQueryConfigurationAsync(facilityId, ct), ct: ct));

            // POST again → 409 (already exists)
            results.Add(await RunStepAsync(StepNames.FhirConfigPost409, 409, async () =>
                await _client.CreateFhirQueryConfigurationAsync(BuildFhirConfigRequest(facilityId), ct), ct: ct));

            // CREATE Query Plan (Discharge)
            results.Add(await RunStepAsync(StepNames.QueryPlanPost201, 201, async () =>
            {
                var plan = BuildDischargePlanRequest(facilityId, "ApiHealth-Discharge");
                var resp = await _client.CreateQueryPlanAsync(facilityId, plan, ct);
                if (resp.IsSuccessStatusCode) dischargePlanCreated = true;
                return resp;
            }, ct: ct));

            // GET Query Plan (depends on create above)
            results.Add(await RunStepAsync(StepNames.QueryPlanGet200, 200, async () =>
                await _client.GetQueryPlanAsync(facilityId, "Discharge", ct), ct: ct));

            // POST Query Plan again → 409 (already exists)
            results.Add(await RunStepAsync(StepNames.QueryPlanPost409, 409, async () =>
                await _client.CreateQueryPlanAsync(facilityId, BuildDischargePlanRequest(facilityId, "ApiHealth-Discharge-Dup"), ct), ct: ct));

            // DELETE Query Plan
            results.Add(await RunStepAsync(StepNames.QueryPlanDelete202, 202, async () =>
            {
                var resp = await _client.DeleteQueryPlanAsync(facilityId, "Discharge", ct);
                if (resp.IsSuccessStatusCode) dischargePlanCreated = false;
                return resp;
            }, ct: ct));

            // GET Query Plan → 404 (after delete)
            results.Add(await RunStepAsync(StepNames.QueryPlanGet404, 404, async () =>
                await _client.GetQueryPlanAsync(facilityId, "Discharge", ct), ct: ct));

            // DELETE FHIR Query Config
            results.Add(await RunStepAsync(StepNames.FhirConfigDelete202, 202, async () =>
            {
                var resp = await _client.DeleteFhirQueryConfigurationAsync(facilityId, ct);
                if (resp.IsSuccessStatusCode) queryConfigCreated = false;
                return resp;
            }, ct: ct));

            // GET FHIR Query Config → 404 (after delete)
            results.Add(await RunStepAsync(StepNames.FhirConfigGet404, 404, async () =>
                await _client.GetFhirQueryConfigurationAsync(facilityId, ct), ct: ct));

            // --- FHIR List Configuration CRUD ---

            // POST FHIR List Config → 200
            results.Add(await RunStepAsync(StepNames.FhirListConfigPost200, 200, async () =>
            {
                var resp = await _client.CreateFhirListConfigurationAsync(BuildFhirListConfigRequest(facilityId), ct);
                if (resp.IsSuccessStatusCode) fhirListCreated = true;
                return resp;
            }, ct: ct));

            // GET FHIR List Config → 200
            results.Add(await RunStepAsync(StepNames.FhirListConfigGet200, 200, async () =>
                await _client.GetFhirListConfigurationAsync(facilityId, ct), ct: ct));

            // POST FHIR List Config → 409 (duplicate)
            results.Add(await RunStepAsync(StepNames.FhirListConfigPost409, 409, async () =>
                await _client.CreateFhirListConfigurationAsync(BuildFhirListConfigRequest(facilityId), ct), ct: ct));

            // DELETE FHIR List Config → 200
            results.Add(await RunStepAsync(StepNames.FhirListConfigDelete200, 200, async () =>
            {
                var resp = await _client.DeleteFhirListConfigurationAsync(facilityId, ct);
                if (resp.IsSuccessStatusCode) fhirListCreated = false;
                return resp;
            }, ct: ct));

            // GET FHIR List Config → 404 (after delete)
            results.Add(await RunStepAsync(StepNames.FhirListConfigGet404, 404, async () =>
                await _client.GetFhirListConfigurationAsync(facilityId, ct), ct: ct));

            // --- Read-only operations (non-existent resources — prove reachability) ---
            var fakeReportId = Guid.NewGuid().ToString();

            results.Add(await RunStepAsync(StepNames.AcquisitionLogsGet200, 200, async () =>
                await _client.SearchAcquisitionLogsAsync(facilityId, fakeReportId, cancellationToken: ct), ct: ct));

            // === GET /acquisition-logs/{id} → 400, 404 ===
            results.Add(await RunStepAsync(StepNames.GetLog400Id0, 400, async () =>
                await _client.GetAcquisitionLogByIdAsync(0, ct), ct: ct));

            results.Add(await RunStepAsync(StepNames.GetLog404NonExistent, 404, async () =>
                await _client.GetAcquisitionLogByIdAsync(long.MaxValue, ct), ct: ct));

            // === GET /acquisition-logs/{id}/notes → 400, 200 ===
            results.Add(await RunStepAsync(StepNames.GetNotes400Id0, 400, async () =>
                await _client.GetAcquisitionLogNotesAsync(0, ct), ct: ct));

            results.Add(await RunStepAsync(StepNames.GetNotes200NonExistent, 200, async () =>
                await _client.GetAcquisitionLogNotesAsync(long.MaxValue, ct), ct: ct));

            // === GET /acquisition-logs/{id}/reference-resources → 400, 200 ===
            results.Add(await RunStepAsync(StepNames.GetRefs400Id0, 400, async () =>
                await _client.GetReferenceResourcesForLogAsync(0, cancellationToken: ct), ct: ct));

            results.Add(await RunStepAsync(StepNames.GetRefs200NonExistent, 200, async () =>
                await _client.GetReferenceResourcesForLogAsync(long.MaxValue, cancellationToken: ct), ct: ct));

            // === POST /acquisition-logs/{id}/process → 400, 404 ===
            results.Add(await RunStepAsync(StepNames.Process400Id0, 400, async () =>
                await _client.ProcessAcquisitionLogAsync(0, ct), ct: ct));

            results.Add(await RunStepAsync(StepNames.Process404NonExistent, 404, async () =>
                await _client.ProcessAcquisitionLogAsync(long.MaxValue, ct), ct: ct));

            // === POST /process-bulk → 202, 400 ===
            results.Add(await RunStepAsync(StepNames.ProcessBulk202, 202, async () =>
                await _client.ProcessAcquisitionLogsBulkAsync([long.MaxValue], ct), ct: ct));

            results.Add(await RunStepAsync(StepNames.ProcessBulk400Empty, 400, async () =>
                await _client.ProcessAcquisitionLogsBulkAsync([], ct), ct: ct));

            // === POST /cancel-bulk → 202, 400 (empty), 400 (neg hours) ===
            results.Add(await RunStepAsync(StepNames.CancelBulk202, 202, async () =>
                await _client.CancelAcquisitionLogsBulkAsync([long.MaxValue], minAgeHours: 0, cancellationToken: ct), ct: ct));

            results.Add(await RunStepAsync(StepNames.CancelBulk400Empty, 400, async () =>
                await _client.CancelAcquisitionLogsBulkAsync([], minAgeHours: 0, cancellationToken: ct), ct: ct));

            results.Add(await RunStepAsync(StepNames.CancelBulk400NegHours, 400, async () =>
                await _client.CancelAcquisitionLogsBulkAsync([1], minAgeHours: -1, cancellationToken: ct), ct: ct));

            // === POST /process-by-filter → 202, 400 ===
            results.Add(await RunStepAsync(StepNames.ProcessFilter202, 202, async () =>
                await _client.ProcessAcquisitionLogsByFilterAsync(new { FacilityId = facilityId }, ct), ct: ct));

            results.Add(await RunStepAsync(StepNames.ProcessFilter400NoCriteria, 400, async () =>
                await _client.ProcessAcquisitionLogsByFilterAsync(new { }, ct), ct: ct));

            // === POST /cancel-by-filter → 202, 400 (no criteria), 400 (neg hours) ===
            results.Add(await RunStepAsync(StepNames.CancelFilter202, 202, async () =>
                await _client.CancelAcquisitionLogsByFilterAsync(new { FacilityId = facilityId }, minAgeHours: 0, cancellationToken: ct), ct: ct));

            results.Add(await RunStepAsync(StepNames.CancelFilter400NoCriteria, 400, async () =>
                await _client.CancelAcquisitionLogsByFilterAsync(new { }, minAgeHours: 0, cancellationToken: ct), ct: ct));

            results.Add(await RunStepAsync(StepNames.CancelFilter400NegHours, 400, async () =>
                await _client.CancelAcquisitionLogsByFilterAsync(new { FacilityId = facilityId }, minAgeHours: -1, cancellationToken: ct), ct: ct));

            // === DELETE /acquisition-logs/{id} → 400, 404 ===
            results.Add(await RunStepAsync(StepNames.DeleteLog400Id0, 400, async () =>
                await _client.DeleteAcquisitionLogAsync(0, ct), ct: ct));

            results.Add(await RunStepAsync(StepNames.DeleteLog404NonExistent, 404, async () =>
                await _client.DeleteAcquisitionLogAsync(long.MaxValue, ct), ct: ct));

            // === DELETE /facility/{id} → 204 ===
            results.Add(await RunStepAsync(StepNames.SoftDeleteFacility204, 204, async () =>
                await _client.SoftDeleteLogsByFacilityAsync(facilityId, ct), ct: ct));

            // === DELETE /report/{id} → 204 ===
            results.Add(await RunStepAsync(StepNames.SoftDeleteReport204, 204, async () =>
                await _client.SoftDeleteLogsByReportTrackingIdAsync(Guid.NewGuid().ToString(), ct), ct: ct));

            // === PATCH /report/{id}/restore → 204 ===
            results.Add(await RunStepAsync(StepNames.RestoreReport204, 204, async () =>
                await _client.RestoreLogsByReportTrackingIdAsync(Guid.NewGuid().ToString(), ct), ct: ct));

            // === PATCH /facility/{id}/restore → 200 ===
            results.Add(await RunStepAsync(StepNames.RestoreFacility200, 200, async () =>
                await _client.RestoreLogsByFacilityAsync(facilityId, ct), ct: ct));

            results.Add(await RunStepAsync(StepNames.StatusCountsGet200, 200, async () =>
                await _client.GetReportStatusCountsAsync(fakeReportId, ct), ct: ct));

            results.Add(await RunStepAsync(StepNames.SummaryGet200, 200, async () =>
                await _client.GetReportSummaryAsync(fakeReportId, ct), ct: ct));

            results.Add(await RunStepAsync(StepNames.ResourceIdsGet200, 200, async () =>
                await _client.GetAcquiredResourceIdsForReportAsync(facilityId, fakeReportId, ct), ct: ct));

            // ResourceIds → 400 (empty facilityId)
            results.Add(await RunStepAsync(StepNames.ResourceIds400NoFacility, 400, async () =>
                await _client.GetAcquiredResourceIdsForReportAsync(" ", fakeReportId, ct), ct: ct));

            // Search → 400 (invalid sortBy)
            results.Add(await RunStepAsync(StepNames.Search400BadSortBy, 400, async () =>
                await _client.SearchAcquisitionLogsAsync(facilityId, fakeReportId, sortBy: "InvalidColumn", cancellationToken: ct), ct: ct));
        }
        finally
        {
            if (dischargePlanCreated) await TryCleanupAsync(() => _client.DeleteQueryPlanAsync(facilityId, "Discharge", ct));
            if (queryConfigCreated) await TryCleanupAsync(() => _client.DeleteFhirQueryConfigurationAsync(facilityId, ct));
            if (fhirListCreated) await TryCleanupAsync(() => _client.DeleteFhirListConfigurationAsync(facilityId, ct));
            if (facilityCreated) await TryCleanupAsync(() => _facilityClient.DeleteAsync(facilityId, ct));
        }

        return results;
    }

    private async Task CreateFacilityAsync(string facilityId, CancellationToken ct)
    {
        var model = new FacilityModel
        {
            FacilityId = facilityId,
            FacilityName = facilityId,
            TimeZone = "America/Chicago",
            Vendor = Vendor.Epic,
            ScheduledReports = new TenantScheduledReportConfig { Daily = [], Weekly = [], Monthly = [] }
        };
        await _facilityClient.CreateAsync(model, ct);
    }

    private CreateFhirQueryConfigurationRequestApiModel BuildFhirConfigRequest(string facilityId) => new()
    {
        FacilityId = facilityId,
        FhirServerBaseUrl = _automationConfig.Value.FacilityFhirServerBase,
        MaxConcurrentRequests = 2,
        MaxRetries = 3
    };

    private static CreateQueryPlanRequestApiModel BuildDischargePlanRequest(string facilityId, string planName) => new()
    {
        PlanName = planName,
        FacilityId = facilityId,
        EHRDescription = "Epic",
        LookBack = "P0D",
        Type = "Discharge",
        InitialQueries = new Dictionary<string, object>
        {
            ["0"] = new { QueryConfigType = "Parameter", ResourceType = "Patient", Parameters = new[] { new { ParameterType = "Variable", Name = "_id", Variable = 0 } } },
            ["1"] = new { QueryConfigType = "Parameter", ResourceType = "Encounter", Parameters = new[] { new { ParameterType = "Variable", Name = "subject", Variable = 0 } } },
            ["2"] = new { QueryConfigType = "Parameter", ResourceType = "Location", Parameters = new[] { new { ParameterType = "ResourceIds", Name = "_id", Resource = "Encounter", Paged = "100" } } }
        },
        SupplementalQueries = new Dictionary<string, object>
        {
            ["0"] = new { QueryConfigType = "Reference", ResourceType = "Condition", OperationType = 2, Paged = 100 }
        }
    };

    private object BuildFhirListConfigRequest(string facilityId)
    {
        var listConfigs = new[]
        {
            new { Status = "Admit", TimeFrame = "LessThan24Hours", FhirId = $"census-{facilityId}-admit-lt24" },
            new { Status = "Admit", TimeFrame = "Between24To48Hours", FhirId = $"census-{facilityId}-admit-24to48" },
            new { Status = "Admit", TimeFrame = "MoreThan48Hours", FhirId = $"census-{facilityId}-admit-gt48" },
            new { Status = "Discharge", TimeFrame = "LessThan24Hours", FhirId = $"census-{facilityId}-discharge-lt24" },
            new { Status = "Discharge", TimeFrame = "Between24To48Hours", FhirId = $"census-{facilityId}-discharge-24to48" },
            new { Status = "Discharge", TimeFrame = "MoreThan48Hours", FhirId = $"census-{facilityId}-discharge-gt48" }
        };

        return new
        {
            FacilityId = facilityId,
            FhirBaseServerUrl = _automationConfig.Value.FacilityFhirServerBase,
            EHRPatientLists = listConfigs.Select(c => new
            {
                c.Status,
                c.TimeFrame,
                c.FhirId
            }).ToList()
        };
    }
}
