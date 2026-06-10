namespace LantanaGroup.Link.Sdk.ApiClient;

/// <summary>
/// Wraps an HTTP response from a Link service call, exposing the status code
/// alongside the deserialized body (if any). This allows callers to inspect
/// the actual server response rather than having status codes silently swallowed.
/// </summary>
public sealed class LinkApiResponse<T>
{
    public int StatusCode { get; init; }
    public T? Body { get; init; }
    public bool IsSuccessStatusCode => StatusCode is >= 200 and < 300;

    /// <summary>Raw response body as string (populated on non-success for diagnostics).</summary>
    public string? RawBody { get; init; }

    /// <summary>The full request URL that was called.</summary>
    public string? RequestUrl { get; init; }

    /// <summary>HTTP method used (GET, POST, etc.).</summary>
    public string? RequestMethod { get; init; }

    /// <summary>Trace ID from response headers (traceparent or X-Trace-Id) for log correlation.</summary>
    public string? TraceId { get; init; }

    /// <summary>
    /// Converts to the non-generic LinkApiResponse (drops the body type information).
    /// Useful when only status code validation is needed.
    /// </summary>
    public LinkApiResponse AsUntyped() => new()
    {
        StatusCode = StatusCode,
        RawBody = RawBody,
        RequestUrl = RequestUrl,
        RequestMethod = RequestMethod,
        TraceId = TraceId
    };

    public static implicit operator LinkApiResponse(LinkApiResponse<T> typed) => typed.AsUntyped();
}

/// <summary>
/// Non-generic response for operations that don't return a body.
/// </summary>
public sealed class LinkApiResponse
{
    public int StatusCode { get; init; }
    public bool IsSuccessStatusCode => StatusCode is >= 200 and < 300;
    public string? RawBody { get; init; }

    /// <summary>The full request URL that was called.</summary>
    public string? RequestUrl { get; init; }

    /// <summary>HTTP method used (GET, POST, etc.).</summary>
    public string? RequestMethod { get; init; }

    /// <summary>Trace ID from response headers (traceparent or X-Trace-Id) for log correlation.</summary>
    public string? TraceId { get; init; }
}
