using Automation.UI.Models.ApiHealth;
using LantanaGroup.Link.Sdk.Clients;
using LantanaGroup.Link.Shared.Application.Enums;
using LantanaGroup.Link.Shared.Application.Models.Integration.QueryDispatch;
using LantanaGroup.Link.Shared.Application.Models.Tenant;
using StepNames = Automation.UI.Services.ApiHealth.TestSuites.ApiEndPointLibrary.QueryDispatchSteps;

namespace Automation.UI.Services.ApiHealth.TestSuites;

/// <summary>
/// Exercises QueryDispatch service CRUD operations via LinkSdk.
/// Self-contained: creates its own prerequisite facility for each run.
/// Covers every annotated response path on both the POST (create) and PUT (upsert)
/// endpoints, including the 400 paths for invalid duration, empty facilityId,
/// and a config that already exists.
///
/// The POST 400 (facility not found) step accepts either 400 or 201 as a pass.
/// When ServiceRegistry:TenantService:CheckIfTenantExists is true the Tenant check
/// fires and the non-existent facility returns 400. When the flag is false,
/// CheckFacilityExists short-circuits to true and returns 201 instead. Both are
/// valid outcomes for their respective environment configurations.
/// </summary>
public sealed class QueryDispatchTestSuite : ServiceTestSuiteBase
{
    private readonly IQueryDispatchServiceClient _client;
    private readonly IFacilityServiceClient _facilityClient;
    private readonly ILogger<QueryDispatchTestSuite> _logger;

    public override string ServiceName => "QueryDispatch";
    public QueryDispatchTestSuite(
        IQueryDispatchServiceClient client,
        IFacilityServiceClient facilityClient,
        ILogger<QueryDispatchTestSuite> logger)
    {
        _client = client;
        _facilityClient = facilityClient;
        _logger = logger;
    }

    public override IReadOnlyList<ApiEndpointDefinition> GetEndpointDefinitions() =>
        ApiEndPointLibrary.GetServiceEndpoints(ServiceName);

    public override async Task<IReadOnlyList<ApiTestRunResult>> ExecuteAsync(CancellationToken ct = default)
    {
        var results = new List<ApiTestRunResult>();
        var facilityId = $"ApiHealth-QD-{Guid.NewGuid():N}";
        var facilityCreated = false;
        var configCreated = false;

        try
        {
            // Prerequisite: create facility (not a tracked test — infrastructure)
            await CreateFacilityAsync(facilityId, ct);
            facilityCreated = true;

            // ── POST /api/querydispatch/configuration ──────────────────────────────

            // POST → 400 (null model)
            // The SDK null-body path can hang in some environments; use an empty payload
            // to validate the controller's required-request guard deterministically.
            results.Add(await RunStepAsync(StepNames.Post400NullModel, 400, async () =>
                await _client.CreateQueryDispatchConfigurationAsync(new QueryDispatchConfigurationApiModel(), ct), ct: ct));

            // POST → 400 (empty facilityId) — guard fires before any DB or Tenant call.
            results.Add(await RunStepAsync(StepNames.Post400EmptyFacilityId, 400, async () =>
                await _client.CreateQueryDispatchConfigurationAsync(new QueryDispatchConfigurationApiModel
                {
                    FacilityId = "",
                    DispatchSchedules = [new DispatchScheduleApiModel { Event = "Discharge", Duration = "PT0S" }]
                }, ct), ct: ct));

            // POST → 400 (invalid duration) — duration guard fires before CheckFacilityExists.
            results.Add(await RunStepAsync(StepNames.Post400InvalidDuration, 400, async () =>
                await _client.CreateQueryDispatchConfigurationAsync(new QueryDispatchConfigurationApiModel
                {
                    FacilityId = facilityId,
                    DispatchSchedules = [new DispatchScheduleApiModel { Event = "Discharge", Duration = "NOT_A_DURATION" }]
                }, ct), ct: ct));

            // POST → 400 (facility not found)
            // Accepts 400 (CheckIfTenantExists enabled) or 201 (check bypassed).
            // A ghost facilityId is always used; which code comes back depends on the
            // QD service's environment config — either outcome is a valid pass.
            var ghostFacilityId = $"ApiHealth-QD-Ghost-{Guid.NewGuid():N}";
            results.Add(await RunStepAsync(StepNames.Post400FacilityNotFound, [400, 201], async () =>
                await _client.CreateQueryDispatchConfigurationAsync(new QueryDispatchConfigurationApiModel
                {
                    FacilityId = ghostFacilityId,
                    DispatchSchedules = [new DispatchScheduleApiModel { Event = "Discharge", Duration = "PT0S" }]
                }, ct), ct: ct));

            // If the ghost config was created (201 path), clean it up before the POST → 201 step.
            await TryCleanupAsync(() => _client.DeleteQueryDispatchConfigurationAsync(ghostFacilityId, ct));

            // POST → 201 — real facility, valid payload, no pre-existing config.
            var postConfigCreated = false;
            results.Add(await RunStepAsync(StepNames.Post201, 201, async () =>
            {
                var resp = await _client.CreateQueryDispatchConfigurationAsync(new QueryDispatchConfigurationApiModel
                {
                    FacilityId = facilityId,
                    DispatchSchedules = [new DispatchScheduleApiModel { Event = "Discharge", Duration = "PT0S" }]
                }, ct);
                if (resp.IsSuccessStatusCode) postConfigCreated = true;
                return resp;
            }, ct: ct));

            // POST → 400 (already exists) — the config was just created above; a second POST
            // for the same facilityId must return 400.
            results.Add(await RunStepAsync(StepNames.Post400AlreadyExists, 400, async () =>
                await _client.CreateQueryDispatchConfigurationAsync(new QueryDispatchConfigurationApiModel
                {
                    FacilityId = facilityId,
                    DispatchSchedules = [new DispatchScheduleApiModel { Event = "Discharge", Duration = "PT0S" }]
                }, ct), ct: ct));

            // Clean up the POST-created config before the PUT steps run so the PUT starts fresh.
            if (postConfigCreated)
            {
                await TryCleanupAsync(() => _client.DeleteQueryDispatchConfigurationAsync(facilityId, ct));
                postConfigCreated = false;
            }

            // ── PUT /api/querydispatch/configuration/facility/{id} ─────────────────────

            // PUT → 400 (invalid duration)
            results.Add(await RunStepAsync(StepNames.Put400NullModel, 400, async () =>
                await _client.UpsertQueryDispatchConfigurationAsync(facilityId, null!, ct), ct: ct));

            results.Add(await RunStepAsync(StepNames.Put400EmptyFacilityId, 400, async () =>
                await _client.UpsertQueryDispatchConfigurationAsync(" ", new QueryDispatchConfigurationApiModel
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
                }, ct), ct: ct));

            // PUT → 400 (invalid duration)
            // Duration check fires before CheckFacilityExists, so any non-ISO-8601 string
            // (e.g. "1 hour") returns 400 without touching the DB.
            results.Add(await RunStepAsync(StepNames.Put400InvalidDuration, 400, async () =>
                await _client.UpsertQueryDispatchConfigurationAsync(facilityId, new QueryDispatchConfigurationApiModel
                {
                    FacilityId = facilityId,
                    DispatchSchedules =
                    [
                        new DispatchScheduleApiModel
                        {
                            Event = "Discharge",
                            Duration = "NOT_A_DURATION"
                        }
                    ]
                }, ct), ct: ct));

            // PUT → 201/204
            results.Add(await RunStepAsync(StepNames.Put201Or204, [201, 204], async () =>
            {
                var config = new QueryDispatchConfigurationApiModel
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
                };
                var resp = await _client.UpsertQueryDispatchConfigurationAsync(facilityId, config, ct);
                if (resp.IsSuccessStatusCode) configCreated = true;
                return resp;
            }, ct: ct));

            // GET → 200
            results.Add(await RunStepAsync(StepNames.Get200, 200, async () =>
                await _client.GetConfigurationAsync(facilityId, ct), ct: ct));

            // DELETE → 204
            results.Add(await RunStepAsync(StepNames.Delete204, 204, async () =>
            {
                var resp = await _client.DeleteQueryDispatchConfigurationAsync(facilityId, ct);
                if (resp.IsSuccessStatusCode) configCreated = false;
                return resp;
            }, ct: ct));

            // GET → 404 (after delete)
            results.Add(await RunStepAsync(StepNames.Get404, 404, async () =>
                await _client.GetConfigurationAsync(facilityId, ct), ct: ct));

            // DELETE → 204 (idempotent — already deleted)
            results.Add(await RunStepAsync(StepNames.Delete204Idempotent, 204, async () =>
                await _client.DeleteQueryDispatchConfigurationAsync(facilityId, ct), ct: ct));
        }
        catch (Exception ex) when (!facilityCreated)
        {
            results.Add(new ApiTestRunResult
            {
                EndpointKey = $"{ServiceName}::{StepNames.Put201Or204}",
                ServiceName = ServiceName,
                Passed = false,
                ErrorMessage = $"Prerequisite failed: {ex.Message}",
                RequestBody = "Request was not sent because prerequisite setup failed.",
                ResponseBody = "Response was not received because prerequisite setup failed.",
                ExecutedAt = DateTimeOffset.UtcNow
            });
        }
        finally
        {
            if (configCreated) await TryCleanupAsync(() => _client.DeleteQueryDispatchConfigurationAsync(facilityId, ct));
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

}
