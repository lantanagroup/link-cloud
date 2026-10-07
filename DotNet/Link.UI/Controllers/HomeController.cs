using Link.UI.Models;
using Link.UI.Services;
using Microsoft.AspNetCore.Mvc;

namespace Link.UI.Controllers;

public sealed class HomeController : Controller
{
    private readonly IAdminBffUserService _userService;

    public HomeController(IAdminBffUserService userService)
    {
        _userService = userService;
    }

    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        ViewData["Title"] = "Home";
        var user = await _userService.GetCurrentUserAsync(cancellationToken);
        return View(user);
    }

    /// <summary>
    /// Admin.BFF login sets RedirectUri to {origin}/dashboard. Land on Home.
    /// </summary>
    [HttpGet("/dashboard")]
    public IActionResult Dashboard() => RedirectToAction(nameof(Index));

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        ViewData["Title"] = "Error";
        return View();
    }
}
