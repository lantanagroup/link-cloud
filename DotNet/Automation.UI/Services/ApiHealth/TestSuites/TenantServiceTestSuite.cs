using Automation.UI.Models.ApiHealth;
using Automation.UI.Services.ApiHealth.Seeding;
using LantanaGroup.Link.Sdk.Clients;
using LantanaGroup.Link.Shared.Application.Enums;
using LantanaGroup.Link.Shared.Application.Models.Tenant;
using StepNames = Automation.UI.Services.ApiHealth.TestSuites.ApiEndPointLibrary.TenantSteps;

namespace Automation.UI.Services.ApiHealth.TestSuites;

/// <summary>
/// Exercises Tenant facility and Vendor service endpoints via LinkSdk.
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

    public override async Task<IReadOnlyList<ApiTestRunResult>> ExecuteAsync(CancellationToken ct = default)
    {
        var results = new List<ApiTestRunResult>();
        var testVendor = new VendorModel { Name = $"ApiHealth-Vendor-{Guid.NewGuid():N}" };
        FacilityModel BuildFacility(
            string id,
            string? name = null,
            VendorModel? vendor = null,
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
        var vendorName = $"ApiHealth-Vendor-{Guid.NewGuid():N}";
        var vendorCreated = false;
        var vendorId = Guid.Empty;

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

            // Create → 201
            results.Add(await RunStepAsync(StepNames.Create201, 201, async () =>
            {
                var model = BuildFacility(facilityId, vendor: testVendor);
                var result = await _client.CreateAsync(model, ct);
                if (result.IsSuccessStatusCode) created = true;
                return result;
            }, ct: ct));

            // Create → 400 (duplicate)
            results.Add(await RunStepAsync(StepNames.Create400Duplicate, 400, async () =>
                await _client.CreateAsync(BuildFacility(facilityId, vendor: testVendor), ct), ct: ct));

            // Create → 400 (no name)
            results.Add(await RunStepAsync(StepNames.Create400NoName, 400, async () =>
                await _client.CreateAsync(BuildFacility($"ApiHealth-NoName-{Guid.NewGuid():N}", name: null, allowNullName: true, vendor: testVendor), ct), ct: ct));

            // === POST /api/Vendor ===

            // Vendor POST → 201
            results.Add(await RunStepAsync(StepNames.VendorPost201, 201, async () =>
            {
                var response = await _client.CreateVendorAsync(new CreateVendorModel { Name = vendorName }, ct);
                if (response.IsSuccessStatusCode)
                {
                    vendorId = response.Body?.Id ?? Guid.Empty;
                    vendorCreated = vendorId != Guid.Empty;
                }

                return response;
            }, ct: ct));

            // Vendor POST → 409 (duplicate)
            results.Add(await RunStepAsync(StepNames.VendorPost409, 409, async () =>
                await _client.CreateVendorAsync(new CreateVendorModel { Name = vendorName }, ct), ct: ct));

            // Vendor GET → 200
            results.Add(await RunStepAsync(StepNames.VendorGet200, 200, async () =>
            {
                if (vendorId == Guid.Empty)
                    throw new InvalidOperationException("Expected a vendor id after creating the Vendor.");

                return await _client.GetVendorAsync(vendorId, ct);
            }, ct: ct));

            // Vendors GET → 200
            results.Add(await RunStepAsync(StepNames.VendorsGet200, 200, async () =>
                await _client.GetVendorsAsync(ct), ct: ct));

            // === GET /api/Facility (search) ===

            // Search → 200
            results.Add(await RunStepAsync(StepNames.Search200, 200, async () =>
                await _client.SearchFacilitiesAsync(facilityId, cancellationToken: ct), ct: ct));

            // Search → 204 (no results)
            results.Add(await RunStepAsync(StepNames.Search204NoResults, 204, async () =>
                await _client.SearchFacilitiesAsync($"NonExistent-{Guid.NewGuid():N}", cancellationToken: ct), ct: ct));

            // === GET /api/Facility/list ===
            results.Add(await RunStepAsync(StepNames.List200, 200, async () =>
                await _client.GetFacilityListAsync(search: facilityId, includeDeleted: false, cancellationToken: ct), ct: ct));

            results.Add(await RunStepAsync(StepNames.List204, 204, async () =>
                await _client.GetFacilityListAsync(search: $"NonExistent-{Guid.NewGuid():N}", includeDeleted: false, cancellationToken: ct), ct: ct));

            // === GET /api/Facility/{id} ===

            // Get → 200
            results.Add(await RunStepAsync(StepNames.Get200, 200, async () =>
                await _client.GetAsync(facilityId, ct), ct: ct));

            // Get → 404
            results.Add(await RunStepAsync(StepNames.Get404, 404, async () =>
                await _client.GetAsync(fakeFacilityId, ct), ct: ct));

            // === PUT /api/Facility/{id} ===

            // Update → 200
            results.Add(await RunStepAsync(StepNames.Update200, 200, async () =>
            {
                var updated = new FacilityModel
                {
                    FacilityId = facilityId,
                    FacilityName = facilityId + "-Updated",
                    TimeZone = "America/Chicago",
                    Vendor = new VendorModel { Name = "Epic" },
                    ScheduledReports = new TenantScheduledReportConfig { Daily = [], Weekly = [], Monthly = [] }
                };
                return await _client.UpdateAsync(facilityId, updated, ct);
            }, ct: ct));

            // Update → 404 (non-existent)
            results.Add(await RunStepAsync(StepNames.Update404NonExistent, 404, async () =>
                await _client.UpdateAsync(fakeFacilityId, BuildFacility(fakeFacilityId), ct), ct: ct));

            // === GET /api/Facility/{id} (exists check) ===

            // CheckExists → 200
            results.Add(await RunStepAsync(StepNames.CheckExists200, 200, async () =>
                await _client.CheckFacilityExistsAsync(facilityId, ct), ct: ct));

            // CheckExists → 404
            results.Add(await RunStepAsync(StepNames.CheckExists404, 404, async () =>
                await _client.CheckFacilityExistsAsync(fakeFacilityId, ct), ct: ct));

            // === DELETE /api/Facility/softDelete/{id} ===

            // SoftDelete → 204
            results.Add(await RunStepAsync(StepNames.SoftDelete204, 204, async () =>
                await _client.SoftDeleteAsync(facilityId, ct), ct: ct));

            // SoftDelete → 204 (idempotent — already soft-deleted)
            results.Add(await RunStepAsync(StepNames.SoftDelete204Idempotent, 204, async () =>
                await _client.SoftDeleteAsync(facilityId, ct), ct: ct));

            // SoftDelete → 404 (non-existent)
            results.Add(await RunStepAsync(StepNames.SoftDelete404NonExistent, 404, async () =>
                await _client.SoftDeleteAsync(fakeFacilityId, ct), ct: ct));

            // === PATCH /api/Facility/restore/{id} ===

            // Restore → 204
            results.Add(await RunStepAsync(StepNames.Restore204, 204, async () =>
                await _client.RestoreAsync(facilityId, ct), ct: ct));

            // Restore → 400 (not deleted)
            results.Add(await RunStepAsync(StepNames.Restore400NotDeleted, 400, async () =>
                await _client.RestoreAsync(facilityId, ct), ct: ct));

            // Restore → 404 (non-existent)
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

            // AdHocReport → 400 (no measures)
            results.Add(await RunStepAsync(StepNames.AdHoc400NoMeasures, 400, async () =>
                await _client.GenerateAdhocReportAsync(facilityId, BuildAdhoc([], DateTime.UtcNow.AddDays(-30), DateTime.UtcNow), ct), ct: ct));

            // AdHocReport → 400 (no start date)
            results.Add(await RunStepAsync(StepNames.AdHoc400NoStartDate, 400, async () =>
                await _client.GenerateAdhocReportAsync(facilityId, BuildAdhoc(["SomeMeasure"], null, DateTime.UtcNow), ct), ct: ct));

            // AdHocReport → 400 (end before start)
            results.Add(await RunStepAsync(StepNames.AdHoc400NoEndDate, 400, async () =>
                await _client.GenerateAdhocReportAsync(facilityId, BuildAdhoc(["SomeMeasure"], DateTime.UtcNow.AddDays(-30), null), ct), ct: ct));

            // AdHocReport → 400 (end before start)
            results.Add(await RunStepAsync(StepNames.AdHoc400EndBeforeStart, 400, async () =>
                await _client.GenerateAdhocReportAsync(facilityId, BuildAdhoc(["SomeMeasure"], DateTime.UtcNow, DateTime.UtcNow.AddDays(-30)), ct), ct: ct));

            // AdHocReport → 404 (non-existent facility)
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

            // RegenerateReport → 400 (no report id)
            results.Add(await RunStepAsync(StepNames.Regenerate400NoReportId, 400, async () =>
                await _client.RegenerateReportAsync(facilityId, new RegenerateReportRequest
                {
                    ReportId = ""
                }, ct), ct: ct));

            // RegenerateReport → 404 (non-existent facility)
            results.Add(await RunStepAsync(StepNames.Regenerate404NonExistentFacility, 404, async () =>
                await _client.RegenerateReportAsync(fakeFacilityId, new RegenerateReportRequest
                {
                    ReportId = Guid.NewGuid().ToString()
                }, ct), ct: ct));

            // RegenerateReport → 404 (non-existent report)
            results.Add(await RunStepAsync(StepNames.Regenerate404NonExistentReport, 404, async () =>
                await _client.RegenerateReportAsync(facilityId, new RegenerateReportRequest
                {
                    ReportId = Guid.NewGuid().ToString()
                }, ct), ct: ct));

            // Vendor DELETE → 204
            results.Add(await RunStepAsync(StepNames.VendorDelete204, 204, async () =>
            {
                if (vendorId == Guid.Empty)
                    throw new InvalidOperationException("Expected a vendor id before deleting the Vendor.");

                var response = await _client.DeleteVendorAsync(vendorId, ct);
                if (response.IsSuccessStatusCode)
                    vendorCreated = false;

                return response;
            }, ct: ct));

            // === DELETE /api/Facility/{id} (hard delete) ===

            // Delete → 204
            results.Add(await RunStepAsync(StepNames.Delete204, 204, async () =>
            {
                var resp = await _client.DeleteAsync(facilityId, ct);
                if (resp.IsSuccessStatusCode) created = false;
                return resp;
            }, ct: ct));

            // Delete → 404 (non-existent)
            results.Add(await RunStepAsync(StepNames.Delete404NonExistent, 404, async () =>
                await _client.DeleteAsync(fakeFacilityId, ct), ct: ct));
        }
        finally
        {
            if (vendorCreated) await TryCleanupAsync(() => _client.DeleteVendorAsync(vendorId, ct));
            if (created) await TryCleanupAsync(() => _client.DeleteAsync(facilityId, ct));
        }

        return results;
    }
}
