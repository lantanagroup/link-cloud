using LantanaGroup.Link.Automation.Helpers;
using RestSharp;
using Xunit.Abstractions;

namespace LantanaGroup.Link.Automation.Validation;

/// <summary>
/// Exception-focused validation check.
///
/// This validator does not fail on ordinary FHIR validation result content.
/// It only fails when Validation service/API-level exception conditions are detected.
/// </summary>
public class ValidationResultsValidator
{
    private readonly RestClient _client;
    private readonly ITestOutputHelper _output;
    private readonly LokiScraper? _lokiScraper;

    public ValidationResultsValidator(RestClient client, ITestOutputHelper output, LokiScraper? lokiScraper = null)
    {
        _client = client;
        _output = output;
        _lokiScraper = lokiScraper;
    }

    public async Task ValidateAllAsync(
        string facilityId,
        string reportId,
        List<string> expectedPatientIds,
        TimeSpan? lookback = null)
    {
        var errors = new List<string>();

        // Lightweight API availability check.
        try
        {
            var req = new RestRequest($"validation/result/{facilityId}/{reportId}", Method.Get);
            req.AddParameter("severity", "WARNING");
            var resp = await _client.ExecuteAsync(req);
            if (!resp.IsSuccessful)
            {
                errors.Add($"Validation API call failed with status {(int)resp.StatusCode} {resp.StatusCode}");
            }
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
                20);

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
}
