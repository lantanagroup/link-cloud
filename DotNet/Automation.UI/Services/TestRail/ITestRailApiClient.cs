namespace Automation.UI.Services.TestRail;

public interface ITestRailApiClient
{
    Task<int> AddRunAsync(
        int projectId,
        int suiteId,
        string name,
        IReadOnlyList<int> caseIds,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Replaces the case selection on a run (update_run, include_all false).
    /// The caller sends the full set. A subset drops the other cases and their results.
    /// </summary>
    Task UpdateRunCaseIdsAsync(
        int runId,
        IReadOnlyList<int> caseIds,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TestRailResultDto>> AddResultsForCasesAsync(
        int runId,
        IReadOnlyList<TestRailCaseResult> results,
        CancellationToken cancellationToken = default);

    Task AddAttachmentToResultAsync(
        int resultId,
        string fileName,
        byte[] content,
        CancellationToken cancellationToken = default);

    /// <summary>Lists the projects the configured user can see (get_projects, all pages).</summary>
    Task<IReadOnlyList<TestRailNamedItem>> GetProjectsAsync(CancellationToken cancellationToken = default);

    /// <summary>Lists the suites in a project (get_suites, all pages).</summary>
    Task<IReadOnlyList<TestRailNamedItem>> GetSuitesAsync(int projectId, CancellationToken cancellationToken = default);
}
