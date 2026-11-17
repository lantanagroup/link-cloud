namespace Automation.UI.Services;

/// <summary>Saves a report's manifest by report id. Link.UI provides it; hosts without it skip the save.</summary>
public interface IReportManifestSaver
{
    Task<bool> SaveAsync(string facilityId, string reportId, Guid? runId, CancellationToken cancellationToken);
}