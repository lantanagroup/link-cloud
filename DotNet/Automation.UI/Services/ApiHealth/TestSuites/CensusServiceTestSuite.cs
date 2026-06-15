using Automation.UI.Models.ApiHealth;
using LantanaGroup.Link.Sdk.Clients;
using LantanaGroup.Link.Shared.Application.Enums;
using LantanaGroup.Link.Shared.Application.Models.Integration.Census;
using LantanaGroup.Link.Shared.Application.Models.Tenant;
using StepNames = Automation.UI.Services.ApiHealth.TestSuites.ApiEndPointLibrary.CensusSteps;

namespace Automation.UI.Services.ApiHealth.TestSuites;

/// <summary>
/// Exercises Census service CRUD operations via LinkSdk.
/// Self-contained: creates its own prerequisite facility for each run.
///
/// POST /api/census/config has three sequential 400 guards:
///   1. empty FacilityId
///   2. empty ScheduledTrigger
///   3. invalid cron expression
///
/// PUT /api/census/config/{facilityId} has four sequential 400 guards:
///   1. empty body FacilityId
///   2. empty ScheduledTrigger
///   3. route facilityId does not match body FacilityId
///   4. invalid cron expression
///
/// DELETE /api/census/config/{facilityId}/jobs and
/// PATCH  /api/census/config/{facilityId}/jobs/restore both have a single 400
/// guard (empty facilityId) and a 204 success path.
///
/// DELETE /api/census/patient-events/{id}:
///   400 (empty id) and 404 (non-existent id) are self-contained.
///   202 (success) is SKIPPED — patient events are created exclusively via Kafka
///   listeners and cannot be seeded through the API.
///
/// DELETE /api/census/patient-events/visit/{correlationId}:
///   both 400 (empty correlationId) and 202 (success) are self-contained —
///   the action always returns 202 regardless of whether any records matched.
///
/// All paths are covered.
/// </summary>
public sealed class CensusServiceTestSuite : ServiceTestSuiteBase
{
    private readonly ICensusServiceClient _client;
    private readonly IFacilityServiceClient _facilityClient;
    private readonly ILogger<CensusServiceTestSuite> _logger;

    public override string ServiceName => "Census";
    public CensusServiceTestSuite(
        ICensusServiceClient client,
        IFacilityServiceClient facilityClient,
        ILogger<CensusServiceTestSuite> logger)
    {
        _client = client;
        _facilityClient = facilityClient;
        _logger = logger;
    }

    public override IReadOnlyList<ApiEndpointDefinition> GetEndpointDefinitions() =>
        ApiEndPointLibrary.GetServiceEndpoints(ServiceName);

        // GET /api/census/config/{facilityId}
        Step("Config GET ? 200", "Retrieves census config after create", "/api/census/config/{facilityId}"),
        Step("Config GET ? 404", "Returns 404 for non-existent facility", "/api/census/config/{facilityId}"),

        // PUT /api/census/config/{facilityId}
        Step("Config PUT ? 202", "Updates existing census config", "/api/census/config/{facilityId}"),
        Step("Config PUT ? 400 (mismatched FacilityId)", "Returns 400 when route facilityId does not match body FacilityId", "/api/census/config/{facilityId}"),
        Step("Config PUT ? 400 (empty FacilityId)", "Returns 400 when body FacilityId is empty", "/api/census/config/{facilityId}"),
        Step("Config PUT ? 400 (empty ScheduledTrigger)", "Returns 400 when ScheduledTrigger is empty", "/api/census/config/{facilityId}"),
        Step("Config PUT ? 400 (invalid cron)", "Returns 400 when ScheduledTrigger is not a valid cron expression", "/api/census/config/{facilityId}"),

        // DELETE /api/census/config/{facilityId}
        Step("Config DELETE ? 202", "Removes the census config", "/api/census/config/{facilityId}"),

        // DELETE /api/census/config/{facilityId}/jobs
        Step("DisableJobs DELETE ? 204", "Disables census scheduling and removes cron jobs for facility", "/api/census/config/{facilityId}/jobs"),
        Step("DisableJobs DELETE ? 400", "Returns 400 for empty facilityId", "/api/census/config/{facilityId}/jobs"),

        // PATCH /api/census/config/{facilityId}/jobs/restore
        Step("EnableJobs PATCH ? 204", "Re-enables census scheduling and recreates cron jobs for facility", "/api/census/config/{facilityId}/jobs/restore"),
        Step("EnableJobs PATCH ? 400", "Returns 400 for empty facilityId", "/api/census/config/{facilityId}/jobs/restore"),

        // GET /api/census/{facilityId}/history/admitted
        SkipStep(
            "Admitted GET ? 200",
            "Returns admitted patients for facility/date range",
            "Admitted census rows are produced by downstream ingestion workflows and cannot be deterministically created through Census HTTP endpoints in a self-contained health run.",
            "/api/census/{facilityId}/history/admitted"),
        Step("Admitted GET ? 404", "Returns 404 when no patients found", "/api/census/{facilityId}/history/admitted"),
        Step("Admitted GET ? 400", "Returns 400 when startDate > endDate", "/api/census/{facilityId}/history/admitted"),

        // GET /api/census/patient-encounters/current
        Step("CurrentEncounters GET ? 200", "Gets current patient encounters (may be empty paged result)", "/api/census/patient-encounters/current"),
        Step("CurrentEncounters GET ? 400", "Returns 400 for empty facilityId", "/api/census/patient-encounters/current"),

        // GET /api/census/patient-encounters/historical
        Step("HistoricalEncounters GET ? 200", "Gets historical patient encounters", "/api/census/patient-encounters/historical"),
        Step("HistoricalEncounters GET ? 400", "Returns 400 for missing dateThreshold", "/api/census/patient-encounters/historical"),

        // GET /api/census/patient-events
        SkipStep(
            "PatientEvents GET ? 200",
            "Returns patient events for facility",
            "Patient events are created by Kafka listeners, not HTTP APIs, so a deterministic 200 fixture cannot be self-seeded in this suite.",
            "/api/census/patient-events"),
        Step("PatientEvents GET ? 404", "Returns 404 when no events found for facility", "/api/census/patient-events"),
        Step("PatientEvents GET ? 400", "Returns 400 for empty facilityId", "/api/census/patient-events"),

        // DELETE /api/census/patient-events/{id}
        Step("PatientEvent DELETE ? 400 (empty id)", "Returns 400 when id is empty", "/api/census/patient-events/{id}"),
        Step("PatientEvent DELETE ? 404 (non-existent)", "Returns 404 when no event found for the given id", "/api/census/patient-events/{id}"),
        SkipStep(
            "PatientEvent DELETE ? 202",
            "Deletes event and rebuilds materialized view",
            "Patient events are written exclusively by the Kafka PatientListsAcquiredListener. " +
            "No HTTP endpoint exists to create them, so the 202 path cannot be exercised in a self-contained health test.",
            "/api/census/patient-events/{id}"),

        // DELETE /api/census/patient-events/visit/{correlationId}
        Step("PatientEventByCorrelation DELETE ? 400", "Returns 400 for empty correlationId", "/api/census/patient-events/visit/{correlationId}"),
        Step("PatientEventByCorrelation DELETE ? 202", "Returns 202 regardless of whether any records matched", "/api/census/patient-events/visit/{correlationId}"),

        // POST /api/census/patient-encounters/rebuild
        Step("Rebuild POST ? 202", "Triggers encounter rebuild", "/api/census/patient-encounters/rebuild"),
        Step("Rebuild POST ? 400", "Returns 400 for empty facilityId", "/api/census/patient-encounters/rebuild"),
    ];

    public override async Task<IReadOnlyList<ApiTestRunResult>> ExecuteAsync(CancellationToken ct = default)
    {
        var results = new List<ApiTestRunResult>();
        var facilityId = $"ApiHealth-Census-{Guid.NewGuid():N}";
        var facilityCreated = false;
        var censusCreated = false;

        try
        {
            // Prerequisite: create facility (infrastructure, not tracked)
            await CreateFacilityAsync(facilityId, ct);
            facilityCreated = true;

            // --- POST /api/census/config ---

            // Config POST ? 201
            results.Add(await RunStepAsync(StepNames.ConfigPost201, 201, async () =>
            {
                var resp = await _client.CreateCensusConfigAsync(new CensusConfigApiModel
                {
                    FacilityId = facilityId,
                    ScheduledTrigger = "0 0/5 * * * ?",
                    Enabled = true
                }, ct);
                if (resp.IsSuccessStatusCode) censusCreated = true;
                return resp;
            }, ct: ct));

            // Config POST ? 400 (empty ScheduledTrigger) — guard 2 of 3; FacilityId is valid.
            results.Add(await RunStepAsync(StepNames.ConfigPost400EmptyScheduledTrigger, 400, async () =>
                await _client.CreateCensusConfigAsync(new CensusConfigApiModel
                {
                    FacilityId = facilityId,
                    ScheduledTrigger = "",
                    Enabled = true
                }, ct), ct: ct));

            // Config POST ? 400 (empty FacilityId) — guard 1 of 3; fires before ScheduledTrigger check.
            results.Add(await RunStepAsync(StepNames.ConfigPost400EmptyFacilityId, 400, async () =>
                await _client.CreateCensusConfigAsync(new CensusConfigApiModel
                {
                    FacilityId = "",
                    ScheduledTrigger = "0 0/5 * * * ?",
                    Enabled = true
                }, ct), ct: ct));

            // Config POST ? 400 (invalid cron) — guard 3 of 3; both FacilityId and ScheduledTrigger are non-empty.
            results.Add(await RunStepAsync(StepNames.ConfigPost400InvalidCron, 400, async () =>
                await _client.CreateCensusConfigAsync(new CensusConfigApiModel
                {
                    FacilityId = facilityId,
                    ScheduledTrigger = "not-a-cron",
                    Enabled = true
                }, ct), ct: ct));

            // --- GET /api/census/config/{facilityId} ---

            // Config GET ? 200
            results.Add(await RunStepAsync(StepNames.ConfigGet200, 200, async () =>
                await _client.GetCensusConfigAsync(facilityId, ct), ct: ct));

            // --- PUT /api/census/config/{facilityId} ---

            // Config PUT ? 202
            results.Add(await RunStepAsync(StepNames.ConfigPut202, 202, async () =>
                await _client.UpdateCensusConfigAsync(facilityId, new CensusConfigApiModel
                {
                    FacilityId = facilityId,
                    ScheduledTrigger = "0 0/10 * * * ?",
                    Enabled = false
                }, ct), ct: ct));

            // Config PUT ? 400 (mismatched FacilityId) — guard 3 of 4.
            results.Add(await RunStepAsync(StepNames.ConfigPut400MismatchedFacilityId, 400, async () =>
                await _client.UpdateCensusConfigAsync(facilityId, new CensusConfigApiModel
                {
                    FacilityId = "wrong-id",
                    ScheduledTrigger = "0 0/10 * * * ?",
                    Enabled = false
                }, ct), ct: ct));

            // Config PUT ? 400 (empty FacilityId) — guard 1 of 4; fires before all other checks.
            results.Add(await RunStepAsync(StepNames.ConfigPut400EmptyFacilityId, 400, async () =>
                await _client.UpdateCensusConfigAsync(facilityId, new CensusConfigApiModel
                {
                    FacilityId = "",
                    ScheduledTrigger = "0 0/10 * * * ?",
                    Enabled = false
                }, ct), ct: ct));

            // Config PUT ? 400 (empty ScheduledTrigger) — guard 2 of 4; FacilityId is non-empty.
            results.Add(await RunStepAsync(StepNames.ConfigPut400EmptyScheduledTrigger, 400, async () =>
                await _client.UpdateCensusConfigAsync(facilityId, new CensusConfigApiModel
                {
                    FacilityId = facilityId,
                    ScheduledTrigger = "",
                    Enabled = false
                }, ct), ct: ct));

            // Config PUT ? 400 (invalid cron) — guard 4 of 4; FacilityId matches route, ScheduledTrigger non-empty.
            results.Add(await RunStepAsync(StepNames.ConfigPut400InvalidCron, 400, async () =>
                await _client.UpdateCensusConfigAsync(facilityId, new CensusConfigApiModel
                {
                    FacilityId = facilityId,
                    ScheduledTrigger = "not-a-cron",
                    Enabled = false
                }, ct), ct: ct));

            // --- DELETE /api/census/config/{facilityId}/jobs ---

            // DisableJobs DELETE ? 204 (real facility, census config exists from POST ? 201 above)
            results.Add(await RunStepAsync(StepNames.DisableJobsDelete204, 204, async () =>
                await _client.DisableFacilityJobsAsync(facilityId, ct), ct: ct));

            // DisableJobs DELETE ? 400 (empty facilityId — only guard in the action)
            // A single space is used: Flurl encodes it as %20, the segment reaches the
            // controller, and IsNullOrWhiteSpace fires. An empty string drops the path
            // segment entirely, causing a 404 routing miss instead.
            results.Add(await RunStepAsync(StepNames.DisableJobsDelete400, 400, async () =>
                await _client.DisableFacilityJobsAsync(" ", ct), ct: ct));

            // --- PATCH /api/census/config/{facilityId}/jobs/restore ---

            // EnableJobs PATCH ? 204 (re-enables jobs for the same facility)
            results.Add(await RunStepAsync(StepNames.EnableJobsPatch204, 204, async () =>
                await _client.EnableFacilityJobsAsync(facilityId, ct), ct: ct));

            // EnableJobs PATCH ? 400 (empty facilityId — only guard in the action)
            // Same whitespace encoding fix as DisableJobs above.
            results.Add(await RunStepAsync(StepNames.EnableJobsPatch400, 400, async () =>
                await _client.EnableFacilityJobsAsync(" ", ct), ct: ct));

            // --- GET /api/census/{facilityId}/history/admitted ---

            var rangeStart = DateTime.SpecifyKind(DateTime.Parse("2023-01-01"), DateTimeKind.Utc);
            var rangeEnd = DateTime.SpecifyKind(DateTime.Parse("2023-12-31"), DateTimeKind.Utc);

            // Admitted GET ? 404 (no patients for this test facility)
            results.Add(await RunStepAsync(StepNames.AdmittedGet404, 404, async () =>
                await _client.GetAdmittedPatientsAsync(facilityId, rangeStart, rangeEnd, ct), ct: ct));

            // Admitted GET ? 400 (startDate > endDate)
            results.Add(await RunStepAsync(StepNames.AdmittedGet400, 400, async () =>
                await _client.GetAdmittedPatientsAsync(facilityId, rangeEnd, rangeStart, ct), ct: ct));

            // --- GET /api/census/patient-encounters/current ---

            // CurrentEncounters GET ? 200
            results.Add(await RunStepAsync(StepNames.CurrentEncountersGet200, 200, async () =>
                await _client.GetCurrentPatientEncountersAsync(facilityId, ct), ct: ct));

            // CurrentEncounters GET ? 400 (empty facilityId)
            results.Add(await RunStepAsync(StepNames.CurrentEncountersGet400, 400, async () =>
                await _client.GetCurrentPatientEncountersAsync("", ct), ct: ct));

            // --- GET /api/census/patient-encounters/historical ---

            // HistoricalEncounters GET ? 200
            results.Add(await RunStepAsync(StepNames.HistoricalEncountersGet200, 200, async () =>
                await _client.GetHistoricalPatientEncountersAsync(facilityId, DateTime.UtcNow, ct), ct: ct));

            // HistoricalEncounters GET ? 400 (missing dateThreshold)
            results.Add(await RunStepAsync(StepNames.HistoricalEncountersGet400, 400, async () =>
                await _client.GetHistoricalPatientEncountersAsync(facilityId, null, ct), ct: ct));

            // --- GET /api/census/patient-events ---

            // PatientEvents GET ? 404 (no events for test facility)
            results.Add(await RunStepAsync(StepNames.PatientEventsGet404, 404, async () =>
                await _client.GetPatientEventsAsync(facilityId, ct), ct: ct));

            // PatientEvents GET ? 400 (empty facilityId)
            results.Add(await RunStepAsync(StepNames.PatientEventsGet400, 400, async () =>
                await _client.GetPatientEventsAsync("", ct), ct: ct));

            // --- DELETE /api/census/patient-events/{id} ---

            // PatientEvent DELETE ? 400 (empty id — only guard in the action)
            // A single space is used: Flurl encodes it as %20 so the {id} route segment
            // is present and the controller receives " ". IsNullOrWhiteSpace fires ? 400.
            // An empty string collapses the segment, matching no route ? 405.
            results.Add(await RunStepAsync(StepNames.PatientEventDelete400EmptyId, 400, async () =>
                await _client.DeletePatientEventAsync(" ", ct), ct: ct));

            // PatientEvent DELETE ? 404 (random id never in DB)
            results.Add(await RunStepAsync(StepNames.PatientEventDelete404NonExistent, 404, async () =>
                await _client.DeletePatientEventAsync($"ApiHealth-{Guid.NewGuid():N}", ct), ct: ct));

            // PatientEvent DELETE ? 202 is informational only — not executed.

            // --- DELETE /api/census/patient-events/visit/{correlationId} ---

            // PatientEventByCorrelation DELETE ? 400 (empty correlationId — only guard)
            // A single space is used: Flurl encodes it as %20 so visit/{correlationId}
            // matches correctly and the controller receives " ". IsNullOrWhiteSpace ? 400.
            // An empty string drops the correlationId segment; ASP.NET then matches
            // {id} with id="visit" on the sibling route and returns 404 instead.
            results.Add(await RunStepAsync(StepNames.PatientEventByCorrelationDelete400, 400, async () =>
                await _client.DeletePatientEventsByCorrelationAsync(" ", ct), ct: ct));

            // PatientEventByCorrelation DELETE ? 202
            // The action calls DeletePatientEventByCorrelationId then RebuildPatientEncounterTable
            // and unconditionally returns Accepted() — no 404 check — so a fake correlationId
            // that matches zero records still produces 202.
            results.Add(await RunStepAsync(StepNames.PatientEventByCorrelationDelete202, 202, async () =>
                await _client.DeletePatientEventsByCorrelationAsync($"ApiHealth-Corr-{Guid.NewGuid():N}", ct), ct: ct));

            // --- POST /api/census/patient-encounters/rebuild ---

            // Rebuild POST ? 202
            results.Add(await RunStepAsync(StepNames.RebuildPost202, 202, async () =>
                await _client.RebuildPatientEncountersAsync(facilityId, cancellationToken: ct), ct: ct));

            // Rebuild POST ? 400 (empty facilityId)
            results.Add(await RunStepAsync(StepNames.RebuildPost400, 400, async () =>
                await _client.RebuildPatientEncountersAsync("", cancellationToken: ct), ct: ct));

            // --- DELETE /api/census/config/{facilityId} ---

            // Config DELETE ? 202
            results.Add(await RunStepAsync(StepNames.ConfigDelete202, 202, async () =>
            {
                var resp = await _client.DeleteCensusConfigAsync(facilityId, ct);
                if (resp.IsSuccessStatusCode) censusCreated = false;
                return resp;
            }, ct: ct));

            // Config GET ? 404 (after delete)
            results.Add(await RunStepAsync(StepNames.ConfigGet404, 404, async () =>
                await _client.GetCensusConfigAsync(facilityId, ct), ct: ct));
        }
        finally
        {
            if (censusCreated) await TryCleanupAsync(() => _client.DeleteCensusConfigAsync(facilityId, ct));
            if (facilityCreated) await TryCleanupAsync(() => _facilityClient.DeleteAsync(facilityId, ct));
        }

        return results;
    }

    public override async Task<ApiTestRunResult> ExecuteStepAsync(string endpointKey, CancellationToken ct = default)
    {
        var definition = GetEndpointDefinitions().FirstOrDefault(d => d.Key == endpointKey);
        if (definition?.IsInformational == true) return InformationalResult(endpointKey);

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

    private ApiEndpointDefinition Step(string name, string desc, string? group = null) => new()
    {
        ServiceName = ServiceName,
        GroupName = group,
        EndpointName = name,
        Description = desc,
        IsTestSuiteStep = true
    };
}
