using LantanaGroup.Link.Automation.Link.Helpers;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace LantanaGroup.Link.Automation.Link.Validation;

/// <summary>
/// Checks that the validation result summary endpoint succeeds, that the report's stored category counts
/// match its saved findings, and that Validation logs have no unhandled exceptions for the run.
/// </summary>
public class ValidationResultsValidator
{
    /// <summary>
    /// Above this many saved findings the comparison with live category counts is skipped, because the
    /// validation service loads every finding of the report to count them live.
    /// </summary>
    public const long CategoryCountComparisonLimit = 100_000;

    private const string UncategorizedCategoryId = "uncategorized";

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

        try
        {
            await CheckCategoryCountsAsync(facilityId, reportId, errors, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            errors.Add($"Validation category counts exception: {ex.Message}");
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
    /// Compares the report's stored category counts with live counts of its saved findings. A finding is saved
    /// only when it has a submitted category, so a submitted category's stored total must equal its live count,
    /// while a non-submitted category's total also includes findings that weren't saved and can only be at least
    /// its live count. Uncategorized findings are never saved, so they have nothing to compare with.
    /// See dev-docs/validation-category-counts.md.
    /// </summary>
    private async Task CheckCategoryCountsAsync(string facilityId,
                                                string reportId,
                                                List<string> errors,
                                                CancellationToken cancellationToken)
    {
        var summary = await _validationClient.GetValidationResultSummaryAsync(
            facilityId, reportId, "INFORMATION", cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        var savedFindings = ReadSummaryCount(summary.IsSuccessStatusCode ? summary.Body : null);
        if (savedFindings is null)
        {
            errors.Add($"Validation category counts: the INFORMATION result summary returned HTTP " +
                       $"{summary.StatusCode}: {Snippet(summary.Body ?? summary.RawBody)}");
            return;
        }

        var countsResponse = await _validationClient.GetReportCategoryCountsAsync(
            facilityId, reportId, submit: null, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        if (countsResponse.StatusCode == 404)
        {
            // Every validated patient writes the counts, so a report with saved findings must have them.
            if (savedFindings > 0)
            {
                errors.Add($"Validation category counts are missing although the report has {savedFindings} " +
                           "saved finding(s)");
                return;
            }

            _output.WriteLine("VALIDATION CATEGORY COUNTS: none stored and no saved findings; nothing to compare");
            return;
        }

        if (!countsResponse.IsSuccessStatusCode)
        {
            errors.Add($"Validation category counts returned HTTP {countsResponse.StatusCode}: " +
                       Snippet(countsResponse.RawBody));
            return;
        }

        var stored = ReadStoredCounts(countsResponse.Body);
        if (stored is null)
        {
            errors.Add($"Validation category counts body was not a counts response: {Snippet(countsResponse.Body)}");
            return;
        }

        if (savedFindings > CategoryCountComparisonLimit)
        {
            _output.WriteLine($"VALIDATION CATEGORY COUNTS: {stored.Count} categories stored; comparison with the " +
                              $"{savedFindings} saved findings skipped above {CategoryCountComparisonLimit}");
            return;
        }

        var liveResponse = await _validationClient.GetReportCategorySummariesAsync(
            facilityId, reportId, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        var live = liveResponse.IsSuccessStatusCode ? ReadLiveCounts(liveResponse.Body) : null;
        if (live is null)
        {
            errors.Add($"Validation live category counts returned HTTP {liveResponse.StatusCode}: " +
                       Snippet(liveResponse.Body ?? liveResponse.RawBody));
            return;
        }

        var mismatches = 0;
        foreach (var (categoryId, total) in stored)
        {
            if (categoryId.Equals(UncategorizedCategoryId, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            live.TryGetValue(categoryId, out var liveIssues);
            if (total.Submit && total.Issues != liveIssues)
            {
                mismatches++;
                errors.Add($"Validation category '{categoryId}' (submitted): stored count {total.Issues}, " +
                           $"saved findings {liveIssues}");
            }
            else if (!total.Submit && total.Issues < liveIssues)
            {
                mismatches++;
                errors.Add($"Validation category '{categoryId}' (not submitted): stored count {total.Issues} is " +
                           $"below its {liveIssues} saved finding(s)");
            }
        }

        foreach (var (categoryId, liveIssues) in live)
        {
            if (liveIssues == 0 ||
                stored.ContainsKey(categoryId) ||
                categoryId.Equals(UncategorizedCategoryId, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            mismatches++;
            errors.Add($"Validation category '{categoryId}' has {liveIssues} saved finding(s) but no stored count");
        }

        if (mismatches == 0)
        {
            _output.WriteLine($"VALIDATION CATEGORY COUNTS: {stored.Count} categories match the {savedFindings} " +
                              "saved findings");
        }
    }

    private static long? ReadSummaryCount(string? body)
    {
        if (!IsResultSummary(body))
        {
            return null;
        }

        return JObject.Parse(body!)["count"]!.Value<long>();
    }

    /// <summary>
    /// Reads validation's stored counts, keyed by category id case-insensitively as validation compares them.
    /// Null when the body is not a counts response, including any category without a categoryId, issues or submit.
    /// </summary>
    private static Dictionary<string, (long Issues, bool Submit)>? ReadStoredCounts(string? body)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return null;
        }

        try
        {
            if (JToken.Parse(body) is not JObject obj || obj["categories"] is not JArray categories)
            {
                return null;
            }

            var stored = new Dictionary<string, (long Issues, bool Submit)>(StringComparer.OrdinalIgnoreCase);
            foreach (var entry in categories)
            {
                // A missing field must fail the check: defaulting submit to false would apply the looser rule.
                if (entry is not JObject category ||
                    category["categoryId"] is not { Type: JTokenType.String } categoryId ||
                    string.IsNullOrEmpty(categoryId.Value<string>()) ||
                    category["issues"] is not { Type: JTokenType.Integer } issues ||
                    category["submit"] is not { Type: JTokenType.Boolean } submit)
                {
                    return null;
                }

                stored[categoryId.Value<string>()!] = (issues.Value<long>(), submit.Value<bool>());
            }

            return stored;
        }
        catch (JsonReaderException)
        {
            return null;
        }
    }

    /// <summary>
    /// Reads validation's live per-category counts, summing ids that differ only by case.
    /// </summary>
    private static Dictionary<string, long>? ReadLiveCounts(string? body)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return null;
        }

        try
        {
            if (JToken.Parse(body) is not JArray categories)
            {
                return null;
            }

            var live = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
            foreach (var category in categories.OfType<JObject>())
            {
                var categoryId = category["categoryId"]?.Value<string>();
                if (string.IsNullOrEmpty(categoryId))
                {
                    continue;
                }

                live.TryGetValue(categoryId, out var issues);
                live[categoryId] = issues + (category["issues"]?.Value<long>() ?? 0);
            }

            return live;
        }
        catch (JsonReaderException)
        {
            return null;
        }
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
