using Automation.UI.Models.ApiHealth;
using LantanaGroup.Link.Sdk.Clients;
using System.Text.Json;
using StepNames = Automation.UI.Services.ApiHealth.TestSuites.ApiEndPointLibrary.MeasureEvalSteps;

namespace Automation.UI.Services.ApiHealth.TestSuites;

/// <summary>
/// Exercises MeasureEval service operations via LinkSdk.
///
/// NOTE: MeasureEval is a Java service. All error paths return 400 — there is no 422.
/// FhirParseException, ValidationException, and ResponseStatusException are all
/// mapped to 400 by ExceptionHandlers.java.
///
/// Two distinct PUT 400 paths are covered:
///   1. Malformed body ("{}") — HAPI-FHIR cannot deserialize ? FhirParseException ? 400.
///   2. Valid FHIR Bundle with an id but no Measure resource — passes deserialization
///      and the id guard, then MeasureDefinitionBundleValidator throws ValidationException ? 400.
///
/// A 200 test for PUT requires a complete CQL Bundle and is not self-contained.
/// </summary>
public sealed class MeasureEvalTestSuite : ServiceTestSuiteBase
{
    private readonly IMeasureEvalServiceClient _client;
    private readonly ILogger<MeasureEvalTestSuite> _logger;

    private static readonly string NoMeasureBundleJson = """
        {
          "resourceType": "Bundle",
          "id": "ApiHealth-MeasureEval-NoMeasure",
          "type": "collection",
          "entry": []
        }
        """;

    public override string ServiceName => "MeasureEval";
    public MeasureEvalTestSuite(IMeasureEvalServiceClient client)
    {
        _client = client;
        _logger = logger;
    }

    public override IReadOnlyList<ApiEndpointDefinition> GetEndpointDefinitions() =>
        ApiEndPointLibrary.GetServiceEndpoints(ServiceName);

    public override async Task<IReadOnlyList<ApiTestRunResult>> ExecuteAsync(CancellationToken ct = default)
    {
        return
        [
            await RunStepAsync(StepNames.GetAll200, 200, async () =>
                await _client.GetAllMeasureDefinitionsAsync(ct), ct: ct),

            await RunGetByIdSuccessStepAsync(StepNames.Get200, ct),

            await RunStepAsync(StepNames.Get404, 404, async () =>
                await _client.GetMeasureDefinitionAsync($"ApiHealth-Measure-{Guid.NewGuid():N}", ct), ct: ct),

            await RunStepAsync(StepNames.Put400MalformedBody, 400, async () =>
                await _client.PutMeasureDefinitionAsync("{}", ct), ct: ct),

            await RunStepAsync(StepNames.Put400NoMeasureInBundle, 400, async () =>
                await _client.PutMeasureDefinitionAsync(NoMeasureBundleJson, ct), ct: ct)
        ];
    }

    private async Task<ApiTestRunResult> RunGetByIdSuccessStepAsync(string stepName, CancellationToken ct)
    {
        var allMeasures = await _client.GetAllMeasureDefinitionsAsync(ct);
        var existingMeasureId = TryExtractFirstMeasureId(allMeasures.Body);
        if (string.IsNullOrWhiteSpace(existingMeasureId))
            return SkipStepAsync(stepName, "No measure definitions were available to validate the GET-by-id 200 path.");

        return await RunStepAsync(stepName, 200, async () =>
            await _client.GetMeasureDefinitionAsync(existingMeasureId, ct), ct: ct);
    }

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
