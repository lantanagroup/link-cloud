using LantanaGroup.Link.DataAcquisition.Domain.Application.Interfaces;

namespace LantanaGroup.Link.DataAcquisition.Infrastructure;

/// <summary>
/// Counts requests that arrived on the deprecated /api/data prefix, tagged by the matched route template.
/// </summary>
public class LegacyRoutePrefixMetricsMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<LegacyRoutePrefixMetricsMiddleware> _logger;

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
        
        metrics.IncrementPathRewriteCounter(route, context.Request.Method);
        _logger.LogDebug("Request on deprecated /api/data prefix for route {Route}", route);

        await _next(context);
    }
}
