namespace Automation.UI.Models.ApiHealth;

/// <summary>
/// View model for the API Health dashboard page.
/// </summary>
public sealed class ApiHealthDashboardViewModel
{
    public IReadOnlyList<ServiceEndpointGroup> Services { get; init; } = [];
    public bool HasActiveRun { get; init; }
    public string? LatestRunMode { get; init; }
    public string? LatestRunServiceName { get; init; }
}

/// <summary>
/// Groups endpoints by service for display.
/// </summary>
public sealed class ServiceEndpointGroup
{
    public string ServiceName { get; init; } = string.Empty;
    public bool IsIncludedInLatestRun { get; init; } = true;
    public IReadOnlyList<EndpointViewModel> Endpoints { get; init; } = [];
}

/// <summary>
/// A single endpoint row on the dashboard.
/// </summary>
public sealed class EndpointViewModel
{
    public ApiEndpointDefinition Definition { get; init; } = null!;

    /// <summary>Most recent test result (null if never run).</summary>
    public ApiTestRunResult? LastResult { get; init; }

    /// <summary>
    /// True when <see cref="LastResult"/> belongs to the currently active API Health run.
    /// False indicates a fallback result from a previous run (stale visual state).
    /// </summary>
    public bool IsCurrentRunResult { get; init; } = true;
}

/// <summary>
/// Paged history of test runs for an endpoint.
/// </summary>
public sealed class ApiTestRunHistoryPage
{
    public string EndpointKey { get; init; } = string.Empty;
    public IReadOnlyList<ApiTestRunResult> Runs { get; init; } = [];
    public int PageNumber { get; init; }
    public int PageSize { get; init; }
    public long TotalCount { get; init; }
    public int TotalPages => PageSize == 0 ? 0 : (int)Math.Ceiling((double)TotalCount / PageSize);
}
