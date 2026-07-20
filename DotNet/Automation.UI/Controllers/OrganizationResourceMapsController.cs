using Automation.UI.Models;
using Automation.UI.Services.Persistence;
using Microsoft.AspNetCore.Mvc;

namespace Automation.UI.Controllers;

public class OrganizationResourceMapsController(IOrganizationResourceMapTemplateStore store) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var templates = await store.GetAllAsync(ct);
        return View(templates);
    }

    [HttpGet]
    public async Task<IActionResult> GetJson(Guid id, CancellationToken ct)
    {
        var template = await store.GetByIdAsync(id, ct);
        if (template == null) return NotFound();
        return Json(template);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveInline([FromBody] OrganizationResourceMapTemplate model, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(model.Name))
            return BadRequest("Template name is required.");
        if (model.Conditions == null || model.Conditions.Count == 0)
            return BadRequest("At least one mapping condition is required.");
        if (model.Conditions.Any(c => string.IsNullOrWhiteSpace(c.FhirPath)))
            return BadRequest("All mapping conditions must include a FHIRPath.");

        var existing = await store.GetByIdAsync(model.Id, ct);
        if (existing is { IsSystem: true })
            return StatusCode(StatusCodes.Status403Forbidden, "System template cannot be modified.");

        model.IsSystem = false;
        model.IsDefault = existing?.IsDefault ?? model.IsDefault;
        model.UpdatedAt = DateTimeOffset.UtcNow;
        await store.UpsertAsync(model, ct);
        return Json(new { id = model.Id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteInline([FromBody] IdRequest request, CancellationToken ct)
    {
        var template = await store.GetByIdAsync(request.Id, ct);
        if (template == null) return NotFound();
        if (template.IsSystem)
            return StatusCode(StatusCodes.Status403Forbidden, "System template cannot be deleted.");
        if (template.IsDefault)
            return Conflict("Default template cannot be deleted. Set another template as default first.");

        await store.DeleteAsync(request.Id, ct);
        return Ok();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CloneInline([FromBody] IdRequest request, CancellationToken ct)
    {
        var source = await store.GetByIdAsync(request.Id, ct);
        if (source == null) return NotFound();

        var clone = new OrganizationResourceMapTemplate
        {
            Id = Guid.NewGuid(),
            Name = $"{source.Name} (Copy)",
            Description = source.Description,
            Conditions = source.Conditions.Select(c => new OrganizationResourceMapCondition
            {
                FhirPath = c.FhirPath,
                Priority = c.Priority
            }).ToList(),
            IsSystem = false,
            IsDefault = false,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        await store.UpsertAsync(clone, ct);
        return Json(new { id = clone.Id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SetDefaultInline([FromBody] IdRequest request, CancellationToken ct)
    {
        var template = await store.GetByIdAsync(request.Id, ct);
        if (template == null) return NotFound();

        await store.SetDefaultAsync(request.Id, ct);
        return Ok();
    }

    public sealed class IdRequest
    {
        public Guid Id { get; set; }
    }
}
