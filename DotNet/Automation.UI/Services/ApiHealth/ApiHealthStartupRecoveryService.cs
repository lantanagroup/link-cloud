using Automation.UI.Services.Persistence;
using LantanaGroup.Link.Automation.Link.Models;

namespace Automation.UI.Services.ApiHealth;

/// <summary>
/// On host startup, reconciles interrupted API Health executions from a prior process.
/// </summary>
public sealed class ApiHealthStartupRecoveryService(
    IApiHealthRunStore apiHealthRunStore,
    IAutomationRunManager automationRunManager,
    ILogger<ApiHealthStartupRecoveryService> logger) : IHostedService
{
    private const string InterruptedRunMessage = "API Health run was interrupted by application restart and has been cancelled.";

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            var activeExecution = await apiHealthRunStore.GetActiveExecutionRunStatusAsync(cancellationToken);
            if (activeExecution == null)
                return;

            var cancelledSeedRun = false;

            if (activeExecution.SeedRunId is Guid seedRunId)
            {
                var seedRun = await automationRunManager.GetRunAsync(seedRunId, cancellationToken);
                if (seedRun != null && seedRun.Status is AutomationRunStatus.Queued or AutomationRunStatus.Running)
                {
                    var cancelled = await automationRunManager.CancelRunAsync(seedRunId, cancellationToken);
                    cancelledSeedRun = cancelled;
                    logger.LogInformation(
                        "Startup recovery attempted cancellation of seed run {SeedRunId} for interrupted API Health run {ApiHealthRunId}. Cancelled={Cancelled}",
                        seedRunId,
                        activeExecution.RunId,
                        cancelled);
                }
            }

            await apiHealthRunStore.CompleteExecutionRunAsync(
                activeExecution.RunId,
                failed: true,
                error: InterruptedRunMessage,
                finishedAt: DateTimeOffset.UtcNow,
                ct: cancellationToken);

            logger.LogWarning(
                "Startup recovery marked interrupted API Health run {ApiHealthRunId} as cancelled. CancelledSeedRun={CancelledSeedRun}",
                activeExecution.RunId,
                cancelledSeedRun);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Startup recovery encountered an error while reconciling interrupted API Health runs. Continuing startup.");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
