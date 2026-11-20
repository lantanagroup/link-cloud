using System.Text.Json;
using Link.UI.Models;

namespace Link.UI.Services;

/// <summary>
/// How the rubric screens word what the Validation service returns.
/// </summary>
public static class RubricText
{
    private static readonly Dictionary<string, string> StatusLabels = new(StringComparer.Ordinal)
    {
        ["DRAFT"] = "Draft",
        ["PUBLISHED"] = "Published",
        ["RETIRED"] = "Retired"
    };

    private static readonly Dictionary<string, string> DimensionLabels = new(StringComparer.Ordinal)
    {
        ["CONFORMANCE"] = "Conformance",
        ["TERMINOLOGY"] = "Terminology",
        ["COMPLETENESS"] = "Completeness",
        ["PLAUSIBILITY"] = "Plausibility",
        ["CURRENCY"] = "Currency"
    };

    private static readonly Dictionary<string, string> CheckTypeLabels = new(StringComparer.Ordinal)
    {
        ["FHIR_CONFORMANCE"] = "FHIR conformance",
        ["TERMINOLOGY"] = "Terminology",
        ["FHIRPATH"] = "FHIRPath",
        ["VALUESET"] = "Value set",
        ["PLAUSIBILITY"] = "Plausibility",
        ["COMPLETENESS"] = "Completeness",
        ["CURRENCY"] = "Currency",
        ["CUSTOM"] = "Custom"
    };

    private static readonly Dictionary<string, string> SeverityLabels = new(StringComparer.Ordinal)
    {
        ["ERROR"] = "Error",
        ["WARNING"] = "Warning",
        ["INFORMATION"] = "Information"
    };

    private static readonly Dictionary<string, string> ResultLabels = new(StringComparer.Ordinal)
    {
        ["ACCEPTABLE"] = "Acceptable",
        ["ACCEPTABLE_WITH_WARNINGS"] = "Acceptable with warnings",
        ["UNACCEPTABLE"] = "Unacceptable",
        ["INCONCLUSIVE"] = "Inconclusive"
    };

    private static readonly Dictionary<string, string> ActionLabels = new(StringComparer.Ordinal)
    {
        ["REGISTERED"] = "Saved as draft",
        ["PUBLISHED"] = "Published",
        ["RETIRED"] = "Retired",
        ["DELETED"] = "Draft deleted"
    };

    private static readonly Dictionary<string, string> ScoringLabels = new(StringComparer.Ordinal)
    {
        ["piqi-dimension-scorecard"] = "Dimension scorecard",
        ["piqi-check-scorecard"] = "Check scorecard",
        ["piqi-pass-fail"] = "Pass/fail"
    };

    private static readonly Dictionary<string, string> RollupLabels = new(StringComparer.Ordinal)
    {
        ["worst-of"] = "Worst of",
        ["best-of"] = "Best of",
        ["pass-fail"] = "Pass/fail",
        ["majority"] = "Majority",
        ["all-must-pass"] = "All must pass"
    };

    private static readonly Dictionary<string, string> SettingLabels = new(StringComparer.Ordinal)
    {
        ["expression"] = "Expression",
        ["failureMessage"] = "Failure message",
        ["code"] = "Code",
        ["profiles"] = "Profiles",
        ["path"] = "Path",
        ["valueSet"] = "Value set",
        ["system"] = "Code system",
        ["validateCodings"] = "Validate codings",
        ["valueSetWhitelistRegex"] = "Value sets matching",
        ["matchMode"] = "Passes when",
        ["customCheckId"] = "Plug-in id",
        ["className"] = "Plug-in class",
        ["min"] = "Minimum",
        ["max"] = "Maximum"
    };

    public static string StatusLabel(string? status) => Label(StatusLabels, status);
    public static string DimensionLabel(string? dimension) => Label(DimensionLabels, dimension);
    public static string CheckTypeLabel(string? type) => Label(CheckTypeLabels, type);
    public static string SeverityLabel(string? severity) => Label(SeverityLabels, severity);
    public static string ResultLabel(string? result) => Label(ResultLabels, result);
    public static string ActionLabel(string? action) => Label(ActionLabels, action);
    public static string SettingLabel(string key) => SettingLabels.TryGetValue(key, out var label) ? label : key;

    public static string StatusBadge(string? status) => status switch
    {
        "DRAFT" => "au-badge-warning",
        "PUBLISHED" => "au-badge-success",
        "RETIRED" => "au-badge-muted",
        _ => "au-badge-muted"
    };

    public static string ResultBadge(string? result) => result switch
    {
        "ACCEPTABLE" => "au-badge-success",
        "ACCEPTABLE_WITH_WARNINGS" => "au-badge-warning",
        "UNACCEPTABLE" => "au-badge-danger",
        "INCONCLUSIVE" => "au-badge-attention",
        _ => "au-badge-muted"
    };

    public static string SeverityBadge(string? severity) => severity switch
    {
        "ERROR" => "au-badge-danger",
        "WARNING" => "au-badge-warning",
        "INFORMATION" => "au-badge-active",
        _ => "au-badge-muted"
    };

    public const string InUseBadge = "au-badge-attention";
    public const string ActiveBadge = "au-badge-active";

    public static string RollupLabel(string? rollup) =>
        string.IsNullOrEmpty(rollup) ? "" : RollupLabels.TryGetValue(rollup, out var label) ? label : rollup;

    public static string ScoringLabel(RubricScoringPolicy? policy)
    {
        if (string.IsNullOrEmpty(policy?.Type))
            return "";
        var type = ScoringLabels.TryGetValue(policy.Type, out var known) ? known : policy.Type;
        if (string.IsNullOrEmpty(policy.Rollup))
            return type;
        var rollup = RollupLabels.TryGetValue(policy.Rollup, out var roll) ? roll : policy.Rollup;
        return type + ", " + rollup.ToLowerInvariant();
    }

    public static string? PipelineUse(RubricSettings? settings, string rubricId)
    {
        if (string.IsNullOrEmpty(settings?.PipelineRubricId) || !string.Equals(settings.PipelineRubricId, rubricId, StringComparison.Ordinal))
            return null;
        if (settings.PipelineEnabled)
            return "decides";
        return settings.PipelineShadowEnabled ? "compared" : null;
    }

    public static string PipelineUseTitle(string? use) => use switch
    {
        "decides" => "The reporting pipeline validates patient data against this rubric's newest published version, and its result decides whether the data passes.",
        "compared" => "The reporting pipeline runs this rubric's newest published version beside the validator that decides, to compare the two. Its result does not decide whether the data passes.",
        _ => ""
    };

    public static RubricVersionSummary? NewestInStatus(IEnumerable<RubricVersionSummary>? versions, string status) =>
        versions?.FirstOrDefault(version => version.Status == status);

    public static string DryRunLabel(DateTimeOffset? completedAt, string? status)
    {
        if (completedAt is null)
            return "Not run";
        var label = ResultLabel(status);
        return label.Length == 0 ? "Completed" : label;
    }

    public static bool IsCheckEnabled(RubricCheck check) => check.Enabled != false;

    public static bool IsCombinedCheck(RubricCheck check) => check.Parameters?.Checks is not null;

    public static string CheckSummary(RubricCheck check)
    {
        var parameters = check.Parameters ?? new RubricCheckParameters();
        if (IsCombinedCheck(check))
        {
            var children = parameters.Checks ?? [];
            var mode = parameters.MatchMode == "ALL" ? "All" : "Any";
            return mode + " of " + children.Count + (children.Count == 1 ? " check" : " checks");
        }

        return check.Type switch
        {
            "FHIR_CONFORMANCE" => parameters.Profiles is { Count: > 0 }
                ? string.Join(", ", parameters.Profiles)
                : "Each resource against its own profile",
            "VALUESET" => string.Join(" in ", new[] { parameters.Path, parameters.ValueSet }.Where(part => !string.IsNullOrEmpty(part))),
            "TERMINOLOGY" => string.IsNullOrEmpty(parameters.ValueSetWhitelistRegex)
                ? "Codings against terminology"
                : "Value sets matching " + parameters.ValueSetWhitelistRegex,
            "CUSTOM" => parameters.CustomCheckId ?? parameters.ClassName ?? "",
            _ => parameters.Expression ?? ""
        };
    }

    public static IReadOnlyList<RubricCheck> OrderedChecks(RubricDefinition? definition)
    {
        var checks = definition?.Checks ?? [];
        return checks
            .Select((check, index) => (check, index))
            .OrderBy(entry => entry.check.Ordinal ?? -1)
            .ThenBy(entry => entry.index)
            .Select(entry => entry.check)
            .ToList();
    }

    public static string SettingValue(string key, object? value)
    {
        if (key == "matchMode")
            return value as string == "ALL" ? "All of its checks pass" : "Any of its checks passes";
        if (value is bool flag)
            return flag ? "Yes" : "No";
        if (value is IEnumerable<string> list)
            return string.Join("\n", list);
        if (value is JsonElement element)
            return element.ValueKind == JsonValueKind.String ? element.GetString() ?? "" : element.ToString();
        if (value is not null && value is not string)
            return RubricJson.Write(value);
        return value?.ToString() ?? "";
    }

    public static IReadOnlyList<(string Key, string Label, string Value)> CheckSettings(RubricCheck check)
    {
        var parameters = check.Parameters ?? new RubricCheckParameters();
        var rows = new List<(string Key, string Label, string Value)>();
        void Add(string key, object? value)
        {
            if (value is null)
                return;
            if (value is string text && text.Length == 0)
                return;
            if (value is ICollection<string> collection && collection.Count == 0)
                return;
            rows.Add((key, SettingLabel(key), SettingValue(key, value)));
        }

        Add("expression", parameters.Expression);
        Add("failureMessage", parameters.FailureMessage);
        Add("code", parameters.Code);
        Add("profiles", parameters.Profiles);
        Add("path", parameters.Path);
        Add("valueSet", parameters.ValueSet);
        Add("system", parameters.System);
        Add("validateCodings", parameters.ValidateCodings);
        Add("valueSetWhitelistRegex", parameters.ValueSetWhitelistRegex);
        Add("matchMode", parameters.MatchMode);
        Add("customCheckId", parameters.CustomCheckId);
        Add("className", parameters.ClassName);
        Add("min", parameters.Min);
        Add("max", parameters.Max);
        if (parameters.Extra is not null)
        {
            foreach (var extra in parameters.Extra)
                Add(extra.Key, extra.Value);
        }

        return rows;
    }

    private static string Label(Dictionary<string, string> labels, string? value)
    {
        if (string.IsNullOrEmpty(value))
            return "";
        return labels.TryGetValue(value, out var known) ? known : value;
    }
}
