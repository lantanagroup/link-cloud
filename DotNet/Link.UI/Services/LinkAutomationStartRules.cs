using System.Text.RegularExpressions;
using Automation.UI.Models;
using LantanaGroup.Link.Automation.Link.Models;
using LantanaGroup.Link.Shared.Application.Services.Security;
using Link.UI.Models;

namespace Link.UI.Services;

/// <summary>
/// Maps the new-run form onto <see cref="StartScenarioRequest"/>.
/// Built-in kinds are the engine's non-custom scenarios. A saved scenario id
/// is loaded and passed through <see cref="StartScenarioRequest.FromScenario"/>.
/// </summary>
public static class LinkAutomationStartRules
{
    public const int ScenarioListLimit = 500;

    public const int RunNameMaxLength = 120;

    public static IReadOnlyList<AutomationStartChoice> BuiltInChoices { get; } =
    [
        new() { Value = "kind:AdhocReportTest", Label = "Adhoc report test (1 patient)" },
        new() { Value = "kind:MultiPatientTest", Label = "Multi-patient test (1000 patients)" },
        new() { Value = "kind:MegaPatientTest", Label = "Mega patient test" }
    ];

    public static bool TryParseChoice(
        string? choice,
        out AutomationScenarioKind? kind,
        out Guid scenarioId,
        out string? error)
    {
        kind = null;
        scenarioId = Guid.Empty;
        var text = (choice ?? string.Empty).Sanitize().Trim();
        if (text.Length == 0)
        {
            error = "Select a run.";
            return false;
        }

        const string kindPrefix = "kind:";
        const string scenarioPrefix = "scenario:";
        if (text.StartsWith(kindPrefix, StringComparison.Ordinal))
        {
            var name = text[kindPrefix.Length..];
            if (!Enum.TryParse(name, ignoreCase: false, out AutomationScenarioKind parsed)
                || !BuiltInChoices.Any(item => string.Equals(item.Value, $"{kindPrefix}{parsed}", StringComparison.Ordinal)))
            {
                error = "That run type is not supported.";
                return false;
            }

            kind = parsed;
            error = null;
            return true;
        }

        if (text.StartsWith(scenarioPrefix, StringComparison.Ordinal)
            && Guid.TryParse(text[scenarioPrefix.Length..], out scenarioId)
            && scenarioId != Guid.Empty)
        {
            error = null;
            return true;
        }

        error = text.StartsWith(scenarioPrefix, StringComparison.Ordinal)
            ? "Scenario id is not a valid id."
            : "Select a run.";
        return false;
    }

    public static bool TryRunName(string? value, out string? runName, out string? error)
    {
        var text = Regex.Replace((value ?? string.Empty).Sanitize(), "<[^>]+>", string.Empty).Trim();
        if (text.Length == 0)
        {
            runName = null;
            error = null;
            return true;
        }

        if (text.Length > RunNameMaxLength)
        {
            runName = null;
            error = $"Name must be {RunNameMaxLength} characters or fewer.";
            return false;
        }

        runName = text;
        error = null;
        return true;
    }

    public static StartScenarioRequest BuildKindRequest(AutomationScenarioKind kind, string? runName) =>
        new()
        {
            Scenario = kind,
            ScenarioName = runName,
            ReportMethod = ReportMethod.Adhoc
        };

    public static IReadOnlyList<AutomationScenarioChoice> OrderScenarios(IEnumerable<AutomationScenarioChoice> scenarios) =>
        scenarios
            .Where(scenario => scenario.Id != Guid.Empty)
            .GroupBy(scenario => scenario.Id)
            .Select(group => group.First())
            .OrderBy(scenario => scenario.IsSystemScenario)
            .ThenBy(
                scenario => string.IsNullOrWhiteSpace(scenario.Name) ? scenario.Id.ToString() : scenario.Name.Trim(),
                StringComparer.OrdinalIgnoreCase)
            .Take(ScenarioListLimit)
            .ToList();

    public static string ScenarioLabel(AutomationScenarioChoice scenario)
    {
        var name = string.IsNullOrWhiteSpace(scenario.Name) ? scenario.Id.ToString() : scenario.Name.Trim();
        return scenario.IsSystemScenario ? $"{name} (System)" : name;
    }

    public static string ScenarioChoiceValue(Guid id) => $"scenario:{id:D}";

    public static string ExplainStartFailure(Exception exception)
    {
        if (exception is InvalidOperationException && !string.IsNullOrWhiteSpace(exception.Message))
        {
            var text = exception.Message.Sanitize().Trim();
            if (text.Length > 500)
                text = text[..500];
            if (text.Length > 0)
                return text;
        }

        return "The run could not be started.";
    }
}
