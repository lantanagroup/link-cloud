namespace Automation.UI.Models.ApiHealth;

/// <summary>
/// View model for the API Health dashboard page.
/// </summary>
public sealed class ApiHealthDashboardViewModel
{
    public IReadOnlyList<ServiceEndpointGroup> Services { get; init; } = [];
}

/// <summary>
/// Groups endpoints by service for display.
/// </summary>
public sealed class ServiceEndpointGroup
{
    public string ServiceName { get; init; } = string.Empty;
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
