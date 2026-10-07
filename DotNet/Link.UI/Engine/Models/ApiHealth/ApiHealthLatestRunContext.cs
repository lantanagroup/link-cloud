namespace Automation.UI.Models.ApiHealth;

public sealed class ApiHealthLatestRunContext
{
    public Guid RunId { get; init; }
    public string RunMode { get; init; } = "Single";
    public string ServiceName { get; init; } = string.Empty;
    public DateTimeOffset StartedAt { get; init; }
}
