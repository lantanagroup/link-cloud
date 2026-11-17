using Automation.UI.Services;
using LantanaGroup.Link.Automation.Link.Models;
using Microsoft.AspNetCore.SignalR;

namespace Automation.UI.Services.TestRail;

/// <summary>
/// Stores TestRail runs that accepted an automation run's results, then pushes
/// the run status. The hub update runs only after the summary write succeeds.
/// </summary>
internal sealed class TestRailRunLinkRecorder
{
    private readonly IHubContext<RunHub> _hub;
    private readonly ILogger _logger;
    private readonly ISnapshotStore _store;

    public TestRailRunLinkRecorder(
        IHubContext<RunHub> hub,
        ILogger logger,
        ISnapshotStore store)
    {
        _hub = hub;
        _logger = logger;
        _store = store;
    }

    public async Task RecordAsync(
        Guid runId,
        IReadOnlyList<TestRailRunReference>? runs,
        MutableRunState? state,
        Func<MutableRunState, Task<bool>> persistInMemory,
        Func<MutableRunState, AutomationRunSummary> toSummary,
        CancellationToken cancellationToken = default)
    {
        if (runs == null || runs.Count == 0)
            return;

        if (state != null)
        {
            bool added;
            lock (state.Sync)
                added = TestRailRunLinks.AddDistinct(state.TestRailRuns, runs);
            if (!added)
                return;

            // The hub must not advertise a link the summary write did not keep.
            if (!await persistInMemory(state))
                return;

            await PushAsync(state.RunId, toSummary(state), cancellationToken);
            return;
        }

        var summary = await _store.GetRunSummaryAsync(runId, cancellationToken);
        if (summary == null || !TestRailRunLinks.AddDistinct(summary.TestRailRuns, runs))
            return;

        try
        {
            // A disconnect on the status request must not drop a link that was already published.
            await _store.UpsertRunSummaryAsync(
                summary, summary.FacilityId, summary.ReportId, CancellationToken.None);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to persist the TestRail run link for {RunId}.", runId);
            return;
        }

        await PushAsync(runId, summary, cancellationToken);
    }

    private async Task PushAsync(
        Guid runId,
        AutomationRunSummary summary,
        CancellationToken cancellationToken)
    {
        try
        {
            await _hub.Clients.Group(runId.ToString()).SendAsync("status", summary, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not push the TestRail run link for {RunId}.", runId);
        }
    }
}
