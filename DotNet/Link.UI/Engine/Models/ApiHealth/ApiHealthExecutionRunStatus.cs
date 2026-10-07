namespace Automation.UI.Models.ApiHealth;

public sealed class ApiHealthExecutionRunStatus
{
    public Guid RunId { get; init; }
    public Guid? SeedRunId { get; init; }
    public string? SeedRunName { get; init; }
    public string Scope { get; init; } = string.Empty;
    public string? ServiceName { get; init; }
    public DateTimeOffset StartedAt { get; init; }
    public string? Phase { get; init; }
    public string? Message { get; init; }
    public bool IsError { get; init; }
    public bool IsCompleted { get; init; }
    public bool Failed { get; init; }
    public string? Error { get; init; }
    public DateTimeOffset? FinishedAt { get; init; }

    public string RunMode => string.Equals(Scope, "All", StringComparison.OrdinalIgnoreCase) ? "All" : "Single";
}
