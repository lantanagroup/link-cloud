using Automation.UI.Models;
using Automation.UI.Services;
using LantanaGroup.Link.Automation.Link.Models;

namespace Link.UI.Services.ReportBatches;

internal sealed class AutomationRunManagerBatchGateway(IAutomationRunManager runs) : IReportBatchRunGateway
{
    public Task<Guid> StartAsync(StartScenarioRequest request, CancellationToken cancellationToken = default)
        => runs.StartAsync(request, cancellationToken);

    public Task<bool> CancelRunAsync(Guid runId, CancellationToken cancellationToken = default)
        => runs.CancelRunAsync(runId, cancellationToken);

    public Task<AutomationRunSummary?> GetRunAsync(Guid runId, CancellationToken cancellationToken = default)
        => runs.GetRunAsync(runId, cancellationToken);
}

internal sealed class CatalogReportBatchEhrLookup(EhrSystemCatalog catalog) : IReportBatchEhrLookup
{
    public string? CanonicalName(string? requestedName)
    {
        if (string.IsNullOrWhiteSpace(requestedName))
            return null;

        return catalog.Find(requestedName.Trim())?.Name;
    }
}
