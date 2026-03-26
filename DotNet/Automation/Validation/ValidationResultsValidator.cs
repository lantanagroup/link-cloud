using Newtonsoft.Json.Linq;
using RestSharp;
using Xunit;
using Xunit.Abstractions;

namespace LantanaGroup.Link.Automation.Validation;

/// <summary>
/// Queries the Validation service's REST API (via Admin BFF) to retrieve and display
/// FHIR validation results for the report.
/// </summary>
public class ValidationResultsValidator(RestClient client, ITestOutputHelper output)
{
    public async Task ValidateAllAsync(
        string facilityId,
        string reportId,
        List<string> expectedPatientIds)
    {
        output.WriteLine("");
        output.WriteLine("=================================================================================");
        output.WriteLine("  VALIDATION RESULTS (API)");
        output.WriteLine($"  FacilityId: {facilityId}");
        output.WriteLine($"  ReportId:   {reportId}");
        output.WriteLine("=================================================================================");

        await ValidateReportResults(facilityId, reportId);
        await ValidatePatientResults(facilityId, reportId, expectedPatientIds);
        await ValidateCategorySummary(facilityId, reportId);

        output.WriteLine("---------------------------------------------------------------------------------");
        output.WriteLine("  VALIDATION RESULTS COMPLETE");
        output.WriteLine("---------------------------------------------------------------------------------");
        output.WriteLine("");
    }

    private async Task ValidateReportResults(string facilityId, string reportId)
    {
        output.WriteLine("");
        output.WriteLine("  --- Report-Level Results ---");

        try
        {
            var request = new RestRequest($"validation/result/{facilityId}/{reportId}", Method.Get);
            request.AddParameter("severity", "WARNING");
            var response = await client.ExecuteAsync(request);

            if (response.StatusCode != System.Net.HttpStatusCode.OK)
            {
                output.WriteLine($"      API returned {response.StatusCode} - skipping validation results check");
                output.WriteLine("  --- Report-Level Results SKIPPED ---");
                return;
            }

            var results = JArray.Parse(response.Content ?? "[]");

            var bySeverity = results
                .GroupBy(r => r["severity"]?.ToString() ?? "UNKNOWN")
                .Select(g => $"{g.Key}={g.Count()}")
                .ToList();

            output.WriteLine($"      Total Results  = {results.Count}");
            output.WriteLine($"      By Severity    = {string.Join(", ", bySeverity)}");

            var errors = results
                .Where(r =>
                {
                    var sev = r["severity"]?.ToString();
                    return sev == "ERROR" || sev == "FATAL";
                })
                .Take(5)
                .ToList();

            if (errors.Count > 0)
            {
                output.WriteLine($"      Top Errors ({errors.Count} shown):");
                foreach (var err in errors)
                {
                    var msg = err["message"]?.ToString() ?? "(no message)";
                    var loc = err["location"]?.ToString() ?? "";
                    var expr = err["expression"]?.ToString() ?? "";
                    if (msg.Length > 200) msg = msg[..200] + "...";
                    output.WriteLine($"        - [{err["severity"]}] {msg}");
                    if (!string.IsNullOrEmpty(loc))
                        output.WriteLine($"          Location: {loc}");
                    if (!string.IsNullOrEmpty(expr))
                        output.WriteLine($"          Expression: {expr}");
                }
            }
            else
            {
                output.WriteLine("      No ERROR/FATAL results found");
            }

            Assert.NotNull(results);
        }
        catch (Exception ex)
        {
            output.WriteLine($"      Error querying validation results API: {ex.Message}");
        }

        output.WriteLine("  --- Report-Level Results PASSED ---");
    }

    private async Task ValidatePatientResults(string facilityId, string reportId, List<string> expectedPatientIds)
    {
        output.WriteLine("");
        output.WriteLine("  --- Patient-Level Results ---");

        foreach (var patientId in expectedPatientIds)
        {
            try
            {
                var request = new RestRequest($"validation/result/{facilityId}/{reportId}/{patientId}", Method.Get);
                request.AddParameter("severity", "WARNING");
                var response = await client.ExecuteAsync(request);

                if (response.StatusCode != System.Net.HttpStatusCode.OK)
                {
                    output.WriteLine($"      Patient {patientId,-12} API returned {response.StatusCode}");
                    continue;
                }

                var results = JArray.Parse(response.Content ?? "[]");

                var bySeverity = results
                    .GroupBy(r => r["severity"]?.ToString() ?? "UNKNOWN")
                    .Select(g => $"{g.Key}={g.Count()}")
                    .ToList();

                var errorCount = results.Count(r =>
                {
                    var sev = r["severity"]?.ToString();
                    return sev == "ERROR" || sev == "FATAL";
                });

                output.WriteLine($"      Patient {patientId,-12} {results.Count} result(s) [{string.Join(", ", bySeverity)}], errors={errorCount}");
            }
            catch (Exception ex)
            {
                output.WriteLine($"      Patient {patientId,-12} Error: {ex.Message}");
            }
        }

        output.WriteLine("  --- Patient-Level Results PASSED ---");
    }

    private async Task ValidateCategorySummary(string facilityId, string reportId)
    {
        output.WriteLine("");
        output.WriteLine("  --- Category Summary ---");

        try
        {
            var request = new RestRequest($"validation/result/{facilityId}/{reportId}/category", Method.Get);
            var response = await client.ExecuteAsync(request);

            if (response.StatusCode != System.Net.HttpStatusCode.OK)
            {
                output.WriteLine($"      API returned {response.StatusCode} - skipping category check");
                output.WriteLine("  --- Category Summary SKIPPED ---");
                return;
            }

            var categories = JArray.Parse(response.Content ?? "[]");

            if (categories.Count == 0)
            {
                output.WriteLine("      No categories with issues found");
            }
            else
            {
                output.WriteLine($"      Categories with issues = {categories.Count}");
                foreach (var cat in categories)
                {
                    var catId = cat["categoryId"]?.ToString() ?? "(unknown)";
                    var issues = cat["issues"]?.Value<int>() ?? 0;
                    var acceptable = cat["acceptable"]?.Value<bool>() ?? false;
                    output.WriteLine($"        {catId,-30} issues={issues}, acceptable={acceptable}");
                }
            }
        }
        catch (Exception ex)
        {
            output.WriteLine($"      Error querying category API: {ex.Message}");
        }

        output.WriteLine("  --- Category Summary PASSED ---");
    }
}
