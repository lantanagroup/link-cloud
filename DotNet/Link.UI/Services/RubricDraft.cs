using System.Text.Json;
using System.Text.RegularExpressions;
using Link.UI.Models;

namespace Link.UI.Services;

/// <summary>
/// What the draft editor and the import know about a rubric definition without asking the service.
/// The service has the last word. This catches the mistakes that can be seen while typing.
/// </summary>
public static class RubricDraft
{
    public static readonly string[] DefaultDimensions = ["CONFORMANCE", "TERMINOLOGY", "COMPLETENESS", "PLAUSIBILITY", "CURRENCY"];
    public static readonly string[] DefaultSeverities = ["ERROR", "WARNING", "INFORMATION"];
    public static readonly string[] DefaultRollups = ["worst-of", "best-of", "pass-fail", "majority", "all-must-pass"];
    public static readonly RubricScoringPolicySetting[] DefaultScoringPolicies =
    [
        new() { Type = "piqi-dimension-scorecard", RollupRequired = true },
        new() { Type = "piqi-check-scorecard", RollupRequired = true },
        new() { Type = "piqi-pass-fail", RollupRequired = false }
    ];

    public static readonly string[] FormCheckTypes =
        ["FHIRPATH", "COMPLETENESS", "PLAUSIBILITY", "CURRENCY", "VALUESET", "FHIR_CONFORMANCE", "TERMINOLOGY"];

    private static readonly Dictionary<string, string> DefaultRequiredDimension = new(StringComparer.Ordinal)
    {
        ["FHIR_CONFORMANCE"] = "CONFORMANCE",
        ["TERMINOLOGY"] = "TERMINOLOGY",
        ["VALUESET"] = "TERMINOLOGY",
        ["COMPLETENESS"] = "COMPLETENESS",
        ["PLAUSIBILITY"] = "PLAUSIBILITY",
        ["CURRENCY"] = "CURRENCY"
    };

    private static readonly string[] ExpressionTypes = ["FHIRPATH", "COMPLETENESS", "PLAUSIBILITY", "CURRENCY"];
    private static readonly string[] DetailFields = ["id", "semver", "title", "owner", "dimensions"];

    public const int MaxId = 128;
    public const int MaxTitle = 256;
    public const int MaxOwner = 128;
    public const int MaxSemver = 32;
    public const int MaxCode = 128;
    public const int MaxRegex = 512;
    public const int MaxWorkflowTag = 128;
    public const int DefaultMaxChecks = 200;
    public const int DefaultMaxProfiles = 20;
    public const int DefaultMaxCombinatorChecks = 20;
    public const int DefaultMaxContextEntries = 50;
    public const int DefaultMaxPayloadBytes = 262144;

    public static readonly Regex RubricIdPattern = new("^[a-z0-9]+([.-][a-z0-9]+)*$", RegexOptions.CultureInvariant | RegexOptions.Compiled);
    public static readonly Regex SemverPattern = new(@"^\d+\.\d+\.\d+$", RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex CanonicalPattern = new("^[A-Za-z][A-Za-z0-9+.-]*:[^\\s|]+(\\|\\S+)?$", RegexOptions.CultureInvariant | RegexOptions.Compiled);

    public static string CheckKind(RubricCheck check)
    {
        if (ExpressionTypes.Contains(check.Type))
            return "expression";
        return check.Type switch
        {
            "VALUESET" => RubricText.IsCombinedCheck(check) ? "combined" : "valueset",
            "FHIR_CONFORMANCE" => "conformance",
            "TERMINOLOGY" => "terminology",
            _ => "custom"
        };
    }

    public static string? ReadOnlyReason(RubricCheck check)
    {
        var kind = CheckKind(check);
        if (kind == "custom")
            return "Custom checks cannot be edited here.";
        if (kind == "combined")
        {
            var children = check.Parameters?.Checks ?? [];
            if (children.Any(child => CheckKind(child) == "combined"))
                return "This check combines checks that are themselves combined. Checks nested this deep cannot be edited here.";
            if (children.Any(child => CheckKind(child) == "custom"))
                return "This check combines a custom check, which cannot be edited here.";
        }

        return null;
    }

    public static string? RequiredDimension(string type, RubricSettings? settings)
    {
        var described = settings?.CheckTypes?.FirstOrDefault(setting => setting.Type == type);
        if (described is not null)
            return string.IsNullOrEmpty(described.Dimension) ? null : described.Dimension;
        return DefaultRequiredDimension.TryGetValue(type, out var required) ? required : null;
    }

    public static RubricCheck NewCheck(string type, RubricDefinition definition, RubricSettings? settings)
    {
        return new RubricCheck
        {
            Id = "",
            Type = type,
            Dimension = RequiredDimension(type, settings) ?? definition.Dimensions.FirstOrDefault(),
            SeverityOverride = "ERROR",
            Enabled = true,
            Parameters = new RubricCheckParameters()
        };
    }

    public static RubricDefinition NewDefinition() => new()
    {
        Id = "",
        Semver = "1.0.0",
        Title = "",
        Owner = "",
        Dimensions = ["CONFORMANCE"],
        ScoringPolicy = new RubricScoringPolicy { Type = "piqi-dimension-scorecard", Rollup = "worst-of" },
        Checks = []
    };

    public static RubricDefinition Clone(RubricDefinition definition) =>
        RubricJson.Read<RubricDefinition>(RubricJson.Write(definition)) ?? NewDefinition();

    public static RubricDefinition Normalize(RubricDefinition? definition)
    {
        var source = definition ?? new RubricDefinition();
        var policy = source.ScoringPolicy ?? new RubricScoringPolicy();
        var normalized = new RubricDefinition
        {
            Id = source.Id ?? "",
            Semver = source.Semver ?? "",
            Title = source.Title ?? "",
            Owner = source.Owner ?? "",
            Dimensions = (source.Dimensions ?? []).Where(item => item is not null).ToList(),
            ScoringPolicy = new RubricScoringPolicy { Type = policy.Type ?? "" },
            Checks = RubricText.OrderedChecks(new RubricDefinition
            {
                Checks = (source.Checks ?? []).Select(NormalizeCheck).ToList()
            }).ToList()
        };
        if (!string.IsNullOrEmpty(policy.Rollup))
            normalized.ScoringPolicy.Rollup = policy.Rollup;
        if (source.ApplicableContext is not null)
        {
            normalized.ApplicableContext = new RubricApplicableContext();
            if (source.ApplicableContext.FhirResources is not null)
                normalized.ApplicableContext.FhirResources = source.ApplicableContext.FhirResources.Where(item => item is not null).ToList();
            if (source.ApplicableContext.WorkflowTags is not null)
                normalized.ApplicableContext.WorkflowTags = source.ApplicableContext.WorkflowTags.Where(item => item is not null).ToList();
        }

        return normalized;
    }

    public static RubricDefinition Clean(RubricDefinition definition, RubricSettings? settings = null)
    {
        var cleaned = new RubricDefinition
        {
            Id = definition.Id.Trim(),
            Semver = definition.Semver.Trim(),
            Dimensions = [.. definition.Dimensions],
            ScoringPolicy = new RubricScoringPolicy { Type = definition.ScoringPolicy.Type },
            Checks = definition.Checks.Select((check, index) => CleanCheck(check, index + 1)).ToList()
        };
        if (!string.IsNullOrWhiteSpace(definition.Title))
            cleaned.Title = definition.Title.Trim();
        if (!string.IsNullOrWhiteSpace(definition.Owner))
            cleaned.Owner = definition.Owner.Trim();
        if (!string.IsNullOrEmpty(definition.ScoringPolicy.Rollup) && RollupRequired(definition.ScoringPolicy.Type, settings))
            cleaned.ScoringPolicy.Rollup = definition.ScoringPolicy.Rollup;

        var resources = TrimmedList(definition.ApplicableContext?.FhirResources);
        var tags = TrimmedList(definition.ApplicableContext?.WorkflowTags);
        if (resources.Count > 0 || tags.Count > 0)
        {
            cleaned.ApplicableContext = new RubricApplicableContext();
            if (resources.Count > 0)
                cleaned.ApplicableContext.FhirResources = resources;
            if (tags.Count > 0)
                cleaned.ApplicableContext.WorkflowTags = tags;
        }

        return cleaned;
    }

    public static bool RollupRequired(string? policyType, RubricSettings? settings)
    {
        IEnumerable<RubricScoringPolicySetting> policies = settings?.ScoringPolicies is { Count: > 0 }
            ? settings.ScoringPolicies
            : DefaultScoringPolicies;
        return policies.FirstOrDefault(policy => policy.Type == policyType)?.RollupRequired ?? true;
    }

    public static bool Same(RubricDefinition a, RubricDefinition b) =>
        RubricJson.Write(Clean(a)) == RubricJson.Write(Clean(b));

    public static int CompareVersions(string a, string b)
    {
        var left = ParseVersion(a);
        var right = ParseVersion(b);
        if (left is null || right is null)
            return string.Compare(a, b, StringComparison.Ordinal);
        var major = left.Value.Major - right.Value.Major;
        if (major != 0)
            return major;
        var minor = left.Value.Minor - right.Value.Minor;
        if (minor != 0)
            return minor;
        return left.Value.Patch - right.Value.Patch;
    }

    public static string BumpVersion(string semver, string part)
    {
        var parsed = ParseVersion(semver) ?? (0, 0, 0);
        return part switch
        {
            "major" => (parsed.Major + 1) + ".0.0",
            "minor" => parsed.Major + "." + (parsed.Minor + 1) + ".0",
            _ => parsed.Major + "." + parsed.Minor + "." + (parsed.Patch + 1)
        };
    }

    public static string NextVersion(IEnumerable<string> existing, string part = "patch")
    {
        var valid = existing.Where(semver => ParseVersion(semver) is not null).ToList();
        if (valid.Count == 0)
            return "1.0.0";
        var newest = valid.Aggregate((a, b) => CompareVersions(a, b) >= 0 ? a : b);
        return BumpVersion(newest, part);
    }

    public static IReadOnlyList<string> DimensionChoices(RubricSettings? settings) =>
        settings?.Dimensions is { Count: > 0 } ? settings.Dimensions : DefaultDimensions;

    public static IReadOnlyList<string> SeverityChoices(RubricSettings? settings) =>
        settings?.Severities is { Count: > 0 } ? settings.Severities : DefaultSeverities;

    public static IReadOnlyList<string> RollupChoices(RubricSettings? settings) =>
        settings?.Rollups is { Count: > 0 } ? settings.Rollups : DefaultRollups;

    public static IReadOnlyList<RubricScoringPolicySetting> ScoringChoices(RubricSettings? settings) =>
        settings?.ScoringPolicies is { Count: > 0 } ? settings.ScoringPolicies : DefaultScoringPolicies;

    public static IReadOnlyList<string> FormTypes(RubricSettings? settings)
    {
        if (settings?.CheckTypes is { Count: > 0 })
        {
            return settings.CheckTypes
                .Where(setting => setting.Supported && setting.Type != "CUSTOM" && FormCheckTypes.Contains(setting.Type))
                .Select(setting => setting.Type)
                .ToList();
        }

        return FormCheckTypes;
    }

    public static int PayloadCap(RubricSettings? settings) =>
        settings is { MaxPayloadBytes: > 0 } ? settings.MaxPayloadBytes : DefaultMaxPayloadBytes;

    public static List<RubricIssue> FindIssues(RubricDefinition definition, RubricSettings? settings)
    {
        var issues = new List<RubricIssue>();
        var maxChecks = Limit(settings?.MaxChecks, DefaultMaxChecks);
        var maxContext = Limit(settings?.MaxApplicableContextEntries, DefaultMaxContextEntries);
        var maxProfiles = Limit(settings?.MaxProfiles, DefaultMaxProfiles);
        var maxCombined = Limit(settings?.MaxCombinatorChecks, DefaultMaxCombinatorChecks);

        var id = definition.Id.Trim();
        if (id.Length == 0)
            issues.Add(Issue("id", "Enter an id for the rubric.", "id"));
        else if (id.Length < 3 || id.Length > MaxId)
            issues.Add(Issue("id", "The id must be 3 to " + MaxId + " characters.", "id"));
        else if (!RubricIdPattern.IsMatch(id))
            issues.Add(Issue("id", "Use lower-case letters and digits, in parts separated by a dot or a hyphen, such as piqi.core or bundle-checks-v1.", "id"));

        var semver = definition.Semver.Trim();
        if (semver.Length == 0)
            issues.Add(Issue("semver", "Enter a version.", "semver"));
        else if (!SemverPattern.IsMatch(semver) || semver.Length > MaxSemver)
            issues.Add(Issue("semver", "The version must be three numbers separated by dots, such as 1.2.0.", "semver"));
        else if (CompareVersions(semver, "0.0.0") == 0)
            issues.Add(Issue("semver", "0.0.0 cannot be used as a version.", "semver"));

        if ((definition.Title?.Trim().Length ?? 0) > MaxTitle)
            issues.Add(Issue("title", "The title can be at most " + MaxTitle + " characters.", "title"));
        if ((definition.Owner?.Trim().Length ?? 0) > MaxOwner)
            issues.Add(Issue("owner", "The owner can be at most " + MaxOwner + " characters.", "owner"));
        if (definition.Dimensions.Count == 0)
            issues.Add(Issue("dimensions", "Choose at least one dimension.", "dimensions"));
        if (string.IsNullOrEmpty(definition.ScoringPolicy?.Type))
            issues.Add(Issue("scoringPolicy.type", "Choose how the rubric is scored.", "scoringPolicy.type"));
        else if (RollupRequired(definition.ScoringPolicy.Type, settings) && string.IsNullOrEmpty(definition.ScoringPolicy.Rollup))
            issues.Add(Issue("scoringPolicy.rollup", "Choose a roll-up.", "scoringPolicy.rollup"));

        FindListIssues(issues, "applicableContext.fhirResources", definition.ApplicableContext?.FhirResources, maxContext, "resource type",
            value => settings?.FhirResourceTypes is { Count: > 0 } && !settings.FhirResourceTypes.Contains(value)
                ? "\"" + value + "\" is not a FHIR R4 resource type."
                : null);
        FindListIssues(issues, "applicableContext.workflowTags", definition.ApplicableContext?.WorkflowTags, maxContext, "workflow tag",
            value => value.Length > MaxWorkflowTag ? "A workflow tag can be at most " + MaxWorkflowTag + " characters." : null);

        if (definition.Checks.Count == 0)
            issues.Add(Issue("checks", "Add at least one check.", "checks"));
        else
        {
            if (definition.Checks.Count > maxChecks)
                issues.Add(Issue("checks", "A rubric can have at most " + maxChecks + " checks; this one has " + definition.Checks.Count + ".", "checks"));
            if (definition.Checks.All(check => check.Enabled == false))
                issues.Add(Issue("checks", "At least one check must be enabled.", "checks"));
        }

        var seen = new HashSet<string>(StringComparer.Ordinal);
        for (var index = 0; index < definition.Checks.Count; index++)
        {
            var check = definition.Checks[index];
            FindCheckIssues(issues, check, index, definition, settings, maxProfiles, maxCombined);
            var checkId = check.Id.Trim();
            if (checkId.Length == 0)
                continue;
            if (!seen.Add(checkId))
                issues.Add(Issue("checks[" + checkId + "]", "Another check already has the id \"" + checkId + "\".", "id", index));
        }

        return issues;
    }

    public static RubricIssue PlaceServerError(string error, RubricDefinition definition)
    {
        var separator = error.IndexOf(": ", StringComparison.Ordinal);
        var path = separator > 0 ? error[..separator] : "";
        var message = separator > 0 ? error[(separator + 2)..] : error;
        var check = Regex.Match(path, @"^checks\[([^\]]*)\](.*)$");
        if (!check.Success)
        {
            if (DetailFields.Contains(path) || path == "checks" || path.StartsWith("scoringPolicy", StringComparison.Ordinal) || path.StartsWith("applicableContext", StringComparison.Ordinal))
            {
                var field = Regex.Replace(path, @"\[\d+\]$", "");
                if (field == "scoringPolicy")
                    field = "scoringPolicy.type";
                return Issue(path, message, field);
            }

            return Issue(path, message, "");
        }

        var label = check.Groups[1].Value;
        var rest = check.Groups[2].Value;
        var checkIndex = FindCheck(definition, label);
        if (checkIndex is null)
            return Issue(path, message, "");
        return Issue(path, message, ServerCheckField(rest, message), checkIndex);
    }

    public static bool IdTakenMessage(string id, out RubricIssue issue)
    {
        issue = Issue("id", "A rubric with the id \"" + id + "\" already exists. Choose another id, or open that rubric and create a new version there.", "id");
        return true;
    }

    private static RubricCheck NormalizeCheck(RubricCheck check)
    {
        var normalized = new RubricCheck
        {
            Id = check.Id ?? "",
            Type = check.Type ?? "",
            Dimension = check.Dimension,
            SeverityOverride = check.SeverityOverride,
            Ordinal = check.Ordinal,
            Enabled = check.Enabled,
            Parameters = check.Parameters is null ? null : NormalizeParameters(check.Parameters)
        };
        return normalized;
    }

    private static RubricCheckParameters NormalizeParameters(RubricCheckParameters source)
    {
        var parameters = new RubricCheckParameters
        {
            Profiles = source.Profiles?.Where(item => item is not null).ToList(),
            Expression = source.Expression,
            FailureMessage = source.FailureMessage,
            Code = source.Code,
            ValidateCodings = source.ValidateCodings,
            ValueSetWhitelistRegex = source.ValueSetWhitelistRegex,
            Path = source.Path,
            ValueSet = source.ValueSet,
            System = source.System,
            MatchMode = source.MatchMode,
            CustomCheckId = source.CustomCheckId,
            ClassName = source.ClassName,
            Min = source.Min,
            Max = source.Max,
            Extra = source.Extra
        };
        if (source.Checks is not null)
            parameters.Checks = source.Checks.Select(NormalizeCheck).ToList();
        return parameters;
    }

    private static RubricCheck CleanCheck(RubricCheck check, int ordinal)
    {
        return new RubricCheck
        {
            Id = check.Id.Trim(),
            Type = check.Type,
            Dimension = check.Dimension,
            SeverityOverride = check.SeverityOverride,
            Ordinal = ordinal,
            Enabled = check.Enabled != false,
            Parameters = ReadOnlyReason(check) is not null
                ? check.Parameters ?? new RubricCheckParameters()
                : CleanParameters(check)
        };
    }

    private static RubricCheckParameters CleanParameters(RubricCheck check)
    {
        var source = check.Parameters ?? new RubricCheckParameters();
        var cleaned = new RubricCheckParameters();
        void Text(string? value, Action<string> write)
        {
            if (!string.IsNullOrWhiteSpace(value))
                write(value.Trim());
        }

        switch (CheckKind(check))
        {
            case "expression":
                Text(source.Expression, value => cleaned.Expression = value);
                Text(source.FailureMessage, value => cleaned.FailureMessage = value);
                Text(source.Code, value => cleaned.Code = value);
                break;
            case "valueset":
                Text(source.Path, value => cleaned.Path = value);
                Text(source.ValueSet, value => cleaned.ValueSet = value);
                Text(source.System, value => cleaned.System = value);
                Text(source.FailureMessage, value => cleaned.FailureMessage = value);
                Text(source.Code, value => cleaned.Code = value);
                break;
            case "combined":
                cleaned.MatchMode = source.MatchMode == "ALL" ? "ALL" : "ANY";
                Text(source.FailureMessage, value => cleaned.FailureMessage = value);
                Text(source.Code, value => cleaned.Code = value);
                cleaned.Checks = (source.Checks ?? []).Select(child => new RubricCheck
                {
                    Id = child.Id.Trim(),
                    Type = child.Type,
                    Parameters = CleanParameters(child)
                }).ToList();
                break;
            case "conformance":
                var profiles = TrimmedList(source.Profiles);
                if (profiles.Count > 0)
                    cleaned.Profiles = profiles;
                break;
            case "terminology":
                if (source.ValidateCodings is not null)
                    cleaned.ValidateCodings = source.ValidateCodings;
                Text(source.ValueSetWhitelistRegex, value => cleaned.ValueSetWhitelistRegex = value);
                break;
            default:
                return source;
        }

        return cleaned;
    }

    private static void FindListIssues(List<RubricIssue> issues, string path, IReadOnlyList<string>? values, int max, string noun, Func<string, string?> each)
    {
        var list = TrimmedList(values);
        if (list.Count > max)
            issues.Add(Issue(path, "At most " + max + " " + noun + "s can be listed.", path));
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var value in list)
        {
            if (!seen.Add(value))
                issues.Add(Issue(path, "\"" + value + "\" is listed more than once.", path));
            var problem = each(value);
            if (problem is not null)
                issues.Add(Issue(path, problem, path));
        }
    }

    private static void FindCheckIssues(List<RubricIssue> issues, RubricCheck check, int index, RubricDefinition definition, RubricSettings? settings, int maxProfiles, int maxCombined)
    {
        if (ReadOnlyReason(check) is not null)
            return;
        var label = check.Id.Trim().Length > 0 ? check.Id.Trim() : "#" + (index + 1);
        void Add(string field, string message)
        {
            var suffix = field.Length == 0 ? "" : "." + field;
            issues.Add(Issue("checks[" + label + "]" + suffix, message, field, index));
        }

        if (check.Id.Trim().Length == 0)
            Add("id", "Enter an id for the check.");
        else if (check.Id.Trim().Length > MaxId)
            Add("id", "The id can be at most " + MaxId + " characters.");
        if (string.IsNullOrEmpty(check.SeverityOverride))
            Add("severityOverride", "Choose a severity.");
        if (string.IsNullOrEmpty(check.Dimension))
            Add("dimension", "Choose a dimension.");
        else
        {
            var required = RequiredDimension(check.Type, settings);
            if (required is not null && check.Dimension != required)
                Add("dimension", "A check of this type must use the " + required.ToLowerInvariant() + " dimension.");
            else if (!definition.Dimensions.Contains(check.Dimension))
                Add("dimension", "The rubric does not include the " + check.Dimension.ToLowerInvariant() + " dimension. Add it on the Details tab, or choose another.");
        }

        FindParameterIssues(check, maxProfiles, maxCombined, Add, "");
    }

    private static void FindParameterIssues(RubricCheck check, int maxProfiles, int maxCombined, Action<string, string> add, string prefix)
    {
        var parameters = check.Parameters ?? new RubricCheckParameters();
        string Field(string name) => prefix + "parameters." + name;
        string Text(string? value) => value?.Trim() ?? "";

        void Code()
        {
            if (Text(parameters.Code).Length > MaxCode)
                add(Field("code"), "The code can be at most " + MaxCode + " characters.");
        }

        switch (CheckKind(check))
        {
            case "expression":
                if (Text(parameters.Expression).Length == 0)
                    add(Field("expression"), "Enter a FHIRPath expression.");
                Code();
                break;
            case "valueset":
                if (Text(parameters.Path).Length == 0)
                    add(Field("path"), "Enter the path to the coded element.");
                if (Text(parameters.ValueSet).Length == 0)
                    add(Field("valueSet"), "Enter the value set.");
                else if (!CanonicalPattern.IsMatch(Text(parameters.ValueSet)))
                    add(Field("valueSet"), "The value set must be a full URL, optionally followed by |version.");
                if (Text(parameters.System).Length > 0 && !CanonicalPattern.IsMatch(Text(parameters.System)))
                    add(Field("system"), "The code system must be a full URL, optionally followed by |version.");
                break;
            case "combined":
                var children = parameters.Checks ?? [];
                if (children.Count == 0)
                    add(Field("checks"), "Add at least one check to combine.");
                else if (children.Count > maxCombined)
                    add(Field("checks"), "At most " + maxCombined + " checks can be combined.");
                Code();
                var seen = new HashSet<string>(StringComparer.Ordinal);
                for (var childIndex = 0; childIndex < children.Count; childIndex++)
                {
                    var child = children[childIndex];
                    var childPrefix = prefix + "parameters.checks[" + childIndex + "].";
                    var childId = child.Id.Trim();
                    if (childId.Length == 0)
                        add(childPrefix + "id", "Enter an id for this check.");
                    else if (childId.Length > MaxId)
                        add(childPrefix + "id", "The id can be at most " + MaxId + " characters.");
                    else if (!seen.Add(childId))
                        add(childPrefix + "id", "Another check in this group already has the id \"" + childId + "\".");
                    FindParameterIssues(child, maxProfiles, maxCombined, add, childPrefix);
                }

                break;
            case "conformance":
                var profiles = TrimmedList(parameters.Profiles);
                if (profiles.Count > maxProfiles)
                    add(Field("profiles"), "At most " + maxProfiles + " profiles can be listed.");
                var profileSeen = new HashSet<string>(StringComparer.Ordinal);
                foreach (var profile in profiles)
                {
                    if (!CanonicalPattern.IsMatch(profile))
                        add(Field("profiles"), "\"" + profile + "\" is not a full URL.");
                    else if (!profileSeen.Add(profile))
                        add(Field("profiles"), "\"" + profile + "\" is listed more than once.");
                }

                break;
            case "terminology":
                var pattern = Text(parameters.ValueSetWhitelistRegex);
                if (pattern.Length > MaxRegex)
                    add(Field("valueSetWhitelistRegex"), "The pattern can be at most " + MaxRegex + " characters.");
                else if (pattern.Length > 0)
                {
                    try
                    {
                        _ = new Regex(pattern, RegexOptions.None, TimeSpan.FromMilliseconds(250));
                    }
                    catch (ArgumentException)
                    {
                        add(Field("valueSetWhitelistRegex"), "This is not a valid regular expression.");
                    }
                }

                break;
        }
    }

    private static int? FindCheck(RubricDefinition definition, string label)
    {
        var byId = definition.Checks.FindIndex(check => check.Id == label);
        if (byId >= 0)
            return byId;
        var byParent = definition.Checks.FindIndex(check => check.Id.Length > 0 && label.StartsWith(check.Id + ".", StringComparison.Ordinal));
        if (byParent >= 0)
            return byParent;
        if (int.TryParse(label, out var position) && position >= 0 && position < definition.Checks.Count && label.All(char.IsDigit))
            return position;
        return null;
    }

    private static string ServerCheckField(string rest, string message)
    {
        var named = Regex.Replace(rest.TrimStart('.'), @"\[\d+\]$", "");
        if (named.Length > 0 && named != "parameters")
            return named;
        var required = Regex.Match(message, @"requires parameters\.(\w+)");
        if (required.Success)
            return "parameters." + required.Groups[1].Value;
        if (message.Contains("FHIRPath expression", StringComparison.OrdinalIgnoreCase))
            return "parameters.expression";
        var leading = Regex.Match(message, @"^(id|type|dimension|severityOverride|ordinal)\b");
        if (leading.Success)
            return leading.Groups[1].Value;
        if (message.Contains("duplicate check id", StringComparison.Ordinal))
            return "id";
        return "";
    }

    private static RubricIssue Issue(string path, string message, string field, int? checkIndex = null) =>
        new(path, message, field, checkIndex);

    private static List<string> TrimmedList(IReadOnlyList<string>? values) =>
        (values ?? []).Select(value => value.Trim()).Where(value => value.Length > 0).ToList();

    private static int Limit(int? value, int fallback) => value is > 0 ? value.Value : fallback;

    private static (int Major, int Minor, int Patch)? ParseVersion(string semver)
    {
        var match = Regex.Match(semver.Trim(), @"^(\d+)\.(\d+)\.(\d+)$");
        if (!match.Success)
            return null;
        return (int.Parse(match.Groups[1].Value), int.Parse(match.Groups[2].Value), int.Parse(match.Groups[3].Value));
    }
}
