using Automation.UI.Models;
using LantanaGroup.Link.Automation.Link.Helpers;

namespace Automation.UI.Services;

/// <summary>
/// Reduces pipeline payloads to the counts, timestamps, and milestones the run
/// page charts need. Notes, acquired resource ids, FHIR queries, measure-report
/// id lists, per-patient measure rows, org-location rows, and normalization
/// execution lines stay in Report, Data Acquisition, and the Normalization service.
/// </summary>
public static class RunHistorySlim
{
    public const int AcquisitionFailureSampleCap = 20;

    /// <summary>
    /// Rolls acquisition logs into the counts, durations, and 10-second buckets
    /// the run charts draw. The stored chart has no per-log or per-patient rows.
    /// </summary>
    public static AcquisitionLogChart ToAcquisitionChart(IReadOnlyList<PipelineDataReader.AcquisitionLogInfo>? logs)
    {
        var chart = new AcquisitionLogChart();
        if (logs == null || logs.Count == 0)
            return chart;

        chart.TotalLogs = logs.Count;
        var statusCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var typeCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        long durationSum = 0;
        var durationCount = 0;
        long? minDuration = null;
        long? maxDuration = null;
        DateTimeOffset? windowStart = null;
        DateTimeOffset? windowEnd = null;

        foreach (var log in logs)
        {
            var status = string.IsNullOrWhiteSpace(log.Status) ? "Unknown" : log.Status!;
            statusCounts[status] = statusCounts.GetValueOrDefault(status) + 1;
            if (status.Equals("Completed", StringComparison.OrdinalIgnoreCase))
                chart.CompletedCount++;
            else if (status.Equals("Skipped", StringComparison.OrdinalIgnoreCase))
                chart.SkippedCount++;
            else if (IsFailureStatus(status))
            {
                chart.FailureCount++;
                if (chart.Failures.Count < AcquisitionFailureSampleCap)
                    chart.Failures.Add(FailureSample(log, status));
            }

            foreach (var type in ResourceTypes(log))
                typeCounts[type] = typeCounts.GetValueOrDefault(type) + 1;

            if (log.CompletionTimeMilliseconds is long milliseconds && milliseconds > 0)
            {
                durationSum += milliseconds;
                durationCount++;
                minDuration = minDuration is null ? milliseconds : Math.Min(minDuration.Value, milliseconds);
                maxDuration = maxDuration is null ? milliseconds : Math.Max(maxDuration.Value, milliseconds);
            }

            var start = FirstUsableTimestamp(log.ExecutionDate, log.CreateDate)
                ?? EstimatedStart(log.CompletionDate, log.CompletionTimeMilliseconds);
            var end = FirstUsableTimestamp(log.CompletionDate, log.ExecutionDate, log.CreateDate);
            if (start is null && end is null)
                continue;

            var resolvedEnd = end ?? start!.Value;
            var resolvedStart = start ?? resolvedEnd;
            if (resolvedStart > resolvedEnd)
                resolvedStart = resolvedEnd;

            chart.SpanCount++;
            windowStart = windowStart is null || resolvedStart < windowStart ? resolvedStart : windowStart;
            windowEnd = windowEnd is null || resolvedEnd > windowEnd ? resolvedEnd : windowEnd;

            if (log.CompletionDate is DateTime completion && IsUsable(completion))
                chart.EventCount++;
            else
                chart.InFlight = true;
        }

        chart.MinDurationMs = minDuration ?? 0;
        chart.MaxDurationMs = maxDuration ?? 0;
        chart.AverageDurationMs = durationCount == 0 ? 0 : durationSum / durationCount;
        chart.WindowStart = windowStart;
        chart.WindowEnd = windowEnd;
        chart.StatusCounts = statusCounts
            .OrderByDescending(pair => pair.Value)
            .ThenBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase)
            .Select(pair => new PipelineSummarySnapshotBuilder.CategoryCountSnapshot { Status = pair.Key, Count = pair.Value })
            .ToList();
        chart.ResourceTypeCounts = typeCounts
            .OrderByDescending(pair => pair.Value)
            .ThenBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase)
            .Select(pair => new PipelineSummarySnapshotBuilder.CategoryCountSnapshot { Status = pair.Key, Count = pair.Value })
            .ToList();

        if (windowStart is DateTimeOffset origin)
        {
            var buckets = new Dictionary<int, int>();
            foreach (var log in logs)
            {
                if (log.CompletionDate is not DateTime completion || !IsUsable(completion))
                    continue;

                var key = (int)Math.Floor(Math.Max(0, (AsUtc(completion) - origin).TotalSeconds) / 10);
                buckets[key] = buckets.GetValueOrDefault(key) + 1;
            }

            chart.ThroughputBuckets = buckets
                .OrderBy(pair => pair.Key)
                .Select(pair => new PipelineSummarySnapshotBuilder.ThroughputBucketSnapshot
                {
                    Label = $"{pair.Key * 10}s",
                    Count = pair.Value
                })
                .ToList();
        }

        return chart;
    }

    private static bool IsFailureStatus(string status)
        => status.Contains("fail", StringComparison.OrdinalIgnoreCase)
            || status.Contains("error", StringComparison.OrdinalIgnoreCase)
            || status.Equals("MaxRetriesReached", StringComparison.OrdinalIgnoreCase);

    private static AcquisitionLogFailure FailureSample(PipelineDataReader.AcquisitionLogInfo log, string status)
    {
        var note = log.Notes?.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
        if (note != null && note.Length > 200)
            note = note[..200];

        return new AcquisitionLogFailure
        {
            LogId = log.Id,
            Status = status,
            QueryPhase = log.QueryPhase,
            ResourceTypes = ResourceTypes(log),
            DurationMs = log.CompletionTimeMilliseconds,
            Message = note
        };
    }

    private static List<string> ResourceTypes(PipelineDataReader.AcquisitionLogInfo log)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var types = new List<string>();
        Add(log.ResourceTypes);
        if (log.FhirQueries != null)
        {
            foreach (var query in log.FhirQueries)
                Add(query?.ResourceTypes);
        }

        return types;

        void Add(IEnumerable<string>? values)
        {
            if (values == null)
                return;

            foreach (var value in values)
            {
                if (string.IsNullOrWhiteSpace(value) || !seen.Add(value))
                    continue;

                types.Add(value);
            }
        }
    }

    public static PipelineDataReader.PopulationCountSnapshot ToPopulationCounts(
        IReadOnlyList<PipelineDataReader.ReportPopulationInfo>? populations)
    {
        if (populations == null || populations.Count == 0)
            return new PipelineDataReader.PopulationCountSnapshot(0, 0, 0);

        var groupCount = 0;
        var measureReportCount = 0;
        foreach (var population in populations)
        {
            var groups = population.GroupPopulations;
            if (groups == null)
                continue;

            groupCount += groups.Count;
            foreach (var group in groups)
                measureReportCount += group.MeasureReportPopulations?.Count ?? 0;
        }

        return new PipelineDataReader.PopulationCountSnapshot(populations.Count, groupCount, measureReportCount);
    }

    public static List<PipelineDataReader.PatientResourceTypeCount> SlimMeasureResources(
        IReadOnlyList<PipelineDataReader.PatientResourceTypeCount>? rows)
    {
        if (rows == null || rows.Count == 0)
            return [];

        return rows
            .GroupBy(row => row.ResourceType ?? string.Empty, StringComparer.OrdinalIgnoreCase)
            .Select(group => new PipelineDataReader.PatientResourceTypeCount(
                string.Empty,
                group.Key,
                group.Sum(row => row.Count)))
            .OrderByDescending(row => row.Count)
            .ThenBy(row => row.ResourceType, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public static OrgLocationSummary SlimOrgLocation(StoreBackedServicePoller.OrgLocationSnapshot? snapshot)
    {
        if (snapshot == null)
            return new OrgLocationSummary();

        var configurations = snapshot.Configurations ?? [];
        var mappings = snapshot.LocationMappings ?? [];
        var encounters = snapshot.EncounterMappings ?? [];
        return new OrgLocationSummary
        {
            ConfigurationCount = configurations.Count,
            ActiveConfigurationCount = configurations.Count(configuration => configuration.IsActive),
            LocationMappingCount = mappings.Count,
            OrgLocationMappingCount = mappings.Count(mapping => mapping.IsOrgLocation),
            EncounterMappingCount = encounters.Count,
            MappedToOrgCount = encounters.Count(mapping => mapping.MappedToOrg)
        };
    }

    public static NormalizationEvidenceSnapshot SlimNormalizationEvidence(NormalizationEvidenceSnapshot source)
    {
        return new NormalizationEvidenceSnapshot
        {
            SuiteName = source.SuiteName,
            CollectedLineCount = source.CollectedLineCount,
            RawLinesOmitted = true,
            StepsCollapsed = false,
            EvidenceChunkCount = 0,
            RuntimeSequences = source.RuntimeSequences,
            SuiteSequences = source.SuiteSequences,
            OperationConfigs = (source.OperationConfigs ?? [])
                .Select(operation => new NormalizationOperationConfigSnapshot
                {
                    Name = operation.Name,
                    OperationType = operation.OperationType,
                    ResourceTypes = operation.ResourceTypes
                })
                .ToList()
        };
    }

    private static DateTimeOffset? EstimatedStart(DateTime? completionDate, long? completionTimeMilliseconds)
    {
        if (completionDate is not DateTime completion || !IsUsable(completion))
            return null;

        var end = AsUtc(completion);
        if (completionTimeMilliseconds is long milliseconds && milliseconds > 0)
            return end - TimeSpan.FromMilliseconds(milliseconds);

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

public sealed class OrgLocationSummary
{
    public int ConfigurationCount { get; set; }
    public int ActiveConfigurationCount { get; set; }
    public int LocationMappingCount { get; set; }
    public int OrgLocationMappingCount { get; set; }
    public int EncounterMappingCount { get; set; }
    public int MappedToOrgCount { get; set; }
}

/// <summary>
/// Chart data for one run's acquisition logs. Full log rows stay in Data Acquisition.
/// </summary>
public sealed class AcquisitionLogChart
{
    public int TotalLogs { get; set; }
    public int CompletedCount { get; set; }
    public int SkippedCount { get; set; }
    public int FailureCount { get; set; }
    public int EventCount { get; set; }
    public int SpanCount { get; set; }
    public bool InFlight { get; set; }
    public long MinDurationMs { get; set; }
    public long MaxDurationMs { get; set; }
    public long AverageDurationMs { get; set; }
    public DateTimeOffset? WindowStart { get; set; }
    public DateTimeOffset? WindowEnd { get; set; }
    public List<PipelineSummarySnapshotBuilder.CategoryCountSnapshot> StatusCounts { get; set; } = [];
    public List<PipelineSummarySnapshotBuilder.CategoryCountSnapshot> ResourceTypeCounts { get; set; } = [];
    public List<PipelineSummarySnapshotBuilder.ThroughputBucketSnapshot> ThroughputBuckets { get; set; } = [];
    public List<AcquisitionLogFailure> Failures { get; set; } = [];

    public void Apply(PipelineSummarySnapshotBuilder.DataAcquisitionSnapshot target, int resourceCount, DateTimeOffset generatedAt)
    {
        if (target.Errors.Count == 0 && Failures.Count > 0)
        {
            target.Errors = Failures
                .Select(failure =>
                {
                    var types = failure.ResourceTypes.Count == 0 ? "" : " [" + string.Join(",", failure.ResourceTypes) + "]";
                    var message = string.IsNullOrWhiteSpace(failure.Message) ? "" : ": " + failure.Message;
                    return $"log {failure.LogId} {failure.Status} {failure.QueryPhase}{types}{message}";
                })
                .ToList();
        }

        if (WindowStart is not DateTimeOffset start || WindowEnd is not DateTimeOffset storedEnd || SpanCount == 0)
            return;

        var end = storedEnd;
        if (InFlight && generatedAt > end)
            end = generatedAt;

        var seconds = Math.Max(0, (end - start).TotalSeconds);
        if (seconds < 0.001)
            seconds = 0.001;

        var events = EventCount > 0 ? EventCount : SpanCount;
        target.ActiveDurationSeconds = Math.Round(seconds, 2);
        target.CompletionRatePerSecond = Math.Round(events / seconds, 2);
        target.AverageResourcesPerSecond = resourceCount > 0 ? Math.Round(resourceCount / seconds, 2) : 0;
        target.ThroughputBuckets = ThroughputBuckets;
    }
}

public sealed class AcquisitionLogFailure
{
    public long LogId { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? QueryPhase { get; set; }
    public List<string> ResourceTypes { get; set; } = [];
    public long? DurationMs { get; set; }
    public string? Message { get; set; }
}
