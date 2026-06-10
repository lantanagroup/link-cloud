using Automation.UI.Models.ApiHealth;
using Automation.UI.Services.ApiHealth.Seeding;
using LantanaGroup.Link.Sdk.Clients;

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
    private readonly ILogger<ValidationServiceTestSuite> _logger;

    public override string ServiceName => "Validation";
    public ValidationServiceTestSuite(
        IValidationServiceClient client,
        IApiHealthSeedContextAccessor seedContext,
        ILogger<ValidationServiceTestSuite> logger)
    {
        _client = client;
        _seedContext = seedContext;
        _logger = logger;
    }

    public override IReadOnlyList<ApiHealthSeedRequirement> GetSeedRequirements() =>
    [
        ApiHealthSeedRequirement.ReportSchedule
    ];

    public override IReadOnlyList<ApiEndpointDefinition> GetEndpointDefinitions() =>
    [
        // GET /api/validation/artifacts
        Step("Artifacts GET → 200", "Returns whether artifacts are initialized", "/api/validation/artifacts"),

        // GET /api/validation/categories
        Step("Categories GET → 200", "Returns whether categories are initialized", "/api/validation/categories"),

        // PUT /api/validation/artifacts/{id}
        Step("Artifact PUT → 200/201", "Upserts a test OperationOutcome artifact", "/api/validation/artifacts/{id}"),

        // GET /api/validation/results
        Step("Results GET → 200 (seeded)", "Returns validation results for the seeded facility/report", "/api/validation/results"),
        Step("Results GET → 200 (empty)", "Returns empty results for non-existent report", "/api/validation/results"),
    ];

    public override async Task<IReadOnlyList<ApiTestRunResult>> ExecuteAsync(CancellationToken ct = default)
    {
        var results = new List<ApiTestRunResult>();
        var fakeFacilityId = $"ApiHealth-Val-{Guid.NewGuid():N}";

        // GET /api/validation/artifacts → 200
        results.Add(await RunStepAsync("Artifacts GET → 200", 200, async () =>
            await _client.GetArtifactsAsync(ct), ct: ct));

        // GET /api/validation/categories → 200
        results.Add(await RunStepAsync("Categories GET → 200", 200, async () =>
            await _client.GetCategoriesAsync(ct), ct: ct));

        // PUT /api/validation/artifacts/{id} → 200/201
        results.Add(await RunStepAsync("Artifact PUT → 200/201", async () =>
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
            var resp = await _client.UpsertResourceArtifactAsync(artifactId, payload, ct);
            if (!resp.IsSuccessStatusCode)
                throw new InvalidOperationException($"Expected 200/201 but got {resp.StatusCode}. {resp.RawBody}");
        }, ct: ct));

        // GET /api/validation/results → 200 (empty)
        var seeded = _seedContext.Current?.Report;
        if (seeded is { FacilityId: { Length: > 0 } facilityId, ScheduleId: { Length: > 0 } reportId })
        {
            results.Add(await RunStepAsync("Results GET → 200 (seeded)", 200, async () =>
                await _client.GetValidationResultsAsync(facilityId, reportId, cancellationToken: ct), ct: ct));
        }
        else
        {
            results.Add(SkipStepAsync("Results GET → 200 (seeded)", "Validation seeded result check requires seeded facility/report identifiers."));
        }

        // GET /api/validation/results → 200 (empty)
        results.Add(await RunStepAsync("Results GET → 200 (empty)", 200, async () =>
            await _client.GetValidationResultsAsync(fakeFacilityId, Guid.NewGuid().ToString(), cancellationToken: ct), ct: ct));

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
