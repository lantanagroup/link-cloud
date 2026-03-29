using Automation.UI.Models;

namespace Automation.UI.Services;

public interface IAutomationRunManager
{
    Task<Guid> StartAsync(StartScenarioRequest request, CancellationToken cancellationToken = default);
    IReadOnlyList<AutomationRunSummary> GetRuns();
    AutomationRunSummary? GetRun(Guid runId);
}
