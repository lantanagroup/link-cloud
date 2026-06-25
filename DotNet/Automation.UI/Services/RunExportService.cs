using LantanaGroup.Link.Automation.Link.Helpers;
using LantanaGroup.Link.Sdk.Clients;
using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Text.Json;

namespace Automation.UI.Services;

/// <summary>
/// Assembles a TestRunDiagnostics-{runId}.zip from data already persisted for the run.
/// Each section is built in isolation: a failure in one becomes a *_ERROR.txt entry
/// rather than aborting the whole export, so an operator always gets at least
/// partial diagnostics back.
/// </summary>
public sealed class RunExportService : IRunExportService
{
    private const string SanitizedInternalError = "An internal error occurred processing this run.";

    private static readonly JsonSerializerOptions PrettyJson = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
    };

    private readonly IAutomationRunManager _runManager;
    private readonly ISnapshotStore _snapshotStore;
    private readonly ISubmissionServiceClient _submissionClient;
    private readonly ILogger<RunExportService> _logger;

    public RunExportService(
        IAutomationRunManager runManager,
        ISnapshotStore snapshotStore,
        ISubmissionServiceClient submissionClient,
        ILogger<RunExportService> logger)
    {
        _runManager = runManager;
        _snapshotStore = snapshotStore;
        _submissionClient = submissionClient;
        _logger = logger;
    }

    public async Task<RunExportPackage?> BuildAsync(Guid runId, CancellationToken cancellationToken = default)
    {
        var run = await _runManager.GetRunAsync(runId, cancellationToken);
        if (run == null)
            return null;

        using var ms = new MemoryStream();
        using (var archive = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            await SafeWriteAsync(archive, "RunDetails.txt",
                () => Task.FromResult(BuildRunDetails(run)));

            await SafeWriteAsync(archive, "RunManifest.txt",
                async () => await BuildRunManifestAsync(runId));

            await SafeWriteAsync(archive, "LokiLogs.txt",
                () => Task.FromResult(FilterLogs(run.Logs, IsLokiLine,
                    "Loki error/diagnostic lines captured by the run.")));

            await SafeWriteAsync(archive, "Kafka.txt",
                () => Task.FromResult(FilterLogs(run.Logs, IsKafkaLine,
                    "Kafka error/retry topic entries captured by the run.")));

            // Service-scoped sections sourced from the per-run pipeline snapshot,
            // which is itself rebuilt from the persisted domain snapshots so this
            // path makes no live cross-service calls.
            var pipelineSnapshot = await SafeGetPipelineSnapshotAsync(runId, cancellationToken);

            await SafeWriteAsync(archive, "Report.txt",
                async () => await BuildReportSectionAsync(runId, pipelineSnapshot, cancellationToken));

            await SafeWriteAsync(archive, "DataAcquisition.txt",
                async () => await BuildDataAcquisitionSectionAsync(runId, pipelineSnapshot, cancellationToken));

            await SafeWriteAsync(archive, "Validation.txt",
                async () => await BuildValidationSectionAsync(runId, pipelineSnapshot, cancellationToken));

            await SafeWriteAsync(archive, "MeasureEval.txt",
                async () => await BuildMeasureEvalSectionAsync(runId, pipelineSnapshot, cancellationToken));

            await SafeWriteAsync(archive, "Normalization.txt",
                () => Task.FromResult(BuildNormalizationSection(run.Logs, pipelineSnapshot)));

            await SafeWriteAsync(archive, "abs/_README.txt",
                async () => await WriteAbsFilesAsync(archive, run, cancellationToken));
        }

        return new RunExportPackage(
            FileName: $"TestRunDiagnostics-{runId:D}.zip",
            Content: ms.ToArray());
    }

    // --- Section builders ---

    private static string BuildRunDetails(AutomationRunSummary run)
    {
        var sb = new StringBuilder();
        WriteHeader(sb, "RUN DETAILS");
        WriteKvp(sb, "Run ID", run.RunId.ToString());
        WriteKvp(sb, "Run Name", run.RunName);
        WriteKvp(sb, "Scenario", run.Scenario.ToString());
        WriteKvp(sb, "Selected Measure", run.SelectedMeasure);
        WriteKvp(sb, "Status", run.Status.ToString());
        WriteKvp(sb, "Patient Count", run.PatientCount.ToString(CultureInfo.InvariantCulture));
        WriteKvp(sb, "Resources / Patient", run.ResourcesPerPatient.ToString(CultureInfo.InvariantCulture));
        WriteKvp(sb, "Seed", run.Seed.ToString(CultureInfo.InvariantCulture));
        WriteKvp(sb, "Facility ID", run.FacilityId);
        WriteKvp(sb, "Report ID", run.ReportId);
        WriteKvp(sb, "Created", FormatTime(run.CreatedAt));
        WriteKvp(sb, "Started", FormatTime(run.StartedAt));
        WriteKvp(sb, "Finished", FormatTime(run.FinishedAt));
        WriteKvp(sb, "Pipeline Duration", run.Duration);
        WriteKvp(sb, "Error", NormalizeErrorSummary(run.Error));

        if (!string.IsNullOrWhiteSpace(run.RunConfigurationJson))
        {
            sb.AppendLine();
            WriteHeader(sb, "RUN CONFIGURATION (JSON)");
            sb.AppendLine(PrettyPrintJson(run.RunConfigurationJson));
        }

        sb.AppendLine();
        WriteHeader(sb, $"RUN LOGS ({run.Logs.Count} line(s))");
        foreach (var line in run.Logs)
            sb.AppendLine(line);

        return sb.ToString();
    }

    private static string? NormalizeErrorSummary(string? error) =>
        string.IsNullOrWhiteSpace(error) ? error : SanitizedInternalError;

    private async Task<string> BuildRunManifestAsync(Guid runId)
    {
        var manifest = await _runManager.GetGenerationManifestAsync(runId);
        var abs = await _runManager.GetAbsUploadSnapshotAsync(runId);

        var sb = new StringBuilder();
        WriteHeader(sb, "GENERATION MANIFEST");

        if (manifest == null)
        {
            sb.AppendLine("(no generation manifest persisted for this run)");
        }
        else
        {
            WriteKvp(sb, "Patient Count", manifest.PatientCount.ToString(CultureInfo.InvariantCulture));
            WriteKvp(sb, "Total Resource Count", manifest.TotalResourceCount.ToString(CultureInfo.InvariantCulture));
            WriteKvp(sb, "Selected Measures", string.Join(", ", manifest.SelectedMeasures));
            WriteKvp(sb, "Measure IDs", string.Join(", ", manifest.MeasureIds));
            WriteKvp(sb, "Acquired Resource Types", string.Join(", ", manifest.AcquiredResourceTypes));
            WriteKvp(sb, "Parameter Query Resource Types", string.Join(", ", manifest.ParameterQueryResourceTypes));
            WriteKvp(sb, "CQL Referenced Resource Types", string.Join(", ", manifest.CqlReferencedResourceTypes));

            sb.AppendLine();
            WriteSubHeader(sb, "Generated totals by resource type");
            foreach (var (type, count) in manifest.TotalCountsByType.OrderBy(kv => kv.Key, StringComparer.OrdinalIgnoreCase))
                sb.AppendLine($"  {type,-32} {count,8:N0}");

            sb.AppendLine();
            WriteSubHeader(sb, "Shared infrastructure totals");
            foreach (var (type, count) in manifest.SharedInfrastructureCountsByType.OrderBy(kv => kv.Key, StringComparer.OrdinalIgnoreCase))
                sb.AppendLine($"  {type,-32} {count,8:N0}");

            sb.AppendLine();
            WriteSubHeader(sb, "Expected ABS totals (generated ∩ acquired ∩ CQL-referenced)");
            foreach (var (type, count) in manifest.ExpectedAbsTotalCountsByType.OrderBy(kv => kv.Key, StringComparer.OrdinalIgnoreCase))
                sb.AppendLine($"  {type,-32} {count,8:N0}");
        }

        sb.AppendLine();
        WriteHeader(sb, "ABS UPLOAD SUMMARY");
        if (abs == null)
        {
            sb.AppendLine("(no ABS upload snapshot — run did not complete the report-download phase)");
        }
        else
        {
            WriteKvp(sb, "Total Resource Count", abs.TotalResourceCount.ToString("N0", CultureInfo.InvariantCulture));
            WriteKvp(sb, "Patient Count", abs.PatientIds.Count.ToString(CultureInfo.InvariantCulture));
            WriteKvp(sb, "Manifest Resource Count", abs.ManifestResourceCount.ToString("N0", CultureInfo.InvariantCulture));
            WriteKvp(sb, "Manifest Resource Types", string.Join(", ", abs.ManifestResourceTypes));

            sb.AppendLine();
            WriteSubHeader(sb, "ABS totals by resource type");
            foreach (var (type, count) in abs.TotalCountsByType.OrderBy(kv => kv.Key, StringComparer.OrdinalIgnoreCase))
                sb.AppendLine($"  {type,-32} {count,8:N0}");
        }

        if (manifest != null && abs != null)
        {
            sb.AppendLine();
            WriteHeader(sb, "EXPECTED vs ACTUAL (per resource type)");
            sb.AppendLine($"  {"Type",-32} {"Expected",10} {"Actual",10} {"Delta",10}");
            sb.AppendLine($"  {new string('-', 32)} {new string('-', 10)} {new string('-', 10)} {new string('-', 10)}");

            var allTypes = manifest.ExpectedAbsTotalCountsByType.Keys
                .Concat(abs.TotalCountsByType.Keys)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(t => t, StringComparer.OrdinalIgnoreCase);

            foreach (var type in allTypes)
            {
                manifest.ExpectedAbsTotalCountsByType.TryGetValue(type, out var expected);
                abs.TotalCountsByType.TryGetValue(type, out var actual);
                var delta = actual - expected;
                sb.AppendLine($"  {type,-32} {expected,10:N0} {actual,10:N0} {delta,+10:+#,0;-#,0;0}");
            }
        }

        return sb.ToString();
    }

    private async Task<string> BuildReportSectionAsync(
        Guid runId,
        PipelineSummarySnapshotBuilder.PipelineSummarySnapshot? pipeline,
        CancellationToken ct)
    {
        var sb = new StringBuilder();
        WriteHeader(sb, "REPORT SERVICE");

        if (pipeline?.Report.Schedule != null)
        {
            var s = pipeline.Report.Schedule;
            WriteKvp(sb, "Report Name", s.ReportName);
            WriteKvp(sb, "Frequency", s.Frequency);
            WriteKvp(sb, "Ad-hoc Type", s.AdHocType);
            WriteKvp(sb, "Start", s.StartDate);
            WriteKvp(sb, "End", s.EndDate);
            WriteKvp(sb, "Status", s.ScheduleStatus);
            WriteKvp(sb, "Created", s.ReportCreated);
            WriteKvp(sb, "Submitted", s.SubmittedAt);
            WriteKvp(sb, "Duration", s.Duration);
        }

        if (pipeline?.Report.Milestones is { Count: > 0 } milestones)
        {
            sb.AppendLine();
            WriteSubHeader(sb, "Milestones");
            foreach (var m in milestones)
            {
                var mark = m.Failed ? "[X]" : (m.Completed ? "[√]" : "[ ]");
                sb.AppendLine($"  {mark} {m.Name}");
            }
        }

        if (pipeline?.Report.EntrySubmissionStatuses is { Count: > 0 } statuses)
        {
            sb.AppendLine();
            WriteSubHeader(sb, "Entry submission statuses");
            foreach (var c in statuses)
                sb.AppendLine($"  {c.Status,-30} {c.Count,8:N0}");
        }

        if (!string.IsNullOrWhiteSpace(pipeline?.Report.PopulationSummary))
        {
            sb.AppendLine();
            WriteSubHeader(sb, "Population summary");
            sb.AppendLine(pipeline.Report.PopulationSummary);
        }

        await AppendRawDomainAsync(sb, runId, "schedule",      "Raw report schedule",      ct);
        await AppendRawDomainAsync(sb, runId, "entries",       "Raw report entries",       ct);
        await AppendRawDomainAsync(sb, runId, "populations",   "Raw report populations",   ct);

        return sb.ToString();
    }

    private async Task<string> BuildDataAcquisitionSectionAsync(
        Guid runId,
        PipelineSummarySnapshotBuilder.PipelineSummarySnapshot? pipeline,
        CancellationToken ct)
    {
        var sb = new StringBuilder();
        WriteHeader(sb, "DATA ACQUISITION");

        if (pipeline?.DataAcquisition is { } da)
            AppendServiceStats(sb, "Data Acquisition",
                da.CompletionRatePerSecond, da.ResourceCount, da.AverageResourcesPerSecond,
                da.StatusCounts, funnelCounts: null, da.ResourceTypeCounts, da.ThroughputBuckets, da.Errors);

        await AppendRawDomainAsync(sb, runId, "acquisitionSummary", "Raw acquisition summary", ct);
        return sb.ToString();
    }

    private async Task<string> BuildValidationSectionAsync(
        Guid runId,
        PipelineSummarySnapshotBuilder.PipelineSummarySnapshot? pipeline,
        CancellationToken ct)
    {
        var sb = new StringBuilder();
        WriteHeader(sb, "VALIDATION");

        if (pipeline?.Validation is { } v)
            AppendServiceStats(sb, "Validation",
                v.CompletionRatePerSecond, v.ResourceCount, v.AverageResourcesPerSecond,
                v.StatusCounts, v.FunnelCounts, v.ResourceTypeCounts, v.ThroughputBuckets, v.Errors);

        if (pipeline?.ValidatorResults is { Count: > 0 } validators)
        {
            sb.AppendLine();
            WriteSubHeader(sb, "Test validator results");
            foreach (var r in validators)
                sb.AppendLine($"  {r.Outcome,-8} {r.IssueCount,4:N0} issue(s)  {r.Name}");
        }

        await AppendRawDomainAsync(sb, runId, "validatorResults", "Raw validator results", ct);
        return sb.ToString();
    }

    private async Task<string> BuildMeasureEvalSectionAsync(
        Guid runId,
        PipelineSummarySnapshotBuilder.PipelineSummarySnapshot? pipeline,
        CancellationToken ct)
    {
        var sb = new StringBuilder();
        WriteHeader(sb, "MEASURE EVALUATION");

        if (pipeline?.MeasureEval is { } me)
            AppendServiceStats(sb, "Measure Eval",
                me.CompletionRatePerSecond, me.ResourceCount, me.AverageResourcesPerSecond,
                me.StatusCounts, me.FunnelCounts, me.ResourceTypeCounts, me.ThroughputBuckets, me.Errors);

        await AppendRawDomainAsync(sb, runId, "measureResources", "Raw measure-eval resource counts", ct);
        return sb.ToString();
    }

    private static string BuildNormalizationSection(
        IReadOnlyList<string> logs,
        PipelineSummarySnapshotBuilder.PipelineSummarySnapshot? pipeline)
    {
        var sb = new StringBuilder();
        WriteHeader(sb, "NORMALIZATION");

        if (pipeline?.Normalization is { } n)
            AppendServiceStats(sb, "Normalization",
                n.CompletionRatePerSecond, n.ResourceCount, n.AverageResourcesPerSecond,
                n.StatusCounts, n.FunnelCounts, n.ResourceTypeCounts, n.ThroughputBuckets, n.Errors);

        var normalizationLines = logs
            .Where(l => l.Contains("[Normalization]", StringComparison.OrdinalIgnoreCase)
                        || l.Contains("normalizing", StringComparison.OrdinalIgnoreCase)
                        || l.Contains("ResourceNormalized", StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (normalizationLines.Count > 0)
        {
            sb.AppendLine();
            WriteSubHeader(sb, $"Normalization-tagged log lines ({normalizationLines.Count})");
            foreach (var line in normalizationLines)
                sb.AppendLine(line);
        }

        return sb.ToString();
    }

    /// <summary>
    /// Writes each persisted ABS file as its own zip entry under <c>abs/</c> and
    /// returns the body of the abs/_README.txt index file.
    /// </summary>
    private async Task<string> WriteAbsFilesAsync(ZipArchive archive, AutomationRunSummary run, CancellationToken ct)
    {
        var sb = new StringBuilder();
        WriteHeader(sb, "ABS FILES");

        var runId = run.RunId;
        var locator = (await _snapshotStore.GetDomainAsync<AbsExportLocatorSnapshot>(runId, "absExportLocator", ct))?.Data;

        // Backward compatibility for older runs that persisted raw absFiles directly.
        var snap = await _snapshotStore.GetDomainAsync<Dictionary<string, string>>(runId, "absFiles", ct);
        var files = snap?.Data;

        if (files is { Count: > 0 })
        {
            sb.AppendLine("Source: persisted snapshot domain 'absFiles' (legacy).\n");
            AppendAbsLocatorDetails(sb, locator, run);
            WriteAbsFilesToArchive(archive, files, sb);
            return sb.ToString();
        }

        // Preferred path: re-download internal ABS at export time.
        var downloaded = await TryDownloadInternalAbsAsync(run, ct);
        if (downloaded is { Count: > 0 })
        {
            sb.AppendLine("Source: live download from Submission Service (external=false).\n");
            AppendAbsLocatorDetails(sb, locator, run);
            WriteAbsFilesToArchive(archive, downloaded, sb);
            return sb.ToString();
        }

        // Final fallback: no raw ABS available; include lightweight summary metadata.
        var absUpload = await _runManager.GetAbsUploadSnapshotAsync(runId);
        sb.AppendLine("(raw ABS files unavailable for this export)");
        sb.AppendLine();
        AppendAbsLocatorDetails(sb, locator, run);

        if (absUpload != null)
        {
            sb.AppendLine();
            WriteSubHeader(sb, "Cached lightweight ABS summary");
            sb.AppendLine($"  Patient count           : {absUpload.PatientIds.Count:N0}");
            sb.AppendLine($"  Manifest resource count : {absUpload.ManifestResourceCount:N0}");
            sb.AppendLine($"  Total resource count    : {absUpload.TotalResourceCount:N0}");

            if (absUpload.TotalCountsByType.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("  Resource totals by type:");
                foreach (var (type, count) in absUpload.TotalCountsByType.OrderBy(kv => kv.Key, StringComparer.OrdinalIgnoreCase))
                    sb.AppendLine($"    {type,-28} {count,8:N0}");
            }
        }

        return sb.ToString();
    }

    private async Task<Dictionary<string, string>?> TryDownloadInternalAbsAsync(AutomationRunSummary run, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(run.FacilityId) || string.IsNullOrWhiteSpace(run.ReportId))
            return null;

        try
        {
            var response = await _submissionClient.DownloadSubmissionAsync(run.FacilityId, run.ReportId, external: false, cancellationToken: ct);
            if (!response.IsSuccessStatusCode || response.Body == null || response.Body.Length < 4)
                return null;

            var bytes = response.Body;
            var isZipPayload = bytes[0] == 0x50
                               && bytes[1] == 0x4B
                               && bytes[2] == 0x03
                               && bytes[3] == 0x04;
            if (!isZipPayload)
                return null;

            using var zipStream = new MemoryStream(bytes);
            using var archive = new ZipArchive(zipStream, ZipArchiveMode.Read);

            var files = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var entry in archive.Entries)
            {
                if (entry.Length == 0)
                    continue;

                using var entryStream = entry.Open();
                using var reader = new StreamReader(entryStream);
                files[entry.FullName] = await reader.ReadToEndAsync(ct);
            }

            return files;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "[Export][{RunId}] Failed to live-download internal ABS artifacts for facility={FacilityId} report={ReportId}.",
                run.RunId, run.FacilityId, run.ReportId);
            return null;
        }
    }

    private static void WriteAbsFilesToArchive(ZipArchive archive, IReadOnlyDictionary<string, string> files, StringBuilder sb)
    {
        sb.AppendLine($"Total files: {files.Count:N0}");
        sb.AppendLine();
        foreach (var name in files.Keys.OrderBy(k => k, StringComparer.OrdinalIgnoreCase))
        {
            var size = files[name]?.Length ?? 0;
            sb.AppendLine($"  {name}  ({size:N0} chars)");

            var entry = archive.CreateEntry($"abs/{name}", CompressionLevel.Optimal);
            using var s = entry.Open();
            var bytes = Encoding.UTF8.GetBytes(files[name] ?? string.Empty);
            s.Write(bytes, 0, bytes.Length);
        }
    }

    private static void AppendAbsLocatorDetails(StringBuilder sb, AbsExportLocatorSnapshot? locator, AutomationRunSummary run)
    {
        WriteSubHeader(sb, "ABS location / retrieval details");
        sb.AppendLine($"  Facility ID                 : {(locator?.FacilityId ?? run.FacilityId ?? "-")}");
        sb.AppendLine($"  Report ID                   : {(locator?.ReportId ?? run.ReportId ?? "-")}");
        sb.AppendLine($"  Submission mode             : {((locator?.External ?? false) ? "external=true" : "external=false")}");
        if (locator != null)
        {
            sb.AppendLine($"  Captured at                 : {locator.CapturedAt:u}");
            sb.AppendLine($"  Last seen file count        : {locator.FileCount:N0}");

            if (locator.FileNames.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("  Last seen file names:");
                foreach (var name in locator.FileNames)
                    sb.AppendLine($"    {name}");
            }
        }
    }

    // --- Helpers ---

    private async Task<PipelineSummarySnapshotBuilder.PipelineSummarySnapshot?> SafeGetPipelineSnapshotAsync(
        Guid runId, CancellationToken ct)
    {
        try
        {
            return await _runManager.GetPipelineSnapshotAsync(runId, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[Export][{RunId}] Failed to build pipeline snapshot for export.", runId);
            return null;
        }
    }

    private async Task AppendRawDomainAsync(StringBuilder sb, Guid runId, string domain, string title, CancellationToken ct)
    {
        try
        {
            object? data = domain switch
            {
                "schedule"           => (await _snapshotStore.GetDomainAsync<PipelineDataReader.ReportScheduleInfo>(runId, domain, ct))?.Data,
                "entries"            => (await _snapshotStore.GetDomainAsync<List<PipelineDataReader.ReportEntryInfo>>(runId, domain, ct))?.Data,
                "populations"        => (await _snapshotStore.GetDomainAsync<List<PipelineDataReader.ReportPopulationInfo>>(runId, domain, ct))?.Data,
                "acquisitionSummary" => (await _snapshotStore.GetDomainAsync<PipelineDataReader.AcquisitionSummaryInfo>(runId, domain, ct))?.Data,
                "measureResources"   => (await _snapshotStore.GetDomainAsync<List<PipelineDataReader.PatientResourceTypeCount>>(runId, domain, ct))?.Data,
                "validatorResults"   => (await _snapshotStore.GetDomainAsync<List<PipelineSummarySnapshotBuilder.ValidatorResultSnapshot>>(runId, domain, ct))?.Data,
                _                    => null
            };

            if (data == null)
                return;

            sb.AppendLine();
            WriteSubHeader(sb, title);
            sb.AppendLine(JsonSerializer.Serialize(data, PrettyJson));
        }
        catch (Exception ex)
        {
            sb.AppendLine();
            WriteSubHeader(sb, title);
            sb.AppendLine($"  (failed to read domain '{domain}': {ex.GetType().Name}: {ex.Message})");
        }
    }

    private static void AppendServiceStats(
        StringBuilder sb,
        string serviceName,
        double completionRatePerSecond,
        int resourceCount,
        double avgResourcesPerSecond,
        IReadOnlyList<PipelineSummarySnapshotBuilder.CategoryCountSnapshot>? statusCounts,
        IReadOnlyList<PipelineSummarySnapshotBuilder.CategoryCountSnapshot>? funnelCounts,
        IReadOnlyList<PipelineSummarySnapshotBuilder.CategoryCountSnapshot>? resourceTypeCounts,
        IReadOnlyList<PipelineSummarySnapshotBuilder.ThroughputBucketSnapshot>? throughput,
        IReadOnlyList<string>? errors)
    {
        WriteKvp(sb, "Service", serviceName);
        WriteKvp(sb, "Completion rate (events/s)", completionRatePerSecond.ToString("F2", CultureInfo.InvariantCulture));
        WriteKvp(sb, "Resource count", resourceCount.ToString("N0", CultureInfo.InvariantCulture));
        WriteKvp(sb, "Avg resources/s", avgResourcesPerSecond.ToString("F2", CultureInfo.InvariantCulture));

        AppendCounts(sb, "Status counts", statusCounts);
        AppendCounts(sb, "Funnel counts", funnelCounts);
        AppendCounts(sb, "Resource-type counts", resourceTypeCounts);

        if (throughput is { Count: > 0 })
        {
            sb.AppendLine();
            WriteSubHeader(sb, "Throughput buckets");
            foreach (var b in throughput)
                sb.AppendLine($"  {b.Label,-20} {b.Count,8:N0}");
        }

        if (errors is { Count: > 0 })
        {
            sb.AppendLine();
            WriteSubHeader(sb, $"Errors ({errors.Count})");
            foreach (var e in errors)
                sb.AppendLine($"  {e}");
        }
    }

    private static void AppendCounts(StringBuilder sb,
        string title,
        IReadOnlyList<PipelineSummarySnapshotBuilder.CategoryCountSnapshot>? counts)
    {
        if (counts is not { Count: > 0 })
            return;

        sb.AppendLine();
        WriteSubHeader(sb, title);
        foreach (var c in counts.OrderByDescending(c => c.Count))
            sb.AppendLine($"  {c.Status,-30} {c.Count,8:N0}");
    }

    private static string FilterLogs(IReadOnlyList<string> logs, Func<string, bool> predicate, string description)
    {
        var sb = new StringBuilder();
        WriteHeader(sb, description);

        var matched = logs.Where(predicate).ToList();
        sb.AppendLine($"Matched {matched.Count:N0} of {logs.Count:N0} log line(s).");
        sb.AppendLine();
        foreach (var line in matched)
            sb.AppendLine(line);
        return sb.ToString();
    }

    private static bool IsKafkaLine(string line) =>
        line.Contains("[Kafka]", StringComparison.Ordinal)
        || line.Contains("[DIAG][Kafka]", StringComparison.Ordinal);

    private static bool IsLokiLine(string line) =>
        !IsKafkaLine(line)
        && (line.Contains("[LOKI", StringComparison.Ordinal) || line.Contains("[DIAG]", StringComparison.Ordinal));

    private async Task SafeWriteAsync(ZipArchive archive, string entryName, Func<Task<string>> contentFactory)
    {
        try
        {
            var content = await contentFactory();
            await WriteEntryAsync(archive, entryName, content);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[Export] Failed to build {Entry}; emitting placeholder.", entryName);
            var fallback = $"Failed to build this section: {ex.GetType().Name}: {ex.Message}";
            await WriteEntryAsync(archive, entryName + ".ERROR.txt", fallback);
        }
    }

    private static async Task WriteEntryAsync(ZipArchive archive, string entryName, string content)
    {
        var entry = archive.CreateEntry(entryName, CompressionLevel.Optimal);
        await using var s = entry.Open();
        var bytes = Encoding.UTF8.GetBytes(content);
        await s.WriteAsync(bytes);
    }

    private static void WriteHeader(StringBuilder sb, string title)
    {
        var bar = new string('=', Math.Max(8, title.Length + 4));
        sb.AppendLine(bar);
        sb.AppendLine($"  {title}");
        sb.AppendLine(bar);
    }

    private static void WriteSubHeader(StringBuilder sb, string title)
    {
        sb.AppendLine($"-- {title} --");
    }

    private static void WriteKvp(StringBuilder sb, string key, string? value)
    {
        sb.AppendLine($"{key,-26}: {value ?? "-"}");
    }

    private static string FormatTime(DateTimeOffset? dto) =>
        dto?.ToString("u", CultureInfo.InvariantCulture) ?? "-";

    private static string PrettyPrintJson(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            return JsonSerializer.Serialize(doc.RootElement, PrettyJson);
        }
        catch
        {
            // Not valid JSON — return the original text so the caller still sees something.
            return json;
        }
    }
}
