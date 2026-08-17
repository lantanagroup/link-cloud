using Automation.UI.Models.ApiHealth;
using Automation.UI.Services.ApiHealth.Seeding;
using LantanaGroup.Link.Sdk.Clients;
using StepNames = Automation.UI.Services.ApiHealth.TestSuites.ApiEndPointLibrary.ValidationSteps;

namespace Automation.UI.Services.ApiHealth.TestSuites;

/// <summary>
/// Exercises Validation service operations via LinkSdk:
///   1. Has Artifacts (check if initialized)
///   2. Has Categories (check if initialized)
///   3. Upsert Resource Artifact
///   4. Get Validation Results (non-existent — proves reachability)
///
/// NOTE: Validation is a Java service. InitializeArtifacts/InitializeCategories
/// are only called if not already initialized, to avoid disrupting existing state.
/// </summary>
public sealed class ValidationServiceTestSuite : ServiceTestSuiteBase
{
    private readonly IValidationServiceClient _client;
    private readonly IApiHealthSeedContextAccessor _seedContext;

    public override string ServiceName => "Validation";
    public ValidationServiceTestSuite(
        IValidationServiceClient client,
        IApiHealthSeedContextAccessor seedContext)
    {
        _client = client;
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
        var fakeFacilityId = $"ApiHealth-Val-{Guid.NewGuid():N}";

        return
        [
            await RunStepAsync(StepNames.ArtifactsGet200, 200, async () =>
                await _client.GetArtifactsAsync(ct), ct: ct),

            await RunStepAsync(StepNames.CategoriesGet200, 200, async () =>
                await _client.GetCategoriesAsync(ct), ct: ct),

            await RunStepAsync(StepNames.ArtifactPut200Or201, [200, 201], async () =>
            {
                var artifactId = $"OperationOutcome-{Guid.NewGuid():N}";
                var payload = $$"""
                {
                    "resourceType": "OperationOutcome",
                    "id": "{{Guid.NewGuid():N}}",
                    "issue": [{
                        "severity": "information",
                        "code": "informational",
                        "diagnostics": "ApiHealth stability test artifact"
                    }]
                }
                """;
                return await _client.UpsertResourceArtifactAsync(artifactId, payload, ct);
            }, ct: ct),

            await RunSeededResultsStepAsync(StepNames.ResultsGet200Seeded, ct),

            await RunStepAsync(StepNames.ResultsGet200Empty, 200, async () =>
                await _client.GetValidationResultsAsync(fakeFacilityId, Guid.NewGuid().ToString(), cancellationToken: ct), ct: ct)
        ];
    }

    private async Task<ApiTestRunResult> RunSeededResultsStepAsync(string stepName, CancellationToken ct)
    {
        var seeded = _seedContext.Current?.Report;
        if (seeded is not { FacilityId: { Length: > 0 } facilityId, ScheduleId: { Length: > 0 } reportId })
            return SkipStepAsync(stepName, "Validation seeded result check requires seeded facility/report identifiers.");

        return await RunStepAsync(stepName, 200, async () =>
            await _client.GetValidationResultsAsync(facilityId, reportId, cancellationToken: ct), ct: ct);
    }
}
