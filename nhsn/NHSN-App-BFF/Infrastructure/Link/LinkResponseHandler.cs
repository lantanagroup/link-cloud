using System.Text.Json;
using System.Text.Json.Serialization;
using LantanaGroup.Link.Nhsn.App.Bff.Domain.Exceptions;
using LantanaGroup.Link.Sdk.ApiClient;

namespace LantanaGroup.Link.Nhsn.App.Bff.Infrastructure.Link;

// The one place in the BFF that inspects IsSuccessStatusCode. Converts a LinkApiResponse<T> into a
// body or a LinkServiceException.
//
// If you find yourself checking IsSuccessStatusCode anywhere outside Infrastructure/Link, the
// boundary has leaked.
internal static class LinkResponseHandler
{
    // Mirrors the SDK's own deserialization settings (LinkApiClientBase) so the untyped overload
    // below parses bodies the same way the typed path would.
    private static readonly JsonSerializerOptions RawBodyOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    // Returns the body, or throws. Use when the resource must exist.
    public static T Require<T>(LinkApiResponse<T> response, string service, string operation)
    {
        EnsureSuccess(response.StatusCode, response.TraceId, response.RawBody, response.RequestUrl, service, operation);

        return response.Body
               ?? throw new LinkServiceException(service, operation, response.StatusCode, response.TraceId,
                   response.RawBody, response.RequestUrl,
                   new InvalidOperationException("Link returned success with an empty body where one was required."));
    }

    // Returns the body, or null on 404. Any other failure throws. Use for read-modify-write, where
    // "absent" decides POST versus PUT and is not an error.
    public static T? Optional<T>(LinkApiResponse<T> response, string service, string operation)
        where T : class
    {
        if (response.StatusCode == StatusCodes.Status404NotFound)
        {
            return null;
        }

        EnsureSuccess(response.StatusCode, response.TraceId, response.RawBody, response.RequestUrl, service, operation);
        return response.Body;
    }

    // Throws unless the call succeeded. Use for writes that return no body we need.
    public static void EnsureSuccess(LinkApiResponse response, string service, string operation) =>
        EnsureSuccess(response.StatusCode, response.TraceId, response.RawBody, response.RequestUrl, service, operation);

    // Deserializes the body of a non-generic LinkApiResponse, which carries no Body — only RawBody.
    //
    // Needed because much of DataAcquisitionServiceClient's configuration surface returns the
    // non-generic type, and RawBody is populated there on success despite its own XML comment
    // saying otherwise. Delete this overload once those methods are typed.
    public static T? OptionalFromRawBody<T>(LinkApiResponse response, string service, string operation)
        where T : class
    {
        if (response.StatusCode == StatusCodes.Status404NotFound)
        {
            return null;
        }

        EnsureSuccess(response, service, operation);

        if (string.IsNullOrWhiteSpace(response.RawBody))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<T>(response.RawBody, RawBodyOptions);
        }
        catch (JsonException ex)
        {
            throw new LinkServiceException(service, operation, response.StatusCode, response.TraceId,
                response.RawBody, response.RequestUrl, ex);
        }
    }

    private static void EnsureSuccess(int statusCode, string? traceId, string? rawBody, string? requestUrl, string service, string operation)
    {
        if (statusCode is >= 200 and < 300)
        {
            return;
        }

        throw new LinkServiceException(service, operation, statusCode, traceId, rawBody, requestUrl);
    }
}
