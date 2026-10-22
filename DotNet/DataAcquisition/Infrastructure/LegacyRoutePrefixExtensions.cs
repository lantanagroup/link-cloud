using Microsoft.AspNetCore.Rewrite;

namespace LantanaGroup.Link.DataAcquisition.Infrastructure;

/// <summary>
/// Wires the deprecated /api/data alias into the request pipeline.
/// </summary>
public static class LegacyRoutePrefixExtensions
{
    /// <summary>
    /// Adds routing with the /api/data alias around it. Call this in place of UseRouting.
    /// </summary>
    /// <remarks>
    /// The rewrite must run before routing matches and the counter after it, or legacy paths 404 and
    /// go uncounted. Owning the UseRouting call is what keeps that order in one place.
    /// </remarks>
    public static IApplicationBuilder UseRoutingWithLegacyRoutePrefix(this IApplicationBuilder app)
    {
        app.UseRewriter(new RewriteOptions().Add(new LegacyRoutePrefixRule()));
        app.UseRouting();
        app.UseMiddleware<LegacyRoutePrefixMetricsMiddleware>();

        return app;
    }
}
