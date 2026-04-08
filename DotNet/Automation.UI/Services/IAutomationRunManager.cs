using Automation.UI.Models;
using Automation.UI.Models;
using LantanaGroup.Link.Automation.Link.Helpers;

namespace Automation.UI.Services;

public interface IAutomationRunManager
{
    Task<Guid> StartAsync(StartScenarioRequest request, CancellationToken cancellationToken = default);
    Task<AutomationRunIndexViewModel> GetRunsPageAsync(int pageNumber = 1, int pageSize = 20, CancellationToken cancellationToken = default);
    Task<AutomationRunSummary?> GetRunAsync(Guid runId, CancellationToken cancellationToken = default);
    Task<bool> DeleteRunAsync(Guid runId, CancellationToken cancellationToken = default);
    Task<PipelineSummarySnapshotBuilder.PipelineSummarySnapshot?> GetPipelineSnapshotAsync(Guid runId, CancellationToken cancellationToken = default);
}
