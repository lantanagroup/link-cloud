using System.IO.Compression;
using System.Net;
using Hl7.Fhir.Model;
using LantanaGroup.Link.Automation.Configuration;
using LantanaGroup.Link.Automation.Helpers;
using LantanaGroup.Link.Shared.Application.SerDes;
using Newtonsoft.Json.Linq;
using RestSharp;
using Xunit;
using Xunit.Abstractions;
using Task = System.Threading.Tasks.Task;

namespace LantanaGroup.Link.Automation.Services;

public class ReportApiClient
{
    private readonly RestClient _client;
    private readonly ITestOutputHelper _output;
    private readonly LokiScraper _lokiScraper;
    private readonly TestScenarioConfig _config;
    private readonly AutomationConfig _automationConfig;

    public ReportApiClient(RestClient client, ITestOutputHelper output, LokiScraper lokiScraper, AutomationConfig automationConfig, TestScenarioConfig config)
    {
        _client = client;
        _output = output;
        _lokiScraper = lokiScraper;
        _config = config;
        _automationConfig = automationConfig;
    }

    public async Task<string> GenerateReportAsync(string facilityId, string measureId)
    {
        _output.WriteLine("Generating report...");
        var request = new RestRequest($"facility/{facilityId}/AdhocReport", Method.Post);
        var body = new
        {
            BypassSubmission = false,
            StartDate = _config.StartDate,
            EndDate = _config.EndDate,
            ReportTypes = new[] { measureId },
            PatientIds = _config.PatientIds
        };
        request.AddJsonBody(body);

        var response = await _client.ExecuteAsync(request);
        Assert.True(response.StatusCode == HttpStatusCode.OK,
            $"Generate Report - Expected HTTP 200 OK but received {response.StatusCode}: {response.Content}");

        Assert.True(response.ContentType != null,
            $"Expected Content-Type to be set but received {response.ContentType}");
        Assert.True(response.ContentType.Contains("application/json"),
            $"Expected Content-Type to be application/json but received {response.ContentType}");
        Assert.False(string.IsNullOrWhiteSpace(response.Content),
            $"Expected Content to be set but received {response.Content}");

        var generateReportResponse = JObject.Parse(response.Content);

        Assert.True(generateReportResponse.ContainsKey("reportId"),
            $"Expected response to include ReportId but received {generateReportResponse}");

        var reportId = generateReportResponse["reportId"]?.ToString();
        Assert.False(string.IsNullOrWhiteSpace(reportId),
            $"Expected ReportId to be set but received {reportId}");

        return reportId!;
    }

    public async Task<bool> CheckSubmissionStatusAsync(string reportId, BackgroundDiagnosticsMonitor? diagnostics = null)
    {
        var pollingIntervalSeconds = _config.PollingIntervalSeconds;
        var maxRetryCount = _config.MaxRetryCount;

        _output.WriteLine($"Polling for report submission (reportId={reportId}, max {maxRetryCount * pollingIntervalSeconds}s)...");

        string? lastStatus = null;

        for (var retry = 0; retry < maxRetryCount; retry++)
        {
            if (diagnostics?.HasCriticalFailure == true)
            {
                _output.WriteLine("[EARLY EXIT] Background diagnostics detected a critical failure — aborting poll loop.");
                _output.WriteLine("Review the [DIAG] entries above for details on the root cause.");
                return false;
            }

            var request = new RestRequest($"/schedules/{reportId}", Method.Get);
            var response = await _client.ExecuteAsync(request);

            string currentStatus;

            if (response.StatusCode == HttpStatusCode.OK &&
                response.ContentType != null &&
                response.ContentType.Contains("application/json") &&
                response.Content != null)
            {
                var jsonResponse = JObject.Parse(response.Content);
                currentStatus = jsonResponse["status"]?.ToString() ?? "unknown";

                if (currentStatus == "Submitted")
                {
                    _output.WriteLine($"Report submitted (after {retry * pollingIntervalSeconds}s).");
                    return true;
                }
            }
            else
            {
                currentStatus = $"HTTP {(int)response.StatusCode}";
            }

            if (currentStatus != lastStatus)
            {
                _output.WriteLine($"[Poll] Report status: {currentStatus}");
                lastStatus = currentStatus;
            }

            await Task.Delay(pollingIntervalSeconds * 1000);
        }

        _output.WriteLine($"Report {reportId} was not submitted after {maxRetryCount * pollingIntervalSeconds}s.");
        return false;
    }

    public async Task<Dictionary<string, object>> DownloadReportAsync(string facilityId, string reportId)
    {
        _output.WriteLine($"Downloading report {reportId}...");
        var request = new RestRequest($"submission/{facilityId}/{reportId}?external=true", Method.Get);
        var response = await _client.ExecuteAsync(request);
        Assert.True(response.StatusCode == HttpStatusCode.OK,
            $"Download Report - Expected HTTP 200 OK but received {response.StatusCode}: {response.Content}");

        Assert.True(response.ContentType?.Contains("application/zip"),
            $"Expected Content-Type to be application/zip but received {response.ContentType}");

        var responseDictionary = new Dictionary<string, object>();

        if (!string.IsNullOrEmpty(_automationConfig.DownloadPath) && response.RawBytes != null)
        {
            if (!Directory.Exists(_automationConfig.DownloadPath))
                Directory.CreateDirectory(_automationConfig.DownloadPath);

            var downloadPath = Path.Combine(_automationConfig.DownloadPath, _config.DownloadFileName);
            await File.WriteAllBytesAsync(downloadPath, response.RawBytes);
            _output.WriteLine($"Report downloaded to {downloadPath}");
        }

        using var zipStream = new MemoryStream(response.RawBytes ?? []);
        using var archive = new ZipArchive(zipStream, ZipArchiveMode.Read);
        var jsonParser = LinkFhirSerializerOptions.FhirJsonParserPermissive;

        foreach (var entry in archive.Entries)
        {
            if (entry.Length == 0)
                continue;

            using var entryStream = entry.Open();
            using var reader = new StreamReader(entryStream);
            var fileContent = reader.ReadToEnd();

            if (entry.FullName.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            {
                var resource = jsonParser.Parse<Resource>(fileContent);
                responseDictionary[entry.FullName] = resource;
            }
            else
            {
                responseDictionary[entry.FullName] = fileContent;
            }
        }

        return responseDictionary;
    }
}
