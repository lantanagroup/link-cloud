using Link.UI.Models;
using Microsoft.AspNetCore.Http;

namespace Link.UI.Services;

/// <summary>
/// Decides whether a request may enter the MVC shell.
/// RequireBffSession redirects anonymous pages to Admin.BFF login.
/// When that flag is off and anonymous access is off, non-public requests get 503
/// (same posture as Automation.UI behind an authenticating proxy).
/// </summary>
public static class ShellAccessGate
{
    public const string AnonymousBlockedMessage =
        "Link UI is not configured to serve requests anonymously in this environment. " +
        "Set Authentication:EnableAnonymousAccess to true when deploying behind an authenticating proxy, " +
        "or set Authentication:RequireBffSession to true to require an Admin.BFF session.";

    public const string SessionCheckFailedMessage =
        "Link UI could not confirm an Admin.BFF session. " +
        "Confirm ServiceRegistry:AdminBffServiceUrl is reachable and /api/user is available.";

    public enum Decision
    {
        Continue,
        Unavailable,
        RedirectToLogin
    }

    public static Decision Evaluate(
        bool allowAnonymousAccess,
        bool requireBffSession,
        PathString path,
        AdminBffUser? user,
        out string? message)
    {
        message = null;

        if (requireBffSession)
        {
            if (IsSessionPublic(path))
                return Decision.Continue;

            if (user is null)
            {
                message = SessionCheckFailedMessage;
                return Decision.Unavailable;
            }

            if (!user.IsAuthenticated)
                return Decision.RedirectToLogin;

            return Decision.Continue;
        }

        if (!allowAnonymousAccess && !IsAnonymousPublic(path))
        {
            message = AnonymousBlockedMessage;
            return Decision.Unavailable;
        }

        return Decision.Continue;
    }

    /// <summary>/health and the /api proxy stay up so probes and Admin.BFF login still answer.</summary>
    public static bool IsAnonymousPublic(PathString path) =>
        path.StartsWithSegments("/health") || path.StartsWithSegments("/api");

    /// <summary>
    /// Paths that must render or redirect without a session: login/logout endpoints,
    /// the post-logout landing, and static files. Hubs use the same sign-in rule as pages.
    /// </summary>
    public static bool IsSessionPublic(PathString path) =>
        IsAnonymousPublic(path)
        || path.StartsWithSegments("/Auth")
        || path.StartsWithSegments("/logout")
        || path.StartsWithSegments("/swagger")
        || Path.HasExtension(path.Value);
}
