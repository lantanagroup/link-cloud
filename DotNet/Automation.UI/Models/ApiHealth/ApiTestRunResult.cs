using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Automation.UI.Models.ApiHealth;

/// <summary>
/// Result of a single API endpoint test execution.
/// </summary>
public sealed class ApiTestRunResult
{
    [BsonRepresentation(BsonType.String)]
    public Guid Id { get; set; } = Guid.NewGuid();

    [BsonRepresentation(BsonType.String)]
    public Guid RunId { get; set; }

    /// <summary>Matches <see cref="ApiEndpointDefinition.Key"/>.</summary>
    public string EndpointKey { get; set; } = string.Empty;

    public string ServiceName { get; set; } = string.Empty;
    public string EndpointName { get; set; } = string.Empty;

    public bool Passed { get; set; }

    /// <summary>
    /// True when the step was deliberately not executed because a prerequisite
    /// configuration is absent in this environment (e.g. CheckIfTenantExists = false).
    /// A skipped step is neither a pass nor a failure.
    /// </summary>
    public bool Skipped { get; set; }

    /// <summary>Human-readable explanation of why the step was skipped.</summary>
    public string? SkipReason { get; set; }

    public int? ActualStatusCode { get; set; }
    public int ExpectedStatusCode { get; set; }
    public string? ErrorMessage { get; set; }
    public string? ResponseSnippet { get; set; }

    public DateTimeOffset ExecutedAt { get; set; } = DateTimeOffset.UtcNow;
    public long DurationMs { get; set; }

    // --- Diagnostic fields (populated from LinkApiResponse) ---

    /// <summary>The full URL that was called (e.g., https://host/api/normalization/operations/facility/xyz).</summary>
    public string? RequestUrl { get; set; }

    /// <summary>HTTP method used (GET, POST, PUT, DELETE, etc.).</summary>
    public string? RequestMethod { get; set; }

    /// <summary>Request body captured for the call (if any).</summary>
    public string? RequestBody { get; set; }

    /// <summary>Trace ID from the response headers for correlating with Grafana/distributed tracing.</summary>
    public string? TraceId { get; set; }

    /// <summary>Truncated response body (for both success and failure diagnostics).</summary>
    public string? ResponseBody { get; set; }
}
