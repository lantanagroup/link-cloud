using Link.UI.Models;

namespace Link.UI.Services;

public sealed class RubricEditorState
{
    public RubricDefinition Definition { get; init; } = new();
    public RubricDefinition Baseline { get; init; } = new();
    public int EditIndex { get; init; } = -1;
    public string? Failure { get; init; }
    public IReadOnlyList<string> ExistingVersions { get; init; } = [];
    public string? SavedChecksum { get; init; }
    public string? SavedSemver { get; init; }
    public string Mode { get; init; } = "new";
}

/// <summary>How a save is sent. A moved draft is created under the new number, then the old draft is deleted.</summary>
public sealed record RubricSavePlan(bool MustNotExist, string? ExpectedChecksum, bool Moving, string? PreviousSemver, string? PreviousChecksum);

public static class RubricEditorLogic
{
    public static RubricSavePlan PlanSave(string? savedSemver, string? savedChecksum, string newSemver, bool overwrite)
    {
        var moving = !string.IsNullOrEmpty(savedSemver) && savedSemver != newSemver;
        if (overwrite)
            return new RubricSavePlan(false, null, moving, moving ? savedSemver : null, savedChecksum);
        if (!string.IsNullOrEmpty(savedChecksum) && !moving)
            return new RubricSavePlan(false, savedChecksum, false, null, null);
        return new RubricSavePlan(true, null, moving, moving ? savedSemver : null, savedChecksum);
    }

    public static RubricEditorState Apply(RubricEditorPost post, RubricSettings? settings)
    {
        var existing = SplitVersions(post.ExistingVersions);
        var intent = post.Intent ?? "";
        if (intent == "paste")
        {
            var pasted = ReadPasted(post.PasteText, out var error);
            if (pasted is null)
                return Fail(post, existing, error ?? "The definition could not be read.");
            if (!string.IsNullOrEmpty(post.RouteRubricId))
                pasted.Id = post.RouteRubricId;
            return State(pasted, Baseline(post), post.EditIndex, existing, post, null);
        }

        var definition = ReadDefinition(post.DefinitionJson) ?? RubricDraft.NewDefinition();
        if (intent != "reload")
            Overlay(definition, post, settings);

        if (post.CheckOpen && AppliesOpenCheck(intent))
            ApplyCheck(definition, post, settings);

        var editIndex = post.EditIndex;
        if (intent == "cancel-check")
            editIndex = -1;
        else if (intent == "add-check")
        {
            var type = string.IsNullOrEmpty(post.AddType) ? RubricDraft.FormTypes(settings).FirstOrDefault() ?? "FHIRPATH" : post.AddType;
            definition.Checks.Add(RubricDraft.NewCheck(type, definition, settings));
            editIndex = definition.Checks.Count - 1;
        }
        else if (intent.StartsWith("open-check:", StringComparison.Ordinal) && int.TryParse(intent["open-check:".Length..], out var open) && open >= 0 && open < definition.Checks.Count)
            editIndex = open;
        else if (intent.StartsWith("move:", StringComparison.Ordinal))
        {
            var parts = intent.Split(':');
            if (parts.Length == 3 && int.TryParse(parts[1], out var index) && int.TryParse(parts[2], out var delta))
                editIndex = Move(definition, index, delta);
        }
        else if (intent.StartsWith("delete-check:", StringComparison.Ordinal) && int.TryParse(intent["delete-check:".Length..], out var remove) && remove >= 0 && remove < definition.Checks.Count)
        {
            definition.Checks.RemoveAt(remove);
            editIndex = -1;
        }
        else if (intent.StartsWith("duplicate:", StringComparison.Ordinal) && int.TryParse(intent["duplicate:".Length..], out var copy) && copy >= 0 && copy < definition.Checks.Count)
        {
            var clone = RubricDraft.Clone(new RubricDefinition { Checks = [definition.Checks[copy]] }).Checks[0];
            clone.Id = "";
            definition.Checks.Insert(copy + 1, clone);
            editIndex = copy + 1;
        }
        else if (intent.StartsWith("toggle:", StringComparison.Ordinal) && int.TryParse(intent["toggle:".Length..], out var toggle) && toggle >= 0 && toggle < definition.Checks.Count)
            definition.Checks[toggle].Enabled = definition.Checks[toggle].Enabled == false;
        else if (intent.StartsWith("bump:", StringComparison.Ordinal))
        {
            var part = intent["bump:".Length..];
            definition.Semver = existing.Count > 0 ? RubricDraft.NextVersion(existing, part) : RubricDraft.BumpVersion("0.0.0", part);
        }
        else if (intent == "add-resource")
            AddContext(definition, resources: true, post.ResourceType);
        else if (intent.StartsWith("remove-resource:", StringComparison.Ordinal) && int.TryParse(intent["remove-resource:".Length..], out var resource) && definition.ApplicableContext?.FhirResources is { } resources && resource >= 0 && resource < resources.Count)
            resources.RemoveAt(resource);
        else if (intent == "add-tag")
            AddContext(definition, resources: false, post.WorkflowTag);
        else if (intent.StartsWith("remove-tag:", StringComparison.Ordinal) && int.TryParse(intent["remove-tag:".Length..], out var tag) && definition.ApplicableContext?.WorkflowTags is { } tags && tag >= 0 && tag < tags.Count)
            tags.RemoveAt(tag);
        else if (intent == "add-child" && editIndex >= 0 && editIndex < definition.Checks.Count)
        {
            var parameters = definition.Checks[editIndex].Parameters ??= new RubricCheckParameters();
            parameters.Checks ??= [];
            parameters.Checks.Add(new RubricCheck { Id = "", Type = "VALUESET", Parameters = new RubricCheckParameters() });
        }
        else if (intent.StartsWith("remove-child:", StringComparison.Ordinal) && editIndex >= 0 && editIndex < definition.Checks.Count && int.TryParse(intent["remove-child:".Length..], out var child))
        {
            var children = definition.Checks[editIndex].Parameters?.Checks;
            if (children is not null && child >= 0 && child < children.Count)
                children.RemoveAt(child);
        }
        else if (intent == "apply-check")
            editIndex = post.EditIndex;

        return State(definition, Baseline(post), editIndex, existing, post, null);
    }

    public static List<RubricIssue> Issues(RubricDefinition definition, RubricSettings? settings, IReadOnlyList<string> existing, string? savedSemver)
    {
        var issues = RubricDraft.FindIssues(definition, settings);
        var semver = definition.Semver.Trim();
        var taken = existing.Contains(semver) && semver != savedSemver;
        if (taken && issues.All(issue => issue.Field != "semver"))
            issues.Add(new RubricIssue("semver", "Version " + semver + " of this rubric already exists. Choose another version number.", "semver"));
        return issues;
    }

    private static bool AppliesOpenCheck(string intent) =>
        intent is "save" or "validate" or "apply-check" or "save-anyway" or "add-check" or "add-child"
        || intent.StartsWith("open-check:", StringComparison.Ordinal)
        || intent.StartsWith("remove-child:", StringComparison.Ordinal)
        || intent.StartsWith("move:", StringComparison.Ordinal)
        || intent.StartsWith("delete-check:", StringComparison.Ordinal)
        || intent.StartsWith("duplicate:", StringComparison.Ordinal)
        || intent.StartsWith("toggle:", StringComparison.Ordinal);

    private static void Overlay(RubricDefinition definition, RubricEditorPost post, RubricSettings? settings)
    {
        if (post.Mode == "new")
            definition.Id = post.Id?.Trim() ?? "";
        else if (!string.IsNullOrEmpty(post.RouteRubricId))
            definition.Id = post.RouteRubricId;
        definition.Semver = post.Semver?.Trim() ?? definition.Semver;
        definition.Title = post.Title ?? "";
        definition.Owner = post.Owner ?? "";
        var catalog = RubricDraft.DimensionChoices(settings);
        var chosen = new HashSet<string>(post.Dimensions, StringComparer.Ordinal);
        foreach (var used in definition.Checks.Select(check => check.Dimension).Where(dimension => !string.IsNullOrEmpty(dimension)))
            chosen.Add(used!);
        var ordered = catalog.Where(chosen.Contains).ToList();
        foreach (var extra in chosen.Where(dimension => !ordered.Contains(dimension)))
            ordered.Add(extra);
        definition.Dimensions = ordered;
        definition.ScoringPolicy ??= new RubricScoringPolicy();
        definition.ScoringPolicy.Type = post.ScoringType ?? "";
        definition.ScoringPolicy.Rollup = RubricDraft.RollupRequired(definition.ScoringPolicy.Type, settings) ? post.Rollup : null;
    }

    private static void ApplyCheck(RubricDefinition definition, RubricEditorPost post, RubricSettings? settings)
    {
        if (post.EditIndex < 0 || post.EditIndex >= definition.Checks.Count)
            return;
        var current = definition.Checks[post.EditIndex];
        if (RubricDraft.ReadOnlyReason(current) is not null)
            return;
        var check = post.Check;
        current.Id = check.Id?.Trim() ?? "";
        current.Type = string.IsNullOrEmpty(check.Type) ? current.Type : check.Type;
        current.Dimension = check.Dimension;
        current.SeverityOverride = check.Severity;
        current.Enabled = check.Enabled;
        current.Parameters = BuildParameters(current, check);
        if (RubricDraft.CheckKind(current) == "combined")
            current.Parameters.Checks = BuildChildren(check);
        _ = settings;
    }

    private static RubricCheckParameters BuildParameters(RubricCheck current, RubricCheckPost check)
    {
        var parameters = new RubricCheckParameters();
        switch (RubricDraft.CheckKind(current))
        {
            case "expression":
                parameters.Expression = check.Expression;
                parameters.FailureMessage = check.FailureMessage;
                parameters.Code = check.Code;
                break;
            case "valueset":
                parameters.Path = check.Path;
                parameters.ValueSet = check.ValueSet;
                parameters.System = check.System;
                parameters.FailureMessage = check.FailureMessage;
                parameters.Code = check.Code;
                break;
            case "combined":
                parameters.MatchMode = check.MatchMode == "ALL" ? "ALL" : "ANY";
                parameters.FailureMessage = check.FailureMessage;
                parameters.Code = check.Code;
                parameters.Checks = [];
                break;
            case "conformance":
                parameters.Profiles = SplitLines(check.Profiles);
                break;
            case "terminology":
                if (check.ValidateCodingsPosted)
                    parameters.ValidateCodings = check.ValidateCodings;
                parameters.ValueSetWhitelistRegex = check.Whitelist;
                break;
        }

        return parameters;
    }

    private static List<RubricCheck> BuildChildren(RubricCheckPost check)
    {
        var count = check.ChildId.Length;
        var children = new List<RubricCheck>();
        for (var i = 0; i < count; i++)
        {
            var type = At(check.ChildType, i);
            if (string.IsNullOrEmpty(type))
                type = "VALUESET";
            var child = new RubricCheck
            {
                Id = At(check.ChildId, i),
                Type = type,
                Parameters = new RubricCheckParameters
                {
                    Expression = At(check.ChildExpression, i),
                    Path = At(check.ChildPath, i),
                    ValueSet = At(check.ChildValueSet, i),
                    System = At(check.ChildSystem, i),
                    FailureMessage = At(check.ChildFailure, i),
                    Code = At(check.ChildCode, i)
                }
            };
            children.Add(child);
        }

        return children;
    }

    private static int Move(RubricDefinition definition, int index, int delta)
    {
        var target = index + delta;
        if (index < 0 || index >= definition.Checks.Count || target < 0 || target >= definition.Checks.Count)
            return index;
        var check = definition.Checks[index];
        definition.Checks.RemoveAt(index);
        definition.Checks.Insert(target, check);
        return target;
    }

    private static void AddContext(RubricDefinition definition, bool resources, string? value)
    {
        var text = value?.Trim() ?? "";
        if (text.Length == 0)
            return;
        definition.ApplicableContext ??= new RubricApplicableContext();
        if (resources)
        {
            definition.ApplicableContext.FhirResources ??= [];
            if (!definition.ApplicableContext.FhirResources.Contains(text))
                definition.ApplicableContext.FhirResources.Add(text);
        }
        else
        {
            definition.ApplicableContext.WorkflowTags ??= [];
            if (!definition.ApplicableContext.WorkflowTags.Contains(text))
                definition.ApplicableContext.WorkflowTags.Add(text);
        }
    }

    private static RubricDefinition? ReadPasted(string? text, out string? error)
    {
        error = null;
        var trimmed = text?.Trim() ?? "";
        if (trimmed.Length == 0)
        {
            error = "Paste a definition to bring it into the editor.";
            return null;
        }

        if (trimmed.Length > RubricDraft.DefaultMaxPayloadBytes)
        {
            error = "That definition is larger than this environment accepts.";
            return null;
        }

        if (!trimmed.StartsWith('{'))
        {
            error = "Paste the definition as JSON. YAML is reviewed on the import page.";
            return null;
        }

        try
        {
            return RubricDraft.Normalize(RubricJson.Read<RubricDefinition>(trimmed));
        }
        catch (System.Text.Json.JsonException)
        {
            error = "That text is not a JSON definition.";
            return null;
        }
    }

    private static RubricDefinition? ReadDefinition(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return null;
        try
        {
            return RubricDraft.Normalize(RubricJson.Read<RubricDefinition>(json));
        }
        catch (System.Text.Json.JsonException)
        {
            return null;
        }
    }

    private static RubricDefinition Baseline(RubricEditorPost post) =>
        ReadDefinition(post.BaselineJson) ?? RubricDraft.NewDefinition();

    private static List<string> SplitVersions(string? text) =>
        (text ?? "").Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();

    private static List<string> SplitLines(string? text) =>
        (text ?? "").Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();

    private static string At(string[] values, int index) => index >= 0 && index < values.Length ? values[index] : "";

    private static RubricEditorState State(RubricDefinition definition, RubricDefinition baseline, int editIndex, IReadOnlyList<string> existing, RubricEditorPost post, string? failure) =>
        new()
        {
            Definition = definition,
            Baseline = baseline,
            EditIndex = editIndex,
            ExistingVersions = existing,
            SavedChecksum = post.SavedChecksum,
            SavedSemver = post.SavedSemver,
            Mode = post.Mode,
            Failure = failure
        };

    private static RubricEditorState Fail(RubricEditorPost post, IReadOnlyList<string> existing, string error)
    {
        var definition = ReadDefinition(post.DefinitionJson) ?? RubricDraft.NewDefinition();
        return State(definition, Baseline(post), post.EditIndex, existing, post, error);
    }
}
