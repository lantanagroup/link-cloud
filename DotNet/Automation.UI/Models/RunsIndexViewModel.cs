namespace Automation.UI.Models;

/// <summary>
/// ViewModel for the Runs/Index page that includes both run history and available scenarios.
/// </summary>
public class RunsIndexViewModel
{
    public AutomationRunIndexViewModel Runs { get; set; } = new();
    public List<TestScenarioDefinition> SavedScenarios { get; set; } = [];
    public List<ClinicalScenarioInfo> ClinicalScenarios { get; set; } = [];
    public List<QueryPlanTemplate> QueryPlanTemplates { get; set; } = [];
}
