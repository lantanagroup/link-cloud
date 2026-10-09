namespace Link.UI.Services;

/// <summary>
/// Sign-in is required exactly when Authentication:RequireBffSession is true.
/// That flag is Link.UI's equivalent of the retired Admin.UI authRequired switch.
/// Authentication:EnableAnonymousAccess only decides whether the shell is browsable
/// when a session is not required. This type is the only place that names the
/// proxied Admin.BFF login and logout paths.
/// </summary>
public static class SignInRules
{
    public const string LoginPath = "/api/login";
    public const string LogoutPath = "/api/logout";
    public const string ReturnCookieName = "link.ui.return";

    public static readonly TimeSpan ReturnCookieLifetime = TimeSpan.FromMinutes(10);

    public static bool IsRequired(bool requireBffSession) => requireBffSession;

    public static bool IsProxiedLogin(PathString path) =>
        path.StartsWithSegments(LoginPath, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// A local path the browser may be sent to after Admin.BFF returns to /dashboard.
    /// Auth endpoints and the dashboard landing itself are not stored.
    /// </summary>
    public static string? NormalizeReturn(string? candidate)
    {
        var safe = ReturnUrlRules.Sanitize(candidate);
        if (safe is null || IsAuthLoop(safe))
            return null;

        return ReturnUrlRules.CanonicalPath(safe) == "/" ? "/" : safe;
    }

    /// <summary>
    /// Prefer a validated returnUrl query, then a same-origin Referer, then the current request.
    /// </summary>
    public static string? ChooseReturn(HttpRequest request, bool includeCurrent)
    {
        var fromQuery = NormalizeReturn(ReturnUrlRules.FromQuery(request));
        if (fromQuery is not null)
            return fromQuery;

        if (request.Headers.TryGetValue("Referer", out var refererValues)
            && Uri.TryCreate(refererValues.ToString(), UriKind.Absolute, out var referer)
            && SameOrigin(request, referer))
        {
            var fromReferer = NormalizeReturn(referer.PathAndQuery);
            if (fromReferer is not null)
                return fromReferer;
        }

        if (!includeCurrent)
            return null;

        return NormalizeReturn(request.PathBase + request.Path + request.QueryString);
    }

    public static void RememberReturn(HttpContext context, bool includeCurrent)
    {
        var target = ChooseReturn(context.Request, includeCurrent);
        if (target is null)
        {
            context.Response.Cookies.Delete(ReturnCookieName, DeleteCookie(context.Request));
            return;
        }

        context.Response.Cookies.Append(ReturnCookieName, target, AppendCookie(context.Request));
    }

    /// <summary>
    /// Reads the return cookie once. The cookie is removed even when the value is rejected.
    /// </summary>
    public static string? TakeReturn(HttpRequest request, HttpResponse response)
    {
        request.Cookies.TryGetValue(ReturnCookieName, out var raw);
        response.Cookies.Delete(ReturnCookieName, DeleteCookie(request));
        return NormalizeReturn(raw);
    }

    private static CookieOptions AppendCookie(HttpRequest request) => new()
    {
        HttpOnly = true,
        Secure = request.IsHttps,
        SameSite = SameSiteMode.Lax,
        Path = "/",
        MaxAge = ReturnCookieLifetime,
        IsEssential = true
    };

    private static CookieOptions DeleteCookie(HttpRequest request) => new()
    {
        HttpOnly = true,
        Secure = request.IsHttps,
        SameSite = SameSiteMode.Lax,
        Path = "/",
        IsEssential = true
    };

    private static bool SameOrigin(HttpRequest request, Uri uri)
    {
        if (!request.Host.HasValue)
            return false;
        if (!string.Equals(uri.Scheme, request.Scheme, StringComparison.OrdinalIgnoreCase))
            return false;
        if (!string.Equals(uri.Host, request.Host.Host, StringComparison.OrdinalIgnoreCase))
            return false;

        var requestPort = request.Host.Port ?? (string.Equals(request.Scheme, "https", StringComparison.OrdinalIgnoreCase) ? 443 : 80);
        return uri.Port == requestPort;
    }

    private static bool IsAuthLoop(string safe)
    {
        var path = safe.Split('?', 2)[0].TrimEnd('/');
        if (path.Length == 0)
            path = "/";

        return path.Equals("/auth/login", StringComparison.OrdinalIgnoreCase)
            || path.Equals("/auth/logout", StringComparison.OrdinalIgnoreCase)
            || path.Equals("/logout", StringComparison.OrdinalIgnoreCase)
            || path.Equals(LoginPath, StringComparison.OrdinalIgnoreCase)
            || path.Equals(LogoutPath, StringComparison.OrdinalIgnoreCase);
    }
}
