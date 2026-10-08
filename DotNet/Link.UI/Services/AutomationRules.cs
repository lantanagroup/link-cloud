using System.Globalization;
using System.Text.RegularExpressions;
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

    public static bool IsCancellable(string? status) =>
        status is "Queued" || IsInProgress(status) || status is "CollectingMetrics";

    public static bool IsTerminal(string? status) =>
        status is "Succeeded" or "Failed" or "Cancelled";

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
        int? templateVersion,
        string? retentionNotice = null)
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
            RetentionNotice = string.IsNullOrWhiteSpace(retentionNotice) ? null : retentionNotice.Trim(),
            Duration = ResolveDuration(duration, storedStatus, startedAt, finishedAt, createdAt),
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
            .Select(row => DurationSeconds(row.Duration, row.Status, row.StartedAt, row.FinishedAt, row.CreatedAt))
            .Where(seconds => seconds is > 0)
            .Select(seconds => seconds!.Value)
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

    /// <summary>
    /// A finished run always has a duration. The stored pipeline span wins.
    /// When that span was never written, the wall clock from start to finish is used.
    /// </summary>
    public static string? ResolveDuration(
        string? stored,
        string? status,
        DateTimeOffset? startedAt,
        DateTimeOffset? finishedAt,
        DateTimeOffset createdAt)
    {
        if (TryParseDuration(stored, out var seconds))
            return seconds > 0 && seconds < 1 ? "< 1s" : FormatDuration(seconds);

        if (!string.IsNullOrWhiteSpace(stored))
            return stored.Trim();

        if (!IsTerminal(status) || finishedAt is null)
            return null;

        return FormatWallClock(finishedAt.Value - (startedAt ?? createdAt));
    }

    /// <summary>
    /// Seconds behind the duration a row shows. Only a succeeded or failed run
    /// counts. A stored span wins. Otherwise the run uses start-to-finish, which
    /// drops queue time. Running, queued, and cancelled runs contribute nothing.
    /// </summary>
    public static double? DurationSeconds(
        string? stored,
        string? status,
        DateTimeOffset? startedAt,
        DateTimeOffset? finishedAt,
        DateTimeOffset createdAt)
    {
        if (status is not ("Succeeded" or "Failed") || finishedAt is null)
            return null;

        if (TryParseDuration(stored, out var parsed) && parsed > 0)
            return parsed;

        var wall = (finishedAt.Value - (startedAt ?? createdAt)).TotalSeconds;
        return wall > 0 ? wall : null;
    }

    public static bool TryParseDuration(string? stored, out double seconds)
    {
        seconds = 0;
        if (string.IsNullOrWhiteSpace(stored))
            return false;

        var text = stored.Trim();
        if (text.Equals("< 1s", StringComparison.OrdinalIgnoreCase))
        {
            seconds = 0.5;
            return true;
        }

        var clock = Regex.Match(text, @"^(?:(\d+):)?(\d+):(\d{2})$");
        if (clock.Success)
        {
            var hours = clock.Groups[1].Success ? int.Parse(clock.Groups[1].Value, CultureInfo.InvariantCulture) : 0;
            var minutes = int.Parse(clock.Groups[2].Value, CultureInfo.InvariantCulture);
            var secs = int.Parse(clock.Groups[3].Value, CultureInfo.InvariantCulture);
            seconds = hours * 3600 + minutes * 60 + secs;
            return true;
        }

        var words = Regex.Match(
            text,
            @"^(?:(\d+)\s*h)?\s*(?:(\d+)\s*m)?\s*(?:(\d+(?:\.\d+)?)\s*s)?$",
            RegexOptions.IgnoreCase);
        if (words.Success && words.Value.Length == text.Length && words.Groups.Cast<Group>().Skip(1).Any(group => group.Success))
        {
            var hours = words.Groups[1].Success ? double.Parse(words.Groups[1].Value, CultureInfo.InvariantCulture) : 0;
            var minutes = words.Groups[2].Success ? double.Parse(words.Groups[2].Value, CultureInfo.InvariantCulture) : 0;
            var secs = words.Groups[3].Success ? double.Parse(words.Groups[3].Value, CultureInfo.InvariantCulture) : 0;
            seconds = hours * 3600 + minutes * 60 + secs;
            return seconds >= 0 && (hours > 0 || minutes > 0 || secs > 0 || text.Contains('0'));
        }

        return false;
    }

    public static string FormatWallClock(TimeSpan span)
    {
        if (span.TotalSeconds < 1)
            return span.TotalSeconds > 0 ? "< 1s" : "0:00";

        return FormatDuration(span.TotalSeconds);
    }

    private sealed class MutableDay
    {
        public int Succeeded { get; set; }

        public int Failed { get; set; }

        public int Cancelled { get; set; }

        public int Other { get; set; }
    }
}
