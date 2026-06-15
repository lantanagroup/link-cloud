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

        // GET /api/data/{facilityId}/fhirQueryConfiguration
        Step("FhirConfig GET ? 200", "Retrieves FHIR query config", "/api/data/{facilityId}/fhirQueryConfiguration"),
        Step("FhirConfig GET ? 404", "Returns 404 for non-existent facility", "/api/data/{facilityId}/fhirQueryConfiguration"),

        // DELETE /api/data/{facilityId}/fhirQueryConfiguration
        Step("FhirConfig DELETE ? 202", "Removes FHIR query config", "/api/data/{facilityId}/fhirQueryConfiguration"),

        // POST /api/data/{facilityId}/QueryPlan
        Step("QueryPlan POST ? 201", "Creates a Discharge query plan", "/api/data/{facilityId}/QueryPlan"),
        Step("QueryPlan POST ? 409", "Returns 409 when plan already exists", "/api/data/{facilityId}/QueryPlan"),

        // GET /api/data/{facilityId}/QueryPlan
        Step("QueryPlan GET ? 200", "Retrieves the Discharge query plan", "/api/data/{facilityId}/QueryPlan"),
        Step("QueryPlan GET ? 404", "Returns 404 after deletion", "/api/data/{facilityId}/QueryPlan"),

        // DELETE /api/data/{facilityId}/QueryPlan
        Step("QueryPlan DELETE ? 202", "Removes the query plan", "/api/data/{facilityId}/QueryPlan"),

        // GET /api/data/acquisition-logs (search)
        Step("AcquisitionLogs GET ? 200 (has data)", "Searches acquisition logs for seeded facility/report", "GET /api/data/acquisition-logs"),
        Step("AcquisitionLogs GET ? 200", "Searches acquisition logs", "GET /api/data/acquisition-logs"),

        // GET /api/data/acquisition-logs/{id}
        Step("GetLog ? 200 (has data)", "Returns a seeded acquisition log", "GET /api/data/acquisition-logs/{id}"),
        Step("GetLog ? 400 (id=0)", "Returns 400 for zero ID", "GET /api/data/acquisition-logs/{id}"),
        Step("GetLog ? 404 (non-existent)", "Returns 404 for non-existent log", "GET /api/data/acquisition-logs/{id}"),

        // GET /api/data/acquisition-logs/{id}/notes
        Step("GetNotes ? 200 (has data)", "Returns notes for a seeded log", "GET /api/data/acquisition-logs/{id}/notes"),
        Step("GetNotes ? 400 (id=0)", "Returns 400 for zero ID", "GET /api/data/acquisition-logs/{id}/notes"),
        Step("GetNotes ? 200 (non-existent)", "Returns 200 with empty list for non-existent log", "GET /api/data/acquisition-logs/{id}/notes"),

        // GET /api/data/acquisition-logs/{id}/reference-resources
        Step("GetRefs ? 200 (has data)", "Returns reference resources page for a seeded log", "GET /api/data/acquisition-logs/{id}/reference-resources"),
        Step("GetRefs ? 400 (id=0)", "Returns 400 for zero ID", "GET /api/data/acquisition-logs/{id}/reference-resources"),
        Step("GetRefs ? 200 (non-existent)", "Returns 200 with empty page for non-existent log", "GET /api/data/acquisition-logs/{id}/reference-resources"),

        // POST /api/data/acquisition-logs/{id}/process
        Step("Process ? 202 (has data)", "Processes a seeded log entry", "POST /api/data/acquisition-logs/{id}/process"),
        Step("Process ? 400 (id=0)", "Returns 400 for zero ID", "POST /api/data/acquisition-logs/{id}/process"),
        Step("Process ? 404 (non-existent)", "Returns 404 for non-existent log", "POST /api/data/acquisition-logs/{id}/process"),

        // POST /api/data/acquisition-logs/process-bulk
        Step("ProcessBulk ? 202", "Triggers bulk log processing", "POST /api/data/acquisition-logs/process-bulk"),
        Step("ProcessBulk ? 400 (empty)", "Returns 400 for empty list", "POST /api/data/acquisition-logs/process-bulk"),

        // POST /api/data/acquisition-logs/cancel-bulk
        Step("CancelBulk ? 202", "Cancels bulk log processing", "POST /api/data/acquisition-logs/cancel-bulk"),
        Step("CancelBulk ? 400 (empty)", "Returns 400 for empty list", "POST /api/data/acquisition-logs/cancel-bulk"),
        Step("CancelBulk ? 400 (neg hours)", "Returns 400 for negative minAgeHours", "POST /api/data/acquisition-logs/cancel-bulk"),

        // POST /api/data/acquisition-logs/process-by-filter
        Step("ProcessFilter ? 202", "Processes logs by filter criteria", "POST /api/data/acquisition-logs/process-by-filter"),
        Step("ProcessFilter ? 400 (no criteria)", "Returns 400 when no filter criteria provided", "POST /api/data/acquisition-logs/process-by-filter"),

        // POST /api/data/acquisition-logs/cancel-by-filter
        Step("CancelFilter ? 202", "Cancels logs by filter criteria", "POST /api/data/acquisition-logs/cancel-by-filter"),
        Step("CancelFilter ? 400 (no criteria)", "Returns 400 when no filter criteria provided", "POST /api/data/acquisition-logs/cancel-by-filter"),
        Step("CancelFilter ? 400 (neg hours)", "Returns 400 for negative minAgeHours", "POST /api/data/acquisition-logs/cancel-by-filter"),

        // DELETE /api/data/acquisition-logs/{id}
        Step("DeleteLog ? 400 (id=0)", "Returns 400 for zero ID", "DELETE /api/data/acquisition-logs/{id}"),
        Step("DeleteLog ? 404 (non-existent)", "Returns 404 for non-existent log", "DELETE /api/data/acquisition-logs/{id}"),

        // DELETE /api/data/acquisition-logs/facility/{id}
        Step("SoftDeleteFacility ? 204", "Soft-deletes logs by facility (no-op if none)", "DELETE /api/data/acquisition-logs/facility/{id}"),

        // DELETE /api/data/acquisition-logs/report/{id}
        Step("SoftDeleteReport ? 204", "Soft-deletes logs by report (no-op if none)", "DELETE /api/data/acquisition-logs/report/{id}"),

        // PATCH /api/data/acquisition-logs/report/{id}/restore
        Step("RestoreReport ? 204", "Restores logs by report (no-op if none)", "PATCH /api/data/acquisition-logs/report/{id}/restore"),

        // PATCH /api/data/acquisition-logs/facility/{id}/restore
        Step("RestoreFacility ? 200", "Restores logs by facility (returns count=0)", "PATCH /api/data/acquisition-logs/facility/{id}/restore"),

        // GET /api/data/report/{reportId}/status-counts
        Step("StatusCounts GET ? 200 (has data)", "Gets report status counts for seeded report", "GET /api/data/report/{reportId}/status-counts"),
        Step("StatusCounts GET ? 200", "Gets report status counts", "GET /api/data/report/{reportId}/status-counts"),

        // GET /api/data/report/{reportId}/summary
        Step("Summary GET ? 200 (has data)", "Gets report summary for seeded report", "GET /api/data/report/{reportId}/summary"),
        Step("Summary GET ? 200", "Gets report summary", "GET /api/data/report/{reportId}/summary"),

        // GET /api/data/report/{reportId}/acquired-resource-ids
        Step("ResourceIds GET ? 200 (has data)", "Gets acquired resource IDs for seeded facility/report", "GET /api/data/report/{reportId}/acquired-resource-ids"),
        Step("ResourceIds GET ? 200", "Gets acquired resource IDs", "GET /api/data/report/{reportId}/acquired-resource-ids"),
        Step("ResourceIds ? 400 (no facility)", "Returns 400 when facilityId query param is empty", "GET /api/data/report/{reportId}/acquired-resource-ids"),

        // GET /api/data/acquisition-logs (search) — bad sortBy
        Step("Search ? 400 (bad sortBy)", "Returns 400 for invalid sortBy column", "GET /api/data/acquisition-logs"),
    ];

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

            // POST again ? 409 (already exists)
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

            // POST Query Plan again ? 409 (already exists)
            results.Add(await RunStepAsync(StepNames.QueryPlanPost409, 409, async () =>
                await _client.CreateQueryPlanAsync(facilityId, BuildDischargePlanRequest(facilityId, "ApiHealth-Discharge-Dup"), ct), ct: ct));

            // DELETE Query Plan
            results.Add(await RunStepAsync(StepNames.QueryPlanDelete202, 202, async () =>
            {
                var resp = await _client.DeleteQueryPlanAsync(facilityId, "Discharge", ct);
                if (resp.IsSuccessStatusCode) dischargePlanCreated = false;
                return resp;
            }, ct: ct));

            // GET Query Plan ? 404 (after delete)
            results.Add(await RunStepAsync(StepNames.QueryPlanGet404, 404, async () =>
                await _client.GetQueryPlanAsync(facilityId, "Discharge", ct), ct: ct));

            // DELETE FHIR Query Config
            results.Add(await RunStepAsync(StepNames.FhirConfigDelete202, 202, async () =>
            {
                var resp = await _client.DeleteFhirQueryConfigurationAsync(facilityId, ct);
                if (resp.IsSuccessStatusCode) queryConfigCreated = false;
                return resp;
            }, ct: ct));

            // GET FHIR Query Config ? 404 (after delete)
            results.Add(await RunStepAsync(StepNames.FhirConfigGet404, 404, async () =>
                await _client.GetFhirQueryConfigurationAsync(facilityId, ct), ct: ct));

            // --- Read-only operations (non-existent resources ? prove reachability) ---
            var fakeReportId = Guid.NewGuid().ToString();

            results.Add(await RunStepAsync(StepNames.AcquisitionLogsGet200, 200, async () =>
                await _client.SearchAcquisitionLogsAsync(facilityId, fakeReportId, cancellationToken: ct), ct: ct));

            // === GET /acquisition-logs/{id} ? 400, 404 ===
            results.Add(await RunStepAsync(StepNames.GetLog400Id0, 400, async () =>
                await _client.GetAcquisitionLogByIdAsync(0, ct), ct: ct));

            results.Add(await RunStepAsync(StepNames.GetLog404NonExistent, 404, async () =>
                await _client.GetAcquisitionLogByIdAsync(long.MaxValue, ct), ct: ct));

            // === GET /acquisition-logs/{id}/notes ? 400, 200 ===
            results.Add(await RunStepAsync(StepNames.GetNotes400Id0, 400, async () =>
                await _client.GetAcquisitionLogNotesAsync(0, ct), ct: ct));

            results.Add(await RunStepAsync(StepNames.GetNotes200NonExistent, 200, async () =>
                await _client.GetAcquisitionLogNotesAsync(long.MaxValue, ct), ct: ct));

            // === GET /acquisition-logs/{id}/reference-resources ? 400, 200 ===
            results.Add(await RunStepAsync(StepNames.GetRefs400Id0, 400, async () =>
                await _client.GetReferenceResourcesForLogAsync(0, cancellationToken: ct), ct: ct));

            results.Add(await RunStepAsync(StepNames.GetRefs200NonExistent, 200, async () =>
                await _client.GetReferenceResourcesForLogAsync(long.MaxValue, cancellationToken: ct), ct: ct));

            // === POST /acquisition-logs/{id}/process ? 400, 404 ===
            results.Add(await RunStepAsync(StepNames.Process400Id0, 400, async () =>
                await _client.ProcessAcquisitionLogAsync(0, ct), ct: ct));

            results.Add(await RunStepAsync(StepNames.Process404NonExistent, 404, async () =>
                await _client.ProcessAcquisitionLogAsync(long.MaxValue, ct), ct: ct));

            // === POST /process-bulk ? 202, 400 ===
            results.Add(await RunStepAsync(StepNames.ProcessBulk202, 202, async () =>
                await _client.ProcessAcquisitionLogsBulkAsync([long.MaxValue], ct), ct: ct));

            results.Add(await RunStepAsync(StepNames.ProcessBulk400Empty, 400, async () =>
                await _client.ProcessAcquisitionLogsBulkAsync([], ct), ct: ct));

            // === POST /cancel-bulk ? 202, 400 (empty), 400 (neg hours) ===
            results.Add(await RunStepAsync(StepNames.CancelBulk202, 202, async () =>
                await _client.CancelAcquisitionLogsBulkAsync([long.MaxValue], minAgeHours: 0, cancellationToken: ct), ct: ct));

            results.Add(await RunStepAsync(StepNames.CancelBulk400Empty, 400, async () =>
                await _client.CancelAcquisitionLogsBulkAsync([], minAgeHours: 0, cancellationToken: ct), ct: ct));

            results.Add(await RunStepAsync(StepNames.CancelBulk400NegHours, 400, async () =>
                await _client.CancelAcquisitionLogsBulkAsync([1], minAgeHours: -1, cancellationToken: ct), ct: ct));

            // === POST /process-by-filter ? 202, 400 ===
            results.Add(await RunStepAsync(StepNames.ProcessFilter202, 202, async () =>
                await _client.ProcessAcquisitionLogsByFilterAsync(new { FacilityId = facilityId }, ct), ct: ct));

            results.Add(await RunStepAsync(StepNames.ProcessFilter400NoCriteria, 400, async () =>
                await _client.ProcessAcquisitionLogsByFilterAsync(new { }, ct), ct: ct));

            // === POST /cancel-by-filter ? 202, 400 (no criteria), 400 (neg hours) ===
            results.Add(await RunStepAsync(StepNames.CancelFilter202, 202, async () =>
                await _client.CancelAcquisitionLogsByFilterAsync(new { FacilityId = facilityId }, minAgeHours: 0, cancellationToken: ct), ct: ct));

            results.Add(await RunStepAsync(StepNames.CancelFilter400NoCriteria, 400, async () =>
                await _client.CancelAcquisitionLogsByFilterAsync(new { }, minAgeHours: 0, cancellationToken: ct), ct: ct));

            results.Add(await RunStepAsync(StepNames.CancelFilter400NegHours, 400, async () =>
                await _client.CancelAcquisitionLogsByFilterAsync(new { FacilityId = facilityId }, minAgeHours: -1, cancellationToken: ct), ct: ct));

            // === DELETE /acquisition-logs/{id} ? 400, 404 ===
            results.Add(await RunStepAsync(StepNames.DeleteLog400Id0, 400, async () =>
                await _client.DeleteAcquisitionLogAsync(0, ct), ct: ct));

            results.Add(await RunStepAsync(StepNames.DeleteLog404NonExistent, 404, async () =>
                await _client.DeleteAcquisitionLogAsync(long.MaxValue, ct), ct: ct));

            // === DELETE /facility/{id} ? 204 ===
            results.Add(await RunStepAsync(StepNames.SoftDeleteFacility204, 204, async () =>
                await _client.SoftDeleteLogsByFacilityAsync(facilityId, ct), ct: ct));

            // === DELETE /report/{id} ? 204 ===
            results.Add(await RunStepAsync(StepNames.SoftDeleteReport204, 204, async () =>
                await _client.SoftDeleteLogsByReportTrackingIdAsync(Guid.NewGuid().ToString(), ct), ct: ct));

            // === PATCH /report/{id}/restore ? 204 ===
            results.Add(await RunStepAsync(StepNames.RestoreReport204, 204, async () =>
                await _client.RestoreLogsByReportTrackingIdAsync(Guid.NewGuid().ToString(), ct), ct: ct));

            // === PATCH /facility/{id}/restore ? 200 ===
            results.Add(await RunStepAsync(StepNames.RestoreFacility200, 200, async () =>
                await _client.RestoreLogsByFacilityAsync(facilityId, ct), ct: ct));

            results.Add(await RunStepAsync(StepNames.StatusCountsGet200, 200, async () =>
                await _client.GetReportStatusCountsAsync(fakeReportId, ct), ct: ct));

            results.Add(await RunStepAsync(StepNames.SummaryGet200, 200, async () =>
                await _client.GetReportSummaryAsync(fakeReportId, ct), ct: ct));

            results.Add(await RunStepAsync(StepNames.ResourceIdsGet200, 200, async () =>
                await _client.GetAcquiredResourceIdsForReportAsync(facilityId, fakeReportId, ct), ct: ct));

            // ResourceIds ? 400 (empty facilityId)
            results.Add(await RunStepAsync(StepNames.ResourceIds400NoFacility, 400, async () =>
                await _client.GetAcquiredResourceIdsForReportAsync(" ", fakeReportId, ct), ct: ct));

            // Search ? 400 (invalid sortBy)
            results.Add(await RunStepAsync(StepNames.Search400BadSortBy, 400, async () =>
                await _client.SearchAcquisitionLogsAsync(facilityId, fakeReportId, sortBy: "InvalidColumn", cancellationToken: ct), ct: ct));
        }
        finally
        {
            if (dischargePlanCreated) await TryCleanupAsync(() => _client.DeleteQueryPlanAsync(facilityId, "Discharge", ct));
            if (queryConfigCreated) await TryCleanupAsync(() => _client.DeleteFhirQueryConfigurationAsync(facilityId, ct));
            if (facilityCreated) await TryCleanupAsync(() => _facilityClient.DeleteAsync(facilityId, ct));
        }

        return results;
    }

    public override async Task<ApiTestRunResult> ExecuteStepAsync(string endpointKey, CancellationToken ct = default)
    {
        var results = await ExecuteAsync(ct);
        return results.FirstOrDefault(r => r.EndpointKey == endpointKey)
            ?? new ApiTestRunResult
            {
                EndpointKey = endpointKey,
                ServiceName = ServiceName,
                Passed = false,
                ErrorMessage = "Step not found in suite execution."
            };
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
            ["0"] = new { QueryConfigType = "Parameter", ResourceType = "Patient", Parameters = new[] { new { ParameterType = "Variable", Name = "_id", Variable = 0 } } }
        },
        SupplementalQueries = new Dictionary<string, object>
        {
            ["0"] = new { QueryConfigType = "Reference", ResourceType = "Condition", OperationType = 2, Paged = 100 }
        }
    };

    private ApiEndpointDefinition Step(string name, string desc, string? group = null) => new()
    {
        ServiceName = ServiceName,
        GroupName = group,
        EndpointName = name,
        Description = desc,
        IsTestSuiteStep = true
    };
}
