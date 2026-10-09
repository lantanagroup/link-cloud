using Link.UI.Services;
using Microsoft.AspNetCore.Mvc;

namespace Link.UI.Controllers;

public sealed class HomeController : Controller
{
    private readonly IHomeOverview _overview;

    public HomeController(IHomeOverview overview)
    {
        _overview = overview;
    }

    public IActionResult Index()
    {
        ViewData["Title"] = "Dashboard";
        return View();
    }

    /// <summary>
    /// Admin.BFF login sets RedirectUri to {origin}/dashboard. Land on the dashboard,
    /// or on the local return path stored before the challenge. That path is read once.
    /// </summary>
    [HttpGet("/dashboard")]
    public IActionResult Dashboard()
    {
        var target = SignInRules.TakeReturn(Request, Response);
        if (!string.IsNullOrEmpty(target) && !string.Equals(target, "/", StringComparison.Ordinal))
            return Redirect(target);

        return RedirectToAction(nameof(Index));
    }

    /// <summary>HTML fragment the home region swaps in. The shell paints before this runs.</summary>
    [HttpGet("/Home/overview")]
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public async Task<IActionResult> Overview(CancellationToken cancellationToken)
    {
        return PartialView("_Overview", await _overview.LoadAsync(cancellationToken));
    }

    /// <summary>Same cards as <see cref="Overview"/>, as JSON. Same sign-in as the rest of the shell.</summary>
    [HttpGet("/Home/overview/data")]
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public async Task<IActionResult> OverviewData(CancellationToken cancellationToken)
    {
        return Json(await _overview.LoadAsync(cancellationToken));
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        ViewData["Title"] = "Error";
        return View();
    }
}
