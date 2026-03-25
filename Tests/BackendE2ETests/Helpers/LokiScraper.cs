using System.Net;
using Newtonsoft.Json.Linq;
using RestSharp;
using Xunit.Abstractions;

namespace LantanaGroup.Link.Tests.E2ETests.Helpers;

public class LokiScraper(ITestOutputHelper output)
{
    private static readonly RestClient LokiClient = new(TestConfig.LokiBaseUrl);
    private DateTime _lastQueryTime = DateTime.UtcNow;

    public async Task ScrapeErrorsAsync()
    {
        await ScrapeQueryAsync(
            "{app=\"link-cloud\"} |= \"Error\"",
            "LOKI ERROR");
    }

    /// <summary>
    /// Scrapes Loki for warning/error logs from specific services (measureeval, validation)
    /// that are critical to the pipeline but may not surface in the general error query.
    /// </summary>
    public async Task ScrapeServiceLogsAsync(params string[] serviceNames)
    {
        foreach (var service in serviceNames)
        {
            await ScrapeQueryAsync(
                $"{{app=\"link-cloud\", component=\"{service}\"}} |~ \"(?i)(error|exception|fail|timeout)\"",
                $"LOKI {service.ToUpper()}");
        }
    }

    /// <summary>
    /// One-time scrape of a larger time window for a specific service — useful for
    /// post-test diagnostics to capture the full evaluation/validation lifecycle.
    /// </summary>
    public async Task ScrapeServiceHistoryAsync(string serviceName, TimeSpan lookback, string label)
    {
        var end = DateTime.UtcNow;
        var start = end - lookback;

        var startUnix = ((DateTimeOffset)start).ToUnixTimeMilliseconds() * 1000000;
        var endUnix = ((DateTimeOffset)end).ToUnixTimeMilliseconds() * 1000000;

        var query = $"{{app=\"link-cloud\", component=\"{serviceName}\"}} |~ \"(?i)(error|warn|exception|fail|timeout|duration|evaluated|validated)\"";
        var request = new RestRequest("/loki/api/v1/query_range");
        request.AddParameter("query", query);
        request.AddParameter("start", startUnix.ToString());
        request.AddParameter("end", endUnix.ToString());
        request.AddParameter("limit", "50");

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

    private async Task ScrapeQueryAsync(string query, string logPrefix)
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

        try
        {
            var response = await LokiClient.ExecuteAsync(request);
            if (response.StatusCode == HttpStatusCode.OK && response.Content != null)
            {
                var jsonResponse = JObject.Parse(response.Content);
                var results = jsonResponse["data"]?["result"] as JArray;
                if (results != null)
                {
                    foreach (var result in results)
                    {
                        var stream = result["stream"];
                        var component = stream?["component"]?.ToString() ?? "unknown";
                        var values = result["values"] as JArray;
                        if (values != null)
                        {
                            foreach (var value in values)
                            {
                                var logLine = value[1]?.ToString();
                                output.WriteLine($"[{logPrefix}][{component}] {logLine}");
                            }
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
}
