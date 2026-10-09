using Microsoft.AspNetCore.Rewrite;

namespace LantanaGroup.Link.DataAcquisition.Infrastructure;

// See dev-docs/data-acquisition-route-prefix.md
/// <summary>
/// Serves the deprecated /api/data prefix by rewriting it to /api/data-acquisition before routing.
/// </summary>
public class LegacyRoutePrefixRule : IRule
{
    /// <summary>
    /// HttpContext.Items key set when a request arrived on the legacy prefix.
    /// </summary>
    public const string LegacyRequestItemKey = "LegacyDataRoutePrefix";

    private static readonly PathString _legacyPrefix = "/api/data";
    private static readonly PathString _canonicalPrefix = "/api/data-acquisition";

    /// <summary>
    /// Rewrites a legacy-prefixed path in place, leaving the query string untouched.
    /// </summary>
    public void ApplyRule(RewriteContext context)
    {
        var request = context.HttpContext.Request;
        if (!request.Path.StartsWithSegments(_legacyPrefix, StringComparison.OrdinalIgnoreCase, out var remainder))
        {
            return;
        }

        request.Path = _canonicalPrefix.Add(remainder);
        context.HttpContext.Items[LegacyRequestItemKey] = true;
        context.Result = RuleResult.ContinueRules;
    }
}
