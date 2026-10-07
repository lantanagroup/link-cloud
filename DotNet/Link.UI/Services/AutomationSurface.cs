using Microsoft.AspNetCore.Http;

namespace Link.UI.Services;

/// <summary>
/// Routes that exist only while <see cref="LinkUiFeatureOptions.AutomationEnabled"/> is on.
/// </summary>
public static class AutomationSurface
{
    private static readonly string[] Prefixes =
    [
        "/Automation",
        "/Cleanup",
        "/Metrics",
        "/ApiHealth",
        "/Runs",
        "/api/runs",
        "/api/api-health-runs",
        "/hubs/runs",
        "/hubs/cleanup"
    ];

    public static bool IsAutomationPath(PathString path)
    {
        foreach (var prefix in Prefixes)
        {
            if (path.StartsWithSegments(prefix))
                return true;
        }

        return false;
    }
}
