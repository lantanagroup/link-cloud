using Microsoft.AspNetCore.Mvc;

namespace Link.UI.Controllers;

/// <summary>
/// Thin MVC wrappers around the Admin.BFF cookie auth contract proxied at /api/*.
/// </summary>
public sealed class AuthController : Controller
{
    [HttpGet]
    public IActionResult Login()
    {
        return Redirect("/api/login");
    }

    [HttpGet]
    public IActionResult Logout()
    {
        return Redirect("/api/logout");
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
        return RedirectToAction("Index", "Home");
    }
}
