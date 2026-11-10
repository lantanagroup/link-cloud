using LantanaGroup.Link.Report.Domain.Models;

namespace LantanaGroup.Link.Report.Application.Interfaces;

/// <summary>
/// Re-sends retention requests that participants have not answered. The scheduled retention check calls it
/// before it selects new reports. See dev-docs/retention-check.md.
/// </summary>
public interface IRetentionPendingResender
{
    /// <summary>
    /// Re-sends every request still unanswered for longer than the configured wait.
    /// </summary>
    Task ResendAsync(RetentionCheckRun run, CancellationToken cancellationToken);
}
