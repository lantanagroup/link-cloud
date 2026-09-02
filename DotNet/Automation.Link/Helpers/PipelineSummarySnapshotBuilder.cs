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
        public IReadOnlyList<PipelineDataReader.AcquisitionLogInfo> AcquisitionLogs { get; init; } = [];
        public IReadOnlyList<PipelineDataReader.PatientResourceTypeCount> MeasureEvalResourceCounts { get; init; } = [];

        /// <summary>
        /// Structured test-validator results persisted by the run manager.
        /// </summary>
        public IReadOnlyList<ValidatorResultSnapshot>? ValidatorResults { get; init; }
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
        public string? ReportCreated { get; set; }
        public string? SubmittedAt { get; set; }
        /// <summary>Human-readable duration from report creation to submission.</summary>
        public string? Duration { get; set; }
    }

    public class DataAcquisitionSnapshot
    {
        public double CompletionRatePerSecond { get; set; }
        public int ResourceCount { get; set; }
        public double AverageResourcesPerSecond { get; set; }
        /// <summary>Wall-clock seconds the stage was actually active, not end-to-end run time.</summary>
        public double? ActiveDurationSeconds { get; set; }
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
        /// <summary>Wall-clock seconds the stage was actually active, not end-to-end run time.</summary>
        public double? ActiveDurationSeconds { get; set; }
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

        var lokiErrors = ParseLokiErrorEntries(logs);

        snapshot.MeasureEval.Errors = GetServiceLokiErrors(lokiErrors, "Measure Eval");
        snapshot.Validation.Errors = GetServiceLokiErrors(lokiErrors, "Validation");
        snapshot.DataAcquisition.Errors = GetServiceLokiErrors(lokiErrors, "Data Acquisition");
        snapshot.Normalization.Errors = GetServiceLokiErrors(lokiErrors, "Normalization");

        if (string.IsNullOrWhiteSpace(facilityId) ||
            string.IsNullOrWhiteSpace(reportId) ||
            !Guid.TryParse(reportId, out var scheduleId))
        {
            snapshot.Report.Milestones =
            [
                new MilestoneSnapshot { Name = "Report Scheduled", Completed = false },
                new MilestoneSnapshot { Name = "Data Acquired", Completed = false },
                new MilestoneSnapshot { Name = "Normalized", Completed = false },
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
        IReadOnlyList<PipelineDataReader.AcquisitionLogInfo> acquisitionLogs;
        IReadOnlyList<PipelineDataReader.PatientResourceTypeCount> measureEvalResourceCounts;

        var data = await _domainDataProvider(scheduleId, facilityId);
        schedule = data.Schedule;
        entries = data.Entries;
        populations = data.Populations;
        acquisitionSummary = data.AcquisitionSummary;
        acquisitionLogs = data.AcquisitionLogs;
        measureEvalResourceCounts = data.MeasureEvalResourceCounts;

        snapshot.Report.Schedule = schedule == null
            ? null
            : new ReportScheduleSnapshot
            {
                ReportName = reportId,
                StartDate = schedule.ReportStartDate?.ToString("u"),
                EndDate = schedule.ReportEndDate?.ToString("u"),
                AdHocType = schedule.AdHocType,
                Frequency = schedule.Frequency,
                ScheduleStatus = schedule.Status,
                ReportCreated = schedule.CreateDate?.ToString("u"),
                SubmittedAt = schedule.SubmitReportDateTime?.ToString("u"),
                Duration = schedule.CreateDate.HasValue && schedule.SubmitReportDateTime.HasValue
                    ? FormatDuration(schedule.SubmitReportDateTime.Value - schedule.CreateDate.Value)
                    : null
            };

        snapshot.ValidatorResults = (data.ValidatorResults ?? []).ToList();

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
        snapshot.DataAcquisition.ResourceTypeCounts = (acquisitionSummary?.ResourceTypeCounts ?? [])
            .Select(r => new CategoryCountSnapshot { Status = r.ResourceType, Count = r.Count })
            .OrderByDescending(x => x.Count)
            .Take(15)
            .ToList();

        var measureResources = measureEvalResourceCounts.Sum(x => x.Count);

        // Normalization processes every resource acquired by DataAcquisition, so its
        // resource count mirrors DataAcquisition output. Resource type breakdown is the same.
        // Rates stay 0 unless an independent Normalization window exists — do not reuse DA's clock.
        var normalizationResources = acquisitionSummary?.TotalResourcesAcquired ?? 0;
        snapshot.Normalization.ResourceCount = normalizationResources;
        snapshot.Normalization.ResourceTypeCounts = snapshot.DataAcquisition.ResourceTypeCounts.ToList();

        snapshot.MeasureEval.ResourceCount = measureResources;

        // Validation operates in a per-patient context -- its 'resource count' is the
        // number of patients whose validation reached a terminal status. The per-status
        // breakdown is supplied below via FunnelCounts.
        snapshot.Validation.ResourceCount = entries.Count;

        ApplyAcquisitionWindowRates(snapshot.DataAcquisition, acquisitionLogs, dataAcqResources, snapshot.GeneratedAt);
        ApplyValidationWindowRates(snapshot.Validation, entries);

        snapshot.MeasureEval.ResourceTypeCounts = measureEvalResourceCounts
            .GroupBy(x => x.ResourceType)
            .Select(g => new CategoryCountSnapshot { Status = g.Key, Count = g.Sum(x => x.Count) })
            .OrderByDescending(x => x.Count)
            .Take(15)
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

        // Normalization has produced output once MeasureEval has resources or measure reports.
        // Require DA complete so the pill cannot light before acquisition finishes.
        var normalizedComplete = dataAcqComplete
            && (measureResources > 0 || entries.Any(e => e.MeasureReports.Count > 0));

        var measureComplete = normalizedComplete
            && entries.Count > 0
            && entries.All(e =>
                e.MeasureReports.Any(mr =>
                    string.Equals(mr.Status, "ReadyForValidation", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(mr.Status, "NotReportable", StringComparison.OrdinalIgnoreCase)));

        // Validation is complete when report entries exist and every entry has
        // reached a terminal reporting status (PassedValidation, FailedValidation,
        // or NotReportable). This prevents the milestone from completing before
        // the validation service has actually processed all entries.
        var validationComplete = entries.Count > 0
            && entries.All(e =>
                string.Equals(e.ReportingStatus, "PassedValidation", StringComparison.OrdinalIgnoreCase)
                || string.Equals(e.ReportingStatus, "FailedValidation", StringComparison.OrdinalIgnoreCase)
                || string.Equals(e.ReportingStatus, "NotReportable", StringComparison.OrdinalIgnoreCase));

        var submitted = string.Equals(scheduleStatus, "Submitted", StringComparison.OrdinalIgnoreCase);

        var expectedHardValidators = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "REPORT INTERNAL ABS MANIFEST VALIDATION",
            "REPORT DATABASE VALIDATION",
            "DATA ACQUISITION DATABASE VALIDATION",
            "NORMALIZATION DATABASE VALIDATION",
            "TENANT DATABASE VALIDATION"
        };

        var hardValidatorResults = snapshot.ValidatorResults
            .Where(v => expectedHardValidators.Contains(v.Name ?? string.Empty))
            .ToList();

        var hasAllHardValidatorResults = expectedHardValidators.IsSubsetOf(
            hardValidatorResults.Select(v => v.Name).ToHashSet(StringComparer.OrdinalIgnoreCase));

        var hasFailedValidators = hasAllHardValidatorResults
            && hardValidatorResults.Any(v =>
                string.Equals(v.Outcome, "Failed", StringComparison.OrdinalIgnoreCase));

        snapshot.Report.Milestones =
        [
            new MilestoneSnapshot { Name = "Report Scheduled", Completed = schedule != null },
            new MilestoneSnapshot { Name = "Data Acquired", Completed = dataAcqComplete },
            new MilestoneSnapshot { Name = "Normalized", Completed = normalizedComplete },
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

    private static string FormatDuration(TimeSpan ts)
    {
        if (ts.TotalSeconds < 1) return "< 1s";
        if (ts.TotalMinutes < 1) return $"{ts.Seconds}s";
        if (ts.TotalHours < 1) return $"{(int)ts.TotalMinutes}m {ts.Seconds}s";
        return $"{(int)ts.TotalHours}h {ts.Minutes}m {ts.Seconds}s";
    }

    private static bool IsErrorLike(string line) => LokiLogLineParser.IsErrorLike(line);

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
            || string.Equals(component, LokiScraper.Components.DataAcquisitionWorker, StringComparison.OrdinalIgnoreCase)
            || string.Equals(component, LokiScraper.Components.DataAcquisitionWorkerDev, StringComparison.OrdinalIgnoreCase))
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

        if (string.Equals(component, LokiScraper.Components.Census, StringComparison.OrdinalIgnoreCase))
            return "Census";

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

    private static void ApplyAcquisitionWindowRates(
        DataAcquisitionSnapshot target,
        IReadOnlyList<PipelineDataReader.AcquisitionLogInfo> logs,
        int resourceCount,
        DateTimeOffset generatedAt)
    {
        var spans = new List<(DateTimeOffset Start, DateTimeOffset End)>();
        var eventTimes = new List<DateTimeOffset>();
        var inFlight = false;

        foreach (var log in logs)
        {
            var start = FirstUsableTimestamp(log.ExecutionDate, log.CreateDate)
                ?? EstimatedStart(log.CompletionDate, log.CompletionTimeMilliseconds);
            var end = FirstUsableTimestamp(log.CompletionDate, log.ExecutionDate, log.CreateDate);
            if (start is null && end is null)
                continue;

            var resolvedEnd = end ?? start!.Value;
            var resolvedStart = start ?? resolvedEnd;
            if (resolvedStart > resolvedEnd)
                resolvedStart = resolvedEnd;

            spans.Add((resolvedStart, resolvedEnd));

            if (log.CompletionDate is DateTime completion && IsUsable(completion))
                eventTimes.Add(AsUtc(completion));
            else
                inFlight = true;
        }

        if (spans.Count == 0)
            return;

        var windowStart = spans.Min(s => s.Start);
        var windowEnd = spans.Max(s => s.End);
        if (inFlight && generatedAt > windowEnd)
            windowEnd = generatedAt;

        ApplyWindow(target, eventTimes.Count > 0 ? eventTimes.Count : spans.Count, resourceCount, windowStart, windowEnd, eventTimes);
    }

    private static void ApplyValidationWindowRates(
        ServiceSnapshot target,
        IReadOnlyList<PipelineDataReader.ReportEntryInfo> entries)
    {
        var validated = entries
            .Where(e =>
                string.Equals(e.ReportingStatus, "PassedValidation", StringComparison.OrdinalIgnoreCase)
                || string.Equals(e.ReportingStatus, "FailedValidation", StringComparison.OrdinalIgnoreCase))
            .ToList();

        var times = validated
            .Select(e => e.ModifyDate ?? e.CreateDate)
            .Where(t => t is DateTime dt && IsUsable(dt))
            .Select(t => AsUtc(t!.Value))
            .ToList();

        if (times.Count == 0)
            return;

        ApplyWindow(target, validated.Count, validated.Count, times.Min(), times.Max(), times);
    }

    private static void ApplyWindow(
        DataAcquisitionSnapshot target,
        int eventCount,
        int resourceCount,
        DateTimeOffset start,
        DateTimeOffset end,
        IReadOnlyList<DateTimeOffset> eventTimes)
    {
        var seconds = Math.Max(0, (end - start).TotalSeconds);
        if (seconds < 0.001)
            seconds = 0.001;

        target.ActiveDurationSeconds = Math.Round(seconds, 2);
        target.CompletionRatePerSecond = Math.Round(eventCount / seconds, 2);
        target.AverageResourcesPerSecond = resourceCount > 0 ? Math.Round(resourceCount / seconds, 2) : 0;
        target.ThroughputBuckets = BuildThroughputBucketsFromTimes(eventTimes, start);
    }

    private static void ApplyWindow(
        ServiceSnapshot target,
        int eventCount,
        int resourceCount,
        DateTimeOffset start,
        DateTimeOffset end,
        IReadOnlyList<DateTimeOffset> eventTimes)
    {
        var seconds = Math.Max(0, (end - start).TotalSeconds);
        if (seconds < 0.001)
            seconds = 0.001;

        target.ActiveDurationSeconds = Math.Round(seconds, 2);
        target.CompletionRatePerSecond = Math.Round(eventCount / seconds, 2);
        target.AverageResourcesPerSecond = resourceCount > 0 ? Math.Round(resourceCount / seconds, 2) : 0;
        target.ThroughputBuckets = BuildThroughputBucketsFromTimes(eventTimes, start);
    }

    private static List<ThroughputBucketSnapshot> BuildThroughputBucketsFromTimes(
        IReadOnlyList<DateTimeOffset> times,
        DateTimeOffset origin,
        int bucketSeconds = 10)
    {
        if (times.Count == 0)
            return [];

        return times
            .GroupBy(t => (int)Math.Floor(Math.Max(0, (t - origin).TotalSeconds) / bucketSeconds))
            .OrderBy(g => g.Key)
            .Select(g => new ThroughputBucketSnapshot
            {
                Label = $"{g.Key * bucketSeconds}s",
                Count = g.Count()
            })
            .ToList();
    }

    private static DateTimeOffset? EstimatedStart(DateTime? completionDate, long? completionTimeMilliseconds)
    {
        if (completionDate is not DateTime completion || !IsUsable(completion))
            return null;

        var end = AsUtc(completion);
        if (completionTimeMilliseconds is long ms && ms > 0)
            return end - TimeSpan.FromMilliseconds(ms);

        return end;
    }

    private static DateTimeOffset? FirstUsableTimestamp(params DateTime?[] candidates)
    {
        foreach (var candidate in candidates)
        {
            if (candidate is DateTime value && IsUsable(value))
                return AsUtc(value);
        }

        return null;
    }

    private static bool IsUsable(DateTime value)
        => value.Year >= 2000;

    private static DateTimeOffset AsUtc(DateTime value)
    {
        return value.Kind switch
        {
            DateTimeKind.Utc => new DateTimeOffset(value),
            DateTimeKind.Local => new DateTimeOffset(value).ToUniversalTime(),
            _ => new DateTimeOffset(DateTime.SpecifyKind(value, DateTimeKind.Utc))
        };
    }
}

