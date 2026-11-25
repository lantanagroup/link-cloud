using Link.UI.Models;

namespace Link.UI.Services;

public static class RubricCompare
{
    public static (string From, string To)? DefaultComparison(RubricDetail rubric)
    {
        var versions = rubric.Versions ?? [];
        if (versions.Count < 2)
            return null;
        var to = versions[0].Semver;
        var active = rubric.LatestPublishedSemver;
        var from = !string.IsNullOrEmpty(active) && active != to ? active : versions[1].Semver;
        return (from, to);
    }

    public static RubricComparison CompareDefinitions(RubricDefinition before, RubricDefinition after)
    {
        var settings = new List<RubricChange>();
        Add(settings, "Title", before.Title ?? "", after.Title ?? "");
        Add(settings, "Owner", before.Owner ?? "", after.Owner ?? "");
        Add(settings, "Dimensions", List(before.Dimensions?.Select(RubricText.DimensionLabel)), List(after.Dimensions?.Select(RubricText.DimensionLabel)));
        Add(settings, "Scoring", RubricText.ScoringLabel(before.ScoringPolicy), RubricText.ScoringLabel(after.ScoringPolicy));
        Add(settings, "Resource types", List(before.ApplicableContext?.FhirResources), List(after.ApplicableContext?.FhirResources));
        Add(settings, "Workflow tags", List(before.ApplicableContext?.WorkflowTags), List(after.ApplicableContext?.WorkflowTags));

        var beforeChecks = RubricText.OrderedChecks(before);
        var afterChecks = RubricText.OrderedChecks(after);
        var beforeById = new Dictionary<string, RubricCheck>(StringComparer.Ordinal);
        foreach (var check in beforeChecks)
            beforeById.TryAdd(check.Id, check);
        var afterIds = afterChecks.Select(check => check.Id).ToHashSet(StringComparer.Ordinal);
        var added = new List<RubricCheck>();
        var changed = new List<RubricCheckChange>();
        var unchanged = 0;
        foreach (var check in afterChecks)
        {
            if (!beforeById.TryGetValue(check.Id, out var earlier))
            {
                added.Add(check);
                continue;
            }

            var diffs = CompareCheck(earlier, check);
            if (diffs.Count > 0)
                changed.Add(new RubricCheckChange(check.Id, diffs));
            else
                unchanged++;
        }

        var removed = beforeChecks.Where(check => !afterIds.Contains(check.Id)).ToList();
        var sharedBefore = beforeChecks.Where(check => afterIds.Contains(check.Id)).Select(check => check.Id).Distinct(StringComparer.Ordinal).ToList();
        var sharedAfter = afterChecks.Where(check => beforeById.ContainsKey(check.Id)).Select(check => check.Id).Distinct(StringComparer.Ordinal).ToList();
        var reordered = sharedBefore.Count == sharedAfter.Count && sharedBefore.Where((id, index) => id != sharedAfter[index]).Any();
        return new RubricComparison
        {
            Settings = settings,
            Added = added,
            Removed = removed,
            Changed = changed,
            UnchangedCount = unchanged,
            Reordered = reordered,
            Identical = settings.Count == 0 && added.Count == 0 && removed.Count == 0 && changed.Count == 0 && !reordered
        };
    }

    public static string? ImportEffect(RubricValidationResult? review)
    {
        if (review is not { Valid: true, Definition: not null })
            return null;
        return review.ExistingStatus switch
        {
            null or "" => review.RubricExists == true ? "new-version" : "new-rubric",
            "DRAFT" => !string.IsNullOrEmpty(review.Checksum) && review.Checksum == review.ExistingChecksum ? "unchanged-draft" : "replace-draft",
            _ => "immutable"
        };
    }

    public static string EffectText(string? effect) => effect switch
    {
        "new-rubric" => "This creates a new rubric and saves this definition as its first draft. Nothing is published.",
        "new-version" => "This adds a draft version to the rubric that already has this id. Nothing is published.",
        "replace-draft" => "This replaces the draft already saved under this version. A published or retired version is left as it is.",
        "unchanged-draft" => "The draft already saved under this version is the same definition. There is nothing to import.",
        "immutable" => "This version is published or retired, so it cannot be replaced. Review it again under the next free version number.",
        _ => ""
    };

    private static List<RubricChange> CompareCheck(RubricCheck before, RubricCheck after)
    {
        var changes = new List<RubricChange>();
        Add(changes, "Type", RubricText.CheckTypeLabel(before.Type), RubricText.CheckTypeLabel(after.Type));
        Add(changes, "Dimension", RubricText.DimensionLabel(before.Dimension), RubricText.DimensionLabel(after.Dimension));
        Add(changes, "Severity", RubricText.SeverityLabel(before.SeverityOverride), RubricText.SeverityLabel(after.SeverityOverride));
        Add(changes, "Enabled", RubricText.IsCheckEnabled(before) ? "Yes" : "No", RubricText.IsCheckEnabled(after) ? "Yes" : "No");
        var beforeSettings = RubricText.CheckSettings(before).ToDictionary(row => row.Key, row => row.Value);
        var afterSettings = RubricText.CheckSettings(after).ToDictionary(row => row.Key, row => row.Value);
        var keys = afterSettings.Keys.Concat(beforeSettings.Keys.Where(key => !afterSettings.ContainsKey(key)));
        foreach (var key in keys)
            Add(changes, RubricText.SettingLabel(key), beforeSettings.GetValueOrDefault(key) ?? "", afterSettings.GetValueOrDefault(key) ?? "");
        Add(changes, "Child checks", ChildrenText(before), ChildrenText(after));
        return changes;
    }

    private static string ChildrenText(RubricCheck check)
    {
        if (!RubricText.IsCombinedCheck(check))
            return "";
        return string.Join("\n", (check.Parameters?.Checks ?? []).Select(child =>
        {
            var details = new List<string>();
            if (!string.IsNullOrEmpty(child.Dimension))
                details.Add("Dimension: " + RubricText.DimensionLabel(child.Dimension));
            if (!string.IsNullOrEmpty(child.SeverityOverride))
                details.Add("Severity: " + RubricText.SeverityLabel(child.SeverityOverride));
            if (child.Enabled == false)
                details.Add("Enabled: No");
            details.AddRange(RubricText.CheckSettings(child)
                .OrderBy(setting => setting.Key, StringComparer.Ordinal)
                .Select(setting => setting.Label + ": " + setting.Value.Replace("\n", ", ")));
            var nested = RubricText.IsCombinedCheck(child) ? " [" + ChildrenText(child).Replace("\n", "; ") + "]" : "";
            return child.Id + " (" + RubricText.CheckTypeLabel(child.Type) + "): " + string.Join("; ", details.Where(part => part.Length > 0)) + nested;
        }));
    }

    private static void Add(List<RubricChange> changes, string label, string before, string after)
    {
        if (before != after)
            changes.Add(new RubricChange(label, before, after));
    }

    private static string List(IEnumerable<string>? values) => string.Join(", ", values ?? []);
}
