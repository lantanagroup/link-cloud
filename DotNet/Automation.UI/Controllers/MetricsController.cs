using Automation.UI.Services;
using Microsoft.AspNetCore.Mvc;

namespace Automation.UI.Controllers;

public class MetricsController(MetricsRunPresenter presenter) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(
        int pageNumber = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var dashboard = await presenter.GetDashboardAsync(pageNumber, pageSize, cancellationToken);
        return View(dashboard);
    }

    [HttpGet]
    public async Task<IActionResult> Details(Guid id, CancellationToken cancellationToken)
    {
        var detail = await presenter.GetDetailAsync(id, cancellationToken);
        if (detail == null)
            return NotFound();

        return View(detail);
    }
}
