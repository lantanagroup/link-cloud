using LantanaGroup.Link.Report.Application.Options;
using LantanaGroup.Link.Shared.Application.Services.Security;

namespace LantanaGroup.Link.Report.Jobs;

/// <summary>
/// At startup, before the shared scheduler starts: reports which participants retention cleanups will expect,
/// then reconciles the retention job with Retention:Schedule.
/// </summary>
/// <remarks>
/// Registered ahead of RetryScheduleService, which owns the scheduler's start and shutdown, so this service
/// never stops the scheduler.
/// </remarks>
public class RetentionScheduleHostedService : IHostedService
{
    private readonly IRetentionScheduleReconciler _reconciler;
    private readonly RetentionParticipants _participants;
    private readonly ILogger<RetentionScheduleHostedService> _logger;

    /// <summary>
    /// Creates the hosted service.
    /// </summary>
    public RetentionScheduleHostedService(IRetentionScheduleReconciler reconciler,
                                          RetentionParticipants participants,
                                          ILogger<RetentionScheduleHostedService> logger)
    {
        _reconciler = reconciler ?? throw new ArgumentNullException(nameof(reconciler));
        _participants = participants ?? throw new ArgumentNullException(nameof(participants));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Logs the retention participants, then reconciles the retention job in the Quartz store.
    /// </summary>
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        // First, so a reconcile failure cannot hide an invalid participant list.
        LogParticipants();

        try
        {
            await _reconciler.ReconcileAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to reconcile the retention job with Retention:Schedule.");
        }
    }

    /// <summary>
    /// Does nothing; RetryScheduleService shuts the scheduler down.
    /// </summary>
    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    // Report still starts with an invalid list; each cleanup is then recorded Failed without a request.
    private void LogParticipants()
    {
        if (!_participants.IsValid)
        {
            _logger.LogError(
                "{Key} is invalid, so every retention cleanup will be recorded Failed and no request sent " +
                "until it is fixed: {Errors}",
                RetentionParticipants.Key,
                string.Join(" ", _participants.Errors).SanitizeForLog());

            return;
        }

        var list = string.Join(", ", _participants.Participants.Select(p => $"{p.ServiceName} ({p.Scope})"));
        _logger.LogInformation(
            "Retention cleanups will expect {Participants}{Source}.",
            list.SanitizeForLog(),
            _participants.IsDefault ? $", the default, because {RetentionParticipants.Key} is not set" : "");
    }
}
