namespace Automation.UI.Services.TestRail;

public interface ITestRailApiClient
{
    Task<int> AddRunAsync(
        int projectId,
        int suiteId,
        string name,
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
}
