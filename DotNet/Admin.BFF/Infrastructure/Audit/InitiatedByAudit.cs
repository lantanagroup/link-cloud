using System.Security.Claims;
using System.Text;

namespace LantanaGroup.Link.LinkAdmin.BFF.Infrastructure.Audit;

/// <summary>
/// Reads the audit headers a background caller (for example a Link.UI automation run that outlived
/// its HTTP request) sends with a system token, so the proxy log records who started the work.
/// The values are a claim by the caller, not an identity: they are logged next to the authenticated
/// principal and never used for authorization.
/// </summary>
public static class InitiatedByAudit
{
    public const string InitiatedByHeader = "X-Link-Initiated-By";
    public const string InitiatedByNameHeader = "X-Link-Initiated-By-Name";
    public const int MaxValueLength = 256;

    public readonly record struct Entry(string InitiatedById, string? InitiatedByName, string Principal);

    /// <summary>Returns null when the request carries no initiated-by header.</summary>
    public static Entry? Read(HttpContext context)
    {
        var id = Clean(Unescape(context.Request.Headers[InitiatedByHeader].ToString()));
        if (id is null)
            return null;

        var name = Clean(Unescape(context.Request.Headers[InitiatedByNameHeader].ToString()));
        return new Entry(id, name, PrincipalOf(context.User));
    }

    private static string Unescape(string value)
    {
        try { return Uri.UnescapeDataString(value); }
        catch (UriFormatException) { return value; }
    }

    public static string PrincipalOf(ClaimsPrincipal? user)
    {
        if (user?.Identity?.IsAuthenticated != true)
            return "anonymous";
        return user.FindFirst("sub")?.Value
            ?? user.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? user.Identity?.Name
            ?? "authenticated";
    }

    /// <summary>Drops control characters (log forging) and caps the length.</summary>
    public static string? Clean(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;
        var sb = new StringBuilder(Math.Min(value.Length, MaxValueLength));
        foreach (var c in value)
        {
            if (sb.Length >= MaxValueLength)
                break;
            if (!char.IsControl(c))
                sb.Append(c);
        }
        var cleaned = sb.ToString().Trim();
        return cleaned.Length == 0 ? null : cleaned;
    }
}
