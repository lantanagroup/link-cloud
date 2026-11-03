using LantanaGroup.Link.Report.Application.Interfaces;
using LantanaGroup.Link.Report.Domain.Models;

namespace LantanaGroup.Link.Report.Services;

/// <summary>
/// The placeholder retention check that ships until LEGLINK-1211 replaces it. It only logs that it ran.
/// </summary>
public class LoggingRetentionCheck : IRetentionCheckWorker
{
    private readonly ILogger<LoggingRetentionCheck> _logger;

    /// <summary>
    /// Creates the placeholder check.
    /// </summary>
    public LoggingRetentionCheck(ILogger<LoggingRetentionCheck> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Logs that no retention check is implemented, with the details of the firing.
    /// </summary>
    public Task RunAsync(RetentionCheckRun run, CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "No retention check is implemented; nothing was done for the firing due at {ScheduledFireTimeUtc:O} " +
            "(recovery {IsRecovery}, fire instance {FireInstanceId}).",
            run.ScheduledFireTimeUtc,
            run.IsRecovery,
            run.FireInstanceId);

        return Task.CompletedTask;
    }
}
