using Automation.UI.Models.ApiHealth;
using LantanaGroup.Link.Sdk.Clients;
using System.Text.Json;

namespace Automation.UI.Services.ApiHealth.TestSuites;

/// <summary>
/// Exercises MeasureEval service operations via LinkSdk.
///
/// NOTE: MeasureEval is a Java service. All error paths return 400 — there is no 422.
/// FhirParseException, ValidationException, and ResponseStatusException are all
/// mapped to 400 by ExceptionHandlers.java.
///
/// Two distinct PUT 400 paths are covered:
///   1. Malformed body ("{}") — HAPI-FHIR cannot deserialize → FhirParseException → 400.
///   2. Valid FHIR Bundle with an id but no Measure resource — passes deserialization
///      and the id guard, then MeasureDefinitionBundleValidator throws ValidationException → 400.
///
/// A 200 test for PUT requires a complete CQL Bundle and is not self-contained.
/// </summary>
public sealed class MeasureEvalTestSuite : ServiceTestSuiteBase
{
    private readonly IMeasureEvalServiceClient _client;
    private readonly ILogger<MeasureEvalTestSuite> _logger;

    public override string ServiceName => "MeasureEval";
    public MeasureEvalTestSuite(IMeasureEvalServiceClient client, ILogger<MeasureEvalTestSuite> logger)
    {
        _client = client;
        _logger = logger;
    }

    public override IReadOnlyList<ApiEndpointDefinition> GetEndpointDefinitions() =>
    [
        // GET /measureeval/measure-definition
        Step("GET ALL → 200", "Returns list of all measure definitions", "/measureeval/measure-definition"),

        // GET /measureeval/measure-definition/{id}
        Step("GET → 200", "Returns an existing measure definition", "/measureeval/measure-definition/{id}"),
        Step("GET → 404", "Returns 404 for a non-existent measure definition", "/measureeval/measure-definition/{id}"),

        // PUT /measureeval/measure-definition
        Step("PUT → 400 (malformed body)", "Returns 400 when body is not valid FHIR JSON — FhirParseException path", "/measureeval/measure-definition"),
        Step("PUT → 400 (no measure in bundle)", "Returns 400 when Bundle has an id but no Measure resource — BundleValidator path", "/measureeval/measure-definition"),
    ];

    public override async Task<IReadOnlyList<ApiTestRunResult>> ExecuteAsync(CancellationToken ct = default)
    {
        var results = new List<ApiTestRunResult>();
        var fakeMeasureId = $"ApiHealth-Measure-{Guid.NewGuid():N}";

        // GET ALL → 200 (always succeeds, returns empty or populated list)
        results.Add(await RunStepAsync("GET ALL → 200", 200, async () =>
            await _client.GetAllMeasureDefinitionsAsync(ct), ct: ct));

        var allMeasures = await _client.GetAllMeasureDefinitionsAsync(ct);
        var existingMeasureId = TryExtractFirstMeasureId(allMeasures.Body);
        if (!string.IsNullOrWhiteSpace(existingMeasureId))
        {
            results.Add(await RunStepAsync("GET → 200", 200, async () =>
                await _client.GetMeasureDefinitionAsync(existingMeasureId, ct), ct: ct));
        }
        else
        {
            results.Add(SkipStepAsync("GET → 200", "No measure definitions were available to validate the GET-by-id 200 path."));
        }

        // GET → 404 (non-existent measure)
        results.Add(await RunStepAsync("GET → 404", 404, async () =>
            await _client.GetMeasureDefinitionAsync(fakeMeasureId, ct), ct: ct));

        // PUT → 400 (malformed body)
        // "{}" is not valid FHIR JSON — HAPI-FHIR throws FhirParseException → ExceptionHandlers → 400.
        results.Add(await RunStepAsync("PUT → 400 (malformed body)", 400, async () =>
            await _client.PutMeasureDefinitionAsync("{}", ct), ct: ct));

        // PUT → 400 (no measure in bundle)
        // A syntactically valid FHIR Bundle with an id but no Measure entry passes deserialization
        // and the Bundle.id guard, but MeasureDefinitionBundleValidator throws ValidationException → 400.
        var bundleNoMeasure = """
            {
              "resourceType": "Bundle",
              "id": "ApiHealth-MeasureEval-NoMeasure",
              "type": "collection",
              "entry": []
            }
            """;
        results.Add(await RunStepAsync("PUT → 400 (no measure in bundle)", 400, async () =>
            await _client.PutMeasureDefinitionAsync(bundleNoMeasure, ct), ct: ct));

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

                if (item.ValueKind == JsonValueKind.Object)
                {
                    if (item.TryGetProperty("id", out var id) && id.ValueKind == JsonValueKind.String)
                        return id.GetString();
                    if (item.TryGetProperty("measureId", out var measureId) && measureId.ValueKind == JsonValueKind.String)
                        return measureId.GetString();
                }
            }
        }
        catch
        {
            return null;
        }

        return null;
    }
}
