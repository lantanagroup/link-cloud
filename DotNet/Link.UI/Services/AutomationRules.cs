using Link.UI.Models;

namespace Link.UI.Services;

/// <summary>
/// Read-side rules for Automation run summaries. Status names match the strings
/// Automation.UI stores. Counts follow the 14-day dashboard window.
/// </summary>
public static class AutomationRules
{
    public const int WindowDays = 14;

    public const int DefaultPageSize = 20;

    public const int MaxPageSize = 100;

    public static readonly string[] ActiveStatuses =
    [
        "Queued",
        "Running",
        "LiveWindowOpen",
        "ReportFinalization",
        "CollectingMetrics"
    ];

    public static string NormalizeSortBy(string? sortBy) =>
        (sortBy ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            "runname" => "runName",
            "patientcount" => "patientCount",
            "seed" => "seed",
            "status" => "status",
            "finishedat" => "finishedAt",
            "createdat" => "createdAt",
            _ => "createdAt"
        };

    public static bool IsDescending(string? sortDir) =>
        !string.Equals(sortDir?.Trim(), "asc", StringComparison.OrdinalIgnoreCase);

    public static string SortDir(bool descending) => descending ? "desc" : "asc";

    public static int NormalizePageNumber(int pageNumber) => Math.Max(1, pageNumber);

    public static int NormalizePageSize(int pageSize) =>
        pageSize <= 0 ? DefaultPageSize : Math.Min(pageSize, MaxPageSize);

    public static int TotalPages(long totalCount, int pageSize)
    {
        if (totalCount <= 0 || pageSize <= 0)
            return 1;

        return (int)Math.Min(int.MaxValue, (totalCount + pageSize - 1) / pageSize);
    }

    public static bool IsInProgress(string? status) =>
        status is "Running" or "LiveWindowOpen" or "ReportFinalization" or "CollectingMetrics";

    public static bool IsActiveCard(string? status) =>
        status is "Queued" || IsInProgress(status);

    public static string StatusLabel(string? status)
    {
        var name = (status ?? string.Empty).Trim();
        return name switch
        {
            "CollectingMetrics" => "Collecting",
            "ReportFinalization" => "Finalizing",
            "LiveWindowOpen" => "Live window",
            "Queued" or "Running" or "Cancelled" or "Succeeded" or "Failed" => name,
            "" => "Unknown",
            _ => name
        };
    }

    /// <summary>
    /// A run marks its facility when the facility id is the run id, or when the
    /// stored flag says this run created the facility. Tenant, report, and log
    /// pages keep those rows. A later slice badges them from this same rule.
    /// </summary>
    public static bool MarksAutomationFacility(Guid runId, string? facilityId, bool automationCreatedFacility)
    {
        if (string.IsNullOrWhiteSpace(facilityId))
            return false;

        if (string.Equals(facilityId.Trim(), runId.ToString(), StringComparison.OrdinalIgnoreCase))
            return true;

        return automationCreatedFacility;
    }

    public static AutomationRunRow ToRow(
        Guid runId,
        string? runName,
        string? scenario,
        string? status,
        int patientCount,
        int seed,
        bool isMetricsRun,
        DateTimeOffset createdAt,
        DateTimeOffset? startedAt,
        DateTimeOffset? finishedAt,
        string? error,
        string? duration,
        string? facilityId,
        bool automationCreatedFacility,
        string? reportId,
        int? templateVersion)
    {
        var facility = string.IsNullOrWhiteSpace(facilityId) ? null : facilityId.Trim();
        var report = string.IsNullOrWhiteSpace(reportId) ? null : reportId.Trim();
        var storedStatus = (status ?? string.Empty).Trim();

        return new AutomationRunRow
        {
            RunId = runId,
            RunName = string.IsNullOrWhiteSpace(runName) ? $"Run {runId}" : runName.Trim(),
            Scenario = string.IsNullOrWhiteSpace(scenario) ? string.Empty : scenario.Trim(),
            Status = storedStatus,
            StatusLabel = StatusLabel(storedStatus),
            PatientCount = patientCount,
            Seed = seed,
            IsMetricsRun = isMetricsRun,
            CreatedAt = createdAt,
            StartedAt = startedAt,
            FinishedAt = finishedAt,
            Error = string.IsNullOrWhiteSpace(error) ? null : error.Trim(),
            Duration = string.IsNullOrWhiteSpace(duration) ? null : duration.Trim(),
            FacilityId = facility,
            AutomationCreatedFacility = automationCreatedFacility,
            ReportId = report,
            TemplateVersion = templateVersion,
            InProgress = IsInProgress(storedStatus),
            ThrowawayFacility = MarksAutomationFacility(runId, facility, automationCreatedFacility)
        };
    }

    public static AutomationStats BuildStats(IEnumerable<AutomationRunRow> runs, DateTimeOffset now)
    {
        var rows = runs as IReadOnlyList<AutomationRunRow> ?? runs.ToList();
        var succeeded = rows.Count(row => row.Status == "Succeeded");
        var failed = rows.Count(row => row.Status == "Failed");
        var completed = rows
            .Where(row => row.Status is "Succeeded" or "Failed" && row.FinishedAt.HasValue)
            .Select(row => (row.FinishedAt!.Value - row.CreatedAt).TotalSeconds)
            .Where(seconds => seconds > 0)
            .ToList();

        var cutoff = now.AddDays(-(WindowDays - 1)).UtcDateTime.Date;
        var end = now.UtcDateTime.Date;
        var buckets = new Dictionary<string, MutableDay>();
        for (var day = cutoff; day <= end; day = day.AddDays(1))
            buckets[day.ToString("yyyy-MM-dd")] = new MutableDay();

        foreach (var row in rows)
        {
            var key = row.CreatedAt.UtcDateTime.Date.ToString("yyyy-MM-dd");
            if (!buckets.TryGetValue(key, out var bucket))
                continue;

            switch (row.Status)
            {
                case "Succeeded":
                    bucket.Succeeded++;
                    break;
                case "Failed":
                    bucket.Failed++;
                    break;
                case "Cancelled":
                    bucket.Cancelled++;
                    break;
                default:
                    bucket.Other++;
                    break;
            }
        }

        var decided = succeeded + failed;
        return new AutomationStats
        {
            TotalRuns = rows.Count,
            Succeeded = succeeded,
            Failed = failed,
            Cancelled = rows.Count(row => row.Status == "Cancelled"),
            Running = rows.Count(row => IsInProgress(row.Status)),
            Queued = rows.Count(row => row.Status == "Queued"),
            AvgDurationSeconds = completed.Count == 0 ? 0 : Math.Round(completed.Average(), 1),
            SuccessRate = decided == 0 ? 0 : Math.Round(100.0 * succeeded / decided, 1),
            RunsPerDay = buckets
                .OrderBy(pair => pair.Key, StringComparer.Ordinal)
                .Select(pair => new AutomationDayBucket
                {
                    Date = pair.Key,
                    Succeeded = pair.Value.Succeeded,
                    Failed = pair.Value.Failed,
                    Cancelled = pair.Value.Cancelled,
                    Other = pair.Value.Other
                })
                .ToList()
        };
    }

    public static string FormatDuration(double seconds)
    {
        if (seconds <= 0)
            return "—";

        var span = TimeSpan.FromSeconds(seconds);
        return span.TotalHours >= 1
            ? span.ToString(@"h\:mm\:ss")
            : span.ToString(@"m\:ss");
    }

    private sealed class MutableDay
    {
        public int Succeeded { get; set; }

        public int Failed { get; set; }

        public int Cancelled { get; set; }

        public int Other { get; set; }
    }
}
