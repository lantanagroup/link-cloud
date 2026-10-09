using Link.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Link.UI.Controllers;

/// <summary>
/// Thin MVC wrappers around the Admin.BFF cookie auth contract proxied at /api/*.
/// When sign-in is not required, these actions stay on this host.
/// </summary>
public sealed class AuthController : Controller
{
    private readonly LinkUiFeatureOptions _features;

    public AuthController(IOptions<LinkUiFeatureOptions> features)
    {
        _features = features.Value;
    }

    [HttpGet]
    public IActionResult Login()
    {
        if (!SignInRules.IsRequired(_features.SignInRequired))
        {
            var target = SignInRules.ChooseReturn(Request, includeCurrent: false);
            return Redirect(string.IsNullOrEmpty(target) ? "/" : target);
        }

        SignInRules.RememberReturn(HttpContext, includeCurrent: false);
        return Redirect(SignInRules.LoginPath);
    }

    [HttpGet]
    public IActionResult Logout()
    {
        if (!SignInRules.IsRequired(_features.SignInRequired))
            return Redirect("/");

        return Redirect(SignInRules.LogoutPath);
    }

    [HttpGet]
    public IActionResult UnauthorizedAccess()
    {
        ViewData["Title"] = "Unauthorized";
        return View("Unauthorized");
    }

    /// <summary>
    /// Admin.BFF logout redirects the browser to /logout on this host.
    /// </summary>
    [HttpGet("/logout")]
    public IActionResult SignedOut()
    {
        ViewData["Title"] = "Signed out";
        return View();
    }
}
