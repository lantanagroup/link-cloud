using Automation.UI.Services.Persistence;
using Automation.UI.Models;
using Automation.UI.Services.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Automation.UI.Controllers;

[Authorize]
public class QueryPlansController(IQueryPlanTemplateStore store) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var templates = await store.GetAllAsync(ct);
        return View(templates);
    }

    [HttpGet]
    public IActionResult Create()
    {
        // Pre-populate with system defaults so the user has a starting point.
        var defaults = QueryPlanDefaults.GetDefaultAsInput();
        var model = new QueryPlanTemplate
        {
            Name = "New Query Plan",
            EhrDescription = defaults.EhrDescription ?? "Epic",
            LookBack = defaults.LookBack ?? "P0D",
            InitialQueries = defaults.InitialQueries.Select(ToQueryEntry).ToList(),
            SupplementalQueries = defaults.SupplementalQueries.Select(ToQueryEntry).ToList()
        };
        return View("Edit", model);
    }

    [HttpGet]
    public async Task<IActionResult> Edit(Guid id, CancellationToken ct)
    {
        var template = await store.GetByIdAsync(id, ct);
        if (template == null) return NotFound();
        if (template.IsSystem) return RedirectToAction(nameof(Details), new { id });
        return View(template);
    }

    [HttpGet]
    public async Task<IActionResult> Details(Guid id, CancellationToken ct)
    {
        var template = await store.GetByIdAsync(id, ct);
        if (template == null) return NotFound();
        return View(template);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Save(QueryPlanTemplate model, CancellationToken ct)
    {
        if (!ModelState.IsValid)
            return View("Edit", model);

        var existing = await store.GetByIdAsync(model.Id, ct);
        if (existing is { IsSystem: true })
            return RedirectToAction(nameof(Details), new { id = model.Id });

        model.IsSystem = false;
        model.UpdatedAt = DateTimeOffset.UtcNow;
        await store.UpsertAsync(model, ct);
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Clone(Guid id, CancellationToken ct)
    {
        var source = await store.GetByIdAsync(id, ct);
        if (source == null) return NotFound();

        var clone = new QueryPlanTemplate
        {
            Id = Guid.NewGuid(),
            Name = $"{source.Name} (Copy)",
            Description = source.Description,
            IsSystem = false,
            IsDefault = false,
            EhrDescription = source.EhrDescription,
            LookBack = source.LookBack,
            InitialQueries = source.InitialQueries.Select(CloneEntry).ToList(),
            SupplementalQueries = source.SupplementalQueries.Select(CloneEntry).ToList(),
            UpdatedAt = DateTimeOffset.UtcNow
        };

        await store.UpsertAsync(clone, ct);
        return RedirectToAction(nameof(Edit), new { id = clone.Id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SetDefault(Guid id, CancellationToken ct)
    {
        var template = await store.GetByIdAsync(id, ct);
        if (template == null) return NotFound();

        await store.SetDefaultAsync(id, ct);
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var template = await store.GetByIdAsync(id, ct);
        if (template is null or { IsSystem: true })
            return RedirectToAction(nameof(Index));

        await store.DeleteAsync(id, ct);
        return RedirectToAction(nameof(Index));
    }

    /// <summary>Returns the acquired resource types for a template (JSON API for AJAX).</summary>
    [HttpGet]
    public async Task<IActionResult> AcquiredTypes(Guid id, CancellationToken ct)
    {
        var template = await store.GetByIdAsync(id, ct);
        if (template == null) return NotFound();
        return Json(template.GetAcquiredResourceTypes().OrderBy(t => t).ToList());
    }

    private static QueryEntry ToQueryEntry(QueryPlanQueryEntry src) => new()
    {
        ResourceType = src.ResourceType,
        QueryConfigType = src.QueryConfigType,
        OperationType = src.OperationType,
        Paged = src.Paged,
        Parameters = src.Parameters.Select(p => new QueryParameter
        {
            ParameterType = p.ParameterType,
            Name = p.Name,
            Variable = p.Variable,
            Format = p.Format,
            Literal = p.Literal,
            Resource = p.Resource,
            PagedValue = p.PagedValue
        }).ToList()
    };

    private static QueryEntry CloneEntry(QueryEntry src) => new()
    {
        ResourceType = src.ResourceType,
        QueryConfigType = src.QueryConfigType,
        OperationType = src.OperationType,
        Paged = src.Paged,
        Parameters = src.Parameters.Select(p => new QueryParameter
        {
            ParameterType = p.ParameterType,
            Name = p.Name,
            Variable = p.Variable,
            Format = p.Format,
            Literal = p.Literal,
            Resource = p.Resource,
            PagedValue = p.PagedValue
        }).ToList()
    };
}
