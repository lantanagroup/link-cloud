namespace Automation.UI.Services.ApiHealth;

public interface IApiHealthExecutionRunManager
{
    Task<Guid> StartAllAsync();

    bool TryGetRun(Guid runId, out ApiHealthRunInfo runInfo);

    IReadOnlyList<ApiHealthStoredEvent> GetEventsSince(Guid runId, long afterSequence);
}
