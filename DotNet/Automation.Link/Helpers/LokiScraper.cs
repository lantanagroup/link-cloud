using LantanaGroup.Link.Automation.Link.Configuration;
using Newtonsoft.Json.Linq;
using System.Net;

namespace LantanaGroup.Link.Automation.Link.Helpers;

public class LokiScraper
{
    private readonly IAutomationOutput _output;
    private readonly HttpClient _lokiClient;
    private readonly string _lokiAppLabel;
    private DateTime _lastQueryTime = DateTime.UtcNow;

    public LokiScraper(HttpClient lokiClient, IAutomationOutput output, AutomationConfig config)
    {
        if (lokiClient.BaseAddress == null)
        {
            if (!Uri.TryCreate(config.LokiBaseUrl, UriKind.Absolute, out var lokiBaseUri))
                throw new InvalidOperationException("LokiBaseUrl must be an absolute URI.");

            lokiClient.BaseAddress = lokiBaseUri;
        }

        _output = output;
        _lokiClient = lokiClient;
        _lokiAppLabel = string.IsNullOrWhiteSpace(config.LokiAppLabel)
            ? throw new InvalidOperationException("LokiAppLabel is required.")
            : config.LokiAppLabel.Trim();
    }

    public static class Components
    {
        public const string MeasureEval = "MeasureEval";
        public const string Validation = "Validation";
        public const string Normalization = "Normalization";
        public const string Report = "Report";
        public const string DataAcquisition = "DataAcquisition";
        public const string DataAcquisitionWorker = "DataAcquisition.AcquisitionWorker";
        /// <summary>
        /// Development appsettings label for the acquisition worker. Docker/prod uses
        /// <see cref="DataAcquisitionWorker"/>.
        /// </summary>
        public const string DataAcquisitionWorkerDev = "DataAcquisitionWorker";
        public const string Submission = "Submission";
        public const string QueryDispatch = "QueryDispatch";
        public const string Tenant = "Tenant";
        public const string Census = "Census";
    }

    private static readonly string[] AllServices =
    [
        Components.DataAcquisition,
        Components.DataAcquisitionWorker,
        Components.DataAcquisitionWorkerDev,
        Components.QueryDispatch,
        Components.Normalization,
        Components.MeasureEval,
        Components.Validation,
        Components.Report,
        Components.Submission,
        Components.Tenant,
        Components.Census
    ];

    private const string HarmlessPatterns = "healthcheck|health-check|actuator|AppInfoParser|InstanceAlreadyExistsException|UQ_LocationMapping_Facility_Location|Cannot insert duplicate key row in object 'dbo.OrganizationLocationMapping'";

    public async Task ScrapeErrorsAsync(string? facilityId = null, string? reportId = null)
    {
        // Only filter by facilityId if provided. Requiring both facilityId AND
        // reportId as literal substrings in the log line was too restrictive —
        // most error/exception log lines do not contain the report GUID.
        var correlationFilter = !string.IsNullOrWhiteSpace(facilityId)
            ? $" |= \"{facilityId}\""
            : "";
        await ScrapeQueryAsync(
            $"{{app=\"{_lokiAppLabel}\"}} |~ \"(?i)(error|exception)\" !~ \"(?i)({HarmlessPatterns})\"{correlationFilter}",
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
                var query = $"{{app=\"{_lokiAppLabel}\", component=\"{component}\"}} |~ \"(?i)(error|exception|fail|timeout|disconnect)\" !~ \"(?i)({HarmlessPatterns})\"{correlationFilter}";
                var (statusCode, content) = await ExecuteQueryRangeAsync(query, startUnix, endUnix, limit: 5);
                if (statusCode != HttpStatusCode.OK || content == null)
                {
                    cleanServices.Add($"{component}(?)");
                    continue;
                }

                var jsonResponse = JObject.Parse(content);
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
                                    lines.Add(LokiLogLineParser.FormatLogLine(logLine, 0));
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
        IReadOnlyCollection<string>? additionalContainsFilters = null,
        int limit = 2000,
        int maxPages = 10)
    {
        var end = DateTime.UtcNow;
        var start = end - lookback;

        var startUnix = ((DateTimeOffset)start).ToUnixTimeMilliseconds() * 1000000;
        var endUnix = ((DateTimeOffset)end).ToUnixTimeMilliseconds() * 1000000;

        var escapedIncludePattern = includePattern.Replace("\\", "\\\\").Replace("\"", "\\\"");

        var containsFilter = string.Empty;
        if (additionalContainsFilters != null)
        {
            foreach (var filter in additionalContainsFilters)
            {
                if (string.IsNullOrWhiteSpace(filter))
                    continue;

                var escapedFilter = filter.Replace("\\", "\\\\").Replace("\"", "\\\"");
                containsFilter += $" |= \"{escapedFilter}\"";
            }
        }

        var query = $"{{app=\"{_lokiAppLabel}\", component=\"{componentName}\"}} |= \"{escapedIncludePattern}\"{containsFilter}";

        var lines = new List<string>();
        var pageSize = Math.Max(1, limit);
        var pageCount = 0;
        var currentEndUnix = endUnix;

        try
        {
            var seenEntries = new HashSet<string>(StringComparer.Ordinal);
            var overlapAttemptsByTimestamp = new Dictionary<long, int>();

            while (pageCount < Math.Max(1, maxPages))
            {
                var (statusCode, content) = await ExecuteQueryRangeAsync(query, startUnix, currentEndUnix, pageSize, "backward");
                if (statusCode != HttpStatusCode.OK || content == null)
                    return lines;

                var jsonResponse = JObject.Parse(content);
                var results = jsonResponse["data"]?["result"] as JArray;
                if (results == null)
                    return lines;

                var pageEntries = new List<(long? Timestamp, string Line)>();
                var malformedTimestampCount = 0;

                foreach (var result in results)
                {
                    var values = result["values"] as JArray;
                    if (values == null) continue;

                    foreach (var value in values)
                    {
                        var timestampToken = value[0]?.ToString();
                        var logLine = value[1]?.ToString();
                        if (string.IsNullOrWhiteSpace(logLine))
                            continue;

                        if (!long.TryParse(timestampToken, out var timestamp))
                        {
                            malformedTimestampCount++;
                            pageEntries.Add((null, logLine));
                            continue;
                        }

                        pageEntries.Add((timestamp, logLine));
                    }
                }

                if (malformedTimestampCount > 0)
                {
                    _output.WriteLine($"[DIAG][Loki] Encountered {malformedTimestampCount} malformed timestamp token(s) in query_range response; preserving lines and continuing pagination using valid timestamps only.");
                }

                if (pageEntries.Count == 0)
                    break;

                foreach (var entry in pageEntries)
                {
                    var dedupeKey = $"{entry.Timestamp?.ToString() ?? "(null)"}|{entry.Line}";
                    if (seenEntries.Add(dedupeKey))
                        lines.Add(entry.Line);
                }

                pageCount++;

                if (pageEntries.Count < pageSize)
                    break;

                var validTimestamps = pageEntries
                    .Where(e => e.Timestamp.HasValue)
                    .Select(e => e.Timestamp!.Value)
                    .ToList();

                if (validTimestamps.Count == 0)
                    break;

                var oldestTimestamp = validTimestamps.Min();
                if (oldestTimestamp <= startUnix)
                    break;

                overlapAttemptsByTimestamp.TryGetValue(oldestTimestamp, out var overlapAttempts);
                if (overlapAttempts == 0)
                {
                    // Overlap once at the oldest timestamp so entries sharing the boundary
                    // timestamp have a chance to appear on the next page.
                    overlapAttemptsByTimestamp[oldestTimestamp] = 1;
                    currentEndUnix = oldestTimestamp;
                    continue;
                }

                var nextEndUnix = Math.Max(startUnix, oldestTimestamp - 1);
                if (nextEndUnix >= currentEndUnix)
                    break;

                currentEndUnix = nextEndUnix;
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

        var query = $"{{app=\"{_lokiAppLabel}\", component=\"{componentName}\"}} |~ \"(?i)(error|warn|exception|fail|timeout|duration|evaluated|validated|submitted|generated|measure.?report)\" !~ \"(?i)({HarmlessPatterns})\"";
        try
        {
            var (statusCode, content) = await ExecuteQueryRangeAsync(query, startUnix, endUnix, limit: 100);
            if (statusCode == HttpStatusCode.OK && content != null)
            {
                var jsonResponse = JObject.Parse(content);
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

        try
        {
            var (statusCode, content) = await ExecuteQueryRangeAsync(query, startUnix, endUnix, limit);
            if (statusCode == HttpStatusCode.OK && content != null)
            {
                var jsonResponse = JObject.Parse(content);
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

                            var formatted = LokiLogLineParser.FormatLogLine(logLine, truncateLength);

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
            else if (statusCode != HttpStatusCode.OK)
            {
                _output.WriteLine($"Warning: Failed to scrape Loki: {statusCode} {content}");
            }
        }
        catch (Exception ex)
        {
            _output.WriteLine($"Warning: Exception while scraping Loki: {ex.Message}");
        }
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

        // MeasureEval is Java/logback at INFO. Per-resource "Consuming RESOURCE=[...]" lines no longer exist.
        // Current INFO markers: Bulk upsert complete, Normalized-to-MeasureReportGenerated, Cache empty,
        // Compiling measure. DEBUG still emits MESSAGE RECEIVED / EVALUATING MEASURES when enabled.
        var query = $"{{app=\"{_lokiAppLabel}\", component=\"{Components.MeasureEval}\"}} |~ \"(?i)(Bulk upsert complete|Normalized-to-MeasureReportGenerated|Cache empty for correlationId|Compiling measure|Measure evaluation failed|MESSAGE RECEIVED|EVALUATING MEASURES|Consuming)\"";
        try
        {
            var (statusCode, content) = await ExecuteQueryRangeAsync(query, startUnix, endUnix, limit: 200);
            if (statusCode != HttpStatusCode.OK || content == null)
                return null;

            var jsonResponse = JObject.Parse(content);
            var results = jsonResponse["data"]?["result"] as JArray;
            if (results == null) return null;

            var patients = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var logCount = 0;
            long maxOffset = 0;

            foreach (var result in results)
            {
                var values = result["values"] as JArray;
                if (values == null) continue;

                foreach (var value in values)
                {
                    var logLine = value[1]?.ToString();
                    if (logLine == null) continue;

                    logCount++;
                    TryExtractKafkaOffset(logLine, ref maxOffset);

                    foreach (var patient in ExtractLabeledValues(logLine, "PATIENT=", "patient=", "patient "))
                        patients.Add(patient);
                }
            }

            if (logCount == 0)
                return null;

            var patientList = patients.Count > 0
                ? string.Join(", ", patients.OrderBy(p => p).Take(5))
                : "unknown";
            var extra = patients.Count > 5 ? $" (+{patients.Count - 5} more)" : "";

            var offsetInfo = maxOffset > 0 ? $", offset {maxOffset}" : "";

            return $"evaluating {patientList}{extra} ({logCount} log lines in last {lookback.TotalSeconds:F0}s{offsetInfo})";
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
        var query = $"{{app=\"{_lokiAppLabel}\", component=\"{componentName}\"}} |~ \"(?i)(exception|fatal|unhandled|stack\\s*trace|critical|error|Failed to process event)\" !~ \"(?i)({HarmlessPatterns}|Unknown message ID)\"{correlationFilter}";

        var lines = new List<string>();

        try
        {
            var (statusCode, content) = await ExecuteQueryRangeAsync(query, startUnix, endUnix, limit);
            if (statusCode != HttpStatusCode.OK || content == null)
                return lines;

            var jsonResponse = JObject.Parse(content);
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

                    var formatted = LokiLogLineParser.FormatLogLine(logLine, 220);
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

        // ReadyForValidation "Processing ... patient ... report" is DEBUG (not shipped at Loki INFO).
        // Current INFO/ERROR markers: Pre-qual skip, retrieve failures, ValidationComplete produce failures.
        var query = $"{{app=\"{_lokiAppLabel}\", component=\"{Components.Validation}\"}} |~ \"(?i)(Processing .+patient|Validation completed|Failed to send ValidationComplete|Unexpected error while retrieving|Pre-qual OperationOutcome)\" !~ \"(?i)({HarmlessPatterns})\"";
        try
        {
            var (statusCode, content) = await ExecuteQueryRangeAsync(query, startUnix, endUnix, limit: 200);
            if (statusCode != HttpStatusCode.OK || content == null)
                return null;

            var jsonResponse = JObject.Parse(content);
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

        var query = $"{{app=\"{_lokiAppLabel}\", component=\"{Components.Normalization}\"}} |~ \"(?i)(\\[NormalizationExecutionSummary\\]|Normalization Operation Failed|Failed to produce ResourceNormalized|ResourceNormalized|Producing|Acquisition Complete)\" !~ \"(?i)({HarmlessPatterns})\"";
        try
        {
            var (statusCode, content) = await ExecuteQueryRangeAsync(query, startUnix, endUnix, limit: 200);
            if (statusCode != HttpStatusCode.OK || content == null)
                return null;

            var jsonResponse = JObject.Parse(content);
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

                    if (TryExtractResourceType(logLine, out var resourceType))
                    {
                        resourceTypes.TryGetValue(resourceType, out var count);
                        resourceTypes[resourceType] = count + 1;
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

    private static void TryExtractKafkaOffset(string logLine, ref long maxOffset)
    {
        var atIdx = logLine.IndexOf('@');
        if (atIdx <= 0)
            return;

        var colonIdx = logLine.IndexOf(':', atIdx);
        if (colonIdx < 0) colonIdx = logLine.IndexOf(' ', atIdx);
        if (colonIdx <= atIdx)
            return;

        var offsetStr = logLine.Substring(atIdx + 1, colonIdx - atIdx - 1);
        if (long.TryParse(offsetStr, out var offset) && offset > maxOffset)
            maxOffset = offset;
    }

    private static IEnumerable<string> ExtractLabeledValues(string logLine, params string[] markers)
    {
        foreach (var marker in markers)
        {
            var idx = 0;
            while (idx >= 0)
            {
                idx = logLine.IndexOf(marker, idx, StringComparison.OrdinalIgnoreCase);
                if (idx < 0)
                    break;

                var start = idx + marker.Length;
                if (start < logLine.Length && logLine[start] == '[')
                    start++;

                var end = start;
                while (end < logLine.Length && logLine[end] is not (',' or ';' or ']' or ')' or '"' or ' ' or '}'))
                    end++;

                var value = logLine[start..end].Trim();
                if (!string.IsNullOrWhiteSpace(value) && value.Length <= 80 && value != "unknown")
                    yield return value;

                idx = start;
            }
        }
    }

    private static bool TryExtractResourceType(string logLine, out string resourceType)
    {
        foreach (var marker in new[] { "ResourceType=", "FhirResourceType=", "FhirResourceType:" })
        {
            var typeIdx = logLine.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
            if (typeIdx < 0)
                continue;

            var tail = logLine[(typeIdx + marker.Length)..].TrimStart();
            var endIdx = tail.IndexOfAny([',', ' ', ';', '"', ')', ']']);
            var value = endIdx > 0 ? tail[..endIdx] : tail;
            if (!string.IsNullOrWhiteSpace(value) && value.Length <= 40)
            {
                resourceType = value;
                return true;
            }
        }

        resourceType = "";
        return false;
    }

    private async Task<(HttpStatusCode StatusCode, string? Content)> ExecuteQueryRangeAsync(
        string query,
        long startUnix,
        long endUnix,
        int? limit = null,
        string? direction = null)
    {
        var queryString =
            $"query={Uri.EscapeDataString(query)}&start={startUnix}&end={endUnix}";

        if (limit.HasValue)
            queryString += $"&limit={limit.Value}";

        if (!string.IsNullOrWhiteSpace(direction))
            queryString += $"&direction={Uri.EscapeDataString(direction)}";

        using var response = await _lokiClient.GetAsync($"/loki/api/v1/query_range?{queryString}");
        var content = await response.Content.ReadAsStringAsync();
        return (response.StatusCode, string.IsNullOrWhiteSpace(content) ? null : content);
    }
}
