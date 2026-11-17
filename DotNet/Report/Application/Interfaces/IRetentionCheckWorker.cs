using LantanaGroup.Link.Report.Domain.Models;

namespace LantanaGroup.Link.Report.Application.Interfaces;

/// <summary>
/// The work the daily retention job runs: the scheduled retention check. See dev-docs/retention-check.md.
/// </summary>
public interface IRetentionCheckWorker
{
    /// <summary>
    /// Runs one retention check for the firing described by <paramref name="run"/>.
    /// </summary>
    Task RunAsync(RetentionCheckRun run, CancellationToken cancellationToken);
}
