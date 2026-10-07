using System.Text.Json;
using LantanaGroup.Link.Shared.Application.Enums;
using LantanaGroup.Link.Shared.Application.Services.Security;
using Link.UI.Models;

namespace Link.UI.Services;

/// <summary>
/// Report-area decisions that do not call a service: generate dates, download, and how validation
/// issues group into the prequalification tables.
/// </summary>
public static class ReportsRules
{
    public const string InformationSeverity = "INFORMATION";
    public const int MaxListedIssues = 500;
    public const int MaxResultChars = 4_000_000;
    public const int MaxJsonChars = 750_000;
    public const int DefaultLogPageSize = 10;

    public static readonly string[] Cadences = ["Daily", "Monthly", "Custom"];
    public static readonly int[] LogPageSizes = [10, 20, 50];

    public static bool CanDownload(bool deleted, string? payloadRootUri) =>
        !deleted && !string.IsNullOrWhiteSpace(payloadRootUri);

    public static int ClampLogPageSize(int pageSize) =>
        LogPageSizes.Contains(pageSize) ? pageSize : DefaultLogPageSize;

    public static string ReportingLabel(string? status) => status switch
    {
        "PatientIdentified" => "Patient Identified",
        "NotReportable" => "Not Reportable",
        "PendingValidation" => "Pending Validation",
        "PassedValidation" => "Passed Validation",
        "FailedValidation" => "Failed Validation",
        null or "" => "—",
        _ => status
    };

    public static string SubmissionLabel(string? status) => status switch
    {
        "PendingValidation" => "Pending Validation",
        "FailedSubmission" => "Failed Submission",
        "NotEligable" => "Not Eligible",
        "NotSubmitted" => "Submission Skipped",
        null or "" => "—",
        _ => status
    };

    public static IReadOnlyList<string> SplitIds(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return [];

        return text
            .Split([',', '\n', '\r', '\t', ' '], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(value => value.Sanitize())
            .Where(value => value.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .ToList();
    }

    public static IReadOnlyList<string> ReportTypes(IEnumerable<string>? selected, string? typed)
    {
        var ids = new List<string>();
        if (selected is not null)
        {
            foreach (var value in selected)
                ids.AddRange(SplitIds(value));
        }

        ids.AddRange(SplitIds(typed));
        return ids.Distinct(StringComparer.Ordinal).ToList();
    }

    public static IReadOnlyList<string> MeasureIds(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return [];

        try
        {
            using var document = JsonDocument.Parse(json);
            var ids = new List<string>();
            CollectIds(document.RootElement, ids);
            return ids.Distinct(StringComparer.Ordinal).ToList();
        }
        catch (JsonException)
        {
            return [];
        }
    }

    /// <summary>
    /// Turns the chosen calendar days into UTC instants in the facility time zone.
    /// A daily report is that calendar day. A monthly report is the start date's calendar month.
    /// </summary>
    public static bool TryReportingPeriod(
        string? cadence,
        DateOnly? start,
        DateOnly? end,
        string? timeZoneId,
        out DateTime startUtc,
        out DateTime endUtc,
        out string? error)
    {
        startUtc = default;
        endUtc = default;
        var chosen = FacilityViewRules.Clean(cadence);
        if (chosen is null || !Cadences.Contains(chosen, StringComparer.OrdinalIgnoreCase))
        {
            error = "Choose a daily, monthly, or custom period.";
            return false;
        }

        if (start is null)
        {
            error = "Start date is required.";
            return false;
        }

        if (!TryZone(timeZoneId, out var zone, out error))
            return false;

        var startDay = start.Value;
        DateOnly endDay;
        if (chosen.Equals("Daily", StringComparison.OrdinalIgnoreCase))
        {
            endDay = startDay;
        }
        else if (chosen.Equals("Monthly", StringComparison.OrdinalIgnoreCase))
        {
            endDay = new DateOnly(startDay.Year, startDay.Month, DateTime.DaysInMonth(startDay.Year, startDay.Month));
        }
        else if (end is null)
        {
            error = "End date is required for a custom period.";
            return false;
        }
        else
        {
            endDay = end.Value;
        }

        startUtc = ToUtc(startDay, TimeOnly.MinValue, zone);
        endUtc = ToUtc(endDay, new TimeOnly(23, 59, 59), zone);
        if (endUtc <= startUtc)
        {
            error = "End date must be after the start date.";
            return false;
        }

        error = null;
        return true;
    }

    public static IReadOnlyList<ValidationIssueRow> ParseIssues(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return [];

        try
        {
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind != JsonValueKind.Array)
                return [];

            var issues = new List<ValidationIssueRow>();
            foreach (var item in document.RootElement.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Object)
                    continue;

                issues.Add(new ValidationIssueRow
                {
                    PatientId = Text(item, "patientId"),
                    Severity = Text(item, "severity"),
                    Code = Text(item, "code"),
                    Message = Text(item, "message"),
                    Location = Text(item, "location"),
                    Expression = Text(item, "expression"),
                    Categories = Categories(item)
                });
            }

            return issues;
        }
        catch (JsonException)
        {
            return [];
        }
    }

    public static bool TryParseResultSummary(string? json, out long count, out string severity)
    {
        count = 0;
        severity = string.Empty;
        if (string.IsNullOrWhiteSpace(json))
            return false;

        try
        {
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
                return false;

            if (document.RootElement.TryGetProperty("count", out var countValue)
                && countValue.TryGetInt64(out var parsed))
            {
                count = parsed;
            }

            severity = Text(document.RootElement, "severity");
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    public static IReadOnlyList<PrequalCategoryGroup> GroupIssues(IReadOnlyList<ValidationIssueRow> issues)
    {
        var groups = new Dictionary<(string Name, bool Acceptable), (string Guidance, int Count)>();
        foreach (var issue in issues)
        {
            if (issue.Categories.Count == 0)
            {
                Add(groups, "Uncategorized", false, "These issues are not categorized and must be reviewed individually.");
                continue;
            }

            foreach (var category in issue.Categories)
            {
                var name = string.IsNullOrWhiteSpace(category.Title) ? "Uncategorized" : category.Title.Trim();
                var acceptable = name.Equals("Uncategorized", StringComparison.OrdinalIgnoreCase)
                    ? false
                    : category.Acceptable;
                var guidance = name.Equals("Uncategorized", StringComparison.OrdinalIgnoreCase)
                    ? "These issues are not categorized and must be reviewed individually."
                    : category.Guidance;
                Add(groups, name, acceptable, guidance);
            }
        }

        return groups
            .Select(pair => new PrequalCategoryGroup
            {
                Name = pair.Key.Name,
                Acceptable = pair.Key.Acceptable,
                Guidance = pair.Value.Guidance,
                Count = pair.Value.Count
            })
            .OrderBy(group => group.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public static IReadOnlyList<ValidationIssueRow> IssuesInCategory(
        IReadOnlyList<ValidationIssueRow> issues,
        string? category,
        out int total)
    {
        var name = FacilityViewRules.Clean(category);
        IEnumerable<ValidationIssueRow> matched = issues;
        if (name is not null)
        {
            matched = name.Equals("Uncategorized", StringComparison.OrdinalIgnoreCase)
                ? issues.Where(issue => issue.Categories.Count == 0
                    || issue.Categories.Any(item => string.IsNullOrWhiteSpace(item.Title)
                        || item.Title.Equals("Uncategorized", StringComparison.OrdinalIgnoreCase)))
                : issues.Where(issue => issue.Categories.Any(item =>
                    item.Title.Equals(name, StringComparison.OrdinalIgnoreCase)));
        }

        var list = matched.ToList();
        total = list.Count;
        return list.Count <= MaxListedIssues ? list : list.Take(MaxListedIssues).ToList();
    }

    public static string? PrettyJson(string? json, out bool tooLarge)
    {
        tooLarge = false;
        if (string.IsNullOrWhiteSpace(json))
            return null;

        if (json.Length > MaxJsonChars)
        {
            tooLarge = true;
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            var pretty = JsonSerializer.Serialize(document.RootElement, new JsonSerializerOptions { WriteIndented = true });
            if (pretty.Length > MaxJsonChars)
            {
                tooLarge = true;
                return null;
            }

            return pretty;
        }
        catch (JsonException)
        {
            return json;
        }
    }

    private static void Add(
        Dictionary<(string Name, bool Acceptable), (string Guidance, int Count)> groups,
        string name,
        bool acceptable,
        string guidance)
    {
        var key = (name, acceptable);
        if (groups.TryGetValue(key, out var existing))
            groups[key] = (string.IsNullOrWhiteSpace(existing.Guidance) ? guidance : existing.Guidance, existing.Count + 1);
        else
            groups[key] = (guidance, 1);
    }

    private static bool TryZone(string? timeZoneId, out TimeZoneInfo zone, out string? error)
    {
        var id = FacilityViewRules.Clean(timeZoneId);
        if (id is null || id.Equals("UTC", StringComparison.OrdinalIgnoreCase))
        {
            zone = TimeZoneInfo.Utc;
            error = null;
            return true;
        }

        if (TimeZoneInfo.TryFindSystemTimeZoneById(id, out zone!))
        {
            error = null;
            return true;
        }

        zone = TimeZoneInfo.Utc;
        error = $"Facility time zone '{id}' is not recognized.";
        return false;
    }

    private static DateTime ToUtc(DateOnly date, TimeOnly time, TimeZoneInfo zone)
    {
        var local = date.ToDateTime(time, DateTimeKind.Unspecified);
        return TimeZoneInfo.ConvertTimeToUtc(local, zone);
    }

    private static void CollectIds(JsonElement element, List<string> ids)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Array:
                foreach (var item in element.EnumerateArray())
                    CollectIds(item, ids);
                break;
            case JsonValueKind.Object:
                if (element.TryGetProperty("id", out var id) && id.ValueKind == JsonValueKind.String)
                {
                    var text = id.GetString().Sanitize().Trim();
                    if (text.Length > 0 && !element.TryGetProperty("resourceType", out _))
                        ids.Add(text);
                }

                if (element.TryGetProperty("entry", out var entries))
                    CollectIds(entries, ids);
                break;
        }
    }

    private static IReadOnlyList<ValidationCategoryRow> Categories(JsonElement item)
    {
        if (!item.TryGetProperty("categories", out var categories) || categories.ValueKind != JsonValueKind.Array)
            return [];

        var rows = new List<ValidationCategoryRow>();
        foreach (var category in categories.EnumerateArray())
        {
            if (category.ValueKind != JsonValueKind.Object)
                continue;

            rows.Add(new ValidationCategoryRow
            {
                Id = Text(category, "id"),
                Title = Text(category, "title"),
                Acceptable = category.TryGetProperty("acceptable", out var acceptable)
                    && acceptable.ValueKind == JsonValueKind.True,
                Guidance = Text(category, "guidance")
            });
        }

        return rows;
    }

    private static string Text(JsonElement item, string name)
    {
        if (!item.TryGetProperty(name, out var value))
            return string.Empty;

        return value.ValueKind switch
        {
            JsonValueKind.String => value.GetString().Sanitize(),
            JsonValueKind.Number => value.GetRawText(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            _ => string.Empty
        };
    }
}
