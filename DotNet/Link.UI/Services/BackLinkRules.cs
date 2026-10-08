namespace Link.UI.Services;

/// <summary>
/// Decides whether a back control has somewhere else to go, and the label for that place.
/// A control that only returns to the current page, or to the section that page already
/// shows, stays hidden. A real origin is named "Back to &lt;origin&gt;".
/// </summary>
public static class BackLinkRules
{
    public static bool Show(string? currentPath, string? fallbackHref, string? returnUrl)
    {
        var current = StripIndex(ReturnUrlRules.CanonicalPath(currentPath));
        var target = StripIndex(ReturnUrlRules.CanonicalPath(string.IsNullOrWhiteSpace(returnUrl) ? fallbackHref : returnUrl));
        if (string.Equals(current, target, StringComparison.OrdinalIgnoreCase))
            return false;

        if (string.IsNullOrWhiteSpace(returnUrl) && IsSectionHome(current, target))
            return false;

        return true;
    }

    public static string LabelFor(string? returnUrl, string? fallbackHref, string? fallbackLabel)
    {
        if (!string.IsNullOrWhiteSpace(returnUrl))
        {
            var origin = ReturnUrlRules.Label(returnUrl);
            if (origin is not null)
                return origin;
        }

        var fallback = ReturnUrlRules.Label(fallbackHref);
        if (fallback is not null)
            return fallback;

        var raw = (fallbackLabel ?? string.Empty).Trim();
        if (raw.StartsWith("Back to ", StringComparison.OrdinalIgnoreCase))
            return "Back to " + raw["Back to ".Length..].Trim();
        if (raw.Length == 0)
            return "Back to the previous page";
        return "Back to " + raw.ToLowerInvariant();
    }

    /// <summary>
    /// The section root a page belongs to, when that page sits under Logs, Configuration, or System.
    /// </summary>
    public static string? SectionHome(string? path)
    {
        var bare = ReturnUrlRules.CanonicalPath(path).ToLowerInvariant();
        if (bare == "/logs" || bare.StartsWith("/logs/", StringComparison.Ordinal))
            return "/Logs";
        if (bare == "/configuration" || bare.StartsWith("/configuration/", StringComparison.Ordinal))
            return "/Configuration";
        if (bare == "/system" || bare.StartsWith("/system/", StringComparison.Ordinal))
            return "/System";
        return null;
    }

    private static string StripIndex(string path)
    {
        if (path.EndsWith("/Index", StringComparison.OrdinalIgnoreCase))
            path = path[..^"/Index".Length];
        return path.Length == 0 ? "/" : path;
    }

    private static bool IsSectionHome(string current, string target)
    {
        var home = SectionHome(current);
        return home is not null && string.Equals(home, target, StringComparison.OrdinalIgnoreCase);
    }
}
