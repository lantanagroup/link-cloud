using Automation.UI.Models.ApiHealth;
using Automation.UI.Models.ApiHealth;
using Automation.UI.Services.ApiHealth.Seeding;
using LantanaGroup.Link.Sdk.Clients;

namespace Automation.UI.Services.ApiHealth.TestSuites;

/// <summary>
/// Exercises Submission service operations via LinkSdk:
///   1. Download Submission → 404 (proves reachability with non-existent resource)
///   2. Download Submission → 400 (empty/whitespace facilityId)
///   3. Download Submission → 400 (empty/whitespace reportId)
///
/// Note: A 200 test requires a fully-submitted report in blob storage.
/// This is inherently not self-contained without running a full pipeline.
/// The 404 test proves the service is reachable and the 400 tests validate both
/// independent input guards in the controller.
/// </summary>
public sealed class SubmissionServiceTestSuite : ServiceTestSuiteBase
{
    private readonly ISubmissionServiceClient _client;
    private readonly IApiHealthSeedContextAccessor _seedContext;
    private readonly ILogger<SubmissionServiceTestSuite> _logger;

    public override string ServiceName => "Submission";

    public SubmissionServiceTestSuite(
        ISubmissionServiceClient client,
        IApiHealthSeedContextAccessor seedContext,
        ILogger<SubmissionServiceTestSuite> logger)
    {
        _client = client;
        _seedContext = seedContext;
        _logger = logger;
    }

    public override IReadOnlyList<ApiEndpointDefinition> GetEndpointDefinitions() =>
    [
        Step("GET → 200", "Downloads submission payload for API-health seed run", "/api/Submission/{facilityId}/{reportId}"),
        Step("GET → 400 (bad reportId)", "Returns 400 when reportId is not a valid GUID (proxied from Report service)", "/api/Submission/{facilityId}/{reportId}"),
        Step("GET → 404 (not found)", "Returns 404 for non-existent report (proves reachability)", "/api/Submission/{facilityId}/{reportId}"),
        Step("GET → 400 (empty facilityId)", "Returns 400 for empty/whitespace facilityId", "/api/Submission/{facilityId}/{reportId}"),
        Step("GET → 400 (empty reportId)", "Returns 400 for empty/whitespace reportId", "/api/Submission/{facilityId}/{reportId}"),
    ];

    public override IReadOnlyList<ApiHealthSeedRequirement> GetSeedRequirements() =>
    [
        ApiHealthSeedRequirement.ReportSchedule
    ];

    public override async Task<IReadOnlyList<ApiTestRunResult>> ExecuteAsync(CancellationToken ct = default)
    {
        var results = new List<ApiTestRunResult>();
        var fakeFacilityId = $"ApiHealth-Sub-{Guid.NewGuid():N}";
        var fakeReportId = Guid.NewGuid().ToString();

        var seeded = _seedContext.Current?.Report;
        if (seeded is { FacilityId: { Length: > 0 } facilityId, ScheduleId: { Length: > 0 } reportId })
        {
            results.Add(await RunStepAsync("GET → 200", 200, async () =>
                await _client.DownloadSubmissionAsync(facilityId, reportId, cancellationToken: ct), ct: ct));
        }
        else
        {
            results.Add(new ApiTestRunResult
            {
                EndpointKey = $"{ServiceName}::GET → 200",
                ServiceName = ServiceName,
                EndpointName = "GET → 200",
                ExpectedStatusCode = 200,
                Passed = false,
                ErrorMessage = "Submission seed was unavailable. This test requires API-health seeded facility/report identifiers.",
                ExecutedAt = DateTimeOffset.UtcNow
            });
        }

        // GET → 404: Non-existent report proves service reachability.
        results.Add(await RunStepAsync("GET → 400 (bad reportId)", 400, async () =>
            await _client.DownloadSubmissionAsync(fakeFacilityId, "not-a-valid-guid", cancellationToken: ct), ct: ct));

        // GET → 404: Non-existent report proves service reachability.
        results.Add(await RunStepAsync("GET → 404 (not found)", 404, async () =>
            await _client.DownloadSubmissionAsync(fakeFacilityId, fakeReportId, cancellationToken: ct), ct: ct));

        // GET → 400: Empty facilityId — first input guard in the controller.
        results.Add(await RunStepAsync("GET → 400 (empty facilityId)", 400, async () =>
            await _client.DownloadSubmissionAsync(" ", fakeReportId, cancellationToken: ct), ct: ct));

        // GET → 400: Empty reportId — second independent input guard in the controller.
        results.Add(await RunStepAsync("GET → 400 (empty reportId)", 400, async () =>
            await _client.DownloadSubmissionAsync(fakeFacilityId, " ", cancellationToken: ct), ct: ct));

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

    private ApiEndpointDefinition Step(string name, string desc, string? group = null) => new()
    {
        ServiceName = ServiceName,
        GroupName = group,
        EndpointName = name,
        Description = desc,
        IsTestSuiteStep = true
    };
}
