namespace Link.UI.Models;

/// <summary>
/// Back control. When the request has a safe returnUrl, that target and its label win.
/// Otherwise the fallback is the logical parent.
/// </summary>
public sealed record BackLink(string FallbackHref, string FallbackLabel);
