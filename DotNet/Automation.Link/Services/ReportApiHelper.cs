using System.IO.Compression;
using LantanaGroup.Link.Sdk.ApiClient;
using Hl7.Fhir.Model;
using LantanaGroup.Link.Automation.Link.Configuration;
using LantanaGroup.Link.Automation.Link.Helpers;
using LantanaGroup.Link.Shared.Application.Models.Tenant;
using LantanaGroup.Link.Shared.Application.SerDes;
using LantanaGroup.Link.Sdk.Clients;
using Task = System.Threading.Tasks.Task;

namespace LantanaGroup.Link.Automation.Link.Services;

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
        return await GenerateReportAsync(facilityId, [measureId], config);
    }

    public async Task<string> GenerateReportAsync(string facilityId, List<string> measureIds, TestScenarioConfig config)
    {
        _output.WriteLine($"Generating report with {measureIds.Count} measure(s): [{string.Join(", ", measureIds)}]...");
        var body = new AdHocReportRequest
        {
            BypassSubmission = false,
            StartDate = DateTime.Parse(config.StartDate),
            EndDate = DateTime.Parse(config.EndDate),
            ReportTypes = measureIds,
            PatientIds = config.PatientIds
        };

        var payload = await _facilityClient.GenerateAdhocReportAsync(facilityId, body);

        AutomationInvariant.Require(payload?.ReportId != null && payload.ReportId != Guid.Empty,
            "Expected response to include reportId but received empty payload.");

        return payload!.ReportId.ToString();
    }

    public async Task<bool> CheckSubmissionStatusAsync(string reportId, TestScenarioConfig config, BackgroundDiagnosticsMonitor? diagnostics = null)
    {
        var pollingInterval = TimeSpan.FromSeconds(Math.Max(1, config.PollingIntervalSeconds));
        var hardTimeout = GetEffectiveSubmissionTimeout(config);
        var timeoutLabel = hardTimeout == TimeSpan.MaxValue ? "no timeout" : $"hard timeout={hardTimeout.TotalSeconds:F0}s";

        // -------------------------------------------------------------------
        //  Phase 1: Wait for MeasureReportsGenerated milestone
        //  The pipeline must complete DA → Normalization → MeasureEval before
        //  submission can happen. Do not burn submission poll retries before
        //  this milestone is reached.
        // -------------------------------------------------------------------
        if (diagnostics != null)
        {
            var milestoneToAwait = "MeasureReportsGenerated";
            _output.WriteLine($"Waiting for pipeline milestone '{milestoneToAwait}' before polling submission (reportId={reportId}, {timeoutLabel})...");

            var milestoneReached = false;
            var milestonePhaseStart = DateTime.UtcNow;
            while (hardTimeout == TimeSpan.MaxValue || DateTime.UtcNow - milestonePhaseStart < hardTimeout)
            {
                if (diagnostics.HasCriticalFailure)
                {
                    _output.WriteLine("[EARLY EXIT] Background diagnostics detected a critical failure before submission polling.");
                    _output.WriteLine("Review the [DIAG] entries above for details on the root cause.");
                    return false;
                }

                if (diagnostics.HasReachedMilestone(milestoneToAwait))
                {
                    milestoneReached = true;
                    var elapsed = (DateTime.UtcNow - milestonePhaseStart).TotalSeconds;
                    _output.WriteLine($"Milestone '{milestoneToAwait}' reached after {elapsed:F0}s.");
                    break;
                }

                await Task.Delay(pollingInterval);
            }

            if (diagnostics.HasCriticalFailure)
            {
                _output.WriteLine("[EARLY EXIT] Background diagnostics detected a critical failure before submission polling.");
                _output.WriteLine("Review the [DIAG] entries above for details on the root cause.");
                return false;
            }

            if (!milestoneReached)
            {
                _output.WriteLine($"Milestone '{milestoneToAwait}' was not reached before timeout.");
                return false;
            }
        }

        // -------------------------------------------------------------------
        //  Phase 2: Poll the Report API for Submitted status
        //  Prefer state-based completion: if diagnostics already reports
        //  SubmissionCompleted, return success immediately.
        //  Otherwise poll schedule status until submitted / critical failure /
        //  hard timeout.
        // -------------------------------------------------------------------
        if (diagnostics?.HasReachedMilestone("SubmissionCompleted") == true)
        {
            _output.WriteLine("Submission milestone already reached. Skipping schedule polling.");
            return true;
        }

        _output.WriteLine($"Polling for report submission (reportId={reportId}, {timeoutLabel})...");

        string? lastStatus = null;
        var submissionPhaseStart = DateTime.UtcNow;
        while (hardTimeout == TimeSpan.MaxValue || DateTime.UtcNow - submissionPhaseStart < hardTimeout)
        {
            if (diagnostics?.HasCriticalFailure == true)
            {
                _output.WriteLine("[EARLY EXIT] Background diagnostics detected a critical failure — aborting poll loop.");
                _output.WriteLine("Review the [DIAG] entries above for details on the root cause.");
                return false;
            }

            if (diagnostics?.HasReachedMilestone("SubmissionCompleted") == true)
            {
                var elapsedMilestone = (DateTime.UtcNow - submissionPhaseStart).TotalSeconds;
                _output.WriteLine($"Submission milestone reached after {elapsedMilestone:F0}s of status polling.");
                return true;
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
                    var elapsed = (DateTime.UtcNow - submissionPhaseStart).TotalSeconds;
                    _output.WriteLine($"Report submitted (after {elapsed:F0}s of status polling).");
                    return true;
                }
            }

            if (currentStatus != lastStatus)
            {
                _output.WriteLine($"[Poll] Report status: {currentStatus}");
                lastStatus = currentStatus;
            }

            await Task.Delay(pollingInterval);
        }

        _output.WriteLine($"Report {reportId} was not submitted before timeout.");
        return false;
    }

    public static TimeSpan GetEffectiveSubmissionTimeout(TestScenarioConfig config)
    {
        if (config.MaxPollingDurationMinutes <= 0)
            return TimeSpan.MaxValue;

        // Adaptive lower bound to avoid premature timeout on high-volume tests.
        // Example: 1000 patients => at least ~20 minutes.
        var adaptiveFloor = TimeSpan.FromSeconds(Math.Max(300, config.PatientIds.Count * 1.2));
        return config.MaxPollingDuration > adaptiveFloor
            ? config.MaxPollingDuration
            : adaptiveFloor;
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
