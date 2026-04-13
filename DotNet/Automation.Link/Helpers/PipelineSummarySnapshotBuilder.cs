using System.Globalization;
using System.Text.RegularExpressions;

namespace LantanaGroup.Link.Automation.Link.Helpers;

public class PipelineSummarySnapshotBuilder
{
    private readonly Func<Guid, string, Task<ResolvedDomainData>> _domainDataProvider;

    /// <summary>
    /// Constructor that accepts a delegate for loading domain data.
    /// This allows the Automation.UI layer to supply data from any backing
    /// store (Mongo, Redis, etc.) without the Automation library depending on it.
    /// </summary>
    public PipelineSummarySnapshotBuilder(Func<Guid, string, Task<ResolvedDomainData>> domainDataProvider)
    {
        _domainDataProvider = domainDataProvider;
    }

    /// <summary>
    /// Pre-resolved domain data that can be supplied by any store.
    /// </summary>
    public sealed class ResolvedDomainData
    {
        public PipelineDataReader.ReportScheduleInfo? Schedule { get; init; }
        public IReadOnlyList<PipelineDataReader.ReportEntryInfo> Entries { get; init; } = [];
        public IReadOnlyList<PipelineDataReader.ReportPopulationInfo> Populations { get; init; } = [];
        public PipelineDataReader.AcquisitionSummaryInfo? AcquisitionSummary { get; init; }
        public IReadOnlyList<PipelineDataReader.PatientResourceTypeCount> MeasureEvalResourceCounts { get; init; } = [];
        public IReadOnlyList<PipelineDataReader.PatientResourceTypeCount> ReportResourceCounts { get; init; } = [];
    }
    public class PipelineSummarySnapshot
    {
        public DateTimeOffset GeneratedAt { get; set; }
        public string? FacilityId { get; set; }
        public string? ReportId { get; set; }
        public bool IsFinal { get; set; }
        public ReportSnapshot Report { get; set; } = new();
        public List<ServiceErrorSnapshot> ServiceErrorSummary { get; set; } = [];
        public List<ValidatorResultSnapshot> ValidatorResults { get; set; } = [];
        public DataAcquisitionSnapshot DataAcquisition { get; set; } = new();
        public ServiceSnapshot Normalization { get; set; } = new();
        public ServiceSnapshot MeasureEval { get; set; } = new();
        public ServiceSnapshot Validation { get; set; } = new();
    }

    public class ServiceErrorSnapshot
    {
        public string ServiceName { get; set; } = string.Empty;
        public int TotalErrors { get; set; }
        public List<CategoryCountSnapshot> ErrorGroups { get; set; } = [];
        public List<string> ErrorLines { get; set; } = [];
    }

    public class ValidatorResultSnapshot
    {
        public string Name { get; set; } = string.Empty;
        public string Outcome { get; set; } = string.Empty;
        public int IssueCount { get; set; }
    }

    public class ReportSnapshot
    {
        public List<MilestoneSnapshot> Milestones { get; set; } = [];
        public ReportScheduleSnapshot? Schedule { get; set; }
        public List<CategoryCountSnapshot> EntrySubmissionStatuses { get; set; } = [];
        public string PopulationSummary { get; set; } = "No report population data yet.";
    }

    public class MilestoneSnapshot
    {
        public string Name { get; set; } = string.Empty;
        public bool Completed { get; set; }
        public bool Failed { get; set; }
    }

    public class ReportScheduleSnapshot
    {
        public string? ReportName { get; set; }
        public string? StartDate { get; set; }
        public string? EndDate { get; set; }
        public string? AdHocType { get; set; }
        public string? Frequency { get; set; }
        public string? ScheduleStatus { get; set; }
    }

    public class DataAcquisitionSnapshot
    {
        public double CompletionRatePerSecond { get; set; }
        public int ResourceCount { get; set; }
        public double AverageResourcesPerSecond { get; set; }
        public List<CategoryCountSnapshot> StatusCounts { get; set; } = [];
        public List<CategoryCountSnapshot> ResourceTypeCounts { get; set; } = [];
        public List<ThroughputBucketSnapshot> ThroughputBuckets { get; set; } = [];
        public List<string> Errors { get; set; } = [];
    }

    public class ServiceSnapshot
    {
        public double CompletionRatePerSecond { get; set; }
        public int ResourceCount { get; set; }
        public double AverageResourcesPerSecond { get; set; }
        public List<CategoryCountSnapshot> StatusCounts { get; set; } = [];
        public List<CategoryCountSnapshot> FunnelCounts { get; set; } = [];
        public List<CategoryCountSnapshot> ResourceTypeCounts { get; set; } = [];
        public List<ThroughputBucketSnapshot> ThroughputBuckets { get; set; } = [];
        public List<string> Errors { get; set; } = [];
    }

    public class ThroughputBucketSnapshot
    {
        public string Label { get; set; } = string.Empty;
        public int Count { get; set; }
    }

    public class CategoryCountSnapshot
    {
        public string Status { get; set; } = string.Empty;
        public int Count { get; set; }
    }

    public async Task<PipelineSummarySnapshot> BuildAsync(
        string? facilityId,
        string? reportId,
        IReadOnlyList<string> logs,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var snapshot = new PipelineSummarySnapshot
        {
            GeneratedAt = DateTimeOffset.UtcNow,
            FacilityId = facilityId,
            ReportId = reportId
        };

        var measureLines = FilterLines(logs, "measure", "measureeval", "subject-list", "evaluate");
        var validationLines = FilterLines(logs, "validation", "operationoutcome", "failedvalidation", "report internal abs manifest validation");
        var dataAcquisitionLines = FilterLines(logs, "data acquisition", "dataacq", "query plan", "fhir query", "resourceacquired", "acquisition");
        var normalizationLines = FilterLines(logs, "normalization", "resourcenormalized", "normalizing");
        var lokiErrors = ParseLokiErrorEntries(logs);

        snapshot.MeasureEval.CompletionRatePerSecond = ComputeEventRatePerSecond(measureLines);
        snapshot.Validation.CompletionRatePerSecond = ComputeEventRatePerSecond(validationLines);
        snapshot.DataAcquisition.CompletionRatePerSecond = ComputeEventRatePerSecond(dataAcquisitionLines);
        snapshot.Normalization.CompletionRatePerSecond = ComputeEventRatePerSecond(normalizationLines);
        snapshot.MeasureEval.ThroughputBuckets = BuildThroughputBuckets(measureLines);
        snapshot.Validation.ThroughputBuckets = BuildThroughputBuckets(validationLines);
        snapshot.DataAcquisition.ThroughputBuckets = BuildThroughputBuckets(dataAcquisitionLines);
        snapshot.Normalization.ThroughputBuckets = BuildThroughputBuckets(normalizationLines);

        snapshot.MeasureEval.Errors = GetServiceLokiErrors(lokiErrors, "Measure Eval");
        snapshot.Validation.Errors = GetServiceLokiErrors(lokiErrors, "Validation");
        snapshot.DataAcquisition.Errors = GetServiceLokiErrors(lokiErrors, "Data Acquisition");
        snapshot.Normalization.Errors = GetServiceLokiErrors(lokiErrors, "Normalization");
        snapshot.ValidatorResults = ParseValidatorResults(logs);

        if (string.IsNullOrWhiteSpace(facilityId) ||
            string.IsNullOrWhiteSpace(reportId) ||
            !Guid.TryParse(reportId, out var scheduleId))
        {
            snapshot.Report.Milestones =
            [
                new MilestoneSnapshot { Name = "Report Scheduled", Completed = false },
                new MilestoneSnapshot { Name = "Data Acquired", Completed = false },
                new MilestoneSnapshot { Name = "Measure Evaluated", Completed = false },
                new MilestoneSnapshot { Name = "Validation", Completed = false },
                new MilestoneSnapshot { Name = "Submitted", Completed = false },
                new MilestoneSnapshot { Name = "Test Validation", Completed = false }
            ];

            snapshot.Validation.StatusCounts =
            [
                new CategoryCountSnapshot { Status = "Errors", Count = snapshot.Validation.Errors.Count }
            ];

            return snapshot;
        }

        PipelineDataReader.ReportScheduleInfo? schedule;
        IReadOnlyList<PipelineDataReader.ReportEntryInfo> entries;
        IReadOnlyList<PipelineDataReader.ReportPopulationInfo> populations;
        PipelineDataReader.AcquisitionSummaryInfo? acquisitionSummary;
        IReadOnlyList<PipelineDataReader.PatientResourceTypeCount> measureEvalResourceCounts;
        IReadOnlyList<PipelineDataReader.PatientResourceTypeCount> reportResourceCounts;

        var data = await _domainDataProvider(scheduleId, facilityId);
        schedule = data.Schedule;
        entries = data.Entries;
        populations = data.Populations;
        acquisitionSummary = data.AcquisitionSummary;
        measureEvalResourceCounts = data.MeasureEvalResourceCounts;
        reportResourceCounts = data.ReportResourceCounts;

        snapshot.Report.Schedule = schedule == null
            ? null
            : new ReportScheduleSnapshot
            {
                ReportName = schedule.FacilityId,
                StartDate = schedule.ReportStartDate?.ToString("u"),
                EndDate = schedule.ReportEndDate?.ToString("u"),
                AdHocType = schedule.AdHocType,
                Frequency = schedule.Frequency,
                ScheduleStatus = schedule.Status
            };

        snapshot.Report.EntrySubmissionStatuses = entries
            .GroupBy(e => string.IsNullOrWhiteSpace(e.SubmissionStatus) ? "Unknown" : e.SubmissionStatus!)
            .Select(g => new CategoryCountSnapshot { Status = g.Key, Count = g.Count() })
            .OrderByDescending(x => x.Count)
            .ToList();

        var populationGroupCount = populations.Sum(p => p.GroupPopulations.Count);
        var measureReportPopulationCount = populations.Sum(p => p.GroupPopulations.Sum(g => g.MeasureReportPopulations.Count));
        snapshot.Report.PopulationSummary = populations.Count == 0
            ? "No report populations available yet."
            : $"{populations.Count} report type(s), {populationGroupCount} group population set(s), {measureReportPopulationCount} measure report population reference(s).";

        snapshot.DataAcquisition.StatusCounts = (acquisitionSummary?.StatusCounts ?? [])
            .Select(s => new CategoryCountSnapshot { Status = s.Status, Count = s.Count })
            .OrderByDescending(x => x.Count)
            .ToList();

        var dataAcqResources = acquisitionSummary?.TotalResourcesAcquired ?? 0;
        snapshot.DataAcquisition.ResourceCount = dataAcqResources;
        snapshot.DataAcquisition.AverageResourcesPerSecond = ComputeResourcesPerSecond(dataAcqResources, dataAcquisitionLines);
        snapshot.DataAcquisition.ResourceTypeCounts = (acquisitionSummary?.ResourceTypeCounts ?? [])
            .Select(r => new CategoryCountSnapshot { Status = r.ResourceType, Count = r.Count })
            .OrderByDescending(x => x.Count)
            .Take(6)
            .ToList();

        var measureResources = measureEvalResourceCounts.Sum(x => x.Count);
        var validationResources = reportResourceCounts.Sum(x => x.Count);

        // Normalization processes every resource acquired by DataAcquisition, so its
        // resource count mirrors DataAcquisition output. Resource type breakdown is the same.
        var normalizationResources = acquisitionSummary?.TotalResourcesAcquired ?? 0;
        snapshot.Normalization.ResourceCount = normalizationResources;
        snapshot.Normalization.AverageResourcesPerSecond = ComputeResourcesPerSecond(normalizationResources, normalizationLines);
        snapshot.Normalization.ResourceTypeCounts = snapshot.DataAcquisition.ResourceTypeCounts.ToList();

        snapshot.MeasureEval.ResourceCount = measureResources;
        snapshot.Validation.ResourceCount = validationResources;

        snapshot.MeasureEval.AverageResourcesPerSecond = ComputeResourcesPerSecond(measureResources, measureLines);
        snapshot.Validation.AverageResourcesPerSecond = ComputeResourcesPerSecond(validationResources, validationLines);

        snapshot.MeasureEval.ResourceTypeCounts = measureEvalResourceCounts
            .GroupBy(x => x.ResourceType)
            .Select(g => new CategoryCountSnapshot { Status = g.Key, Count = g.Sum(x => x.Count) })
            .OrderByDescending(x => x.Count)
            .Take(6)
            .ToList();

        snapshot.Validation.ResourceTypeCounts = reportResourceCounts
            .GroupBy(x => x.ResourceType)
            .Select(g => new CategoryCountSnapshot { Status = g.Key, Count = g.Sum(x => x.Count) })
            .OrderByDescending(x => x.Count)
            .Take(6)
            .ToList();

        var measureReadyForValidationCount = entries.Count(e =>
            e.MeasureReports.Any(mr => string.Equals(mr.Status, "ReadyForValidation", StringComparison.OrdinalIgnoreCase)));

        var measureNotReportableCount = entries.Count(e =>
            !e.MeasureReports.Any(mr => string.Equals(mr.Status, "ReadyForValidation", StringComparison.OrdinalIgnoreCase))
            && e.MeasureReports.Any(mr => string.Equals(mr.Status, "NotReportable", StringComparison.OrdinalIgnoreCase)));

        var measureNoReportCount = entries.Count - measureReadyForValidationCount - measureNotReportableCount;

        snapshot.MeasureEval.FunnelCounts =
        [
            new CategoryCountSnapshot { Status = "No Measure Report", Count = Math.Max(0, measureNoReportCount) },
            new CategoryCountSnapshot { Status = "NotReportable", Count = measureNotReportableCount },
            new CategoryCountSnapshot { Status = "ReadyForValidation", Count = measureReadyForValidationCount }
        ];

        var validationPassedCount = entries.Count(e =>
            string.Equals(e.ReportingStatus, "PassedValidation", StringComparison.OrdinalIgnoreCase));
        var validationFailedCount = entries.Count(e =>
            string.Equals(e.ReportingStatus, "FailedValidation", StringComparison.OrdinalIgnoreCase));
        var validationNotValidatedCount = entries.Count - validationPassedCount - validationFailedCount;

        snapshot.Validation.FunnelCounts =
        [
            new CategoryCountSnapshot { Status = "NotValidated", Count = Math.Max(0, validationNotValidatedCount) },
            new CategoryCountSnapshot { Status = "FailedValidation", Count = validationFailedCount },
            new CategoryCountSnapshot { Status = "PassedValidation", Count = validationPassedCount }
        ];

        snapshot.Validation.StatusCounts = entries
            .GroupBy(e => string.IsNullOrWhiteSpace(e.ReportingStatus) ? "Unknown" : e.ReportingStatus!)
            .Select(g => new CategoryCountSnapshot { Status = g.Key, Count = g.Count() })
            .OrderByDescending(x => x.Count)
            .ToList();

        var scheduleStatus = schedule?.Status ?? string.Empty;
        var dataAcqTotalLogs = snapshot.DataAcquisition.StatusCounts.Sum(s => s.Count);
        var dataAcqTerminalLogs = snapshot.DataAcquisition.StatusCounts
            .Where(s => string.Equals(s.Status, "Completed", StringComparison.OrdinalIgnoreCase)
                        || string.Equals(s.Status, "Skipped", StringComparison.OrdinalIgnoreCase))
            .Sum(s => s.Count);

        // DA is complete when all logs reached a terminal state.
        // For regenerated reports there are zero DA logs (data is reused from the
        // prior report). In that case, if downstream stages have already progressed
        // (entries with measure reports exist), DA is implicitly complete.
        var dataAcqExplicitlyComplete = dataAcqTotalLogs > 0 && dataAcqTerminalLogs == dataAcqTotalLogs;
        var dataAcqImplicitlyComplete = dataAcqTotalLogs == 0 && entries.Any(e => e.MeasureReports.Count > 0);
        var dataAcqComplete = dataAcqExplicitlyComplete || dataAcqImplicitlyComplete;
        var measureComplete = measureResources > 0;

        var hasValidationOutcome = entries.Any(e =>
            string.Equals(e.ReportingStatus, "PassedValidation", StringComparison.OrdinalIgnoreCase)
            || string.Equals(e.ReportingStatus, "FailedValidation", StringComparison.OrdinalIgnoreCase));

        var allEntriesValidationTerminal = entries.Count > 0 && entries.All(e =>
            string.Equals(e.ReportingStatus, "PassedValidation", StringComparison.OrdinalIgnoreCase)
            || string.Equals(e.ReportingStatus, "FailedValidation", StringComparison.OrdinalIgnoreCase)
            || string.Equals(e.ReportingStatus, "NotReportable", StringComparison.OrdinalIgnoreCase));

        var onlyNotReportable = entries.Count > 0 && entries.All(e =>
            string.Equals(e.ReportingStatus, "NotReportable", StringComparison.OrdinalIgnoreCase));

        var validationValidatorRan = snapshot.ValidatorResults.Any(v =>
            v.Name.Contains("VALIDATION RESULTS", StringComparison.OrdinalIgnoreCase));

        var dataAcquisitionStillActive = snapshot.DataAcquisition.StatusCounts.Any(s =>
            s.Count > 0 && IsActiveAcquisitionStatus(s.Status));

        var validationComplete = allEntriesValidationTerminal
            && (hasValidationOutcome || onlyNotReportable || validationValidatorRan)
            && !dataAcquisitionStillActive;

        var submitted = string.Equals(scheduleStatus, "Submitted", StringComparison.OrdinalIgnoreCase);
        var effectiveValidatorResults = snapshot.ValidatorResults
            .Where(v => !(v.Name ?? string.Empty).StartsWith("[Snapshot]", StringComparison.OrdinalIgnoreCase))
            .ToList();

        var expectedHardValidators = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "REPORT INTERNAL ABS MANIFEST VALIDATION",
            "REPORT DATABASE VALIDATION",
            "DATA ACQUISITION DATABASE VALIDATION",
            "NORMALIZATION DATABASE VALIDATION",
            "TENANT DATABASE VALIDATION"
        };

        var hardValidatorResults = effectiveValidatorResults
            .Where(v => !string.Equals(v.Name, "VALIDATION RESULTS (API)", StringComparison.OrdinalIgnoreCase))
            .ToList();

        var observedHardValidatorNames = hardValidatorResults
            .Select(v => v.Name)
            .Where(n => !string.IsNullOrWhiteSpace(n))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var hasAllHardValidatorResults = expectedHardValidators.IsSubsetOf(observedHardValidatorNames);
        var hasFailedValidators = hardValidatorResults.Any(v =>
            string.Equals(v.Outcome, "Failed", StringComparison.OrdinalIgnoreCase));

        snapshot.Report.Milestones =
        [
            new MilestoneSnapshot { Name = "Report Scheduled", Completed = schedule != null },
            new MilestoneSnapshot { Name = "Data Acquired", Completed = dataAcqComplete },
            new MilestoneSnapshot { Name = "Measure Evaluated", Completed = measureComplete },
            new MilestoneSnapshot { Name = "Validation", Completed = validationComplete },
            new MilestoneSnapshot { Name = "Submitted", Completed = submitted },
            new MilestoneSnapshot
            {
                Name = "Test Validation",
                Completed = hasAllHardValidatorResults && !hasFailedValidators,
                Failed = hasFailedValidators
            }
        ];

        snapshot.ServiceErrorSummary =
        [
            BuildServiceErrorSummary("Data Acquisition", GetServiceLokiErrors(lokiErrors, "Data Acquisition")),
            BuildServiceErrorSummary("Normalization", GetServiceLokiErrors(lokiErrors, "Normalization")),
            BuildServiceErrorSummary("Measure Eval", GetServiceLokiErrors(lokiErrors, "Measure Eval")),
            BuildServiceErrorSummary("Validation", GetServiceLokiErrors(lokiErrors, "Validation")),
            BuildServiceErrorSummary("Report", GetServiceLokiErrors(lokiErrors, "Report"))
        ];

        return snapshot;
    }

    private static List<string> FilterLines(IEnumerable<string> lines, params string[] terms)
    {
        return lines
            .Where(l => terms.Any(t => l.Contains(t, StringComparison.OrdinalIgnoreCase)))
            .ToList();
    }

    private static List<string> ExtractErrorLines(IEnumerable<string> lines, Func<string, bool> serviceFilter)
    {
        return lines
            .Where(l =>
                IsErrorLike(l) && serviceFilter(l))
            .TakeLast(50)
            .ToList();
    }

    private static bool IsErrorLike(string line)
    {
        if (string.IsNullOrWhiteSpace(line))
            return false;

        // Diagnostic rollup lines are telemetry, not errors.
        if (line.Contains("[DIAG]", StringComparison.OrdinalIgnoreCase))
            return false;

        // Aggregate counters like ERROR=140 are summaries, not individual error events.
        if (Regex.IsMatch(line, @"\b(ERROR|WARNING|INFO|FATAL)=\d+\b", RegexOptions.IgnoreCase))
            return false;

        // User intent: only actual exception logging from Loki should drive these views.
        return line.Contains("exception", StringComparison.OrdinalIgnoreCase)
            || line.Contains("unhandled", StringComparison.OrdinalIgnoreCase)
            || line.Contains("stacktrace", StringComparison.OrdinalIgnoreCase)
            || line.Contains("caused by", StringComparison.OrdinalIgnoreCase)
            || line.Contains("System.", StringComparison.OrdinalIgnoreCase)
            || line.Contains("java.", StringComparison.OrdinalIgnoreCase);
    }

    private static List<LokiErrorEntry> ParseLokiErrorEntries(IReadOnlyList<string> logs)
    {
        var errorRegex = new Regex(@"\]\s+\[LOKI ERROR\]\[(?<component>[^\]]+)\]\s+(?<message>.+)$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        var detailRegex = new Regex(@"\]\s+\[LOKI ERROR DETAIL\]\[(?<component>[^\]]+)\]\s+(?<detail>.+)$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        var list = new List<LokiErrorEntry>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        for (var i = 0; i < logs.Count; i++)
        {
            var line = logs[i];

            var m = errorRegex.Match(line);
            if (!m.Success)
                continue;

            var component = m.Groups["component"].Value.Trim();
            var message = m.Groups["message"].Value.Trim();

            // Restrict to actual exception/error style Loki lines only.
            if (!IsErrorLike(message))
                continue;

            // Peek ahead for a matching DETAIL line immediately following.
            string? detail = null;
            if (i + 1 < logs.Count)
            {
                var dm = detailRegex.Match(logs[i + 1]);
                if (dm.Success && string.Equals(dm.Groups["component"].Value.Trim(), component, StringComparison.OrdinalIgnoreCase))
                {
                    detail = dm.Groups["detail"].Value.Trim();
                    i++; // skip the detail line
                }
            }

            // Deduplicate on the summary portion only.
            if (!seen.Add($"{component}|{message}"))
                continue;

            // Rejoin with ||| so the UI modal can split summary vs detail.
            var fullMessage = string.IsNullOrEmpty(detail) ? message : $"{message}|||{detail}";
            list.Add(new LokiErrorEntry(component, fullMessage));
        }

        return list;
    }

    private static List<string> GetServiceLokiErrors(List<LokiErrorEntry> entries, string serviceName)
    {
        return entries
            .Where(e => string.Equals(MapServiceName(e.Component), serviceName, StringComparison.OrdinalIgnoreCase))
            .Select(e => $"[{e.Component}] {e.Message}")
            .TakeLast(100)
            .ToList();
    }

    private static string MapServiceName(string component)
    {
        if (string.Equals(component, LokiScraper.Components.DataAcquisition, StringComparison.OrdinalIgnoreCase)
            || string.Equals(component, LokiScraper.Components.DataAcquisitionWorker, StringComparison.OrdinalIgnoreCase))
            return "Data Acquisition";

        if (string.Equals(component, LokiScraper.Components.MeasureEval, StringComparison.OrdinalIgnoreCase))
            return "Measure Eval";

        if (string.Equals(component, LokiScraper.Components.Validation, StringComparison.OrdinalIgnoreCase))
            return "Validation";

        if (string.Equals(component, LokiScraper.Components.Report, StringComparison.OrdinalIgnoreCase))
            return "Report";

        if (string.Equals(component, LokiScraper.Components.Normalization, StringComparison.OrdinalIgnoreCase))
            return "Normalization";

        if (string.Equals(component, LokiScraper.Components.Submission, StringComparison.OrdinalIgnoreCase))
            return "Submission";

        if (string.Equals(component, LokiScraper.Components.QueryDispatch, StringComparison.OrdinalIgnoreCase))
            return "Query Dispatch";

        if (string.Equals(component, LokiScraper.Components.Tenant, StringComparison.OrdinalIgnoreCase))
            return "Tenant";

        return component;
    }

    private static ServiceErrorSnapshot BuildServiceErrorSummary(string serviceName, List<string> errors)
    {
        var groups = errors
            .Select(ToErrorGroupKey)
            .Where(k => !string.IsNullOrWhiteSpace(k))
            .GroupBy(k => k)
            .Select(g => new CategoryCountSnapshot { Status = g.Key, Count = g.Count() })
            .OrderByDescending(x => x.Count)
            .Take(6)
            .ToList();

        return new ServiceErrorSnapshot
        {
            ServiceName = serviceName,
            TotalErrors = errors.Count,
            ErrorGroups = groups,
            ErrorLines = errors.TakeLast(100).ToList()
        };
    }

    private static string ToErrorGroupKey(string line)
    {
        var cleaned = Regex.Replace(line, @"^\[[^\]]+\]\s*", string.Empty).Trim();
        while (cleaned.StartsWith("[", StringComparison.Ordinal))
        {
            var close = cleaned.IndexOf(']');
            if (close <= 0)
                break;
            cleaned = cleaned[(close + 1)..].Trim();
        }

        var separator = cleaned.IndexOf(':');
        if (separator >= 0 && separator + 1 < cleaned.Length)
            cleaned = cleaned[(separator + 1)..].Trim();

        var paren = cleaned.IndexOf(" (", StringComparison.Ordinal);
        if (paren > 0)
            cleaned = cleaned[..paren].Trim();

        if (cleaned.Length > 120)
            cleaned = cleaned[..120].Trim();

        return cleaned;
    }

    private sealed record LokiErrorEntry(string Component, string Message);

    private static double ComputeEventRatePerSecond(IReadOnlyList<string> lines)
    {
        if (lines.Count == 0)
            return 0;

        var times = lines
            .Select(TryParseLineTime)
            .Where(t => t.HasValue)
            .Select(t => t!.Value)
            .ToList();

        if (times.Count < 2)
            return lines.Count;

        var durationSeconds = Math.Max(1, (times[^1] - times[0]).TotalSeconds);
        return Math.Round(lines.Count / durationSeconds, 2);
    }

    private static double ComputeResourcesPerSecond(int resources, IReadOnlyList<string> lines)
    {
        if (resources <= 0)
            return 0;

        var times = lines
            .Select(TryParseLineTime)
            .Where(t => t.HasValue)
            .Select(t => t!.Value)
            .ToList();

        if (times.Count < 2)
            return resources;

        var durationSeconds = Math.Max(1, (times[^1] - times[0]).TotalSeconds);
        return Math.Round(resources / durationSeconds, 2);
    }

    private static TimeSpan? TryParseLineTime(string line)
    {
        if (string.IsNullOrWhiteSpace(line) || line.Length < 10 || line[0] != '[')
            return null;

        var closing = line.IndexOf(']');
        if (closing <= 1)
            return null;

        var token = line[1..closing];
        if (TimeSpan.TryParseExact(token, "hh\\:mm\\:ss", CultureInfo.InvariantCulture, out var value))
            return value;

        return null;
    }

    private static List<ThroughputBucketSnapshot> BuildThroughputBuckets(IReadOnlyList<string> lines, int bucketSeconds = 10)
    {
        var times = lines
            .Select(TryParseLineTime)
            .Where(t => t.HasValue)
            .Select(t => t!.Value)
            .ToList();

        if (times.Count == 0)
            return [];

        var origin = times[0];
        return times
            .GroupBy(t => (int)Math.Floor((t - origin).TotalSeconds / bucketSeconds))
            .OrderBy(g => g.Key)
            .Select(g => new ThroughputBucketSnapshot
            {
                Label = $"{g.Key * bucketSeconds}s",
                Count = g.Count()
            })
            .ToList();
    }

    private static List<ValidatorResultSnapshot> ParseValidatorResults(IReadOnlyList<string> logs)
    {
        var map = new Dictionary<string, ValidatorResultSnapshot>(StringComparer.OrdinalIgnoreCase);
        var regex = new Regex(@"\]\s+(?<name>.+?)\s*:\s*(?<outcome>Passed|Failed)(?:\s*\((?<issues>\d+)\s+issue\(s\)\))?", RegexOptions.Compiled);

        foreach (var line in logs)
        {
            var match = regex.Match(line);
            if (!match.Success)
                continue;

            var name = match.Groups["name"].Value.Trim();

            var outcome = match.Groups["outcome"].Value.Trim();
            var issues = 0;
            if (match.Groups["issues"].Success)
                int.TryParse(match.Groups["issues"].Value, out issues);

            map[name] = new ValidatorResultSnapshot
            {
                Name = name,
                Outcome = outcome,
                IssueCount = issues
            };
        }

        return map.Values
            .OrderBy(v => v.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static bool IsActiveAcquisitionStatus(string? status)
    {
        if (string.IsNullOrWhiteSpace(status))
            return false;

        return string.Equals(status, "Pending", StringComparison.OrdinalIgnoreCase)
            || string.Equals(status, "Processing", StringComparison.OrdinalIgnoreCase)
            || string.Equals(status, "InProgress", StringComparison.OrdinalIgnoreCase)
            || string.Equals(status, "Queued", StringComparison.OrdinalIgnoreCase)
            || string.Equals(status, "Running", StringComparison.OrdinalIgnoreCase);
    }
}

