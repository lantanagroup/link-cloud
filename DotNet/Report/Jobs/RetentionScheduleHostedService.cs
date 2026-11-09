namespace LantanaGroup.Link.Report.Jobs;

/// <summary>
/// Reconciles the retention job with Retention:Schedule at startup, before the shared scheduler starts.
/// </summary>
/// <remarks>
/// Registered ahead of RetryScheduleService, which owns the scheduler's start and shutdown, so this service
/// never stops the scheduler.
/// </remarks>
public class RetentionScheduleHostedService : IHostedService
{
    private readonly IRetentionScheduleReconciler _reconciler;
    private readonly ILogger<RetentionScheduleHostedService> _logger;

    /// <summary>
    /// Creates the hosted service.
    /// </summary>
    public RetentionScheduleHostedService(
        IRetentionScheduleReconciler reconciler,
        ILogger<RetentionScheduleHostedService> logger)
    {
        _reconciler = reconciler ?? throw new ArgumentNullException(nameof(reconciler));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Reconciles the retention job in the Quartz store.
    /// </summary>
    public async Task StartAsync(CancellationToken cancellationToken)
    {
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
}
