using System.IO.Compression;
using LantanaGroup.Link.Sdk.ApiClient;
using Hl7.Fhir.Model;
using LantanaGroup.Link.Automation.Configuration;
using LantanaGroup.Link.Automation.Helpers;
using LantanaGroup.Link.Shared.Application.Models.Tenant;
using LantanaGroup.Link.Shared.Application.SerDes;
using LantanaGroup.Link.Sdk.Clients;
using Task = System.Threading.Tasks.Task;

namespace LantanaGroup.Link.Automation.Services;

public class ReportApiHelper
{
    private readonly IReportServiceClient _reportClient;
    private readonly IFacilityServiceClient _facilityClient;
    private readonly ISubmissionServiceClient _submissionClient;
    private readonly IAutomationOutput _output;
    private readonly AutomationConfig _automationConfig;

    public ReportApiHelper(IReportServiceClient reportClient, IFacilityServiceClient facilityClient, ISubmissionServiceClient submissionClient, IAutomationOutput output, AutomationConfig config)
    {
        _reportClient = reportClient;
        _facilityClient = facilityClient;
        _submissionClient = submissionClient;
        _output = output;
        _automationConfig = config;
    }

    public async Task<string> GenerateReportAsync(string facilityId, string measureId, TestScenarioConfig config)
    {
        _output.WriteLine("Generating report...");
        var body = new AdHocReportRequest
        {
            BypassSubmission = false,
            StartDate = DateTime.Parse(config.StartDate),
            EndDate = DateTime.Parse(config.EndDate),
            ReportTypes = [measureId],
            PatientIds = config.PatientIds
        };

        var payload = await _facilityClient.GenerateAdhocReportAsync(facilityId, body);

        AutomationInvariant.Require(payload?.ReportId != null && payload.ReportId != Guid.Empty,
            "Expected response to include reportId but received empty payload.");

        return payload!.ReportId.ToString();
    }

    public async Task<bool> CheckSubmissionStatusAsync(string reportId, TestScenarioConfig config, BackgroundDiagnosticsMonitor? diagnostics = null)
    {
        var pollingIntervalSeconds = config.PollingIntervalSeconds;
        var maxRetryCount = config.MaxRetryCount;

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

            string currentStatus;
            var schedule = await _reportClient.GetScheduleAsync(reportId);
            if (schedule == null)
            {
                currentStatus = "not found";
            }
            else
            {
                currentStatus = schedule.Status.ToString() ?? "unknown";

                if (string.Equals(currentStatus, "Submitted", StringComparison.OrdinalIgnoreCase))
                {
                    _output.WriteLine($"Report submitted (after {retry * pollingIntervalSeconds}s).");
                    return true;
                }
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

    public async Task<Dictionary<string, object>> DownloadReportAsync(string facilityId, string reportId, TestScenarioConfig config, bool external = true)
    {
        _output.WriteLine($"Downloading report {reportId}...");

        var (bytes, contentType) = await _submissionClient.DownloadSubmissionAsync(facilityId, reportId, external);

        AutomationInvariant.Require(contentType?.Contains("application/zip") == true,
            $"Expected Content-Type to be application/zip but received {contentType}");

        var responseDictionary = new Dictionary<string, object>();

        if (!string.IsNullOrEmpty(_automationConfig.DownloadPath) && bytes != null)
        {
            if (!Directory.Exists(_automationConfig.DownloadPath))
                Directory.CreateDirectory(_automationConfig.DownloadPath);

            var downloadPath = Path.Combine(_automationConfig.DownloadPath, config.DownloadFileName);
            await File.WriteAllBytesAsync(downloadPath, bytes);
            _output.WriteLine($"Report downloaded to {downloadPath}");
        }

        using var zipStream = new MemoryStream(bytes ?? []);
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
