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
        var start = _lastQueryTime;
        var end = DateTime.UtcNow;
        _lastQueryTime = end;

        var startUnix = ((DateTimeOffset)start).ToUnixTimeMilliseconds() * 1000000;
        var endUnix = ((DateTimeOffset)end).ToUnixTimeMilliseconds() * 1000000;

        var query = "{app=\"link-cloud\"} |= \"Error\"";
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
                                output.WriteLine($"[LOKI ERROR][{component}] {logLine}");
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
