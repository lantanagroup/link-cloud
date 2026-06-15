using Automation.UI.Models.ApiHealth;
using LantanaGroup.Link.Sdk.Clients;
using LantanaGroup.Link.Sdk.ApiClient;
using Automation.UI.Services.ApiHealth.Seeding;
using StepNames = Automation.UI.Services.ApiHealth.TestSuites.ApiEndPointLibrary.ReportSteps;

namespace Automation.UI.Services.ApiHealth.TestSuites;

/// <summary>
/// Exercises Report service endpoints via LinkSdk.
/// Tests schedules, entries, resources, and populations across all response code paths.
/// </summary>
public sealed class ReportServiceTestSuite : ServiceTestSuiteBase
{
    private readonly IReportServiceClient _client;
    private readonly IApiHealthSeedContextAccessor _seedContext;

    public override string ServiceName => "Report";
    public ReportServiceTestSuite(
        IReportServiceClient client,
        IApiHealthSeedContextAccessor seedContext)
    {
        _client = client;
        _seedContext = seedContext;
    }

    public override IReadOnlyList<ApiEndpointDefinition> GetEndpointDefinitions() =>
        ApiEndPointLibrary.GetServiceEndpoints(ServiceName);

        // GET /api/schedules/facilities/{facilityId}
        Step("GET /api/schedules/facilities/{id}", "Get By Facility ? 400 (empty)", "Returns 400 for empty facilityId"),
        Step("GET /api/schedules/facilities/{id}", "Get By Facility ? 200 (empty)", "Returns empty list for non-existent facility"),
        Step("GET /api/schedules/facilities/{id}", "Get By Facility ? 200 (has data)", "Returns schedule(s) for facility with reports"),

        // GET /api/schedules/search
        Step("GET /api/schedules/search", "Search ? 200", "Returns paged results"),

        // DELETE /api/schedules/{id}
        Step("DELETE /api/schedules/{id}", "SoftDelete ? 400 (bad guid)", "Returns 400 for invalid GUID format"),
        Step("DELETE /api/schedules/{id}", "SoftDelete ? 404", "Returns 404 for non-existent schedule"),
        Step("DELETE /api/schedules/{id}", "SoftDelete ? 204", "Soft-deletes a real schedule"),

        // PATCH /api/schedules/{id}/restore
        Step("PATCH /api/schedules/{id}/restore", "Restore ? 400 (bad guid)", "Returns 400 for invalid GUID format"),
        Step("PATCH /api/schedules/{id}/restore", "Restore ? 404", "Returns 404 for non-existent schedule"),
        Step("PATCH /api/schedules/{id}/restore", "Restore ? 204", "Restores a soft-deleted schedule"),

        // PATCH /api/schedules/facility/{facilityId}/status
        Step("PATCH /api/schedules/facility/{id}/status", "SetStatus ? 204", "Sets deleted status for facility"),

        // GET /api/entries/schedules/{id}
        Step("GET /api/entries/{id}", "Entry GET ? 200 (has data)", "Returns seeded entry by id"),
        Step("GET /api/entries/{id}", "Entry GET ? 404", "Returns 404 for non-existent entry"),
        Step("GET /api/entries/{id}", "Entry GET ? 400 (bad guid)", "Returns 400 for invalid GUID format"),
        Step("GET /api/entries/schedules/{id}", "Entries ? 200 (has data)", "Returns entries for seeded schedule"),
        Step("GET /api/entries/schedules/{id}", "Entries ? 404", "Returns 404 for non-existent schedule"),
        Step("GET /api/entries/schedules/{id}", "Entries ? 400 (empty id)", "Returns 400 for empty reportScheduleId"),
        Step("GET /api/entries/schedules/{id}", "Entries ? 400 (bad guid)", "Returns 400 for invalid GUID format"),

        // GET /api/entries/schedules/{id}/count
        Step("GET /api/entries/schedules/{id}/count", "Entry Count ? 200 (has data)", "Returns count for seeded schedule"),
        Step("GET /api/entries/schedules/{id}/count", "Entry Count ? 404", "Returns 404 for non-existent schedule"),
        Step("GET /api/entries/schedules/{id}/count", "Entry Count ? 400 (bad guid)", "Returns 400 for invalid GUID"),

        // GET /api/entries/schedules/{id}/summary
        Step("GET /api/entries/schedules/{id}/summary", "Entry Summary ? 200 (has data)", "Returns summary for seeded schedule"),
        Step("GET /api/entries/schedules/{id}/summary", "Entry Summary ? 404", "Returns 404 for non-existent schedule"),
        Step("GET /api/entries/schedules/{id}/summary", "Entry Summary ? 400 (bad guid)", "Returns 400 for invalid GUID"),

        // GET /api/entries/schedules/{id}/patients/{patientId}
        Step("GET /api/entries/schedules/{id}/patients/{pid}", "Entry By Patient ? 200 (has data)", "Returns seeded entry for seeded patient"),
        Step("GET /api/entries/schedules/{id}/patients/{pid}", "Entry By Patient ? 404", "Returns 404 for non-existent"),
        Step("GET /api/entries/schedules/{id}/patients/{pid}", "Entry By Patient ? 400 (bad guid)", "Returns 400 for invalid GUID"),
        Step("GET /api/entries/patients/{pid}", "Entries By Patient ? 200 (has data)", "Returns entries for seeded patient"),
        Step("GET /api/entries/patients/{pid}", "Entries By Patient ? 404", "Returns 404 for non-existent patient"),

        // GET /api/resources/search
        SkipStep("Resource GET ? 200 (has data)", "Seeded resource lookup by id", "Resource rows are intentionally not persisted for seeded runs in this environment, so no deterministic resource id exists.", "GET /api/resources/{id}"),
        Step("GET /api/resources/{id}", "Resource GET ? 404", "Returns 404 for non-existent resource"),
        Step("GET /api/resources/{id}", "Resource GET ? 400 (bad guid)", "Returns 400 for invalid GUID format"),
        Step("GET /api/resources/schedules/{id}", "Resources By Schedule ? 200 (has data)", "Returns resources for seeded schedule"),
        Step("GET /api/resources/schedules/{id}", "Resources By Schedule ? 400 (bad guid)", "Returns 400 for invalid GUID format"),
        Step("GET /api/resources/schedules/{id}/patients/{pid}", "Resources By Schedule+Patient ? 200 (has data)", "Returns resources for seeded schedule/patient"),
        Step("GET /api/resources/schedules/{id}/patients/{pid}", "Resources By Schedule+Patient ? 400 (bad guid)", "Returns 400 for invalid GUID format"),
        Step("GET /api/resources/patients/{pid}", "Resources By Patient ? 200 (has data)", "Returns resources for seeded patient"),
        Step("GET /api/resources/search", "Search Resources ? 200 (has data)", "Returns paged resource results for seeded schedule"),
        Step("GET /api/resources/search", "Search Resources ? 200", "Returns paged resource results"),

        // GET /api/populations/schedules/{id}
        Step("GET /api/populations/{id}", "Population GET ? 200 (has data)", "Returns seeded population by id"),
        Step("GET /api/populations/{id}", "Population GET ? 404", "Returns 404 for non-existent population"),
        Step("GET /api/populations/{id}", "Population GET ? 400 (bad guid)", "Returns 400 for invalid GUID format"),
        Step("GET /api/populations/schedules/{id}", "Populations ? 400 (empty id)", "Returns 400 for empty reportScheduleId"),
        Step("GET /api/populations/schedules/{id}", "Populations ? 200 (has data)", "Returns populations for seeded schedule"),
        Step("GET /api/populations/schedules/{id}", "Populations ? 200", "Returns list for valid GUID"),
        Step("GET /api/populations/schedules/{id}", "Populations ? 400 (bad guid)", "Returns 400 for invalid GUID"),

        // GET /api/populations/schedules/{id}/initial-population-count
        Step("GET /api/populations/schedules/{id}/initial-population-count", "InitPop Count ? 200 (has data)", "Returns count for seeded schedule"),
        Step("GET /api/populations/schedules/{id}/initial-population-count", "InitPop Count ? 200", "Returns count (0) for valid GUID"),
        Step("GET /api/populations/schedules/{id}/initial-population-count", "InitPop Count ? 400 (empty id)", "Returns 400 for empty reportScheduleId"),
        Step("GET /api/populations/schedules/{id}/initial-population-count", "InitPop Count ? 400 (bad guid)", "Returns 400 for invalid GUID"),
    ];

    public override IReadOnlyList<ApiHealthSeedRequirement> GetSeedRequirements() =>
    [
        ApiHealthSeedRequirement.ReportSchedule
    ];

    public override async Task<IReadOnlyList<ApiTestRunResult>> ExecuteAsync(CancellationToken ct = default)
    {
        var results = new List<ApiTestRunResult>();

        async Task AddSeededOrSkipAsync<T>(
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

        async Task AddSeededOrSkipActionAsync(
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

        var fakeReportId = Guid.NewGuid().ToString();
        var fakeFacilityId = $"ApiHealth-Report-{Guid.NewGuid():N}";
        var fakePatientId = $"Patient-{Guid.NewGuid():N}";

        // === GET /api/schedules/{id} ===
        results.Add(await RunStepAsync(StepNames.GetSchedule404, 404, async () =>
            await _client.GetScheduleAsync(fakeReportId, ct), ct: ct));

        results.Add(await RunStepAsync(StepNames.GetSchedule400BadGuid, 400, async () =>
            await _client.GetScheduleAsync("not-a-valid-guid", ct), ct: ct));

        // === GET /api/schedules/facilities/{id} ===
        results.Add(await RunStepAsync(StepNames.GetByFacility400Empty, 400, async () =>
            await _client.GetSchedulesByFacilityAsync(" ", cancellationToken: ct), ct: ct));

        results.Add(await RunStepAsync(StepNames.GetByFacility200Empty, 200, async () =>
            await _client.GetSchedulesByFacilityAsync(fakeFacilityId, cancellationToken: ct), ct: ct));

        // === GET /api/schedules/search ===
        results.Add(await RunStepAsync(StepNames.Search200, 200, async () =>
            await _client.SearchSchedulesAsync(fakeReportId, ct), ct: ct));

        // === DELETE /api/schedules/{id} ===
        results.Add(await RunStepAsync(StepNames.SoftDelete400BadGuid, 400, async () =>
            await _client.SoftDeleteScheduleAsync("not-a-valid-guid", ct), ct: ct));

        results.Add(await RunStepAsync(StepNames.SoftDelete404, 404, async () =>
            await _client.SoftDeleteScheduleAsync(fakeReportId, ct), ct: ct));

        // === PATCH /api/schedules/{id}/restore ===
        results.Add(await RunStepAsync(StepNames.Restore400BadGuid, 400, async () =>
            await _client.RestoreScheduleAsync("not-a-valid-guid", ct), ct: ct));

        results.Add(await RunStepAsync(StepNames.Restore404, 404, async () =>
            await _client.RestoreScheduleAsync(fakeReportId, ct), ct: ct));

        // === PATCH /api/schedules/facility/{id}/status ===
        results.Add(await RunStepAsync(StepNames.SetStatus204, 204, async () =>
            await _client.SetReportsDeletedStatusForFacilityAsync(fakeFacilityId, false, ct), ct: ct));

        // === Schedule lifecycle: soft-delete ? restore (seed-owned data only) ===
        string? scheduleId = null;
        string? scheduleFacilityId = null;
        string? seededEntryId = null;
        string? seededPatientId = null;
        string? seededPopulationId = null;
        var seeded = _seedContext.Current?.Report;
        if (seeded != null)
        {
            scheduleId = seeded.ScheduleId;
            scheduleFacilityId = seeded.FacilityId;

            var seededEntries = await _client.GetEntriesByScheduleAsync(scheduleId, ct);
            if (seededEntries.IsSuccessStatusCode && seededEntries.Body is { Count: > 0 })
            {
                seededEntryId = seededEntries.Body[0].Id.ToString();
                seededPatientId = seededEntries.Body[0].PatientId;
            }

            var seededPopulations = await _client.GetPopulationsByScheduleAsync(scheduleId, cancellationToken: ct);
            if (seededPopulations.IsSuccessStatusCode && seededPopulations.Body is { Count: > 0 })
                seededPopulationId = seededPopulations.Body[0].Id.ToString();
        }

        const string scheduleUnavailableReason = "Report seed was unavailable. This lifecycle test requires seed-owned schedule data from the seeding phase.";
        const string seededDataUnavailableReason = "Seeded schedule data was available, but dependent entry/resource/population payloads were not yet materialized.";

        await AddSeededOrSkipAsync(
            scheduleId != null,
            StepNames.GetSchedule200HasData,
            200,
            () => _client.GetScheduleAsync(scheduleId!, ct),
            scheduleUnavailableReason);

        await AddSeededOrSkipAsync(
            scheduleFacilityId != null,
            StepNames.GetByFacility200HasData,
            200,
            () => _client.GetSchedulesByFacilityAsync(scheduleFacilityId!, cancellationToken: ct),
            scheduleUnavailableReason);

        await AddSeededOrSkipActionAsync(
            scheduleId != null,
            StepNames.SoftDelete204,
            204,
            () => _client.SoftDeleteScheduleAsync(scheduleId!, ct),
            scheduleUnavailableReason);

        await AddSeededOrSkipActionAsync(
            scheduleId != null,
            StepNames.Restore204,
            204,
            () => _client.RestoreScheduleAsync(scheduleId!, ct),
            scheduleUnavailableReason);

        // === GET /api/entries/schedules/{id} ===
        await AddSeededOrSkipAsync(
            seededEntryId != null,
            StepNames.EntryGet200HasData,
            200,
            () => _client.GetEntryByIdAsync(seededEntryId!, ct),
            seededDataUnavailableReason);

        results.Add(await RunStepAsync(StepNames.EntryGet404, 404, async () =>
            await _client.GetEntryByIdAsync(Guid.NewGuid().ToString(), ct), ct: ct));

        results.Add(await RunStepAsync(StepNames.EntryGet400BadGuid, 400, async () =>
            await _client.GetEntryByIdAsync("not-a-valid-guid", ct), ct: ct));

        await AddSeededOrSkipAsync(
            scheduleId != null,
            StepNames.Entries200HasData,
            200,
            () => _client.GetEntriesByScheduleAsync(scheduleId!, ct),
            scheduleUnavailableReason);

        results.Add(await RunStepAsync(StepNames.Entries404, 404, async () =>
            await _client.GetEntriesByScheduleAsync(fakeReportId, ct), ct: ct));

        results.Add(await RunStepAsync(StepNames.Entries400EmptyId, 400, async () =>
            await _client.GetEntriesByScheduleAsync(" ", ct), ct: ct));

        results.Add(await RunStepAsync(StepNames.Entries400BadGuid, 400, async () =>
            await _client.GetEntriesByScheduleAsync("not-a-valid-guid", ct), ct: ct));

        // === GET /api/entries/schedules/{id}/count ===
        await AddSeededOrSkipActionAsync(
            scheduleId != null,
            StepNames.EntryCount200HasData,
            200,
            async () => await _client.GetEntryCountByScheduleAsync(scheduleId!, ct),
            scheduleUnavailableReason);

        results.Add(await RunStepAsync(StepNames.EntryCount404, 404, async () =>
            await _client.GetEntryCountByScheduleAsync(fakeReportId, ct), ct: ct));

        results.Add(await RunStepAsync(StepNames.EntryCount400BadGuid, 400, async () =>
            await _client.GetEntryCountByScheduleAsync("not-a-valid-guid", ct), ct: ct));

        // === GET /api/entries/schedules/{id}/summary ===
        await AddSeededOrSkipActionAsync(
            scheduleId != null,
            StepNames.EntrySummary200HasData,
            200,
            async () => await _client.GetEntrySummaryByScheduleAsync(scheduleId!, ct),
            scheduleUnavailableReason);

        results.Add(await RunStepAsync(StepNames.EntrySummary404, 404, async () =>
            await _client.GetEntrySummaryByScheduleAsync(fakeReportId, ct), ct: ct));

        results.Add(await RunStepAsync(StepNames.EntrySummary400BadGuid, 400, async () =>
            await _client.GetEntrySummaryByScheduleAsync("not-a-valid-guid", ct), ct: ct));

        // === GET /api/entries/schedules/{id}/patients/{patientId} ===
        await AddSeededOrSkipActionAsync(
            scheduleId != null && seededPatientId != null,
            StepNames.EntryByPatient200HasData,
            200,
            async () => await _client.GetEntryByScheduleAndPatientAsync(scheduleId!, seededPatientId!, ct),
            seededDataUnavailableReason);

        results.Add(await RunStepAsync(StepNames.EntryByPatient404, 404, async () =>
            await _client.GetEntryByScheduleAndPatientAsync(fakeReportId, fakePatientId, ct), ct: ct));

        results.Add(await RunStepAsync(StepNames.EntryByPatient400BadGuid, 400, async () =>
            await _client.GetEntryByScheduleAndPatientAsync("not-a-valid-guid", fakePatientId, ct), ct: ct));

        await AddSeededOrSkipAsync(
            seededPatientId != null,
            StepNames.EntriesByPatient200HasData,
            200,
            () => _client.GetEntriesByPatientAsync(seededPatientId!, ct),
            seededDataUnavailableReason);

        results.Add(await RunStepAsync(StepNames.EntriesByPatient404, 404, async () =>
            await _client.GetEntriesByPatientAsync(fakePatientId, ct), ct: ct));

        // === GET /api/resources/search ===
        results.Add(SkipStepAsync(StepNames.ResourceGet200HasData, "Resource rows are intentionally not persisted for seeded runs in this environment, so no deterministic resource id exists."));

        results.Add(await RunStepAsync(StepNames.ResourceGet404, 404, async () =>
            await _client.GetResourceByIdAsync(Guid.NewGuid().ToString(), ct), ct: ct));

        results.Add(await RunStepAsync(StepNames.ResourceGet400BadGuid, 400, async () =>
            await _client.GetResourceByIdAsync("not-a-valid-guid", ct), ct: ct));

        await AddSeededOrSkipAsync(
            scheduleId != null,
            StepNames.ResourcesBySchedule200HasData,
            200,
            () => _client.GetResourcesByScheduleAsync(scheduleId!, ct),
            scheduleUnavailableReason);

        results.Add(await RunStepAsync(StepNames.ResourcesBySchedule400BadGuid, 400, async () =>
            await _client.GetResourcesByScheduleAsync("not-a-valid-guid", ct), ct: ct));

        await AddSeededOrSkipAsync(
            scheduleId != null && seededPatientId != null,
            StepNames.ResourcesBySchedulePatient200HasData,
            200,
            () => _client.GetResourcesByScheduleAndPatientAsync(scheduleId!, seededPatientId!, ct),
            seededDataUnavailableReason);

        results.Add(await RunStepAsync(StepNames.ResourcesBySchedulePatient400BadGuid, 400, async () =>
            await _client.GetResourcesByScheduleAndPatientAsync("not-a-valid-guid", fakePatientId, ct), ct: ct));

        await AddSeededOrSkipAsync(
            seededPatientId != null,
            StepNames.ResourcesByPatient200HasData,
            200,
            () => _client.GetResourcesByPatientAsync(seededPatientId!, ct),
            seededDataUnavailableReason);

        await AddSeededOrSkipAsync(
            scheduleFacilityId != null && scheduleId != null,
            StepNames.SearchResources200HasData,
            200,
            () => _client.SearchResourcesAsync(scheduleFacilityId!, scheduleId!, cancellationToken: ct),
            scheduleUnavailableReason);

        results.Add(await RunStepAsync(StepNames.SearchResources200, 200, async () =>
            await _client.SearchResourcesAsync(fakeFacilityId, fakeReportId, cancellationToken: ct), ct: ct));

        // === GET /api/populations/schedules/{id} ===
        await AddSeededOrSkipAsync(
            seededPopulationId != null,
            StepNames.PopulationGet200HasData,
            200,
            () => _client.GetPopulationByIdAsync(seededPopulationId!, ct),
            seededDataUnavailableReason);

        results.Add(await RunStepAsync(StepNames.PopulationGet404, 404, async () =>
            await _client.GetPopulationByIdAsync(Guid.NewGuid().ToString(), ct), ct: ct));

        results.Add(await RunStepAsync(StepNames.PopulationGet400BadGuid, 400, async () =>
            await _client.GetPopulationByIdAsync("not-a-valid-guid", ct), ct: ct));

        results.Add(await RunStepAsync(StepNames.Populations400EmptyId, 400, async () =>
            await _client.GetPopulationsByScheduleAsync(" ", cancellationToken: ct), ct: ct));

        await AddSeededOrSkipAsync(
            scheduleId != null,
            StepNames.Populations200HasData,
            200,
            () => _client.GetPopulationsByScheduleAsync(scheduleId!, cancellationToken: ct),
            scheduleUnavailableReason);

        results.Add(await RunStepAsync(StepNames.Populations200, 200, async () =>
            await _client.GetPopulationsByScheduleAsync(fakeReportId, cancellationToken: ct), ct: ct));

        results.Add(await RunStepAsync(StepNames.Populations400BadGuid, 400, async () =>
            await _client.GetPopulationsByScheduleAsync("not-a-valid-guid", cancellationToken: ct), ct: ct));

        // === GET /api/populations/schedules/{id}/initial-population-count ===
        await AddSeededOrSkipActionAsync(
            scheduleId != null,
            StepNames.InitPopCount200HasData,
            200,
            async () => await _client.GetInitialPopulationCountAsync(scheduleId!, ct),
            scheduleUnavailableReason);

        results.Add(await RunStepAsync(StepNames.InitPopCount200, 200, async () =>
            await _client.GetInitialPopulationCountAsync(fakeReportId, ct), ct: ct));

        results.Add(await RunStepAsync(StepNames.InitPopCount400EmptyId, 400, async () =>
            await _client.GetInitialPopulationCountAsync(" ", ct), ct: ct));

        results.Add(await RunStepAsync(StepNames.InitPopCount400BadGuid, 400, async () =>
            await _client.GetInitialPopulationCountAsync("not-a-valid-guid", ct), ct: ct));

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
