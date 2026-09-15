using LantanaGroup.Link.MockDmrpApi.Application.Services;

namespace LantanaGroup.Link.MockDmrpApi.Application.Middleware;

/// <summary>
/// Holds contract requests for the configured artificial delay, so a caller's timeout and
/// retry behaviour can be exercised against a slow upstream.
/// </summary>
/// <remarks>
/// <b>The delay applies to the contract surface only.</b> That is not a detail -- it is what
/// keeps the feature from being a trap:
/// <list type="bullet">
/// <item>Delaying <c>/api/mock-dmrp</c> would mean a five-minute delay takes five minutes to turn off,
/// because the endpoint that clears it would be delayed too. The escape hatch has to stay
/// fast.</item>
/// <item>Delaying <c>/health</c> would push the container past its probe timeout and get it
/// restarted, which reads as an outage rather than a test in progress.</item>
/// </list>
/// The rule is expressed as "everything except our own namespaced paths", rather than a list
/// of contract routes, so an endpoint added to the contract is delayed automatically. The
/// contract endpoints sit at the root and everything of ours is prefixed, which is what makes
/// that inversion safe.
/// <para>
/// Registered before routing, after the availability gate. A disabled deployment should
/// refuse a request immediately rather than refuse it slowly.
/// </para>
/// </remarks>
public class ResponseDelayMiddleware
{
    /// <summary>
    /// Path prefixes that are never delayed: our support surface, and the operational
    /// endpoints a delay must not be able to take down. "/api" covers the whole support
    /// surface, including the endpoints that clear the delay -- a delay must never be able
    /// to make itself unremovable. What is left delayed is the contract surface at the
    /// root, which is the only thing worth delaying.
    /// </summary>
    private static readonly string[] NeverDelayed = ["/health", "/api", "/swagger"];

    private readonly RequestDelegate _next;

    public ResponseDelayMiddleware(RequestDelegate next)
    {
        _next = next ?? throw new ArgumentNullException(nameof(next));
    }

    public static bool AppliesTo(PathString path) =>
        !NeverDelayed.Any(prefix => path.StartsWithSegments(prefix, StringComparison.OrdinalIgnoreCase));

    public async Task InvokeAsync(HttpContext context, IResponseDelayService delays)
    {
        if (AppliesTo(context.Request.Path))
        {
            try
            {
                await delays.ApplyAsync(context.RequestAborted);
            }
            catch (OperationCanceledException)
            {
                // The caller timed out and went away mid-delay. Returning without invoking
                // the rest of the pipeline is the point of passing the token: the request is
                // released instead of being held for the full delay and then written to a
                // socket nobody is reading. Not an error, so nothing is logged.
                return;
            }
        }

        await _next(context);
    }
}

public static class ResponseDelayMiddlewareExtensions
{
    public static IApplicationBuilder UseContractResponseDelay(this IApplicationBuilder app) =>
        app.UseMiddleware<ResponseDelayMiddleware>();
}
