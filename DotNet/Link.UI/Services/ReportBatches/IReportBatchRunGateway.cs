using Automation.UI.Models;
using LantanaGroup.Link.Automation.Link.Models;

namespace Link.UI.Services.ReportBatches;

/// <summary>
/// The existing run start path. A batch does not start runs any other way.
/// </summary>
public interface IReportBatchRunGateway
{
    Task<Guid> StartAsync(StartScenarioRequest request, CancellationToken cancellationToken = default);

    Task<bool> CancelRunAsync(Guid runId, CancellationToken cancellationToken = default);

    Task<AutomationRunSummary?> GetRunAsync(Guid runId, CancellationToken cancellationToken = default);
}

public interface IReportBatchEhrLookup
{
    /// <summary>Configured name, or null when <paramref name="requestedName"/> is missing or unknown.</summary>
    string? CanonicalName(string? requestedName);
}
