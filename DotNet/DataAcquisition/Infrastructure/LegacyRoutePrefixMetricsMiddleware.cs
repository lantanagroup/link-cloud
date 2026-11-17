using LantanaGroup.Link.DataAcquisition.Domain.Application.Interfaces;
using LantanaGroup.Link.Shared.Application.Services.Security;

namespace LantanaGroup.Link.DataAcquisition.Infrastructure;

/// <summary>
/// Counts requests that arrived on the deprecated /api/data prefix, tagged by the matched route template.
/// </summary>
public class LegacyRoutePrefixMetricsMiddleware
{
    /// <summary>
    /// Method tag value for any method outside the standard set, as OpenTelemetry's HTTP conventions use.
    /// </summary>
    public const string OtherMethod = "_OTHER";

    // Kestrel accepts any method token, so an unbounded tag would let a client mint a series per request.
    private static readonly HashSet<string> _knownMethods =
    [
        HttpMethods.Connect,
        HttpMethods.Delete,
        HttpMethods.Get,
        HttpMethods.Head,
        HttpMethods.Options,
        HttpMethods.Patch,
        HttpMethods.Post,
        HttpMethods.Put,
        HttpMethods.Trace
    ];

    private readonly RequestDelegate _next;
    private readonly ILogger<LegacyRoutePrefixMetricsMiddleware> _logger;

    /// <summary>
    /// Creates the middleware. Instantiated once by the pipeline.
    /// </summary>
    public LegacyRoutePrefixMetricsMiddleware(RequestDelegate next,
                                              ILogger<LegacyRoutePrefixMetricsMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    /// <summary>
    /// Records the legacy request, if it is one, then continues the pipeline.
    /// </summary>
    public async Task InvokeAsync(HttpContext context, IDataAcquisitionServiceMetrics metrics)
    {
        if (!context.Items.ContainsKey(LegacyRoutePrefixRule.LegacyRequestItemKey))
        {
            await _next(context);
            return;
        }

        var route = (context.GetEndpoint() as RouteEndpoint)?.RoutePattern.RawText ?? "unmatched";

        metrics.IncrementPathRewriteCounter(route, NormalizeMethod(context.Request.Method));
        _logger.LogDebug("Request on deprecated /api/data prefix for route {Route}", route.SanitizeForLog());

        await _next(context);
    }

    private static string NormalizeMethod(string method)
    {
        return _knownMethods.Contains(method) ? method : OtherMethod;
    }
}
