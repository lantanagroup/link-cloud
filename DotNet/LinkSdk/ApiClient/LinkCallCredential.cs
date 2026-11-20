using LantanaGroup.Link.Shared.Application.Models.Configs;

namespace LantanaGroup.Link.Sdk.ApiClient;

/// <summary>How a call routed through Admin.BFF authenticates. Exactly one kind per call.</summary>
public enum LinkCallCredentialKind
{
    /// <summary>Send no credential. Admin.BFF answers 401 unless it allows anonymous access.</summary>
    None,
    /// <summary>Forward the signed-in user's own cookie and/or Authorization header. No system token.</summary>
    ForwardUser,
    /// <summary>Mint the system token and name the user who started the work in the audit headers.</summary>
    SystemOnBehalfOf
}

/// <summary>
/// The credential for one Admin.BFF call. The factories are the only way to build one, so a value is
/// either the user's own session or the system token with an initiator, never both.
/// </summary>
public sealed class LinkCallCredential
{
    public static readonly LinkCallCredential None = new(LinkCallCredentialKind.None, null, null, null, null);

    public LinkCallCredentialKind Kind { get; }
    public string? Cookie { get; }
    public string? Authorization { get; }
    public string? InitiatedById { get; }
    public string? InitiatedByName { get; }

    private LinkCallCredential(LinkCallCredentialKind kind, string? cookie, string? authorization, string? initiatedById, string? initiatedByName)
    {
        Kind = kind;
        Cookie = cookie;
        Authorization = authorization;
        InitiatedById = initiatedById;
        InitiatedByName = initiatedByName;
    }

    public static LinkCallCredential ForwardUser(string? cookie, string? authorization)
    {
        cookie = string.IsNullOrWhiteSpace(cookie) ? null : cookie;
        authorization = string.IsNullOrWhiteSpace(authorization) ? null : authorization;
        return cookie is null && authorization is null
            ? None
            : new LinkCallCredential(LinkCallCredentialKind.ForwardUser, cookie, authorization, null, null);
    }

    public static LinkCallCredential SystemOnBehalfOf(string initiatedById, string? initiatedByName)
    {
        if (string.IsNullOrWhiteSpace(initiatedById))
            throw new ArgumentException("A background call must name who started it.", nameof(initiatedById));
        return new LinkCallCredential(LinkCallCredentialKind.SystemOnBehalfOf, null, null, initiatedById, initiatedByName);
    }

    /// <summary>Never prints the cookie or token.</summary>
    public override string ToString() => Kind switch
    {
        LinkCallCredentialKind.ForwardUser => $"ForwardUser(cookie={(Cookie is null ? "no" : "yes")}, authorization={(Authorization is null ? "no" : "yes")})",
        LinkCallCredentialKind.SystemOnBehalfOf => $"SystemOnBehalfOf({InitiatedById})",
        _ => "None"
    };
}

/// <summary>Chooses the credential for the call about to be sent.</summary>
public interface ILinkCallCredentialSource
{
    LinkCallCredential Resolve();
}

/// <summary>Audit header names shared by Link.UI and Admin.BFF.</summary>
public static class LinkAuditHeaders
{
    public const string InitiatedBy = "X-Link-Initiated-By";
    /// <summary>Percent-encoded display name, so non-ASCII names survive the header.</summary>
    public const string InitiatedByName = "X-Link-Initiated-By-Name";
    public const int MaxValueLength = 256;
}

/// <summary>
/// Routes a service client through Admin.BFF's pass-through proxy. The base URL is
/// <see cref="ServiceRegistry.AdminBffServiceApiUrl"/>; the client's relative paths stay the same,
/// because <c>{bff}/api/census/...</c> is proxied to <c>{census}/api/census/...</c>.
/// </summary>
public sealed class AdminBffRoute
{
    public string BaseUrl { get; }
    public ILinkCallCredentialSource Credentials { get; }

    public AdminBffRoute(string baseUrl, ILinkCallCredentialSource credentials)
    {
        if (string.IsNullOrWhiteSpace(baseUrl))
            throw new ArgumentException("Admin.BFF URL is not configured.", nameof(baseUrl));
        BaseUrl = baseUrl;
        Credentials = credentials ?? throw new ArgumentNullException(nameof(credentials));
    }

    public static AdminBffRoute FromRegistry(ServiceRegistry registry, ILinkCallCredentialSource credentials) =>
        new(registry.AdminBffServiceApiUrl
                ?? throw new InvalidOperationException("Admin.BFF URL is not configured in ServiceRegistry."),
            credentials);
}
