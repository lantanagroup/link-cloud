using LantanaGroup.Link.Tenant.Config;
using System.Diagnostics;
using System.Net;

namespace LantanaGroup.Link.Tenant.Extensions
{
    /// <summary>
    /// The RFC 9110 sections used as problem-detail <c>type</c> values.
    /// </summary>
    /// <remarks>
    /// Named rather than inlined so the same status always carries the same type, matching
    /// <c>DmrpProblemTypes</c> in MockDmrpApi.
    /// </remarks>
    internal static class TenantProblemTypes
    {
        public const string BadRequest = "https://tools.ietf.org/html/rfc9110#section-15.5.1";
        public const string NotFound = "https://tools.ietf.org/html/rfc9110#section-15.5.5";
        public const string InternalServerError = "https://tools.ietf.org/html/rfc9110#section-15.6.1";
    }

    /// <summary>
    /// Problem-detail shaping, following the pattern Terminology and MockDmrpApi use.
    /// </summary>
    /// <remarks>
    /// What this guarantees that <c>AddProblemDetails()</c> on its own does not:
    /// <list type="bullet">
    /// <item>A <c>traceId</c> on every problem response, so a report of "it returned 500" can be
    /// traced without asking the reporter to reproduce it.</item>
    /// <item>A <c>detail</c> on responses that would otherwise carry only a status code — a bare
    /// 400 tells a caller nothing about which field was rejected.</item>
    /// <item>No exception detail leaking outside development. A 500's detail is replaced wholesale
    /// rather than filtered, so an exception message cannot reach a caller by accident.</item>
    /// </list>
    /// <para>
    /// This only shapes responses that reach the problem-details pipeline. A controller that
    /// returns a bare string — <c>BadRequest(ex.Message)</c> — bypasses it entirely and is written
    /// out as <c>text/plain</c>, so the endpoints answer through the <c>*Problem</c> helpers on
    /// <see cref="Controllers.FacilityController"/> rather than through <c>BadRequest</c>.
    /// </para>
    /// </remarks>
    internal static class TenantProblemDetailsExtensions
    {
        internal static IServiceCollection AddTenantProblemDetails(
            this IServiceCollection services,
            IWebHostEnvironment environment,
            bool includeExceptionDetails = false)
        {
            services.AddProblemDetails(options =>
            {
                options.CustomizeProblemDetails = ctx =>
                {
                    var statusCode = ctx.ProblemDetails.Status ?? ctx.HttpContext.Response.StatusCode;

                    // A 500 may carry a raw exception message the caller should not see. Every
                    // other status is framework- or controller-authored and already safe to show,
                    // so only fall back to the generic text when nothing more specific was set.
                    if (statusCode == (int)HttpStatusCode.InternalServerError
                        || string.IsNullOrWhiteSpace(ctx.ProblemDetails.Detail))
                    {
                        ctx.ProblemDetails.Detail = "An error occured in our API. Please use the trace id when requesting assistence.";
                    }

                    if (!ctx.ProblemDetails.Extensions.ContainsKey("traceId"))
                    {
                        string? traceId = Activity.Current?.Id ?? ctx.HttpContext.TraceIdentifier;
                        ctx.ProblemDetails.Extensions.Add(new KeyValuePair<string, object?>("traceId", traceId));
                    }

                    if (environment.IsDevelopment() || includeExceptionDetails)
                    {
                        // Indexer rather than Add: Add throws on a key that is already present, and
                        // throwing while building an error response replaces a useful 400 with an
                        // opaque 500.
                        ctx.ProblemDetails.Extensions["service"] = TenantConstants.ServiceName;
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
}
