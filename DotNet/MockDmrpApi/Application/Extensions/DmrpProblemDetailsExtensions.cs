using System.Diagnostics;
using System.Net;
using LantanaGroup.Link.MockDmrpApi.Settings;

namespace LantanaGroup.Link.MockDmrpApi.Application.Extensions;

/// <summary>
/// The RFC 9110 sections ASP.NET uses as problem-detail <c>type</c> values.
/// </summary>
/// <remarks>
/// Named rather than inlined so the same status always carries the same type. ASP.NET already
/// supplies these for the statuses it produces itself; a controller that passes a title and
/// detail must supply the type too, or the response ends up with a hand-written title beside a
/// missing type.
/// </remarks>
internal static class DmrpProblemTypes
{
    public const string BadRequest = "https://tools.ietf.org/html/rfc9110#section-15.5.1";
    public const string Unauthorized = "https://tools.ietf.org/html/rfc9110#section-15.5.2";
    public const string NotFound = "https://tools.ietf.org/html/rfc9110#section-15.5.5";
    public const string Conflict = "https://tools.ietf.org/html/rfc9110#section-15.5.10";
    public const string ServiceUnavailable = "https://tools.ietf.org/html/rfc9110#section-15.6.4";
}

/// <summary>
/// Problem-detail shaping, following the pattern Terminology uses.
/// </summary>
/// <remarks>
/// Three things this guarantees that <c>AddProblemDetails()</c> on its own does not:
/// <list type="bullet">
/// <item>A <c>traceId</c> on every problem response, so a report of "it returned 500" can be
/// traced without asking the reporter to reproduce it.</item>
/// <item>A <c>detail</c> on responses that would otherwise carry only a status code — a bare
/// 404 tells a caller nothing about which of several lookups failed.</item>
/// <item>No exception detail leaking outside development. A 500's detail is replaced wholesale
/// rather than filtered, so an exception message cannot reach a caller by accident.</item>
/// </list>
/// <para>
/// This applies to <b>both</b> surfaces, including the two contract endpoints. The service's own
/// error shape is not something the real DMRP API has been observed to define, so matching Link's
/// house style is the better default — but it is a divergence, and a consumer should not read
/// this service's error bodies as evidence of what the real one returns.
/// </para>
/// </remarks>
internal static class DmrpProblemDetailsExtensions
{
    internal static IServiceCollection AddDmrpProblemDetails(
        this IServiceCollection services,
        IWebHostEnvironment environment,
        bool includeExceptionDetails = false)
    {
        services.AddProblemDetails(options =>
        {
            options.CustomizeProblemDetails = ctx =>
            {
                var statusCode = ctx.ProblemDetails.Status ?? ctx.HttpContext.Response.StatusCode;

                if (statusCode == (int)HttpStatusCode.InternalServerError)
                {
                    // Replaced, not appended: whatever the framework put here may quote an
                    // exception, and a stand-in for a third-party API is still a service whose
                    // internals stay internal.
                    ctx.ProblemDetails.Detail =
                        "An error occurred in our API. Please use the trace id when requesting assistance.";
                }
                else if (string.IsNullOrWhiteSpace(ctx.ProblemDetails.Detail))
                {
                    ctx.ProblemDetails.Detail = statusCode switch
                    {
                        (int)HttpStatusCode.NotFound =>
                            "The requested Mock DMRP resource was not found.",
                        (int)HttpStatusCode.Unauthorized =>
                            "A valid bearer token is required for this endpoint.",
                        (int)HttpStatusCode.ServiceUnavailable =>
                            "The Mock DMRP API is not available in this environment.",
                        _ =>
                            "The request could not be completed. Please use the trace id when requesting assistance."
                    };
                }

                if (!ctx.ProblemDetails.Extensions.ContainsKey("traceId"))
                {
                    var traceId = Activity.Current?.Id ?? ctx.HttpContext.TraceIdentifier;
                    ctx.ProblemDetails.Extensions.Add(new KeyValuePair<string, object?>("traceId", traceId));
                }

                if (environment.IsDevelopment() || includeExceptionDetails)
                {
                    // Indexer rather than Add: Add throws on a key that is already present, and
                    // nothing here owns this dictionary exclusively. Overwriting our own value
                    // with the same value is harmless; throwing while building an error
                    // response is not, since it replaces a useful 404 with an opaque 500.
                    ctx.ProblemDetails.Extensions["API"] = DmrpApiConstants.ServiceName;
                }
                else
                {
                    ctx.ProblemDetails.Extensions.Remove("exception");
                }
            };
        });

        return services;
    }
}
