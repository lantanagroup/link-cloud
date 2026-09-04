using Automation.UI.Models.ApiHealth;
using LantanaGroup.Link.Sdk.Clients;
using LantanaGroup.Link.Sdk.ApiClient;
using Automation.UI.Services.ApiHealth.Seeding;
using StepNames = Automation.UI.Services.ApiHealth.TestSuites.ApiEndPointLibrary.ReportSteps;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using Microsoft.Extensions.Options;
using System.Diagnostics;

namespace Automation.UI.Services.ApiHealth.TestSuites;

/// <summary>
/// Exercises Report service endpoints via LinkSdk.
/// Tests schedules, entries, resources, and populations across all response code paths.
/// </summary>
public sealed class ReportServiceTestSuite : ServiceTestSuiteBase
{
    private readonly IReportServiceClient _client;
    private readonly IApiHealthSeedContextAccessor _seedContext;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IOptions<ServiceRegistry> _serviceRegistry;

    public override string ServiceName => "Report";
    public ReportServiceTestSuite(
        IReportServiceClient client,
        IApiHealthSeedContextAccessor seedContext,
        IHttpClientFactory httpClientFactory,
        IOptions<ServiceRegistry> serviceRegistry)
    {
        _client = client;
        _seedContext = seedContext;
        _httpClientFactory = httpClientFactory;
        _serviceRegistry = serviceRegistry;
    }

    public override IReadOnlyList<ApiEndpointDefinition> GetEndpointDefinitions() =>
        ApiEndPointLibrary.GetServiceEndpoints(ServiceName);

    public override IReadOnlyList<ApiHealthSeedRequirement> GetSeedRequirements() =>
    [
        ApiHealthSeedRequirement.ReportSchedule
    ];

    public override async Task<IReadOnlyList<ApiTestRunResult>> ExecuteAsync(CancellationToken ct = default)
    {
        var results = new List<ApiTestRunResult>();

        var baseUrl = _serviceRegistry.Value.ReportServiceUrl?.TrimEnd('/');

        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            const string error =
                "ServiceRegistry:ReportServiceUrl is not configured.";

            foreach (var endpointName in new[]
            {
                StepNames.InfoGet200,
                StepNames.RootHealthGet200
            })
            {
                results.Add(new ApiTestRunResult
                {
                    EndpointKey = $"{ServiceName}::{endpointName}",
                    ServiceName = ServiceName,
                    EndpointName = endpointName,
                    Passed = false,
                    ExpectedStatusCode = 200,
                    ErrorMessage = error,
                    RequestBody =
                        "Request was not sent because the Report service URL is missing.",
                    ResponseBody =
                        "Response was not received because the Report service URL is missing.",
                    ExecutedAt = DateTimeOffset.UtcNow
                });
            }
        }
        else
        {
            results.Add(await CallRawGetAsync(
                StepNames.InfoGet200,
                baseUrl,
                "/api/Report/info",
                ct));

            results.Add(await CallRawGetAsync(
                StepNames.RootHealthGet200,
                baseUrl,
                "/health",
                ct));
        }

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

        // === Schedule lifecycle: soft-delete → restore (seed-owned data only) ===
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

        // === GET /api/schedules/summaries ===
        results.Add(await RunStepAsync(StepNames.ReportSummaries200, 200, async () =>
            await _client.GetReportSummariesAsync(fakeFacilityId, cancellationToken: ct), ct: ct));

        await AddSeededOrSkipAsync(
            scheduleFacilityId != null,
            StepNames.ReportSummaries200HasData,
            200,
            async () =>
            {
                var response = await _client.GetReportSummariesAsync(scheduleFacilityId, cancellationToken: ct);
                if (response.IsSuccessStatusCode && response.Body?.Records is not { Count: > 0 })
                {
                    throw new InvalidOperationException("Expected the seeded facility to have at least one report summary.");
                }

                return response;
            },
            scheduleUnavailableReason);

        // === GET /api/schedules/{id}/summary ===
        await AddSeededOrSkipAsync(
            scheduleId != null,
            StepNames.ReportSummaryGet200HasData,
            200,
            async () =>
            {
                var response = await _client.GetReportSummaryAsync(scheduleId!, ct);
                if (response.IsSuccessStatusCode && response.Body is null)
                {
                    throw new InvalidOperationException("The report summary endpoint returned no summary for the seeded schedule.");
                }

                return response;
            },
            scheduleUnavailableReason);

        results.Add(await RunStepAsync(StepNames.ReportSummaryGet404, 404, async () =>
            await _client.GetReportSummaryAsync(fakeReportId, ct), ct: ct));

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

        results.Add(SkipStepAsync(
            StepNames.ResourcesByPatient200HasData,
            "ReportResource rows are no longer populated, so this endpoint cannot be validated against seeded data."));

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

    private async Task<ApiTestRunResult> CallRawGetAsync(
        string endpointName,
        string baseUrl,
        string relativePath,
        CancellationToken ct)
    {
        var result = new ApiTestRunResult
        {
            EndpointKey = $"{ServiceName}::{endpointName}",
            ServiceName = ServiceName,
            EndpointName = endpointName,
            ExpectedStatusCode = 200,
            ExecutedAt = DateTimeOffset.UtcNow,
            RequestMethod = "GET",
            RequestUrl = $"{baseUrl}{relativePath}",
            RequestBody = "No request body was sent (GET)."
        };

        var sw = Stopwatch.StartNew();

        try
        {
            ct.ThrowIfCancellationRequested();

            var httpClient =
                _httpClientFactory.CreateClient("ApiHealthTest");

            using var response = await httpClient.GetAsync(
                $"{baseUrl}{relativePath}",
                ct);

            var responseBody =
                await response.Content.ReadAsStringAsync(ct);

            sw.Stop();

            result.ActualStatusCode = (int)response.StatusCode;
            result.DurationMs = sw.ElapsedMilliseconds;
            result.Passed = result.ActualStatusCode == 200;

            result.ResponseBody = string.IsNullOrWhiteSpace(responseBody)
                ? $"No response body was returned (HTTP {result.ActualStatusCode})."
                : responseBody;

            if (!result.Passed)
            {
                result.ErrorMessage =
                    $"Expected HTTP 200 but got {result.ActualStatusCode}.";
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (TaskCanceledException)
        {
            sw.Stop();
            result.DurationMs = sw.ElapsedMilliseconds;
            result.Passed = false;
            result.ErrorMessage = "Request timed out.";
            result.ResponseBody =
                "No response body was received because the request timed out.";
        }
        catch (HttpRequestException ex)
        {
            sw.Stop();
            result.DurationMs = sw.ElapsedMilliseconds;
            result.Passed = false;
            result.ErrorMessage = $"HTTP error: {ex.Message}";
            result.ResponseBody =
                "No response body was received because the HTTP request failed.";
        }

        return result;
    }
}
