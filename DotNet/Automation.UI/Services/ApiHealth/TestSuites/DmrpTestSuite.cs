using System.Text.Json;
using Automation.UI.Models.ApiHealth;
using LantanaGroup.Link.Sdk.Clients;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Models.Integration.DMRP;
using LantanaGroup.Link.Shared.Application.Models.Responses;
using LantanaGroup.Link.Shared.Application.Models.Tenant;
using StepNames = Automation.UI.Services.ApiHealth.TestSuites.ApiEndPointLibrary.DmrpSteps;

namespace Automation.UI.Services.ApiHealth.TestSuites;

/// <summary>
/// Exercises the DMRP module's measure mapping and reporting plan endpoints, and the one thing DMRP
/// changes about an endpoint it does not own: a facility may no longer carry its own schedule.
/// </summary>
/// <remarks>
/// <para>
/// DMRP is hosted in-process by the Tenant service rather than deployed on its own, and it is behind
/// a feature switch. A disabled module strips its own controllers from the host, so every route here
/// answers 404 - which is a configuration state, not a failure. The suite asks first and reports its
/// steps as skipped rather than failing a stack that is simply not running DMRP.
/// </para>
/// <para>
/// It declares no seed requirement on purpose. Seeding runs a full pipeline scenario before any suite
/// executes, which is a minute or two of waiting with nothing to show for it - and the entire cost
/// would be wasted whenever DMRP is switched off, because every step is skipped. The two fixtures
/// this needs are cheap to obtain directly: a dQM MeasureEval already holds, and a facility of its
/// own that it removes when it is done.
/// </para>
/// </remarks>
public sealed class DmrpTestSuite : ServiceTestSuiteBase
{
    /// <summary>
    /// Far enough out that it cannot collide with a facility's real enrollment, so running this suite
    /// never alters the schedule any facility actually reports on.
    /// </summary>
    private const int ReportingYear = 2099;
    private const int ReportingMonth = 1;

    private readonly IDmrpServiceClient _client;
    private readonly IFacilityServiceClient _facilityClient;
    private readonly IMeasureEvalServiceClient _measureEvalClient;

    public override string ServiceName => ApiEndPointLibrary.ServiceNames.Dmrp;

    public DmrpTestSuite(
        IDmrpServiceClient client,
        IFacilityServiceClient facilityClient,
        IMeasureEvalServiceClient measureEvalClient)
    {
        _client = client;
        _facilityClient = facilityClient;
        _measureEvalClient = measureEvalClient;
    }

    public override IReadOnlyList<ApiEndpointDefinition> GetEndpointDefinitions() =>
        ApiEndPointLibrary.GetServiceEndpoints(ServiceName);

    public override async Task<IReadOnlyList<ApiTestRunResult>> ExecuteAsync(CancellationToken ct = default)
    {
        if (!await DmrpIsEnabledAsync(ct))
        {
            return SkipEverything(
                "DMRP is not enabled on the Tenant service, so its endpoints are not routed. Set DMRP:Enabled to exercise them.");
        }

        // A measure mapping is refused for a dQM MeasureEval does not hold, so the dQM has to be one
        // it actually has rather than a made-up string.
        var measureDefinitions = await _measureEvalClient.GetAllMeasureDefinitionsAsync(ct);
        var dqm = TryExtractFirstMeasureId(measureDefinitions.Body);

        if (string.IsNullOrWhiteSpace(dqm))
        {
            return SkipEverything(
                "MeasureEval holds no measure definitions, so there is no dQM a measure mapping could be created against.");
        }

        return await RunLifecycleAsync(dqm!, ct);
    }

    private async Task<IReadOnlyList<ApiTestRunResult>> RunLifecycleAsync(string dqm, CancellationToken ct)
    {
        var results = new List<ApiTestRunResult>();

        // Unique per run, because measure and dQM together are unique and the stack keeps mappings
        // between runs. The dQM is shared and real; only the NHSN measure name is synthetic.
        var measure = $"ApiHealth-Measure-{Guid.NewGuid():N}";
        var unknownId = Guid.NewGuid().ToString();

        string? mappingId = null;
        string? planId = null;
        string? facilityId = null;
        string? scheduledFacilityId = null;

        try
        {
            // A reporting plan is refused for a facility that does not exist, so stand one up. An empty
            // schedule is what DMRP accepts; it derives the real one from the plans created below.
            facilityId = await TryCreateFacilityAsync(ct);

            // === POST /api/dmrp/measure-mappings ===

            results.Add(await RunStepAsync<MeasureMappingModel>(StepNames.MappingPost201, 201, async () =>
            {
                var response = await _client.CreateMeasureMappingAsync(Mapping(measure, dqm), ct);
                mappingId = response.Body?.Id;
                return response;
            }, ct));

            results.Add(await RunStepAsync<MeasureMappingModel>(StepNames.MappingPost400UnknownDqm, 400, () =>
                _client.CreateMeasureMappingAsync(
                    Mapping($"ApiHealth-Measure-{Guid.NewGuid():N}", $"NotAMeasure-{Guid.NewGuid():N}"), ct), ct));

            results.Add(await RunStepAsync<MeasureMappingModel>(StepNames.MappingPost400Duplicate, 400, () =>
                _client.CreateMeasureMappingAsync(Mapping(measure, dqm), ct), ct));

            // === GET /api/dmrp/measure-mappings/{id} ===

            results.Add(mappingId is null
                ? SkipStepAsync(StepNames.MappingGet200, MappingMissing)
                : await RunStepAsync<MeasureMappingModel>(StepNames.MappingGet200, 200, () =>
                    _client.GetMeasureMappingAsync(mappingId, ct), ct));

            results.Add(await RunStepAsync<MeasureMappingModel>(StepNames.MappingGet404, 404, () =>
                _client.GetMeasureMappingAsync(unknownId, ct), ct));

            // === GET /api/dmrp/measure-mappings/search ===

            results.Add(await RunStepAsync<PagedConfigModel<MeasureMappingModel>>(StepNames.MappingSearch200, 200, () =>
                _client.SearchMeasureMappingsAsync(measure: measure, dqm: dqm, cancellationToken: ct), ct));

            // 204 rather than a 200 carrying an empty page. Callers that find-or-create read the
            // status, so this is load-bearing rather than cosmetic.
            results.Add(await RunStepAsync<PagedConfigModel<MeasureMappingModel>>(StepNames.MappingSearch204, 204, () =>
                _client.SearchMeasureMappingsAsync(measure: $"NoSuchMeasure-{Guid.NewGuid():N}", cancellationToken: ct), ct));

            // === PUT /api/dmrp/measure-mappings/{id} ===

            results.Add(mappingId is null
                ? SkipStepAsync(StepNames.MappingPut202, MappingMissing)
                : await RunStepAsync<MeasureMappingModel>(StepNames.MappingPut202, 202, () =>
                    _client.UpdateMeasureMappingAsync(mappingId, Mapping(measure, dqm, Frequency.Daily), ct), ct));

            results.Add(await RunStepAsync<MeasureMappingModel>(StepNames.MappingPut404, 404, () =>
                _client.UpdateMeasureMappingAsync(unknownId, Mapping(measure, dqm), ct), ct));

            // === POST /api/dmrp/reporting-plans ===

            results.Add(mappingId is null || facilityId is null
                ? SkipStepAsync(StepNames.PlanPost201, FixturesMissing)
                : await RunStepAsync<FacilityReportingPlanModel>(StepNames.PlanPost201, 201, async () =>
                {
                    var response = await _client.CreateFacilityReportingPlanAsync(Plan(facilityId, mappingId), ct);
                    planId = response.Body?.Id;
                    return response;
                }, ct));

            results.Add(mappingId is null || facilityId is null
                ? SkipStepAsync(StepNames.PlanPost409Duplicate, FixturesMissing)
                : await RunStepAsync<FacilityReportingPlanModel>(StepNames.PlanPost409Duplicate, 409, () =>
                    _client.CreateFacilityReportingPlanAsync(Plan(facilityId, mappingId), ct), ct));

            results.Add(mappingId is null
                ? SkipStepAsync(StepNames.PlanPost400UnknownFacility, MappingMissing)
                : await RunStepAsync<FacilityReportingPlanModel>(StepNames.PlanPost400UnknownFacility, 400, () =>
                    _client.CreateFacilityReportingPlanAsync(
                        Plan($"ApiHealth-NoSuchFacility-{Guid.NewGuid():N}", mappingId), ct), ct));

            results.Add(facilityId is null
                ? SkipStepAsync(StepNames.PlanPost400UnknownMapping, FacilityMissing)
                : await RunStepAsync<FacilityReportingPlanModel>(StepNames.PlanPost400UnknownMapping, 400, () =>
                    _client.CreateFacilityReportingPlanAsync(Plan(facilityId, unknownId), ct), ct));

            // === GET /api/dmrp/reporting-plans ===

            results.Add(planId is null
                ? SkipStepAsync(StepNames.PlanGet200, PlanMissing)
                : await RunStepAsync<FacilityReportingPlanModel>(StepNames.PlanGet200, 200, () =>
                    _client.GetFacilityReportingPlanAsync(planId, ct), ct));

            results.Add(await RunStepAsync<FacilityReportingPlanModel>(StepNames.PlanGet404, 404, () =>
                _client.GetFacilityReportingPlanAsync(unknownId, ct), ct));

            results.Add(facilityId is null
                ? SkipStepAsync(StepNames.PlansForFacilityGet200, FacilityMissing)
                : await RunStepAsync<List<FacilityReportingPlanModel>>(StepNames.PlansForFacilityGet200, 200, () =>
                    _client.GetFacilityReportingPlansForFacilityAsync(facilityId, cancellationToken: ct), ct));

            // === DELETE, in the order the foreign key forces ===

            // The mapping cannot go while a plan still points at it. Answering 404 here would tell the
            // caller the opposite of what happened, so this is a conflict.
            results.Add(mappingId is null || planId is null
                ? SkipStepAsync(StepNames.MappingDelete409InUse, FixturesMissing)
                : await RunStepAsync(StepNames.MappingDelete409InUse, 409, () =>
                    _client.DeleteMeasureMappingAsync(mappingId, ct), ct));

            results.Add(planId is null
                ? SkipStepAsync(StepNames.PlanDelete204, PlanMissing)
                : await RunStepAsync(StepNames.PlanDelete204, 204, async () =>
                {
                    var response = await _client.DeleteFacilityReportingPlanAsync(planId, ct);
                    if (response.IsSuccessStatusCode)
                    {
                        planId = null;
                    }

                    return response;
                }, ct));

            results.Add(await RunStepAsync(StepNames.PlanDelete404, 404, () =>
                _client.DeleteFacilityReportingPlanAsync(unknownId, ct), ct));

            results.Add(mappingId is null
                ? SkipStepAsync(StepNames.MappingDelete204, MappingMissing)
                : await RunStepAsync(StepNames.MappingDelete204, 204, async () =>
                {
                    var response = await _client.DeleteMeasureMappingAsync(mappingId, ct);
                    if (response.IsSuccessStatusCode)
                    {
                        mappingId = null;
                    }

                    return response;
                }, ct));

            results.Add(await RunStepAsync(StepNames.MappingDelete404, 404, () =>
                _client.DeleteMeasureMappingAsync(unknownId, ct), ct));

            // === The one endpoint DMRP changes without owning ===

            scheduledFacilityId = $"ApiHealth-DMRP-{Guid.NewGuid():N}";
            var candidate = scheduledFacilityId;

            results.Add(await RunStepAsync<FacilityModel>(StepNames.FacilityPost400WithSchedule, 400, async () =>
            {
                var response = await _facilityClient.CreateAsync(FacilityWithSchedule(candidate, dqm), ct);
                if (!response.IsSuccessStatusCode)
                {
                    // Nothing was created, so nothing needs removing in cleanup.
                    scheduledFacilityId = null;
                }

                return response;
            }, ct));
        }
        finally
        {
            if (planId is not null)
            {
                await TryCleanupAsync(() => _client.DeleteFacilityReportingPlanAsync(planId, CancellationToken.None));
            }

            if (mappingId is not null)
            {
                await TryCleanupAsync(() => _client.DeleteMeasureMappingAsync(mappingId, CancellationToken.None));
            }

            if (facilityId is not null)
            {
                await TryCleanupAsync(() => _facilityClient.DeleteAsync(facilityId, CancellationToken.None));
            }

            if (scheduledFacilityId is not null)
            {
                await TryCleanupAsync(() => _facilityClient.DeleteAsync(scheduledFacilityId, CancellationToken.None));
            }
        }

        return results;
    }

    private const string MappingMissing = "The measure mapping was not created.";
    private const string PlanMissing = "The reporting plan was not created.";
    private const string FacilityMissing = "The facility this suite creates for itself was not available.";
    private const string FixturesMissing = "The measure mapping and the facility were not both available.";

    /// <summary>
    /// Reads a route the module only serves when it is registered. 404 means the module is switched
    /// off; anything else means it answered.
    /// </summary>
    private async Task<bool> DmrpIsEnabledAsync(CancellationToken ct)
    {
        try
        {
            var probe = await _client.SearchFacilityReportingPlansAsync(pageSize: 1, pageNumber: 1, cancellationToken: ct);
            return probe.StatusCode != StatusCodes.Status404NotFound;
        }
        catch
        {
            // Unreachable is not the same as disabled, so let the suite run and report what failed.
            return true;
        }
    }

    private async Task<string?> TryCreateFacilityAsync(CancellationToken ct)
    {
        var facilityId = $"ApiHealth-DMRP-Facility-{Guid.NewGuid():N}";

        var response = await _facilityClient.CreateAsync(new FacilityModel
        {
            FacilityId = facilityId,
            FacilityName = facilityId,
            TimeZone = "America/Chicago",
            Vendor = new VendorModel { Name = "Epic" },
            ScheduledReports = EmptySchedule()
        }, ct);

        return response.IsSuccessStatusCode ? facilityId : null;
    }

    private IReadOnlyList<ApiTestRunResult> SkipEverything(string reason) =>
        GetEndpointDefinitions()
            .Select(definition => SkipStepAsync(definition.EndpointName, reason))
            .ToList();

    private static string? TryExtractFirstMeasureId(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return null;

        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Array)
                return null;

            foreach (var item in doc.RootElement.EnumerateArray())
            {
                if (item.ValueKind == JsonValueKind.String)
                    return item.GetString();

                if (item.ValueKind != JsonValueKind.Object)
                    continue;

                if (item.TryGetProperty("id", out var id) && id.ValueKind == JsonValueKind.String)
                    return id.GetString();

                if (item.TryGetProperty("measureId", out var measureId) && measureId.ValueKind == JsonValueKind.String)
                    return measureId.GetString();
            }
        }
        catch (JsonException)
        {
            return null;
        }

        return null;
    }

    private static MeasureMappingModel Mapping(string measure, string dqm, Frequency frequency = Frequency.Monthly) =>
        new()
        {
            Measure = measure,
            DQM = dqm,
            Frequency = frequency
        };

    private static FacilityReportingPlanRequest Plan(string facilityId, string measureMappingId) =>
        new()
        {
            FacilityId = facilityId,
            MeasureMappingId = measureMappingId,
            ReportingMonth = ReportingMonth,
            ReportingYear = ReportingYear,
            IsReporting = true
        };

    private static FacilityModel FacilityWithSchedule(string facilityId, string dqm) =>
        new()
        {
            FacilityId = facilityId,
            FacilityName = facilityId,
            TimeZone = "America/Chicago",
            Vendor = new VendorModel { Name = "Epic" },
            ScheduledReports = new TenantScheduledReportConfig
            {
                Monthly = [dqm],
                Daily = [],
                Weekly = []
            }
        };

    private static TenantScheduledReportConfig EmptySchedule() => new()
    {
        Monthly = [],
        Daily = [],
        Weekly = []
    };
}
