using System.Net;
using Newtonsoft.Json.Linq;
using RestSharp;
using Xunit.Abstractions;

namespace LantanaGroup.Link.Tests.E2ETests.Helpers;

public class LokiScraper(ITestOutputHelper output)
{
    private static readonly RestClient LokiClient = new(TestConfig.LokiBaseUrl);
    private DateTime _lastQueryTime = DateTime.UtcNow;

    public static class Components
    {
        public const string MeasureEval = "MeasureEval";
        public const string Validation = "Validation";
        public const string Normalization = "Normalization";
        public const string Report = "Report";
        public const string DataAcquisition = "DataAcquisition";
        public const string DataAcquisitionWorker = "DataAcquisition.AcquisitionWorker";
        public const string Submission = "Submission";
        public const string QueryDispatch = "QueryDispatch";
        public const string Tenant = "Tenant";
    }

    private static readonly string[] AllServices =
    [
        Components.DataAcquisition,
        Components.DataAcquisitionWorker,
        Components.QueryDispatch,
        Components.Normalization,
        Components.MeasureEval,
        Components.Validation,
        Components.Report,
        Components.Submission,
        Components.Tenant
    ];

    private const string HarmlessPatterns = "healthcheck|health-check|actuator|AppInfoParser|InstanceAlreadyExistsException";

    public async Task ScrapeErrorsAsync()
    {
        await ScrapeQueryAsync(
            $"{{app=\"link-cloud\"}} |~ \"(?i)(error|exception)\" !~ \"(?i)({HarmlessPatterns})\"",
            "LOKI ERROR");
    }

    /// <summary>
    /// Scans ALL services for errors/exceptions in the given time window and prints
    /// a concise summary grouped by service. Services with no issues are listed on
    /// one line. Services with issues get their most recent error lines shown.
    /// </summary>
    public async Task ScrapeAllServicesErrorSummaryAsync(TimeSpan lookback)
    {
        var end = DateTime.UtcNow;
        var start = end - lookback;
        var startUnix = ((DateTimeOffset)start).ToUnixTimeMilliseconds() * 1000000;
        var endUnix = ((DateTimeOffset)end).ToUnixTimeMilliseconds() * 1000000;

        output.WriteLine($"[DIAG] === Error summary across all services (last {lookback.TotalMinutes:F0}m) ===");

        var cleanServices = new List<string>();

        foreach (var component in AllServices)
        {
            try
            {
                var query = $"{{app=\"link-cloud\", component=\"{component}\"}} |~ \"(?i)(error|exception|fail|timeout|disconnect)\" !~ \"(?i)({HarmlessPatterns})\"";
                var request = new RestRequest("/loki/api/v1/query_range");
                request.AddParameter("query", query);
                request.AddParameter("start", startUnix.ToString());
                request.AddParameter("end", endUnix.ToString());
                request.AddParameter("limit", "5");

                var response = await LokiClient.ExecuteAsync(request);
                if (response.StatusCode != HttpStatusCode.OK || response.Content == null)
                {
                    cleanServices.Add($"{component}(?)");
                    continue;
                }

                var jsonResponse = JObject.Parse(response.Content);
                var results = jsonResponse["data"]?["result"] as JArray;

                var lines = new List<string>();
                if (results != null)
                {
                    foreach (var result in results)
                    {
                        var values = result["values"] as JArray;
                        if (values != null)
                        {
                            foreach (var value in values)
                            {
                                var logLine = value[1]?.ToString();
                                if (logLine != null)
                                {
                                    if (logLine.Length > 200) logLine = logLine[..200] + "...";
                                    lines.Add(logLine);
                                }
                            }
                        }
                    }
                }

                if (lines.Count == 0)
                {
                    cleanServices.Add(component);
                }
                else
                {
                    output.WriteLine($"[DIAG]   {component}: {lines.Count} issue(s)");
                    foreach (var line in lines)
                    {
                        output.WriteLine($"[DIAG]     -> {line}");
                    }
                }
            }
            catch (Exception ex)
            {
                cleanServices.Add($"{component}(err:{ex.Message})");
            }
        }

        if (cleanServices.Count > 0)
        {
            output.WriteLine($"[DIAG]   Clean: {string.Join(", ", cleanServices)}");
        }

        output.WriteLine("[DIAG] === End error summary ===");
    }

    /// <summary>
    /// One-time scrape of a larger time window for a specific service -- useful for
    /// post-test diagnostics to capture the full evaluation/validation lifecycle.
    /// </summary>
    public async Task ScrapeServiceHistoryAsync(string componentName, TimeSpan lookback, string label)
    {
        var end = DateTime.UtcNow;
        var start = end - lookback;

        var startUnix = ((DateTimeOffset)start).ToUnixTimeMilliseconds() * 1000000;
        var endUnix = ((DateTimeOffset)end).ToUnixTimeMilliseconds() * 1000000;

        var query = $"{{app=\"link-cloud\", component=\"{componentName}\"}} |~ \"(?i)(error|warn|exception|fail|timeout|duration|evaluated|validated|submitted|generated|measure.?report)\" !~ \"(?i)({HarmlessPatterns})\"";
        var request = new RestRequest("/loki/api/v1/query_range");
        request.AddParameter("query", query);
        request.AddParameter("start", startUnix.ToString());
        request.AddParameter("end", endUnix.ToString());
        request.AddParameter("limit", "100");

        try
        {
            var response = await LokiClient.ExecuteAsync(request);
            if (response.StatusCode == HttpStatusCode.OK && response.Content != null)
            {
                var jsonResponse = JObject.Parse(response.Content);
                var results = jsonResponse["data"]?["result"] as JArray;
                if (results != null)
                {
                    var lineCount = 0;
                    foreach (var result in results)
                    {
                        var values = result["values"] as JArray;
                        if (values != null)
                        {
                            foreach (var value in values)
                            {
                                var logLine = value[1]?.ToString();
                                if (logLine != null)
                                {
                                    if (logLine.Length > 300) logLine = logLine[..300] + "...";
                                    output.WriteLine($"[{label}] {logLine}");
                                    lineCount++;
                                }
                            }
                        }
                    }

                    if (lineCount == 0)
                        output.WriteLine($"[{label}] No matching logs found in the last {lookback.TotalMinutes:F0}m");
                }
            }
        }
        catch (Exception ex)
        {
            output.WriteLine($"[{label}] Scrape error: {ex.Message}");
        }
    }

    private async Task ScrapeQueryAsync(string query, string logPrefix, int? limit = null, int truncateLength = 0)
    {
        var start = _lastQueryTime;
        var end = DateTime.UtcNow;
        _lastQueryTime = end;

        var startUnix = ((DateTimeOffset)start).ToUnixTimeMilliseconds() * 1000000;
        var endUnix = ((DateTimeOffset)end).ToUnixTimeMilliseconds() * 1000000;

        var request = new RestRequest("/loki/api/v1/query_range");
        request.AddParameter("query", query);
        request.AddParameter("start", startUnix.ToString());
        request.AddParameter("end", endUnix.ToString());
        if (limit.HasValue)
            request.AddParameter("limit", limit.Value.ToString());

        try
        {
            var response = await LokiClient.ExecuteAsync(request);
            if (response.StatusCode == HttpStatusCode.OK && response.Content != null)
            {
                var jsonResponse = JObject.Parse(response.Content);
                var results = jsonResponse["data"]?["result"] as JArray;
                if (results != null)
                {
                    // Deduplicate: track (component, summary) pairs already printed
                    var seen = new HashSet<string>();

                    foreach (var result in results)
                    {
                        var stream = result["stream"];
                        var component = stream?["component"]?.ToString() ?? "unknown";
                        var values = result["values"] as JArray;
                        if (values == null) continue;

                        foreach (var value in values)
                        {
                            var logLine = value[1]?.ToString();
                            if (logLine == null) continue;

                            var summary = FormatLogLine(logLine, truncateLength);
                            var dedupeKey = $"{component}|{summary}";

                            if (!seen.Add(dedupeKey))
                                continue;

                            output.WriteLine($"[{logPrefix}][{component}] {summary}");
                        }
                    }
                }
            }
            else if (response.StatusCode != 0 && response.StatusCode != HttpStatusCode.OK)
            {
                output.WriteLine($"Warning: Failed to scrape Loki: {response.StatusCode} {response.Content}");
            }
        }
        catch (Exception ex)
        {
            output.WriteLine($"Warning: Exception while scraping Loki: {ex.Message}");
        }
    }

    /// <summary>
    /// Attempts to parse a structured JSON log line and extract a concise summary.
    /// Falls back to truncating the raw line if it's not JSON.
    /// </summary>
    private static string FormatLogLine(string logLine, int truncateLength)
    {
        // Try to parse as JSON and extract key fields
        if (logLine.TrimStart().StartsWith("{"))
        {
            try
            {
                var obj = JObject.Parse(logLine);

                var level = obj["level"]?.ToString() ?? "";
                var message = obj["Message"]?.ToString() ?? obj["message"]?.ToString() ?? "";

                // Extract exception info if present
                var exType = obj["Exception"]?["Type"]?.ToString();
                var exMessage = obj["Exception"]?["Message"]?.ToString()
                    ?? obj["ExceptionDetail"]?["Message"]?.ToString();

                var parts = new List<string>();
                if (!string.IsNullOrEmpty(level))
                    parts.Add(level.ToUpper());
                if (!string.IsNullOrEmpty(message))
                    parts.Add(message.Length > 120 ? message[..120] + "..." : message);
                if (!string.IsNullOrEmpty(exType))
                    parts.Add($"{exType}: {(exMessage?.Length > 100 ? exMessage[..100] + "..." : exMessage)}");

                if (parts.Count > 0)
                    return string.Join(" | ", parts);
            }
            catch
            {
                // Not valid JSON, fall through to raw truncation
            }
        }

        // Non-JSON or parse failure: truncate raw line
        var maxLen = truncateLength > 0 ? truncateLength : 200;
        return logLine.Length > maxLen ? logLine[..maxLen] + "..." : logLine;
    }

    /// <summary>
    /// Queries MeasureEval's recent logs to determine processing activity.
    /// Returns a summary string like "processing Patient-003, Patient-004 (offset 14612, ~150 resources in last 30s)"
    /// or null if no activity is detected.
    /// </summary>
    public async Task<string?> GetMeasureEvalActivitySummaryAsync(TimeSpan lookback)
    {
        var end = DateTime.UtcNow;
        var start = end - lookback;
        var startUnix = ((DateTimeOffset)start).ToUnixTimeMilliseconds() * 1000000;
        var endUnix = ((DateTimeOffset)end).ToUnixTimeMilliseconds() * 1000000;

        // Query for all MeasureEval consuming lines (these are DEBUG level)
        var query = $"{{app=\"link-cloud\", component=\"{Components.MeasureEval}\"}} |= \"Consuming\"";
        var request = new RestRequest("/loki/api/v1/query_range");
        request.AddParameter("query", query);
        request.AddParameter("start", startUnix.ToString());
        request.AddParameter("end", endUnix.ToString());
        request.AddParameter("limit", "200");

        try
        {
            var response = await LokiClient.ExecuteAsync(request);
            if (response.StatusCode != HttpStatusCode.OK || response.Content == null)
                return null;

            var jsonResponse = JObject.Parse(response.Content);
            var results = jsonResponse["data"]?["result"] as JArray;
            if (results == null) return null;

            var patients = new HashSet<string>();
            var resourceCount = 0;
            long maxOffset = 0;

            foreach (var result in results)
            {
                var values = result["values"] as JArray;
                if (values == null) continue;

                foreach (var value in values)
                {
                    var logLine = value[1]?.ToString();
                    if (logLine == null) continue;

                    resourceCount++;

                    // Extract Kafka offset from "ResourceNormalized-1@14612"
                    var atIdx = logLine.IndexOf('@');
                    if (atIdx > 0)
                    {
                        var colonIdx = logLine.IndexOf(':', atIdx);
                        if (colonIdx < 0) colonIdx = logLine.IndexOf(' ', atIdx);
                        if (colonIdx > atIdx)
                        {
                            var offsetStr = logLine.Substring(atIdx + 1, colonIdx - atIdx - 1);
                            if (long.TryParse(offsetStr, out var offset) && offset > maxOffset)
                                maxOffset = offset;
                        }
                    }

                    // Extract patient ID from "RESOURCE=[Type/MegaPatient-003-...]"
                    var resIdx = logLine.IndexOf("RESOURCE=[", StringComparison.Ordinal);
                    if (resIdx >= 0)
                    {
                        var slashIdx = logLine.IndexOf('/', resIdx + 10);
                        var endBracket = logLine.IndexOf(']', resIdx + 10);
                        if (slashIdx > 0 && endBracket > slashIdx)
                        {
                            var resourceId = logLine.Substring(slashIdx + 1, endBracket - slashIdx - 1);
                            // Extract patient prefix: "MegaPatient-003-Observation-01234" -> "MegaPatient-003"
                            var parts = resourceId.Split('-');
                            if (parts.Length >= 2)
                            {
                                patients.Add($"{parts[0]}-{parts[1]}");
                            }
                        }
                    }
                }
            }

            if (resourceCount == 0)
                return null;

            var patientList = patients.Count > 0
                ? string.Join(", ", patients.OrderBy(p => p))
                : "unknown";

            var offsetInfo = maxOffset > 0 ? $", offset {maxOffset}" : "";

            return $"evaluating {patientList} ({resourceCount} resources in last {lookback.TotalSeconds:F0}s{offsetInfo})";
        }
        catch
        {
            return null;
        }
    }
}
