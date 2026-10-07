namespace Automation.UI.Models.ApiHealth;

/// <summary>
/// Defines a single API endpoint test case — one code path through one endpoint.
/// Multiple definitions can share the same <see cref="GroupName"/> to visually
/// group them under a common endpoint header on the dashboard.
/// </summary>
public sealed class ApiEndpointDefinition
{
    public string ServiceName { get; init; } = string.Empty;

    /// <summary>
    /// The specific test case name (e.g., "Create Facility → 201", "Get Facility → 404 not found").
    /// </summary>
    public string EndpointName { get; init; } = string.Empty;

    /// <summary>
    /// Optional description shown as secondary text under the test name.
    /// </summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>
    /// Groups tests under an endpoint header (e.g., "POST /api/Facility").
    /// When adjacent tests share the same GroupName, a single header row is rendered above them.
    /// </summary>
    public string? GroupName { get; init; }

    /// <summary>Relative path for the endpoint (e.g., "/health", "/api/Facility").</summary>
    public string RelativePath { get; init; } = string.Empty;

    public string HttpMethod { get; init; } = "GET";

    /// <summary>Expected successful status code (e.g., 200, 201, 204).</summary>
    public int ExpectedStatusCode { get; init; } = 200;

    /// <summary>
    /// When true, this endpoint is exercised via a full CRUD test suite
    /// (using LinkSdk clients) rather than a raw HTTP call.
    /// </summary>
    public bool IsTestSuiteStep { get; init; }

    /// <summary>
    /// When non-null, this step is permanently skipped and the reason is displayed
    /// on the dashboard even before the suite has been run for the first time.
    /// Use for paths that are structurally untestable in a self-contained health test
    /// (e.g. require Kafka seeding or an out-of-band pipeline step).
    /// </summary>
    public string? AlwaysSkipReason { get; init; }

    /// <summary>
    /// True when <see cref="AlwaysSkipReason"/> is set. Informational steps are
    /// rendered in the table for transparency but are never executed, never persisted,
    /// and excluded from all pass/fail/total counts.
    /// </summary>
    public bool IsInformational => AlwaysSkipReason != null;

    /// <summary>
    /// A unique key combining service + endpoint for persistence lookups.
    /// </summary>
    public string Key => $"{ServiceName}::{EndpointName}";
}
