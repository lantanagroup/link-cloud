namespace LantanaGroup.Link.Nhsn.App.Bff.Domain.Exceptions;

// A call to a Link microservice failed. Thrown by Infrastructure/Link adapters, caught by the
// Application layer and surfaced as a 502 carrying TraceId.
//
// LinkApiResponse<T>'s non-throwing shape is right for a gateway and wrong for business logic, so
// this is the boundary where it converts to a throw. Error contracts are also not uniform across
// Link services — this type normalizes them, so nothing above the adapter boundary may assume
// ProblemDetails.
public class LinkServiceException : Exception
{
    public LinkServiceException(
        string service,
        string operation,
        int statusCode,
        string? traceId,
        string? rawBody,
        string? requestUrl = null,
        Exception? innerException = null)
        : base(BuildMessage(service, operation, statusCode, traceId), innerException)
    {
        Service = service;
        Operation = operation;
        StatusCode = statusCode;
        TraceId = traceId;
        RawBody = rawBody;
        RequestUrl = requestUrl;
    }

    // The Link service called, e.g. "Tenant" or "DataAcquisition".
    public string Service { get; }

    // The gateway operation, in our vocabulary — not the Link route.
    public string Operation { get; }

    public int StatusCode { get; }

    // The downstream trace id, propagated into the ProblemDetails extensions.
    public string? TraceId { get; }

    // Raw downstream body. May be ProblemDetails, may be plain text — do not assume.
    public string? RawBody { get; }

    public string? RequestUrl { get; }

    private static string BuildMessage(string service, string operation, int statusCode, string? traceId)
    {
        var trace = string.IsNullOrWhiteSpace(traceId) ? "<none>" : traceId;
        return $"{service}.{operation} failed with status {statusCode}. TraceId={trace}.";
    }
}
