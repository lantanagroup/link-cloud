using Automation.UI.Models;
using LantanaGroup.Link.Automation.Link.Models;

namespace Automation.UI.Services;

/// <summary>
/// One confirmed suggestion for a failed run. Further rules return the same shape.
/// </summary>
public sealed class SuggestedWorkaround
{
    public string RuleId { get; init; } = string.Empty;
    public string Title { get; init; } = "Suggested workaround";
    public string Cause { get; init; } = string.Empty;
    public List<SuggestedWorkaroundCount> Counts { get; init; } = [];
    public string? OrganizationMapName { get; init; }
    public string? OrganizationMapsUrl { get; init; }
    public List<SuggestedWorkaroundCondition> Conditions { get; init; } = [];
    public string? ConditionsNote { get; init; }
    public List<string> Steps { get; init; } = [];
}

public sealed class SuggestedWorkaroundCount
{
    public string Label { get; init; } = string.Empty;
    public int Value { get; init; }
}

public sealed class SuggestedWorkaroundCondition
{
    public int Priority { get; init; }
    public string FhirPath { get; init; } = string.Empty;
}

public sealed class SuggestedWorkaroundInput
{
    public AutomationRunStatus Status { get; init; }
    public OrgLocationSummary? Summary { get; init; }
    public string? AppliedMapName { get; init; }
    public OrganizationResourceMapTemplate? Map { get; init; }
    public string? OrganizationMapsUrl { get; init; }
}

/// <summary>
/// Picks at most one suggestion from the run's stored diagnostics.
/// Rules are tried in order. The first confirmed rule wins.
/// </summary>
public static class SuggestedWorkaroundBuilder
{
    public const string OrgMappingExcludedAllRuleId = "org-mapping-excluded-all-encounters";

    private static readonly Func<SuggestedWorkaroundInput, SuggestedWorkaround?>[] Rules =
    [
        TryOrgMappingExcludedAll
    ];

    public static SuggestedWorkaround? Match(SuggestedWorkaroundInput input)
    {
        foreach (var rule in Rules)
        {
            var match = rule(input);
            if (match != null)
                return match;
        }

        return null;
    }

    /// <summary>
    /// Prefers the counts the poller stored. Older runs stored the row lists instead,
    /// and those lists are rolled up the same way.
    /// </summary>
    public static OrgLocationSummary? ResolveOrgLocation(
        OrgLocationSummary? stored,
        StoreBackedServicePoller.OrgLocationSnapshot? legacy)
    {
        if (stored != null && HasOrgLocationRows(stored))
            return stored;

        if (legacy != null)
            return RunHistorySlim.SlimOrgLocation(legacy);

        return stored;
    }

    /// <summary>
    /// Same confirmation the diagnostics export already prints: organization mapping
    /// is active, at least one encounter was found, and none of them are MappedToOrg.
    /// </summary>
    public static bool OrgMappingExcludedAllEncounters(OrgLocationSummary? summary)
        => summary is { ActiveConfigurationCount: > 0, EncounterMappingCount: > 0, MappedToOrgCount: 0 };

    private static bool HasOrgLocationRows(OrgLocationSummary summary)
        => summary.ConfigurationCount + summary.LocationMappingCount + summary.EncounterMappingCount > 0;

    private static SuggestedWorkaround? TryOrgMappingExcludedAll(SuggestedWorkaroundInput input)
    {
        if (input.Status != AutomationRunStatus.Failed)
            return null;

        if (!OrgMappingExcludedAllEncounters(input.Summary))
            return null;

        var summary = input.Summary!;
        var mapName = FirstNonBlank(input.AppliedMapName, input.Map?.Name);
        var conditions = (input.Map?.Conditions ?? [])
            .Where(condition => !string.IsNullOrWhiteSpace(condition.FhirPath))
            .OrderBy(condition => condition.Priority)
            .Select(condition => new SuggestedWorkaroundCondition
            {
                Priority = condition.Priority,
                FhirPath = condition.FhirPath.Trim()
            })
            .ToList();

        string? conditionsNote = null;
        if (conditions.Count > 0)
        {
            conditionsNote = "These are the conditions saved on the organization resource map now. The run stores the map name and the encounter counts, not a separate copy of the conditions.";
        }
        else if (!string.IsNullOrWhiteSpace(mapName))
        {
            conditionsNote = $"The organization resource map '{mapName}' is not available, so its conditions are not shown.";
        }

        return new SuggestedWorkaround
        {
            RuleId = OrgMappingExcludedAllRuleId,
            Cause = "Organization mapping excluded every encounter. Data Acquisition strips encounters that are not mapped to the organization before MeasureEval, which leaves an empty Initial Population and no ABS patient files.",
            OrganizationMapName = mapName,
            OrganizationMapsUrl = string.IsNullOrWhiteSpace(input.OrganizationMapsUrl) ? null : input.OrganizationMapsUrl,
            Conditions = conditions,
            ConditionsNote = conditionsNote,
            Steps =
            [
                "Open Organization Resource Maps and update the conditions so this facility's locations match an active organization mapping.",
                "Rerun the scenario. This suggestion does not change the map or start a run."
            ],
            Counts =
            [
                new SuggestedWorkaroundCount { Label = "Encounters found", Value = summary.EncounterMappingCount },
                new SuggestedWorkaroundCount { Label = "Excluded by organization mapping", Value = summary.EncounterMappingCount - summary.MappedToOrgCount },
                new SuggestedWorkaroundCount { Label = "Mapped to organization", Value = summary.MappedToOrgCount },
                new SuggestedWorkaroundCount { Label = "Organization locations", Value = summary.OrgLocationMappingCount },
                new SuggestedWorkaroundCount { Label = "Location mappings", Value = summary.LocationMappingCount },
                new SuggestedWorkaroundCount { Label = "Active mapping configurations", Value = summary.ActiveConfigurationCount }
            ]
        };
    }

    private static string? FirstNonBlank(params string?[] values)
    {
        foreach (var value in values)
        {
            if (!string.IsNullOrWhiteSpace(value))
                return value.Trim();
        }

        return null;
    }
}
