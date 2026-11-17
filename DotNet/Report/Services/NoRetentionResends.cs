using LantanaGroup.Link.Report.Application.Interfaces;
using LantanaGroup.Link.Report.Domain.Models;

namespace LantanaGroup.Link.Report.Services;

/// <summary>
/// The default <see cref="IRetentionPendingResender"/>: re-sending unanswered retention requests is not built
/// yet, so it only logs. Replace the registration when it is. See dev-docs/retention-check.md.
/// </summary>
public class NoRetentionResends : IRetentionPendingResender
{
    private readonly ILogger<NoRetentionResends> _logger;

    /// <summary>
    /// Creates the placeholder.
    /// </summary>
    public NoRetentionResends(ILogger<NoRetentionResends> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Does nothing but log at Debug.
    /// </summary>
    public Task ResendAsync(RetentionCheckRun run, CancellationToken cancellationToken)
    {
        _logger.LogDebug("Re-sending unanswered retention requests is not implemented; nothing was re-sent.");

        return Task.CompletedTask;
    }
}
