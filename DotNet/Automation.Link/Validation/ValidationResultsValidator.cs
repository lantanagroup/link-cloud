using LantanaGroup.Link.Automation.Link.Helpers;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace LantanaGroup.Link.Automation.Link.Validation;

/// <summary>
/// Checks that the validation result summary endpoint succeeds and that Validation logs
/// have no unhandled exceptions for the run.
/// </summary>
public class ValidationResultsValidator
{
    private readonly LantanaGroup.Link.Sdk.Clients.IValidationServiceClient _validationClient;
    private readonly IAutomationOutput _output;
    private readonly LokiScraper? _lokiScraper;

    public ValidationResultsValidator(LantanaGroup.Link.Sdk.Clients.IValidationServiceClient validationClient, IAutomationOutput output, LokiScraper? lokiScraper = null)
    {
        _validationClient = validationClient;
        _output = output;
        _lokiScraper = lokiScraper;
    }

    public async Task ValidateAllAsync(
        string facilityId,
        string reportId,
        List<string> expectedPatientIds,
        TimeSpan? lookback = null,
        CancellationToken cancellationToken = default)
    {
        var errors = new List<string>();

        // Count only. The result list for a census or mega-patient is large enough that buffering
        // it as a string throws OutOfMemoryException in this process.
        try
        {
            var result = await _validationClient.GetValidationResultSummaryAsync(
                facilityId, reportId, "WARNING", cancellationToken);
            // Flurl turns a caller cancel into FlurlHttpException, and SendStringAsync reports that as HTTP 0.
            cancellationToken.ThrowIfCancellationRequested();
            var body = result.Body ?? result.RawBody;
            if (!result.IsSuccessStatusCode)
                errors.Add($"Validation API returned HTTP {result.StatusCode}: {body ?? "(no body)"}");
            else if (!IsResultSummary(body))
                errors.Add($"Validation API returned HTTP {result.StatusCode} but the body was not a result summary: {Snippet(body)}");
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            errors.Add($"Validation API exception: {ex.Message}");
        }

        // Exception-focused log check from Validation service.
        if (_lokiScraper != null)
        {
            var window = lookback ?? TimeSpan.FromMinutes(5);
            var exceptionLines = await _lokiScraper.GetServiceExceptionLinesAsync(
                LokiScraper.Components.Validation,
                window,
                20,
                facilityId,
                reportId,
                cancellationToken);

            if (exceptionLines.Count > 0)
            {
                errors.Add($"Validation service reported {exceptionLines.Count} exception/error log line(s) in the last {window.TotalMinutes:F0}m.");
                foreach (var line in exceptionLines)
                {
                    errors.Add($"ValidationLog: {line}");
                }
            }
        }

        if (errors.Count == 0)
        {
            _output.WriteLine("VALIDATION RESULTS (API): Passed");
            return;
        }

        _output.WriteLine($"VALIDATION RESULTS (API): Failed ({errors.Count} issue(s))");
        foreach (var error in errors)
        {
            _output.WriteLine($"  - {error}");
        }

        throw new InvalidOperationException($"VALIDATION RESULTS (API) failed with {errors.Count} issue(s).");
    }

    /// <summary>
    /// An older validation service has no result-summaries route. A 200 whose body is a JSON array
    /// is some other resource, not this count.
    /// </summary>
    private static bool IsResultSummary(string? body)
    {
        if (string.IsNullOrWhiteSpace(body))
            return false;

        try
        {
            if (JToken.Parse(body) is not JObject obj)
                return false;

            var count = obj["count"];
            var countIsNumber = count is { Type: JTokenType.Integer or JTokenType.Float };
            return countIsNumber && obj["severity"]?.Type == JTokenType.String;
        }
        catch (JsonReaderException)
        {
            return false;
        }
    }

    private static string Snippet(string? body)
    {
        if (string.IsNullOrWhiteSpace(body))
            return "(no body)";

        const int max = 180;
        var trimmed = body.Trim();
        return trimmed.Length <= max ? trimmed : trimmed[..max];
    }
}
