using System.Text.Json;
using System.Text.Json.Serialization;

namespace Link.UI.Models;

public static class RubricJson
{
    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public static string Write<T>(T value) => JsonSerializer.Serialize(value, Options);

    public static T? Read<T>(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return default;
        return JsonSerializer.Deserialize<T>(json, Options);
    }
}

public sealed class RubricEnvelope<T>
{
    public int Status { get; set; }
    public string? Message { get; set; }
    public T? Data { get; set; }
    public List<string>? Errors { get; set; }
}

public sealed class RubricPage<T>
{
    public List<T> Content { get; set; } = [];
    public int Page { get; set; }
    public int Size { get; set; }
    public long TotalElements { get; set; }
    public int TotalPages { get; set; }
}

public sealed class RubricScoringPolicy
{
    public string Type { get; set; } = "";
    public string? Rollup { get; set; }
}

public sealed class RubricApplicableContext
{
    public List<string>? FhirResources { get; set; }
    public List<string>? WorkflowTags { get; set; }
}

public sealed class RubricCheckParameters
{
    public List<string>? Profiles { get; set; }
    public string? Expression { get; set; }
    public string? FailureMessage { get; set; }
    public string? Code { get; set; }
    public bool? ValidateCodings { get; set; }
    public string? ValueSetWhitelistRegex { get; set; }
    public string? Path { get; set; }
    public string? ValueSet { get; set; }
    public string? System { get; set; }
    public string? MatchMode { get; set; }
    public List<RubricCheck>? Checks { get; set; }
    public string? CustomCheckId { get; set; }
    public string? ClassName { get; set; }
    public JsonElement? Min { get; set; }
    public JsonElement? Max { get; set; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? Extra { get; set; }
}

public sealed class RubricCheck
{
    public string Id { get; set; } = "";
    public string Type { get; set; } = "";
    public string? Dimension { get; set; }
    public string? SeverityOverride { get; set; }
    public int? Ordinal { get; set; }
    public bool? Enabled { get; set; }
    public RubricCheckParameters? Parameters { get; set; }
}

public sealed class RubricDefinition
{
    public string Id { get; set; } = "";
    public string Semver { get; set; } = "";
    public List<string> Dimensions { get; set; } = [];
    public RubricScoringPolicy ScoringPolicy { get; set; } = new();
    public List<RubricCheck> Checks { get; set; } = [];
    public string? Title { get; set; }
    public string? Owner { get; set; }
    public RubricApplicableContext? ApplicableContext { get; set; }
}

public sealed class RubricVersionSummary
{
    public long RubricVersionId { get; set; }
    public string RubricId { get; set; } = "";
    public string Semver { get; set; } = "";
    public string Status { get; set; } = "";
    public string? Checksum { get; set; }
    public List<string>? Dimensions { get; set; }
    public RubricApplicableContext? ApplicableContext { get; set; }
    public RubricScoringPolicy? ScoringPolicy { get; set; }
    public DateTimeOffset? CreatedAt { get; set; }
    public string? CreatedBy { get; set; }
    public DateTimeOffset? PublishedAt { get; set; }
    public string? PublishedBy { get; set; }
    public DateTimeOffset? RetiredAt { get; set; }
    public string? RetiredBy { get; set; }
    public DateTimeOffset? DryRunCompletedAt { get; set; }
    public string? DryRunStatus { get; set; }
    public int? CheckCount { get; set; }
}

public class RubricSummary
{
    public string RubricId { get; set; } = "";
    public string? Title { get; set; }
    public string? Owner { get; set; }
    public List<RubricVersionSummary>? Versions { get; set; }
    public DateTimeOffset? CreatedAt { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
}

public sealed class RubricDetail : RubricSummary
{
    public string? LatestPublishedSemver { get; set; }
}

public sealed class RubricVersionDetail
{
    public long RubricVersionId { get; set; }
    public string RubricId { get; set; } = "";
    public string Semver { get; set; } = "";
    public string Status { get; set; } = "";
    public string? Checksum { get; set; }
    public RubricDefinition? Definition { get; set; }
    public DateTimeOffset? CreatedAt { get; set; }
    public string? CreatedBy { get; set; }
    public DateTimeOffset? PublishedAt { get; set; }
    public string? PublishedBy { get; set; }
    public DateTimeOffset? RetiredAt { get; set; }
    public string? RetiredBy { get; set; }
    public DateTimeOffset? DryRunCompletedAt { get; set; }
    public string? DryRunStatus { get; set; }
}

public sealed class RubricLifecycleEvent
{
    public string? EventId { get; set; }
    public string? Semver { get; set; }
    public string? Action { get; set; }
    public string? Actor { get; set; }
    public string? Checksum { get; set; }
    public DateTimeOffset? OccurredAt { get; set; }
}

public sealed class RubricCheckTypeSetting
{
    public string Type { get; set; } = "";
    public string? Dimension { get; set; }
    public List<string>? Parameters { get; set; }
    public bool Supported { get; set; }
}

public sealed class RubricScoringPolicySetting
{
    public string Type { get; set; } = "";
    public bool RollupRequired { get; set; }
}

public sealed class RubricSettings
{
    public bool DryRunRequiredForPublish { get; set; }
    public string? PipelineRubricId { get; set; }
    public bool PipelineEnabled { get; set; }
    public bool PipelineShadowEnabled { get; set; }
    public int MaxChecks { get; set; }
    public int MaxPayloadBytes { get; set; }
    public int MaxProfiles { get; set; }
    public int MaxCombinatorChecks { get; set; }
    public int MaxCombinatorDepth { get; set; }
    public int MaxApplicableContextEntries { get; set; }
    public List<RubricCheckTypeSetting>? CheckTypes { get; set; }
    public List<string>? Dimensions { get; set; }
    public List<string>? Severities { get; set; }
    public List<RubricScoringPolicySetting>? ScoringPolicies { get; set; }
    public List<string>? Rollups { get; set; }
    public List<string>? MatchModes { get; set; }
    public List<string>? FhirResourceTypes { get; set; }
}

public sealed class RubricValidationResult
{
    public bool Valid { get; set; }
    public List<string>? Errors { get; set; }
    public RubricDefinition? Definition { get; set; }
    public string? Checksum { get; set; }
    public bool? RubricExists { get; set; }
    public string? ExistingStatus { get; set; }
    public string? ExistingChecksum { get; set; }
}

public sealed class RubricFinding
{
    public string? CheckId { get; set; }
    public string? Dimension { get; set; }
    public string? Severity { get; set; }
    public string? Code { get; set; }
    public string? Message { get; set; }
    public string? Location { get; set; }
    public string? Expression { get; set; }
    public string? OriginalSeverity { get; set; }
    public string? OverriddenSeverity { get; set; }
}

public sealed class RubricScoreCard
{
    public string? Interpretation { get; set; }
    public Dictionary<string, string>? ByDimension { get; set; }
    public Dictionary<string, string>? ByCheck { get; set; }
    public double? Value { get; set; }
}

public sealed class RubricRunSummary
{
    public int ErrorCount { get; set; }
    public int WarningCount { get; set; }
    public int InformationCount { get; set; }
}

public sealed class RubricRunTrace
{
    public long DurationMs { get; set; }
    public Dictionary<string, double>? CheckDurationsMs { get; set; }
}

public sealed class RubricRunResult
{
    public string? RubricId { get; set; }
    public long? RubricVersionId { get; set; }
    public string? Status { get; set; }
    public RubricScoreCard? Score { get; set; }
    public RubricRunSummary? Summary { get; set; }
    public List<RubricFinding>? Findings { get; set; }
    public RubricRunTrace? Trace { get; set; }
}

public sealed record RubricIssue(string Path, string Message, string Field, int? CheckIndex = null);

public sealed class RubricListPage
{
    public bool Configured { get; init; }
    public string? LoadError { get; init; }
    public IReadOnlyList<RubricSummary> Rows { get; init; } = [];
    public IReadOnlyList<string> Owners { get; init; } = [];
    public RubricSettings? Settings { get; init; }
    public string Search { get; init; } = "";
    public string Status { get; init; } = "";
    public string Owner { get; init; } = "";
    public string SortBy { get; init; } = "rubricId";
    public string SortDirection { get; init; } = "asc";
    public BottomPager? Pager { get; init; }
    public bool Filtered => Search.Length > 0 || Status.Length > 0 || Owner.Length > 0;
}

public sealed class RubricDetailPage
{
    public bool Configured { get; init; }
    public string? LoadError { get; init; }
    public bool NotFound { get; init; }
    public RubricDetail? Rubric { get; init; }
    public IReadOnlyList<RubricLifecycleEvent> Events { get; init; } = [];
    public string? EventsError { get; init; }
    public RubricSettings? Settings { get; init; }
    public string? ReturnUrl { get; init; }
}

public sealed class RubricVersionPage
{
    public bool Configured { get; init; }
    public string? LoadError { get; init; }
    public bool NotFound { get; init; }
    public string RubricId { get; init; } = "";
    public RubricVersionDetail? Version { get; init; }
    public RubricDetail? Rubric { get; init; }
    public RubricSettings? Settings { get; init; }
    public string? FocusAction { get; init; }
}

public sealed class RubricEditorPage
{
    public bool Configured { get; init; }
    public string? LoadError { get; init; }
    public bool NotFound { get; init; }
    public string Mode { get; init; } = "new";
    public string? Locked { get; init; }
    public RubricDefinition Definition { get; init; } = new();
    public RubricDefinition Baseline { get; init; } = new();
    public string? SavedChecksum { get; init; }
    public string? SavedSemver { get; init; }
    public IReadOnlyList<string> ExistingVersions { get; init; } = [];
    public RubricSettings? Settings { get; init; }
    public IReadOnlyList<RubricIssue> Issues { get; init; } = [];
    public bool ShowIssues { get; init; }
    public string? Notice { get; init; }
    public string? Failure { get; init; }
    public bool StaleDraft { get; init; }
    public int EditIndex { get; init; } = -1;
    public string RubricId { get; init; } = "";
    public string RouteSemver { get; init; } = "";
}

public sealed record RubricImportPage
{
    public bool Configured { get; init; }
    public string? LoadError { get; init; }
    public string Text { get; init; } = "";
    public string? FileName { get; init; }
    public string? FileError { get; init; }
    public string? Failure { get; init; }
    public RubricValidationResult? Review { get; init; }
    public string? Effect { get; init; }
}

public sealed class RubricComparePage
{
    public bool Configured { get; init; }
    public string? LoadError { get; init; }
    public bool NotFound { get; init; }
    public string RubricId { get; init; } = "";
    public RubricDetail? Rubric { get; init; }
    public string From { get; init; } = "";
    public string To { get; init; } = "";
    public RubricVersionDetail? Before { get; init; }
    public RubricVersionDetail? After { get; init; }
    public RubricComparison? Comparison { get; init; }
}

public sealed record RubricDryRunPage
{
    public bool Configured { get; init; }
    public string? LoadError { get; init; }
    public bool NotFound { get; init; }
    public string RubricId { get; init; } = "";
    public string Semver { get; init; } = "";
    public RubricVersionDetail? Version { get; init; }
    public string? SampleError { get; init; }
    public string? SampleDescription { get; init; }
    public string? Failure { get; init; }
    public RubricRunResult? Result { get; init; }
    public string SampleText { get; init; } = "";
}

public sealed class RubricCheckPost
{
    public string? Id { get; init; }
    public string? Type { get; init; }
    public string? Dimension { get; init; }
    public string? Severity { get; init; }
    public bool Enabled { get; init; } = true;
    public string? Expression { get; init; }
    public string? FailureMessage { get; init; }
    public string? Code { get; init; }
    public string? Profiles { get; init; }
    public bool ValidateCodings { get; init; }
    public bool ValidateCodingsPosted { get; init; }
    public string? Whitelist { get; init; }
    public string? Path { get; init; }
    public string? ValueSet { get; init; }
    public string? System { get; init; }
    public string? MatchMode { get; init; }
    public string[] ChildId { get; init; } = [];
    public string[] ChildType { get; init; } = [];
    public string[] ChildExpression { get; init; } = [];
    public string[] ChildPath { get; init; } = [];
    public string[] ChildValueSet { get; init; } = [];
    public string[] ChildSystem { get; init; } = [];
    public string[] ChildFailure { get; init; } = [];
    public string[] ChildCode { get; init; } = [];
}

public sealed class RubricEditorPost
{
    public string Intent { get; init; } = "";
    public string DefinitionJson { get; init; } = "";
    public string BaselineJson { get; init; } = "";
    public string? SavedChecksum { get; init; }
    public string? SavedSemver { get; init; }
    public string Mode { get; init; } = "new";
    public string ExistingVersions { get; init; } = "";
    public string? Id { get; init; }
    public string? Semver { get; init; }
    public string? Title { get; init; }
    public string? Owner { get; init; }
    public IReadOnlyList<string> Dimensions { get; init; } = [];
    public string? ScoringType { get; init; }
    public string? Rollup { get; init; }
    public string? ResourceType { get; init; }
    public string? WorkflowTag { get; init; }
    public string? PasteText { get; init; }
    public string? AddType { get; init; }
    public bool CheckOpen { get; init; }
    public int EditIndex { get; init; } = -1;
    public RubricCheckPost Check { get; init; } = new();
    public string RouteRubricId { get; init; } = "";
    public string RouteSemver { get; init; } = "";
}

public sealed record RubricChange(string Label, string Before, string After);

public sealed record RubricCheckChange(string Id, IReadOnlyList<RubricChange> Changes);

public sealed class RubricComparison
{
    public IReadOnlyList<RubricChange> Settings { get; init; } = [];
    public IReadOnlyList<RubricCheck> Added { get; init; } = [];
    public IReadOnlyList<RubricCheck> Removed { get; init; } = [];
    public IReadOnlyList<RubricCheckChange> Changed { get; init; } = [];
    public int UnchangedCount { get; init; }
    public bool Reordered { get; init; }
    public bool Identical { get; init; }
}

public sealed class SampleReading
{
    public JsonElement? Sample { get; init; }
    public string? Description { get; init; }
    public string? Error { get; init; }
}
