using Automation.UI.Models;
using LantanaGroup.Automation.Generation;
using LantanaGroup.Link.Automation.Link.Helpers;
using LantanaGroup.Link.Automation.Link.Models;

namespace Automation.UI.Services;

public interface IAutomationRunManager
{
    Task<Guid> StartAsync(StartScenarioRequest request, CancellationToken cancellationToken = default);
    Task<bool> CancelRunAsync(Guid runId, CancellationToken cancellationToken = default);
    Task<AutomationRunIndexViewModel> GetRunsPageAsync(int pageNumber = 1, int pageSize = 20, string? sortBy = null, bool sortDescending = true, CancellationToken cancellationToken = default);
    Task<AutomationRunSummary?> GetRunAsync(Guid runId, CancellationToken cancellationToken = default);
    Task<bool> DeleteRunAsync(Guid runId, CancellationToken cancellationToken = default);
    Task<PipelineSummarySnapshotBuilder.PipelineSummarySnapshot?> GetPipelineSnapshotAsync(Guid runId, CancellationToken cancellationToken = default);
    Task<GenerationManifestSnapshot?> GetGenerationManifestAsync(Guid runId, CancellationToken cancellationToken = default);
    Task<AbsUploadSnapshot?> GetAbsUploadSnapshotAsync(Guid runId, CancellationToken cancellationToken = default);
    Task<RunDashboardStats> GetDashboardStatsAsync(CancellationToken cancellationToken = default);
}
