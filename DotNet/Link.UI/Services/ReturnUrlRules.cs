namespace Link.UI.Services;

/// <summary>
/// Local return targets for list-to-item navigation. Only a same-site path is accepted.
/// </summary>
public static class ReturnUrlRules
{
    public const int MaxLength = 2048;

    public static string? Sanitize(string? candidate)
    {
        if (string.IsNullOrWhiteSpace(candidate))
            return null;

        var value = candidate.Trim();
        if (value.Length > MaxLength || !IsLocal(value))
            return null;

        var decoded = value;
        for (var pass = 0; pass < 3; pass++)
        {
            string next;
            try
            {
                next = Uri.UnescapeDataString(decoded);
            }
            catch (UriFormatException)
            {
                return null;
            }

            if (!IsLocal(next))
                return null;
            if (next == decoded)
                break;
            decoded = next;
        }

        return value;
    }

    public static string? FromQuery(HttpRequest request)
    {
        if (!request.Query.TryGetValue("returnUrl", out var values))
            return null;
        return Sanitize(values.ToString());
    }

    /// <summary>
    /// The page a person can open again. Refresh fragments collapse onto that page.
    /// </summary>
    public static string CanonicalPath(string? path)
    {
        var value = string.IsNullOrWhiteSpace(path) ? "/" : path.Trim();
        if (value.Length == 0 || value[0] != '/')
            value = "/" + value.TrimStart('/');

        var bare = value.Split('?', 2)[0].TrimEnd('/');
        if (bare.Length == 0)
            bare = "/";

        var key = bare.ToLowerInvariant();
        if (key is "/" or "/home" or "/home/index" or "/home/overview" or "/home/overview/data" or "/dashboard")
            return "/";
        if (key is "/automation/recent" or "/automation/data")
            return "/Automation";
        return bare;
    }

    /// <summary>
    /// The current page, including its query, so a detail link can come back to this search.
    /// </summary>
    public static string Here(HttpRequest request)
    {
        var path = CanonicalPath(request.Path.HasValue ? request.Path.Value : "/");

        if (request.Query.Count == 0)
            return path;

        var pairs = new List<string>();
        foreach (var key in request.Query.Keys)
        {
            if (string.IsNullOrEmpty(key))
                continue;
            foreach (var value in request.Query[key])
                pairs.Add(Uri.EscapeDataString(key) + "=" + Uri.EscapeDataString(value ?? ""));
        }

        return pairs.Count == 0 ? path : path + "?" + string.Join("&", pairs);
    }

    public static string WithReturn(string? href, string? returnUrl)
    {
        var safe = Sanitize(returnUrl);
        if (string.IsNullOrWhiteSpace(href) || safe is null)
            return href ?? "";
        if (href.Contains("returnUrl=", StringComparison.OrdinalIgnoreCase))
            return href;

        var join = href.Contains('?', StringComparison.Ordinal) ? "&" : "?";
        return href + join + "returnUrl=" + Uri.EscapeDataString(safe);
    }

    public static Dictionary<string, string> WithOrigin(this Dictionary<string, string> route, string? returnUrl)
    {
        var safe = Sanitize(returnUrl);
        if (safe is not null)
            route["returnUrl"] = safe;
        return route;
    }

    /// <summary>
    /// A short back label for a known list or item path. Unknown paths return null so the caller keeps its own fallback.
    /// </summary>
    public static string? Label(string? safeUrl)
    {
        if (string.IsNullOrWhiteSpace(safeUrl))
            return null;

        var path = safeUrl.Split('?', 2)[0].TrimEnd('/').ToLowerInvariant();
        if (path.Length == 0)
            path = "/";

        if (path is "/" or "/home" or "/home/index" or "/home/overview" or "/home/overview/data" or "/dashboard")
            return "Back to dashboard";
        if (path is "/reports" or "/reports/index" or "/reports/generate")
            return "Back to reports";
        if (path.StartsWith("/reports/", StringComparison.Ordinal))
            return "Back to report";
        if (path is "/logs" or "/logs/index" or "/logs/acquisition" or "/logs/audit" or "/logs/sftp" or "/logs/kafka")
            return "Back to logs";
        if (path.StartsWith("/logs/", StringComparison.Ordinal))
            return "Back to log";
        if (path is "/tenants" or "/tenants/index")
            return "Back to tenants";
        if (path.StartsWith("/tenants/report", StringComparison.Ordinal))
            return "Back to report";
        if (path.StartsWith("/tenants/", StringComparison.Ordinal))
            return "Back to facility";
        if (path is "/automation" or "/automation/index" or "/automation/recent" or "/automation/data")
            return "Back to runs";
        if (path.StartsWith("/automation/run", StringComparison.Ordinal))
            return "Back to run";
        if (path.StartsWith("/automation/", StringComparison.Ordinal))
            return "Back to automation";
        if (path is "/system" or "/system/index")
            return "Back to system";
        if (path is "/system/users" or "/system/users/index")
            return "Back to users";
        if (path.StartsWith("/system/users/", StringComparison.Ordinal))
            return "Back to account";
        if (path is "/configuration" or "/configuration/index")
            return "Back to configuration";
        if (path is "/configuration/notifications")
            return "Back to notifications";
        if (path.StartsWith("/configuration/notifications/", StringComparison.Ordinal))
            return "Back to notification";
        if (path is "/configuration/measures")
            return "Back to measures";
        if (path.StartsWith("/configuration/measures/", StringComparison.Ordinal))
            return "Back to measure";
        if (path is "/configuration/validation")
            return "Back to validation";
        if (path is "/configuration/vendors")
            return "Back to vendors";
        if (path is "/configuration/terminology")
            return "Back to terminology";
        if (path.StartsWith("/configuration/", StringComparison.Ordinal))
            return "Back to configuration";
        if (path is "/metrics" or "/metrics/index")
            return "Back to metrics";
        if (path.StartsWith("/metrics/scenario", StringComparison.Ordinal))
            return "Back to history";
        if (path.StartsWith("/metrics/details", StringComparison.Ordinal))
            return "Back to performance";
        if (path.StartsWith("/metrics/", StringComparison.Ordinal))
            return "Back to metrics";

        return null;
    }

    private static bool IsLocal(string value)
    {
        if (value.Length == 0 || value[0] != '/')
            return false;
        if (value.StartsWith("//", StringComparison.Ordinal) || value.StartsWith("/\\", StringComparison.Ordinal))
            return false;
        if (value.Contains('\\', StringComparison.Ordinal) || value.Contains("://", StringComparison.Ordinal))
            return false;
        if (value.IndexOfAny(['\r', '\n', '\0', ' ', '\t']) >= 0)
            return false;

        var path = value.Split('?', 2)[0];
        return !path.Contains(':', StringComparison.Ordinal);
    }
}
