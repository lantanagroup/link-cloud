namespace LantanaGroup.Link.Nhsn.App.Bff.Application.Models.Onboarding;

// Mirrors CommitResult in NHSN-App-UI/src/core/api/contracts.ts. What POST/GET
// /onboarding/completion return — the outcome of the completion fan-out's arming writes across
// every Link service onboarding touches.
public sealed record CommitResultResponse
{
    public string FacilityId { get; init; } = string.Empty;
    public IReadOnlyList<CommitServiceResultResponse> Services { get; init; } = [];
}

public sealed record CommitServiceResultResponse
{
    public string Service { get; init; } = string.Empty;
    public int Stage { get; init; }

    // "committed" | "failed" | "pending" - matches CommitTargetStatus in contracts.ts.
    public string Status { get; init; } = string.Empty;
    public string? Detail { get; init; }
}
