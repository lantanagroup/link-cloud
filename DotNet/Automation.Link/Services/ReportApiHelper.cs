using Hl7.Fhir.Model;
using Confluent.Kafka;
using LantanaGroup.Link.Automation.Link.Configuration;
using LantanaGroup.Link.Automation.Link.Helpers;
using LantanaGroup.Link.Sdk.Clients;
using LantanaGroup.Link.Shared.Application.Factories;
using LantanaGroup.Link.Shared.Application.Enums;
using LantanaGroup.Link.Shared.Application.Models.DataAcq;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Models.Integration.Report;
using LantanaGroup.Link.Shared.Application.Models.Kafka;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using LantanaGroup.Link.Shared.Application.Models.Tenant;
using LantanaGroup.Link.Shared.Application.SerDes;
using System.Net;
using System.IO.Compression;
using Task = System.Threading.Tasks.Task;

namespace LantanaGroup.Link.Automation.Link.Services;

public class ReportApiHelper
{
    public sealed record ReportTerminalState(
        List<string> EntryPatientIds,
        List<string> SubmittedPatientIds);

    private readonly IReportServiceClient _reportClient;
    private readonly IFacilityServiceClient _facilityClient;
    private readonly ISubmissionServiceClient _submissionClient;
    private readonly IAdminBffIntegrationClient _adminBffClient;
    private readonly IAutomationOutput _output;
    private readonly AutomationConfig _automationConfig;
    private readonly KafkaConnection _kafkaConnection;

    public ReportApiHelper(
        IReportServiceClient reportClient,
        IFacilityServiceClient facilityClient,
        ISubmissionServiceClient submissionClient,
        IAdminBffIntegrationClient adminBffClient,
        IAutomationOutput output,
        AutomationConfig config,
        KafkaConnection kafkaConnection)
    {
        _reportClient = reportClient;
        _facilityClient = facilityClient;
        _submissionClient = submissionClient;
        _adminBffClient = adminBffClient;
        _output = output;
        _automationConfig = config;
        _kafkaConnection = kafkaConnection;
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

        var response = await _facilityClient.GenerateAdhocReportAsync(facilityId, body);

        AutomationInvariant.Require(response.IsSuccessStatusCode && response.Body?.ReportId != null && response.Body.ReportId != Guid.Empty,
            "Expected response to include reportId but received empty payload.");

        return response.Body!.ReportId.ToString();
    }

    /// <summary>
    /// Triggers a regeneration of an existing submitted report.
    /// Returns the new report ID created by the regeneration.
    /// </summary>
    public async Task<string> RegenerateReportAsync(string facilityId, string existingReportId)
    {
        _output.WriteLine($"Regenerating report (facilityId={facilityId}, existingReportId={existingReportId})...");

        var request = new RegenerateReportRequest
        {
            ReportId = existingReportId,
            BypassSubmission = false
        };

        var response = await _facilityClient.RegenerateReportAsync(facilityId, request);

        AutomationInvariant.Require(response.IsSuccessStatusCode && response.Body?.ReportId != null && response.Body.ReportId != Guid.Empty,
            "Expected regenerate response to include a new reportId but received empty payload.");

        var newReportId = response.Body!.ReportId.ToString();
        _output.WriteLine($"Regeneration initiated. New report ID: {newReportId}");
        return newReportId;
    }

    public async Task<string> StartScheduledReportAsync(
        string facilityId,
        IReadOnlyList<string> reportTypes,
        DateTimeOffset startDateUtc,
        TimeSpan reportDuration,
        Frequency frequency,
        string? reportTrackingId = null)
    {
        if (string.IsNullOrWhiteSpace(facilityId))
            throw new ArgumentException("facilityId is required.", nameof(facilityId));
        if (reportTypes == null || reportTypes.Count == 0)
            throw new ArgumentException("At least one report type is required.", nameof(reportTypes));

        var trackingId = string.IsNullOrWhiteSpace(reportTrackingId)
            ? Guid.NewGuid().ToString()
            : reportTrackingId.Trim();

        if (!Guid.TryParse(trackingId, out var trackingGuid))
            throw new ArgumentException("reportTrackingId must be a valid Guid.", nameof(reportTrackingId));

        if (reportDuration <= TimeSpan.Zero)
            throw new ArgumentException("reportDuration must be greater than zero.", nameof(reportDuration));

        var endDateUtc = startDateUtc.UtcDateTime.Add(reportDuration);
        if (endDateUtc <= startDateUtc.UtcDateTime)
            throw new ArgumentException("Scheduled report end date must be later than start date.", nameof(reportDuration));

        var producerConfig = new ProducerConfig
        {
            Acks = Acks.All,
            EnableIdempotence = true
        };

        var producerFactory = new KafkaProducerFactory<string, ReportScheduledValue>(_kafkaConnection);
        using var producer = producerFactory.CreateProducer(producerConfig, useOpenTelemetry: false);

        var value = new ReportScheduledValue
        {
            ReportTypes = reportTypes.ToList(),
            Frequency = frequency,
            StartDate = startDateUtc,
            EndDate = new DateTimeOffset(DateTime.SpecifyKind(endDateUtc, DateTimeKind.Utc)),
            ReportTrackingId = trackingGuid
        };

        await producer.ProduceAsync(
            nameof(KafkaTopic.ReportScheduled),
            new Message<string, ReportScheduledValue>
            {
                Key = facilityId,
                Value = value,
                Headers = new Headers
                {
                    { "X-Correlation-Id", System.Text.Encoding.ASCII.GetBytes(trackingId) }
                }
            });

        producer.Flush(TimeSpan.FromSeconds(5));

        _output.WriteLine(
            $"Scheduled report event produced via Kafka: reportTrackingId={trackingId}, " +
            $"start={startDateUtc:O}, end={endDateUtc:O}, durationMinutes={reportDuration.TotalMinutes:F0}");
        return trackingId;
    }

    /// <summary>
    /// Waits until the scheduled report identified by <paramref name="reportTrackingId"/> has been
    /// persisted by the Report service. The ReportScheduled integration event is processed
    /// asynchronously, so the schedule record does not exist the instant
    /// <see cref="StartScheduledReportAsync"/> returns. The tracking id becomes the schedule's Id,
    /// so a by-id lookup returns 404 until the record is committed. Publishing census snapshots
    /// before this barrier lets Census emit PatientEvents that reach Report's PatientEventListener
    /// before the schedule exists, throwing "No Scheduled Reports found for facilityId ...".
    /// </summary>
    public async Task<ReportScheduleApiModel> WaitForScheduledReportAsync(
        string reportTrackingId,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default)
    {
        if (!Guid.TryParse(reportTrackingId, out _))
            throw new ArgumentException("reportTrackingId must be a valid Guid.", nameof(reportTrackingId));

        var effectiveTimeout = timeout ?? TimeSpan.FromMinutes(2);
        var pollingInterval = TimeSpan.FromSeconds(2);
        var start = DateTime.UtcNow;

        _output.WriteLine($"Waiting for scheduled report {reportTrackingId} to be persisted before publishing census snapshots...");

        while (DateTime.UtcNow - start < effectiveTimeout)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var response = await _reportClient.GetScheduleAsync(reportTrackingId, cancellationToken);
            if (response.IsSuccessStatusCode && response.Body != null)
            {
                var elapsed = (DateTime.UtcNow - start).TotalSeconds;
                _output.WriteLine($"Scheduled report {reportTrackingId} is persisted (after {elapsed:F0}s).");
                return response.Body;
            }

            await Task.Delay(pollingInterval, cancellationToken);
        }

        throw new TimeoutException(
            $"Scheduled report {reportTrackingId} was not persisted within {effectiveTimeout.TotalSeconds:F0}s. " +
            "Census snapshots were not published to avoid a 'No Scheduled Reports found' race.");
    }

    public async Task PublishPatientListAcquiredAsync(
        string facilityId,
        string reportTrackingId,
        IReadOnlyList<string>? admitPatientIds,
        IReadOnlyList<string>? dischargePatientIds)
    {
        if (!Guid.TryParse(reportTrackingId, out var trackingGuid))
            throw new ArgumentException("reportTrackingId must be a valid Guid.", nameof(reportTrackingId));

        var admits = admitPatientIds?.Where(id => !string.IsNullOrWhiteSpace(id)).Distinct().ToList() ?? [];
        var discharges = dischargePatientIds?.Where(id => !string.IsNullOrWhiteSpace(id)).Distinct().ToList() ?? [];
        if (admits.Count == 0 && discharges.Count == 0)
            return;

        // Census validator requires exactly 6 lists: Admit/Discharge x 3 timeframes,
        // each unique and present even when empty.
        var patientLists = new List<PatientListItem>
        {
            new()
            {
                ListType = ListType.Admit,
                TimeFrame = TimeFrame.LessThan24Hours,
                PatientIds = new List<string>()
            },
            new()
            {
                ListType = ListType.Admit,
                TimeFrame = TimeFrame.Between24To48Hours,
                PatientIds = new List<string>()
            },
            new()
            {
                ListType = ListType.Admit,
                TimeFrame = TimeFrame.MoreThan48Hours,
                PatientIds = admits
            },
            new()
            {
                ListType = ListType.Discharge,
                TimeFrame = TimeFrame.LessThan24Hours,
                PatientIds = discharges
            },
            new()
            {
                ListType = ListType.Discharge,
                TimeFrame = TimeFrame.Between24To48Hours,
                PatientIds = new List<string>()
            },
            new()
            {
                ListType = ListType.Discharge,
                TimeFrame = TimeFrame.MoreThan48Hours,
                PatientIds = new List<string>()
            }
        };

        var response = await _adminBffClient.CreatePatientListAcquiredAsync(
            facilityId,
            patientLists,
            trackingGuid);
        AutomationInvariant.Require(response.IsSuccessStatusCode,
            $"Failed to produce PatientListAcquired event for report '{reportTrackingId}'. HTTP {response.StatusCode}: {response.RawBody}");

        _output.WriteLine($"PatientListAcquired event produced: admits={admits.Count}, discharges={discharges.Count}, reportTrackingId={reportTrackingId}");
    }

    public async Task<bool> CheckSubmissionStatusAsync(string reportId, TestScenarioConfig config, BackgroundDiagnosticsMonitor? diagnostics = null)
    {
        var pollingInterval = TimeSpan.FromSeconds(Math.Max(1, config.PollingIntervalSeconds));
        var hardTimeout = GetEffectiveSubmissionTimeout(config);
        var timeoutLabel = hardTimeout == TimeSpan.MaxValue ? "no timeout" : $"hard timeout={hardTimeout.TotalSeconds:F0}s";

        // -------------------------------------------------------------------
        //  Phase 1: Wait for ReportEntriesCreated milestone
        //  Start schedule polling as soon as report entries exist, instead of
        //  waiting for downstream milestones that can be noisy during
        //  regenerate/no-data-acquisition scenarios.
        // -------------------------------------------------------------------
        if (diagnostics != null)
        {
            var milestoneToAwait = "ReportEntriesCreated";
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

                // Entryless scheduled runs are valid when oracle prediction says no
                // patients should participate. In that case the report can transition
                // to Submitted without ever emitting ReportEntriesCreated.
                var scheduleProbe = await _reportClient.GetScheduleAsync(reportId);
                if (scheduleProbe.IsSuccessStatusCode
                    && scheduleProbe.Body?.Status == ScheduleStatus.Submitted)
                {
                    milestoneReached = true;
                    var elapsed = (DateTime.UtcNow - milestonePhaseStart).TotalSeconds;
                    _output.WriteLine(
                        $"Milestone '{milestoneToAwait}' was not observed, but report is already Submitted after {elapsed:F0}s. Continuing.");
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
            var response = await _reportClient.GetScheduleAsync(reportId);
            if (!response.IsSuccessStatusCode)
            {
                if (response.StatusCode == (int)HttpStatusCode.NotFound)
                {
                    currentStatus = "not found";
                }
                else
                {
                    AutomationInvariant.Require(false,
                        $"GetSchedule failed during submission polling with status {response.StatusCode}." +
                        (!string.IsNullOrWhiteSpace(response.RawBody) ? $" Body: {response.RawBody}" : string.Empty));
                    return false;
                }
            }
            else if (response.Body == null)
            {
                currentStatus = "not found";
            }
            else
            {
                currentStatus = response.Body.Status.ToString() ?? "unknown";

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

    public async Task<ReportTerminalState> WaitForTerminalReportStateAsync(
        string reportId,
        TimeSpan? timeout = null,
        bool allowEntrylessTerminal = false,
        CancellationToken cancellationToken = default)
    {
        var effectiveTimeout = timeout ?? TimeSpan.FromMinutes(2);
        var pollingInterval = TimeSpan.FromSeconds(2);
        var start = DateTime.UtcNow;
        string? lastState = null;

        _output.WriteLine($"Waiting for report {reportId} to reach a terminal state before artifact validation...");

        while (DateTime.UtcNow - start < effectiveTimeout)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var scheduleResponse = await _reportClient.GetScheduleAsync(reportId, cancellationToken);
            if (!scheduleResponse.IsSuccessStatusCode || scheduleResponse.Body == null)
            {
                await Task.Delay(pollingInterval, cancellationToken);
                continue;
            }

            var entriesResponse = await _reportClient.GetEntriesByScheduleAsync(reportId, cancellationToken);
            if (!entriesResponse.IsSuccessStatusCode || entriesResponse.Body == null)
            {
                if (allowEntrylessTerminal
                    && scheduleResponse.Body.Status == ScheduleStatus.Submitted)
                {
                    _output.WriteLine(
                        $"Report {reportId} reached Submitted with no report-entry payload available; treating as terminal entryless report.");
                    return new ReportTerminalState([], []);
                }

                await Task.Delay(pollingInterval, cancellationToken);
                continue;
            }

            var entries = entriesResponse.Body;
            var hasIncompleteEntries = entries.Any(e => !IsTerminalEntry(e));

            var state = $"status={scheduleResponse.Body.Status}, entries={entries.Count}, incomplete={(hasIncompleteEntries ? "yes" : "no")}";
            if (!string.Equals(state, lastState, StringComparison.Ordinal))
            {
                _output.WriteLine($"[Poll] Waiting for terminal report state: {state}");
                lastState = state;
            }

            if (scheduleResponse.Body.Status == ScheduleStatus.Submitted && !hasIncompleteEntries)
            {
                var entryPatientIds = entries
                    .Select(e => e.PatientId)
                    .Where(id => !string.IsNullOrWhiteSpace(id))
                    .Distinct(StringComparer.Ordinal)
                    .ToList();

                var submittedPatientIds = entries
                    .Where(e => e.SubmissionStatus == SubmissionStatus.Submitted)
                    .Select(e => e.PatientId)
                    .Where(id => !string.IsNullOrWhiteSpace(id))
                    .Distinct(StringComparer.Ordinal)
                    .ToList();

                _output.WriteLine(
                    $"Report {reportId} terminal state reached: entries={entryPatientIds.Count}, submittedPatients={submittedPatientIds.Count}.");

                return new ReportTerminalState(entryPatientIds, submittedPatientIds);
            }

            await Task.Delay(pollingInterval, cancellationToken);
        }

        throw new TimeoutException(
            $"Report {reportId} did not reach terminal state within {effectiveTimeout.TotalSeconds:F0}s.");
    }

    private static bool IsTerminalEntry(ReportEntryApiModel entry)
    {
        var reportingTerminal = entry.ReportingStatus is ReportingStatus.NotReportable
            or ReportingStatus.PassedValidation
            or ReportingStatus.FailedValidation;

        var submissionTerminal = entry.SubmissionStatus is SubmissionStatus.Submitted
            or SubmissionStatus.NotEligable;

        return reportingTerminal && submissionTerminal;
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

        var response = await _submissionClient.DownloadSubmissionAsync(facilityId, reportId, external);

        AutomationInvariant.Require(response.IsSuccessStatusCode && response.Body != null,
            $"Download failed with status {response.StatusCode}");

        var bytes = response.Body!;

        var isZipPayload = bytes.Length >= 4
            && bytes[0] == 0x50
            && bytes[1] == 0x4B
            && bytes[2] == 0x03
            && bytes[3] == 0x04;

        AutomationInvariant.Require(isZipPayload,
            $"Download payload was not a ZIP (status {response.StatusCode}).");

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
