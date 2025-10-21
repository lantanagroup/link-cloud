using Automation.UI.Models;
using Automation.UI.Services.Persistence;
using Microsoft.AspNetCore.Mvc;

namespace Automation.UI.Controllers;

public class QueryPlansController(IQueryPlanTemplateStore store) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var templates = await store.GetAllAsync(ct);
        return View(templates);
    }

    [HttpGet]
    public IActionResult GetDefaults()
    {
        var defaults = QueryPlanDefaults.GetDefaultAsInput();
        var model = new QueryPlanTemplate
        {
            Id = Guid.NewGuid(),
            Name = "New Query Plan",
            EhrDescription = defaults.EhrDescription ?? "Epic",
            LookBack = defaults.LookBack ?? "P0D",
            InitialQueries = defaults.InitialQueries.Select(ToQueryEntry).ToList(),
            SupplementalQueries = defaults.SupplementalQueries.Select(ToQueryEntry).ToList()
        };
        return Json(model);
    }

    [HttpGet]
    public async Task<IActionResult> GetJson(Guid id, CancellationToken ct)
    {
        var template = await store.GetByIdAsync(id, ct);
        if (template == null) return NotFound();
        return Json(template);
    }

    [HttpGet]
    public async Task<IActionResult> AcquiredTypes(Guid id, CancellationToken ct)
    {
        var template = await store.GetByIdAsync(id, ct);
        if (template == null) return NotFound();
        return Json(template.GetAcquiredResourceTypes().OrderBy(t => t).ToList());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveInline([FromBody] QueryPlanTemplate model, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(model.Name))
            return BadRequest("Query plan name is required.");

        var existing = await store.GetByIdAsync(model.Id, ct);
        if (existing is { IsSystem: true })
            return StatusCode(StatusCodes.Status403Forbidden, "Forbidden: system plan cannot be modified.");

        model.IsSystem = false;
        model.UpdatedAt = DateTimeOffset.UtcNow;
        await store.UpsertAsync(model, ct);
        return Json(new { id = model.Id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteInline([FromBody] IdRequest request, CancellationToken ct)
    {
        if (!this.TryValidateIdRequest(request, out var badRequest))
            return badRequest;

        var template = await store.GetByIdAsync(request.Id, ct);
        if (template == null)
            return NotFound();
        if (template.IsSystem)
            return StatusCode(StatusCodes.Status403Forbidden, "Forbidden: system plan cannot be deleted.");

        await store.DeleteAsync(request.Id, ct);
        return Ok();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CloneInline([FromBody] IdRequest request, CancellationToken ct)
    {
        if (!this.TryValidateIdRequest(request, out var badRequest))
            return badRequest;

        var source = await store.GetByIdAsync(request.Id, ct);
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
        return Json(new { id = clone.Id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SetDefaultInline([FromBody] IdRequest request, CancellationToken ct)
    {
        if (!this.TryValidateIdRequest(request, out var badRequest))
            return badRequest;

        var template = await store.GetByIdAsync(request.Id, ct);
        if (template == null) return NotFound();

        await store.SetDefaultAsync(request.Id, ct);
        return Ok();
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
