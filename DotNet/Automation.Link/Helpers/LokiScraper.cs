using LantanaGroup.Link.Automation.Link.Configuration;
using Newtonsoft.Json.Linq;
using RestSharp;
using System.Net;

namespace LantanaGroup.Link.Automation.Link.Helpers;

public class LokiScraper
{
    private readonly IAutomationOutput _output;
    private readonly RestClient _lokiClient;
    private DateTime _lastQueryTime = DateTime.UtcNow;

    public LokiScraper(IAutomationOutput output, AutomationConfig config)
    {
        _output = output;
        _lokiClient = new RestClient(config.LokiBaseUrl);
    }

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

    public async Task ScrapeErrorsAsync(string? facilityId = null, string? reportId = null)
    {
        // Only filter by facilityId if provided. Requiring both facilityId AND
        // reportId as literal substrings in the log line was too restrictive —
        // most error/exception log lines do not contain the report GUID.
        var correlationFilter = !string.IsNullOrWhiteSpace(facilityId)
            ? $" |= \"{facilityId}\""
            : "";
        await ScrapeQueryAsync(
            $"{{app=\"link-cloud\"}} |~ \"(?i)(error|exception)\" !~ \"(?i)({HarmlessPatterns})\"{correlationFilter}",
            "LOKI ERROR");
    }

    /// <summary>
    /// Scans ALL services for errors/exceptions in the given time window and prints
    /// a concise summary grouped by service. Services with no issues are listed on
    /// one line. Services with issues get their most recent error lines shown.
    /// </summary>
    public async Task ScrapeAllServicesErrorSummaryAsync(TimeSpan lookback, string? facilityId = null, string? reportId = null)
    {
        var end = DateTime.UtcNow;
        var start = end - lookback;
        var startUnix = ((DateTimeOffset)start).ToUnixTimeMilliseconds() * 1000000;
        var endUnix = ((DateTimeOffset)end).ToUnixTimeMilliseconds() * 1000000;

        _output.WriteLine($"[DIAG] === Error summary across all services (last {lookback.TotalMinutes:F0}m) ===");

        var cleanServices = new List<string>();

        foreach (var component in AllServices)
        {
            try
            {
                var correlationFilter = !string.IsNullOrWhiteSpace(facilityId)
                    ? $" |= \"{facilityId}\""
                    : "";
                var query = $"{{app=\"link-cloud\", component=\"{component}\"}} |~ \"(?i)(error|exception|fail|timeout|disconnect)\" !~ \"(?i)({HarmlessPatterns})\"{correlationFilter}";
                var request = new RestRequest("/loki/api/v1/query_range");
                request.AddParameter("query", query);
                request.AddParameter("start", startUnix.ToString());
                request.AddParameter("end", endUnix.ToString());
                request.AddParameter("limit", "5");

                var response = await _lokiClient.ExecuteAsync(request);
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
                                    lines.Add(FormatLogLine(logLine, 0));
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
                    _output.WriteLine($"[DIAG]   {component}: {lines.Count} issue(s)");
                    foreach (var line in lines)
                    {
                        var dIdx = line.IndexOf("|||", StringComparison.Ordinal);
                        var summaryPart = dIdx >= 0 ? line[..dIdx].TrimEnd() : line;

                        _output.WriteLine($"[LOKI ERROR][{component}] {summaryPart}");

                        if (dIdx >= 0)
                        {
                            var detailPart = line[(dIdx + 3)..].TrimStart();
                            _output.WriteLine($"[LOKI ERROR DETAIL][{component}] {detailPart}");
                        }
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
            _output.WriteLine($"[DIAG]   Clean: {string.Join(", ", cleanServices)}");
        }

        _output.WriteLine("[DIAG] === End error summary ===");
    }

    public async Task<List<string>> QueryServiceLogsAsync(
        string componentName,
        string includePattern,
        TimeSpan lookback,
        string? facilityId = null,
        string? structuredFieldName = null,
        string? structuredFieldValue = null,
        int limit = 2000)
    {
        var end = DateTime.UtcNow;
        var start = end - lookback;

        var startUnix = ((DateTimeOffset)start).ToUnixTimeMilliseconds() * 1000000;
        var endUnix = ((DateTimeOffset)end).ToUnixTimeMilliseconds() * 1000000;

        var correlationFilter = string.Empty;
        if (!string.IsNullOrWhiteSpace(facilityId))
        {
            var escapedFacilityId = facilityId.Replace("\\", "\\\\").Replace("\"", "\\\"");
            correlationFilter = $" |= \"{escapedFacilityId}\"";
        }

        var structuredFilter = string.Empty;
        if (!string.IsNullOrWhiteSpace(structuredFieldName) && !string.IsNullOrWhiteSpace(structuredFieldValue))
        {
            var escaped = structuredFieldValue.Replace("\\", "\\\\").Replace("\"", "\\\"");
            structuredFilter = $" | json | {structuredFieldName}=\"{escaped}\"";
        }

        var query = $"{{app=\"link-cloud\", component=\"{componentName}\"}} |= \"{includePattern}\"{correlationFilter}{structuredFilter}";

        var request = new RestRequest("/loki/api/v1/query_range");
        request.AddParameter("query", query);
        request.AddParameter("start", startUnix.ToString());
        request.AddParameter("end", endUnix.ToString());
        request.AddParameter("limit", Math.Max(1, limit).ToString());

        var lines = new List<string>();
        try
        {
            var response = await _lokiClient.ExecuteAsync(request);
            if (response.StatusCode != HttpStatusCode.OK || response.Content == null)
                return lines;

            var jsonResponse = JObject.Parse(response.Content);
            var results = jsonResponse["data"]?["result"] as JArray;
            if (results == null)
                return lines;

            foreach (var result in results)
            {
                var values = result["values"] as JArray;
                if (values == null) continue;

                foreach (var value in values)
                {
                    var logLine = value[1]?.ToString();
                    if (!string.IsNullOrWhiteSpace(logLine))
                        lines.Add(logLine);
                }
            }
        }
        catch
        {
            return lines;
        }

        return lines;
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
            var response = await _lokiClient.ExecuteAsync(request);
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
                                    _output.WriteLine($"[{label}] {logLine}");
                                    lineCount++;
                                }
                            }
                        }
                    }

                    if (lineCount == 0)
                        _output.WriteLine($"[{label}] No matching logs found in the last {lookback.TotalMinutes:F0}m");
                }
            }
        }
        catch (Exception ex)
        {
            _output.WriteLine($"[{label}] Scrape error: {ex.Message}");
        }
    }

    private async Task ScrapeQueryAsync(string query, string logPrefix, int? limit = null, int truncateLength = 0)
    {
        // Use a small overlap to compensate for Loki ingestion lag.
        var overlapBuffer = TimeSpan.FromSeconds(5);
        var start = _lastQueryTime - overlapBuffer;
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
            var response = await _lokiClient.ExecuteAsync(request);
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

                            var formatted = FormatLogLine(logLine, truncateLength);

                            // Dedupe on the summary portion only (before |||)
                            var delimIdx = formatted.IndexOf("|||", StringComparison.Ordinal);
                            var summaryForDedupe = delimIdx >= 0 ? formatted[..delimIdx] : formatted;
                            var dedupeKey = $"{component}|{summaryForDedupe}";

                            if (!seen.Add(dedupeKey))
                                continue;

                            // Write only the summary to the visible log output.
                            var summaryForLog = delimIdx >= 0 ? formatted[..delimIdx].TrimEnd() : formatted;
                            _output.WriteLine($"[{logPrefix}][{component}] {summaryForLog}");

                            // If full detail exists, write a hidden detail line that
                            // PipelineSummarySnapshotBuilder can parse for the modal.
                            if (delimIdx >= 0)
                            {
                                var detail = formatted[(delimIdx + 3)..].TrimStart();
                                _output.WriteLine($"[{logPrefix} DETAIL][{component}] {detail}");
                            }
                        }
                    }
                }
            }
            else if (response.StatusCode != 0 && response.StatusCode != HttpStatusCode.OK)
            {
                _output.WriteLine($"Warning: Failed to scrape Loki: {response.StatusCode} {response.Content}");
            }
        }
        catch (Exception ex)
        {
            _output.WriteLine($"Warning: Exception while scraping Loki: {ex.Message}");
        }
    }

    /// <summary>
    /// Attempts to parse a structured JSON log line and extract both a concise
    /// summary and the full detail (including stack traces). The two parts are
    /// separated by <c>|||</c> so downstream consumers can display the summary
    /// as a header and the detail on expand.
    /// Falls back to the raw line if it's not JSON.
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
                var exType = obj["Exception"]?["Type"]?.ToString()
                    ?? obj["ExceptionDetail"]?["Type"]?.ToString();
                var exMessage = obj["Exception"]?["Message"]?.ToString()
                    ?? obj["ExceptionDetail"]?["Message"]?.ToString();
                var exStackTrace = obj["Exception"]?["StackTrace"]?.ToString()
                    ?? obj["ExceptionDetail"]?["StackTrace"]?.ToString()
                    ?? obj["Exception"]?["StackTraceString"]?.ToString();
                var innerException = obj["Exception"]?["InnerException"]?.ToString()
                    ?? obj["ExceptionDetail"]?["InnerException"]?.ToString();

                // Build short summary for log display and dedupe
                var summaryParts = new List<string>();
                if (!string.IsNullOrEmpty(level))
                    summaryParts.Add(level.ToUpper());
                if (!string.IsNullOrEmpty(message))
                    summaryParts.Add(message.Length > 200 ? message[..200] + "..." : message);
                if (!string.IsNullOrEmpty(exType))
                    summaryParts.Add($"{exType}: {(exMessage?.Length > 150 ? exMessage[..150] + "..." : exMessage)}");

                var summary = summaryParts.Count > 0 ? string.Join(" | ", summaryParts) : "";

                // Build full detail for expand view
                var detailParts = new List<string>();
                if (!string.IsNullOrEmpty(message))
                    detailParts.Add($"Message: {message}");
                if (!string.IsNullOrEmpty(exType))
                    detailParts.Add($"Exception: {exType}");
                if (!string.IsNullOrEmpty(exMessage))
                    detailParts.Add($"Detail: {exMessage}");
                if (!string.IsNullOrEmpty(exStackTrace))
                    detailParts.Add($"Stack Trace:\n{exStackTrace}");
                if (!string.IsNullOrEmpty(innerException))
                    detailParts.Add($"Inner Exception:\n{innerException}");

                var detail = detailParts.Count > 0 ? string.Join("\n\n", detailParts) : "";

                if (!string.IsNullOrEmpty(summary))
                {
                    return string.IsNullOrEmpty(detail) || detail == summary
                        ? summary
                        : $"{summary}|||{detail}";
                }
            }
            catch
            {
                // Not valid JSON, fall through to raw truncation
            }
        }

        // Non-JSON or parse failure: truncate raw line
        var maxLen = truncateLength > 0 ? truncateLength : 500;
        return logLine.Length > maxLen ? logLine[..maxLen] + "..." : logLine;
    }

    /// <summary>
    /// Queries MeasureEval's recent logs to determine processing activity.
    /// Returns a summary string or null if no activity is detected.
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
            var response = await _lokiClient.ExecuteAsync(request);
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

    public async Task<List<string>> GetServiceExceptionLinesAsync(string componentName, TimeSpan lookback, int limit = 20, string? facilityId = null, string? reportId = null)
    {
        var end = DateTime.UtcNow;
        var start = end - lookback;
        var startUnix = ((DateTimeOffset)start).ToUnixTimeMilliseconds() * 1000000;
        var endUnix = ((DateTimeOffset)end).ToUnixTimeMilliseconds() * 1000000;

        var correlationFilter = !string.IsNullOrWhiteSpace(facilityId)
            ? $" |= \"{facilityId}\""
            : "";
        var query = $"{{app=\"link-cloud\", component=\"{componentName}\"}} |~ \"(?i)(exception|fatal|unhandled|stack\\s*trace|critical)\" !~ \"(?i)({HarmlessPatterns}|Unknown message ID)\"{correlationFilter}";

        var request = new RestRequest("/loki/api/v1/query_range");
        request.AddParameter("query", query);
        request.AddParameter("start", startUnix.ToString());
        request.AddParameter("end", endUnix.ToString());
        request.AddParameter("limit", limit.ToString());

        var lines = new List<string>();

        try
        {
            var response = await _lokiClient.ExecuteAsync(request);
            if (response.StatusCode != HttpStatusCode.OK || response.Content == null)
                return lines;

            var jsonResponse = JObject.Parse(response.Content);
            var results = jsonResponse["data"]?["result"] as JArray;
            if (results == null)
                return lines;

            foreach (var result in results)
            {
                var values = result["values"] as JArray;
                if (values == null) continue;

                foreach (var value in values)
                {
                    var logLine = value[1]?.ToString();
                    if (string.IsNullOrWhiteSpace(logLine)) continue;

                    var formatted = FormatLogLine(logLine, 220);
                    if (!lines.Contains(formatted))
                        lines.Add(formatted);
                }
            }
        }
        catch
        {
            // Intentionally silent: callers treat empty result as no detected exceptions.
        }

        return lines;
    }

    public async Task<string?> GetValidationActivitySummaryAsync(TimeSpan lookback)
    {
        var end = DateTime.UtcNow;
        var start = end - lookback;
        var startUnix = ((DateTimeOffset)start).ToUnixTimeMilliseconds() * 1000000;
        var endUnix = ((DateTimeOffset)end).ToUnixTimeMilliseconds() * 1000000;

        // Focus on ReadyForValidation processing lines emitted by the consumer.
        var query = $"{{app=\"link-cloud\", component=\"{Components.Validation}\"}} |= \"Processing\" |= \"patient\" |= \"report\" !~ \"(?i)({HarmlessPatterns})\"";
        var request = new RestRequest("/loki/api/v1/query_range");
        request.AddParameter("query", query);
        request.AddParameter("start", startUnix.ToString());
        request.AddParameter("end", endUnix.ToString());
        request.AddParameter("limit", "200");

        try
        {
            var response = await _lokiClient.ExecuteAsync(request);
            if (response.StatusCode != HttpStatusCode.OK || response.Content == null)
                return null;

            var jsonResponse = JObject.Parse(response.Content);
            var results = jsonResponse["data"]?["result"] as JArray;
            if (results == null)
                return null;

            var logCount = 0;
            var patientIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var result in results)
            {
                var values = result["values"] as JArray;
                if (values == null) continue;

                foreach (var value in values)
                {
                    var logLine = value[1]?.ToString();
                    if (string.IsNullOrWhiteSpace(logLine)) continue;
                    logCount++;

                    var patientMarker = "patient";
                    var idx = logLine.IndexOf(patientMarker, StringComparison.OrdinalIgnoreCase);
                    if (idx >= 0)
                    {
                        var tail = logLine[idx..];
                        var tokens = tail.Split([' ', ',', ';', '"', '\'', ']', '[', ')', '('], StringSplitOptions.RemoveEmptyEntries);
                        var token = tokens.FirstOrDefault(t => t.Contains('-') || t.Any(char.IsDigit));
                        if (!string.IsNullOrWhiteSpace(token) && token.Length <= 40)
                            patientIds.Add(token.Trim());
                    }
                }
            }

            if (logCount == 0)
                return null;

            if (patientIds.Count > 0)
            {
                var sample = string.Join(", ", patientIds.Take(3));
                var suffix = patientIds.Count > 3 ? $" (+{patientIds.Count - 3} more)" : "";
                return $"processing validation activity for {sample}{suffix} ({logCount} log lines/{lookback.TotalSeconds:F0}s)";
            }

            return $"processing validation activity ({logCount} log lines/{lookback.TotalSeconds:F0}s)";
        }
        catch
        {
            return null;
        }
    }

    public async Task<string?> GetNormalizationActivitySummaryAsync(TimeSpan lookback)
    {
        var end = DateTime.UtcNow;
        var start = end - lookback;
        var startUnix = ((DateTimeOffset)start).ToUnixTimeMilliseconds() * 1000000;
        var endUnix = ((DateTimeOffset)end).ToUnixTimeMilliseconds() * 1000000;

        var query = $"{{app=\"link-cloud\", component=\"{Components.Normalization}\"}} |~ \"(?i)(ResourceNormalized|Producing|Normalization Operation|Acquisition Complete)\" !~ \"(?i)({HarmlessPatterns})\"";
        var request = new RestRequest("/loki/api/v1/query_range");
        request.AddParameter("query", query);
        request.AddParameter("start", startUnix.ToString());
        request.AddParameter("end", endUnix.ToString());
        request.AddParameter("limit", "200");

        try
        {
            var response = await _lokiClient.ExecuteAsync(request);
            if (response.StatusCode != HttpStatusCode.OK || response.Content == null)
                return null;

            var jsonResponse = JObject.Parse(response.Content);
            var results = jsonResponse["data"]?["result"] as JArray;
            if (results == null)
                return null;

            var logCount = 0;
            var resourceTypes = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var failedOps = 0;

            foreach (var result in results)
            {
                var values = result["values"] as JArray;
                if (values == null) continue;

                foreach (var value in values)
                {
                    var logLine = value[1]?.ToString();
                    if (string.IsNullOrWhiteSpace(logLine)) continue;
                    logCount++;

                    if (logLine.Contains("Normalization Operation Failed", StringComparison.OrdinalIgnoreCase))
                        failedOps++;

                    // Extract FHIR resource type from "FhirResourceType: Observation" or similar patterns
                    var typeMarker = "FhirResourceType:";
                    var typeIdx = logLine.IndexOf(typeMarker, StringComparison.OrdinalIgnoreCase);
                    if (typeIdx >= 0)
                    {
                        var tail = logLine[(typeIdx + typeMarker.Length)..].TrimStart();
                        var endIdx = tail.IndexOfAny([',', ' ', ';', '"', ')']);
                        var resourceType = endIdx > 0 ? tail[..endIdx] : tail;
                        if (!string.IsNullOrWhiteSpace(resourceType) && resourceType.Length <= 40)
                        {
                            resourceTypes.TryGetValue(resourceType, out var count);
                            resourceTypes[resourceType] = count + 1;
                        }
                    }
                }
            }

            if (logCount == 0)
                return null;

            var parts = new List<string>();
            if (resourceTypes.Count > 0)
            {
                var top3 = resourceTypes.OrderByDescending(kv => kv.Value).Take(3).Select(kv => $"{kv.Key}={kv.Value}");
                parts.Add(string.Join(", ", top3));
            }

            parts.Add($"{logCount} log lines/{lookback.TotalSeconds:F0}s");

            if (failedOps > 0)
                parts.Add($"{failedOps} failed ops");

            return $"normalizing resources ({string.Join(", ", parts)})";
        }
        catch
        {
            return null;
        }
    }
}
