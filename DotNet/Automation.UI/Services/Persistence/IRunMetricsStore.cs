namespace Automation.UI.Services.Persistence;

public interface IRunMetricsStore
{
    Task UpsertAsync(AutomationRunMetricsDocument document, CancellationToken cancellationToken = default);
    Task<AutomationRunMetricsDocument?> GetAsync(Guid runId, CancellationToken cancellationToken = default);
}
