using LantanaGroup.Link.Automation.Link.Models;

namespace Automation.UI.Services;

/// <summary>
/// On host startup, reconciles any scenario runs that were still marked active.
/// Active runs are cancelled first; if cancellation cannot be applied, the run is hard deleted.
/// </summary>
public sealed class ScenarioRunStartupRecoveryService(
    ISnapshotStore snapshotStore,
    IAutomationRunManager runManager,
    ILogger<ScenarioRunStartupRecoveryService> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            var activeRunMetas = await snapshotStore.GetActiveRunsAsync(cancellationToken);
            var nonTerminalSummaries = await snapshotStore.GetAllRunSummariesAsync(since: null, ct: cancellationToken)
                .ConfigureAwait(false);

            var candidateRunIds = activeRunMetas
                .Select(r => r.RunId)
                .Concat(nonTerminalSummaries
                    .Where(r => r.Status is AutomationRunStatus.Queued or AutomationRunStatus.Running)
                    .Select(r => r.RunId))
                .Distinct()
                .ToList();

            if (candidateRunIds.Count == 0)
                return;

            logger.LogWarning("Startup recovery found {Count} non-terminal scenario run(s). Beginning cancel-or-delete reconciliation.", candidateRunIds.Count);

            var cancelledCount = 0;
            var deletedCount = 0;

            foreach (var runId in candidateRunIds)
            {
                try
                {
                    var cancelled = await runManager.CancelRunAsync(runId, cancellationToken);
                    if (cancelled)
                    {
                        var postCancel = await runManager.GetRunAsync(runId, cancellationToken);
                        if (postCancel == null || postCancel.Status is not AutomationRunStatus.Queued and not AutomationRunStatus.Running)
                        {
                            cancelledCount++;
                            logger.LogInformation("Startup recovery cancelled active scenario run {RunId}.", runId);
                            continue;
                        }

                        logger.LogWarning(
                            "Startup recovery cancellation reported success for run {RunId}, but run remains active ({Status}). Hard deleting.",
                            runId,
                            postCancel.Status);
                    }

                    await snapshotStore.DeleteRunAsync(runId, cancellationToken);
                    deletedCount++;
                    logger.LogWarning("Startup recovery hard-deleted active scenario run {RunId} because cancellation was not possible.", runId);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Startup recovery failed while reconciling run {RunId}. Attempting hard delete.", runId);

                    try
                    {
                        await snapshotStore.DeleteRunAsync(runId, cancellationToken);
                        deletedCount++;
                        logger.LogWarning("Startup recovery hard-deleted run {RunId} after cancellation error.", runId);
                    }
                    catch (Exception deleteEx)
                    {
                        logger.LogError(deleteEx, "Startup recovery could not hard-delete run {RunId}. Manual cleanup may be required.", runId);
                    }
                }
            }

            logger.LogWarning(
                "Startup recovery reconciliation complete. Candidates={Candidates}, Cancelled={Cancelled}, HardDeleted={Deleted}.",
                candidateRunIds.Count,
                cancelledCount,
                deletedCount);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Startup recovery encountered an error while reconciling active scenario runs. Continuing startup.");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
