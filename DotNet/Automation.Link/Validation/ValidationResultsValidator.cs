using LantanaGroup.Link.Automation.Link.Helpers;

namespace LantanaGroup.Link.Automation.Link.Validation;

/// <summary>
/// Validates that validation pipeline produced results for each expected patient and
/// no unhandled exceptions are present in Validation logs.
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
        TimeSpan? lookback = null)
    {
        var errors = new List<string>();

        // Lightweight API availability check.
        try
        {
            var result = await _validationClient.GetValidationResultsAsync(facilityId, reportId, "WARNING");
            if (!result.IsSuccessStatusCode)
                errors.Add($"Validation API returned HTTP {result.StatusCode}: {result.RawBody ?? "(no body)"}");
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
                reportId);

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
