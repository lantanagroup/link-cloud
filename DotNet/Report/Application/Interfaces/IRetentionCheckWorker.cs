using LantanaGroup.Link.Report.Domain.Models;

namespace LantanaGroup.Link.Report.Application.Interfaces;

/// <summary>
/// The work the daily retention job runs. LEGLINK-1211 replaces the logging-only default with the real check.
/// </summary>
public interface IRetentionCheckWorker
{
    /// <summary>
    /// Runs one retention check for the firing described by <paramref name="run"/>.
    /// </summary>
    Task RunAsync(RetentionCheckRun run, CancellationToken cancellationToken);
}
