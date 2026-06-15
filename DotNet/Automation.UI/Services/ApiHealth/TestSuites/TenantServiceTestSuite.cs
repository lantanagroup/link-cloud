using Automation.UI.Models.ApiHealth;
using Automation.UI.Services.ApiHealth.Seeding;
using LantanaGroup.Link.Sdk.Clients;
using LantanaGroup.Link.Shared.Application.Enums;
using LantanaGroup.Link.Shared.Application.Models.Tenant;
using StepNames = Automation.UI.Services.ApiHealth.TestSuites.ApiEndPointLibrary.TenantSteps;

namespace Automation.UI.Services.ApiHealth.TestSuites;

/// <summary>
/// Exercises the Tenant (Facility) service endpoints via LinkSdk.
/// Tests the full CRUD lifecycle plus soft-delete/restore, search, and error paths.
/// </summary>
public sealed class TenantServiceTestSuite : ServiceTestSuiteBase
{
    private readonly IFacilityServiceClient _client;
    private readonly IReportServiceClient _reportClient;
    private readonly IApiHealthSeedContextAccessor _seedContext;

    public override string ServiceName => "Tenant";

    public TenantServiceTestSuite(
        IFacilityServiceClient client,
        IReportServiceClient reportClient,
        IApiHealthSeedContextAccessor seedContext)
    {
        _client = client;
        _reportClient = reportClient;
        _seedContext = seedContext;
    }

    public override IReadOnlyList<ApiHealthSeedRequirement> GetSeedRequirements() =>
    [
        ApiHealthSeedRequirement.ReportSchedule
    ];

    public override IReadOnlyList<ApiEndpointDefinition> GetEndpointDefinitions() =>
        ApiEndPointLibrary.GetServiceEndpoints(ServiceName);

        // GET /api/Facility (search)
        Step("GET /api/Facility (search)", "Search ? 200", "Returns paged facility results"),
        Step("GET /api/Facility (search)", "Search ? 204 (no results)", "Returns 204 when no facilities match"),

        // GET /api/Facility/list
        Step("GET /api/Facility/list", "List ? 200", "Returns dictionary of facilities"),
        Step("GET /api/Facility/list", "List ? 204", "Returns 204 when no facilities match search"),

        // GET /api/Facility/{id}
        Step("GET /api/Facility/{id}", "Get ? 200", "Retrieves a facility that exists"),
        Step("GET /api/Facility/{id}", "Get ? 404", "Returns 404 for non-existent facility"),

        // PUT /api/Facility/{id}
        Step("PUT /api/Facility/{id}", "Update ? 200", "Updates a facility and returns it"),
        Step("PUT /api/Facility/{id}", "Update ? 404 (non-existent)", "Returns 404 for non-existent facility"),
        Step("PUT /api/Facility/{id}", "Update ? 400 (no vendor)", "Returns 400 when Vendor is null"),

        // GET /api/Facility/{id} (exists check)
        Step("GET /api/Facility/{id} (exists)", "CheckExists ? 200", "Returns 200 for a known facility"),
        Step("GET /api/Facility/{id} (exists)", "CheckExists ? 404", "Returns 404 for a non-existent facility"),

        // DELETE /api/Facility/softDelete/{id}
        Step("DELETE /api/Facility/softDelete/{id}", "SoftDelete ? 204", "Soft-deletes the facility"),
        Step("DELETE /api/Facility/softDelete/{id}", "SoftDelete ? 204 (idempotent)", "Returns 204 when already soft-deleted"),
        Step("DELETE /api/Facility/softDelete/{id}", "SoftDelete ? 404 (non-existent)", "Returns 404 for non-existent facility"),

        // PATCH /api/Facility/restore/{id}
        Step("PATCH /api/Facility/restore/{id}", "Restore ? 204", "Restores a soft-deleted facility"),
        Step("PATCH /api/Facility/restore/{id}", "Restore ? 400 (not deleted)", "Returns 400 when facility is not soft-deleted"),
        Step("PATCH /api/Facility/restore/{id}", "Restore ? 404 (non-existent)", "Returns 404 for non-existent facility"),

        // DELETE /api/Facility/{id}
        Step("DELETE /api/Facility/{id}", "Delete ? 204", "Hard-deletes the facility"),
        Step("DELETE /api/Facility/{id}", "Delete ? 404 (non-existent)", "Returns 404 for non-existent facility"),

        // POST /api/Facility/{id}/AdHocReport
        Step("POST /api/Facility/{id}/AdHocReport", "AdHocReport ? 200 (seeded)", "Returns 200 for valid seeded facility and known report type"),
        Step("POST /api/Facility/{id}/AdHocReport", "AdHocReport ? 400 (no measures)", "Returns 400 when ReportTypes is empty"),
        Step("POST /api/Facility/{id}/AdHocReport", "AdHocReport ? 400 (no start date)", "Returns 400 when StartDate is missing"),
        Step("POST /api/Facility/{id}/AdHocReport", "AdHocReport ? 400 (no end date)", "Returns 400 when EndDate is missing"),
        Step("POST /api/Facility/{id}/AdHocReport", "AdHocReport ? 400 (end before start)", "Returns 400 when EndDate <= StartDate"),
        Step("POST /api/Facility/{id}/AdHocReport", "AdHocReport ? 404 (non-existent)", "Returns 404 for non-existent facility"),

        // POST /api/Facility/{id}/RegenerateReport
        Step("POST /api/Facility/{id}/RegenerateReport", "RegenerateReport ? 200 (seeded)", "Returns 200 for valid seeded facility and existing report id"),
        Step("POST /api/Facility/{id}/RegenerateReport", "RegenerateReport ? 400 (no report id)", "Returns 400 when ReportId is empty"),
        Step("POST /api/Facility/{id}/RegenerateReport", "RegenerateReport ? 404 (non-existent facility)", "Returns 404 for non-existent facility"),
        Step("POST /api/Facility/{id}/RegenerateReport", "RegenerateReport ? 404 (non-existent report)", "Returns 404 for non-existent report schedule"),
    ];

    public override async Task<IReadOnlyList<ApiTestRunResult>> ExecuteAsync(CancellationToken ct = default)
    {
        var results = new List<ApiTestRunResult>();

        FacilityModel BuildFacility(
            string id,
            string? name = null,
            Vendor? vendor = Vendor.Epic,
            bool allowNullName = false) => new()
            {
                FacilityId = id,
                FacilityName = allowNullName ? name : name ?? id,
                TimeZone = "America/Chicago",
                Vendor = vendor,
                ScheduledReports = new TenantScheduledReportConfig { Daily = [], Weekly = [], Monthly = [] }
            };

        AdHocReportRequest BuildAdhoc(
            List<string>? reportTypes,
            DateTime? startDate,
            DateTime? endDate) => new()
            {
                ReportTypes = reportTypes ?? [],
                StartDate = startDate,
                EndDate = endDate
            };

        var facilityId = $"ApiHealth-Tenant-{Guid.NewGuid():N}";
        var fakeFacilityId = $"ApiHealth-Tenant-Fake-{Guid.NewGuid():N}";
        var created = false;

        var seededFacilityId = _seedContext.Current?.Report?.FacilityId;
        var seededScheduleId = _seedContext.Current?.Report?.ScheduleId;
        string? seededReportType = null;
        const string seededUnavailable = "Seeded facility/report metadata was unavailable for Tenant seeded success-path checks.";

        if (!string.IsNullOrWhiteSpace(seededScheduleId))
        {
            var seededSchedule = await _reportClient.GetScheduleAsync(seededScheduleId, ct);
            if (seededSchedule.IsSuccessStatusCode)
                seededReportType = seededSchedule.Body?.ReportTypes?.FirstOrDefault();
        }

        try
        {
            // === POST /api/Facility ===

            // Create ? 201
            results.Add(await RunStepAsync(StepNames.Create201, 201, async () =>
            {
                var model = BuildFacility(facilityId);
                var result = await _client.CreateAsync(model, ct);
                if (result.IsSuccessStatusCode) created = true;
                return result;
            }, ct: ct));

            // Create ? 400 (duplicate)
            results.Add(await RunStepAsync(StepNames.Create400Duplicate, 400, async () =>
                await _client.CreateAsync(BuildFacility(facilityId), ct), ct: ct));

            // Create ? 400 (no vendor)
            results.Add(await RunStepAsync(StepNames.Create400NoVendor, 400, async () =>
                await _client.CreateAsync(BuildFacility($"ApiHealth-NoVendor-{Guid.NewGuid():N}", "NoVendor", null), ct), ct: ct));

            // Create ? 400 (no name)
            results.Add(await RunStepAsync(StepNames.Create400NoName, 400, async () =>
                await _client.CreateAsync(BuildFacility($"ApiHealth-NoName-{Guid.NewGuid():N}", name: null, allowNullName: true), ct), ct: ct));

            // === GET /api/Facility (search) ===

            // Search ? 200
            results.Add(await RunStepAsync(StepNames.Search200, 200, async () =>
                await _client.SearchFacilitiesAsync(facilityId, cancellationToken: ct), ct: ct));

            // Search ? 204 (no results)
            results.Add(await RunStepAsync(StepNames.Search204NoResults, 204, async () =>
                await _client.SearchFacilitiesAsync($"NonExistent-{Guid.NewGuid():N}", cancellationToken: ct), ct: ct));

            // === GET /api/Facility/list ===
            results.Add(await RunStepAsync(StepNames.List200, 200, async () =>
                await _client.GetFacilityListAsync(search: facilityId, includeDeleted: false, cancellationToken: ct), ct: ct));

            results.Add(await RunStepAsync(StepNames.List204, 204, async () =>
                await _client.GetFacilityListAsync(search: $"NonExistent-{Guid.NewGuid():N}", includeDeleted: false, cancellationToken: ct), ct: ct));

            // === GET /api/Facility/{id} ===

            // Get ? 200
            results.Add(await RunStepAsync(StepNames.Get200, 200, async () =>
                await _client.GetAsync(facilityId, ct), ct: ct));

            // Get ? 404
            results.Add(await RunStepAsync(StepNames.Get404, 404, async () =>
                await _client.GetAsync(fakeFacilityId, ct), ct: ct));

            // === PUT /api/Facility/{id} ===

            // Update ? 200
            results.Add(await RunStepAsync(StepNames.Update200, 200, async () =>
            {
                var updated = new FacilityModel
                {
                    FacilityId = facilityId,
                    FacilityName = facilityId + "-Updated",
                    TimeZone = "America/Chicago",
                    Vendor = Vendor.Epic,
                    ScheduledReports = new TenantScheduledReportConfig { Daily = [], Weekly = [], Monthly = [] }
                };
                return await _client.UpdateAsync(facilityId, updated, ct);
            }, ct: ct));

            // Update ? 404 (non-existent)
            results.Add(await RunStepAsync(StepNames.Update404NonExistent, 404, async () =>
                await _client.UpdateAsync(fakeFacilityId, BuildFacility(fakeFacilityId), ct), ct: ct));

            // Update ? 400 (no vendor)
            results.Add(await RunStepAsync(StepNames.Update400NoVendor, 400, async () =>
                await _client.UpdateAsync(facilityId, BuildFacility(facilityId, vendor: null), ct), ct: ct));

            // === GET /api/Facility/{id} (exists check) ===

            // CheckExists ? 200
            results.Add(await RunStepAsync(StepNames.CheckExists200, 200, async () =>
                await _client.CheckFacilityExistsAsync(facilityId, ct), ct: ct));

            // CheckExists ? 404
            results.Add(await RunStepAsync(StepNames.CheckExists404, 404, async () =>
                await _client.CheckFacilityExistsAsync(fakeFacilityId, ct), ct: ct));

            // === DELETE /api/Facility/softDelete/{id} ===

            // SoftDelete ? 204
            results.Add(await RunStepAsync(StepNames.SoftDelete204, 204, async () =>
                await _client.SoftDeleteAsync(facilityId, ct), ct: ct));

            // SoftDelete ? 204 (idempotent — already soft-deleted)
            results.Add(await RunStepAsync(StepNames.SoftDelete204Idempotent, 204, async () =>
                await _client.SoftDeleteAsync(facilityId, ct), ct: ct));

            // SoftDelete ? 404 (non-existent)
            results.Add(await RunStepAsync(StepNames.SoftDelete404NonExistent, 404, async () =>
                await _client.SoftDeleteAsync(fakeFacilityId, ct), ct: ct));

            // === PATCH /api/Facility/restore/{id} ===

            // Restore ? 204
            results.Add(await RunStepAsync(StepNames.Restore204, 204, async () =>
                await _client.RestoreAsync(facilityId, ct), ct: ct));

            // Restore ? 400 (not deleted)
            results.Add(await RunStepAsync(StepNames.Restore400NotDeleted, 400, async () =>
                await _client.RestoreAsync(facilityId, ct), ct: ct));

            // Restore ? 404 (non-existent)
            results.Add(await RunStepAsync(StepNames.Restore404NonExistent, 404, async () =>
                await _client.RestoreAsync(fakeFacilityId, ct), ct: ct));

            // === POST /api/Facility/{id}/AdHocReport ===

            if (!string.IsNullOrWhiteSpace(seededFacilityId) && !string.IsNullOrWhiteSpace(seededReportType))
            {
                results.Add(await RunStepAsync(StepNames.AdHoc200Seeded, 200, async () =>
                    await _client.GenerateAdhocReportAsync(
                        seededFacilityId,
                        BuildAdhoc([seededReportType], DateTime.UtcNow.AddDays(-1), DateTime.UtcNow),
                        ct), ct: ct));
            }
            else
            {
                results.Add(SkipStepAsync(StepNames.AdHoc200Seeded, seededUnavailable));
            }

            // AdHocReport ? 400 (no measures)
            results.Add(await RunStepAsync(StepNames.AdHoc400NoMeasures, 400, async () =>
                await _client.GenerateAdhocReportAsync(facilityId, BuildAdhoc([], DateTime.UtcNow.AddDays(-30), DateTime.UtcNow), ct), ct: ct));

            // AdHocReport ? 400 (no start date)
            results.Add(await RunStepAsync(StepNames.AdHoc400NoStartDate, 400, async () =>
                await _client.GenerateAdhocReportAsync(facilityId, BuildAdhoc(["SomeMeasure"], null, DateTime.UtcNow), ct), ct: ct));

            // AdHocReport ? 400 (end before start)
            results.Add(await RunStepAsync(StepNames.AdHoc400NoEndDate, 400, async () =>
                await _client.GenerateAdhocReportAsync(facilityId, BuildAdhoc(["SomeMeasure"], DateTime.UtcNow.AddDays(-30), null), ct), ct: ct));

            // AdHocReport ? 400 (end before start)
            results.Add(await RunStepAsync(StepNames.AdHoc400EndBeforeStart, 400, async () =>
                await _client.GenerateAdhocReportAsync(facilityId, BuildAdhoc(["SomeMeasure"], DateTime.UtcNow, DateTime.UtcNow.AddDays(-30)), ct), ct: ct));

            // AdHocReport ? 404 (non-existent facility)
            results.Add(await RunStepAsync(StepNames.AdHoc404NonExistent, 404, async () =>
                await _client.GenerateAdhocReportAsync(fakeFacilityId, BuildAdhoc(["SomeMeasure"], DateTime.UtcNow.AddDays(-30), DateTime.UtcNow), ct), ct: ct));

            // === POST /api/Facility/{id}/RegenerateReport ===

            if (!string.IsNullOrWhiteSpace(seededFacilityId) && !string.IsNullOrWhiteSpace(seededScheduleId))
            {
                results.Add(await RunStepAsync(StepNames.Regenerate200Seeded, 200, async () =>
                    await _client.RegenerateReportAsync(seededFacilityId, new RegenerateReportRequest
                    {
                        ReportId = seededScheduleId
                    }, ct), ct: ct));
            }
            else
            {
                results.Add(SkipStepAsync(StepNames.Regenerate200Seeded, seededUnavailable));
            }

            // RegenerateReport ? 400 (no report id)
            results.Add(await RunStepAsync(StepNames.Regenerate400NoReportId, 400, async () =>
                await _client.RegenerateReportAsync(facilityId, new RegenerateReportRequest
                {
                    ReportId = ""
                }, ct), ct: ct));

            // RegenerateReport ? 404 (non-existent facility)
            results.Add(await RunStepAsync(StepNames.Regenerate404NonExistentFacility, 404, async () =>
                await _client.RegenerateReportAsync(fakeFacilityId, new RegenerateReportRequest
                {
                    ReportId = Guid.NewGuid().ToString()
                }, ct), ct: ct));

            // RegenerateReport ? 404 (non-existent report)
            results.Add(await RunStepAsync(StepNames.Regenerate404NonExistentReport, 404, async () =>
                await _client.RegenerateReportAsync(facilityId, new RegenerateReportRequest
                {
                    ReportId = Guid.NewGuid().ToString()
                }, ct), ct: ct));

            // === DELETE /api/Facility/{id} (hard delete) ===

            // Delete ? 204
            results.Add(await RunStepAsync(StepNames.Delete204, 204, async () =>
            {
                var resp = await _client.DeleteAsync(facilityId, ct);
                if (resp.IsSuccessStatusCode) created = false;
                return resp;
            }, ct: ct));

            // Delete ? 404 (non-existent)
            results.Add(await RunStepAsync(StepNames.Delete404NonExistent, 404, async () =>
                await _client.DeleteAsync(fakeFacilityId, ct), ct: ct));
        }
        finally
        {
            if (created) await TryCleanupAsync(() => _client.DeleteAsync(facilityId, ct));
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

    private ApiEndpointDefinition Step(string group, string name, string desc) => new()
    {
        ServiceName = ServiceName,
        GroupName = group,
        EndpointName = name,
        Description = desc,
        IsTestSuiteStep = true
    };
}
