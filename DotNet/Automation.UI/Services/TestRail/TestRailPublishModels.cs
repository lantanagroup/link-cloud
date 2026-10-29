using LantanaGroup.Link.Automation.Link.Models;

namespace Automation.UI.Services.TestRail;

public sealed record ScenarioTestRailPublishRequest
{
    public Guid RunId { get; init; }
    public Guid? ScenarioId { get; init; }
    public string RunName { get; init; } = string.Empty;
    public AutomationRunStatus Status { get; init; }
    public string? Error { get; init; }
    public IReadOnlyList<string> Logs { get; init; } = [];
    public DateTimeOffset? StartedAt { get; init; }
    public DateTimeOffset? FinishedAt { get; init; }
}

public sealed record ApiHealthTestRailPublishRequest
{
    public Guid RunId { get; init; }
    public string Scope { get; init; } = string.Empty;
    public string? ServiceName { get; init; }
    public bool Failed { get; init; }
    public bool Cancelled { get; init; }
    public string? Error { get; init; }
    public DateTimeOffset StartedAt { get; init; }
    public DateTimeOffset? FinishedAt { get; init; }
}

public sealed class TestRailCaseResult
{
    public int CaseId { get; init; }
    public int StatusId { get; init; }
    public string? Comment { get; init; }
    public string? Elapsed { get; init; }
    public byte[]? Attachment { get; init; }
    public string? AttachmentFileName { get; init; }
}

public sealed class TestRailResultDto
{
    public int Id { get; init; }

    /// <summary>
    /// The case this result was posted for. TestRail's add_results_for_cases response does not
    /// echo case_id, so the client fills it in from the posted result at the same position.
    /// </summary>
    public int CaseId { get; init; }

    /// <summary>The run test the result was added to (TestRail <c>test_id</c>).</summary>
    public int TestId { get; init; }
}

public sealed class TestRailNamedItem
{
    public int Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public bool IsCompleted { get; init; }
}
