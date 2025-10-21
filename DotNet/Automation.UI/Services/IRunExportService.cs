namespace Automation.UI.Services;

/// <summary>
/// Builds the diagnostic ZIP package for a single run. Implementations are
/// expected to be failure-safe: missing or unavailable data sources should not
/// abort the export, but rather produce a placeholder file describing what
/// could not be retrieved.
/// </summary>
public interface IRunExportService
{
    Task<RunExportPackage?> BuildAsync(Guid runId, CancellationToken cancellationToken = default);
}

public sealed record RunExportPackage(string FileName, byte[] Content);
