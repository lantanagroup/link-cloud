using Automation.UI.Models.ApiHealth;
using Automation.UI.Models.ApiHealth;
using Automation.UI.Services.ApiHealth.Seeding;
using LantanaGroup.Link.Sdk.Clients;
using StepNames = Automation.UI.Services.ApiHealth.TestSuites.ApiEndPointLibrary.SubmissionSteps;

namespace Automation.UI.Services.ApiHealth.TestSuites;

/// <summary>
/// Exercises Submission service operations via LinkSdk:
///   1. Download Submission ? 404 (proves reachability with non-existent resource)
///   2. Download Submission ? 400 (empty/whitespace facilityId)
///   3. Download Submission ? 400 (empty/whitespace reportId)
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
        IApiHealthSeedContextAccessor seedContext)
    {
        _client = client;
        _seedContext = seedContext;
        _logger = logger;
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
        var fakeFacilityId = $"ApiHealth-Sub-{Guid.NewGuid():N}";
        var fakeReportId = Guid.NewGuid().ToString();

        return
        [
            await RunSeededSuccessStepAsync(StepNames.Get200, ct),

            await RunStepAsync(StepNames.Get400BadReportId, 400, async () =>
                await _client.DownloadSubmissionAsync(fakeFacilityId, "not-a-valid-guid", cancellationToken: ct), ct: ct),

            await RunStepAsync(StepNames.Get404NotFound, 404, async () =>
                await _client.DownloadSubmissionAsync(fakeFacilityId, fakeReportId, cancellationToken: ct), ct: ct),

            await RunStepAsync(StepNames.Get400EmptyFacilityId, 400, async () =>
                await _client.DownloadSubmissionAsync(" ", fakeReportId, cancellationToken: ct), ct: ct),

            await RunStepAsync(StepNames.Get400EmptyReportId, 400, async () =>
                await _client.DownloadSubmissionAsync(fakeFacilityId, " ", cancellationToken: ct), ct: ct)
        ];
    }

    private async Task<ApiTestRunResult> RunSeededSuccessStepAsync(string stepName, CancellationToken ct)
    {
        var seeded = _seedContext.Current?.Report;
        if (seeded is not { FacilityId: { Length: > 0 } facilityId, ScheduleId: { Length: > 0 } reportId })
        {
            return SkipStepAsync(
                stepName,
                "Submission seed was unavailable. This test requires API-health seeded facility/report identifiers.");
        }

        return await RunStepAsync(stepName, 200, async () =>
            await _client.DownloadSubmissionAsync(facilityId, reportId, cancellationToken: ct), ct: ct);
    }
}
