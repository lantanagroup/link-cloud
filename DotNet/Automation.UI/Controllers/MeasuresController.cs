using System.Text;
using Automation.UI.Models;
using Automation.UI.Services;
using Automation.UI.Services.Persistence;
using LantanaGroup.Automation.Generation;
using Microsoft.AspNetCore.Mvc;

namespace Automation.UI.Controllers;

public class MeasuresController(IMeasureTemplateStore store) : Controller
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
        return Json(new MeasureTemplate
        {
            Id = Guid.NewGuid(),
            Name = "New Measure",
            GenerationFamily = ProfiledMeasureType.NhsnAcuteCareHospitalMonthlyInitialPopulation
        });
    }

    [HttpGet]
    public async Task<IActionResult> GetJson(Guid id, CancellationToken ct)
    {
        var template = await store.GetByIdAsync(id, ct);
        if (template == null) return NotFound();
        return Json(template);
    }

    [HttpGet]
    public async Task<IActionResult> Download(Guid id, CancellationToken ct)
    {
        var template = await store.GetByIdAsync(id, ct);
        if (template == null)
            return NotFound();
        if (string.IsNullOrWhiteSpace(template.BundleJson))
            return NotFound("This measure has no bundle content.");

        return File(
            Encoding.UTF8.GetBytes(template.BundleJson),
            "application/fhir+json",
            FileNameFor(template));
    }

    private static string FileNameFor(MeasureTemplate template)
    {
        var raw = !string.IsNullOrWhiteSpace(template.MeasureId)
            ? template.MeasureId
            : template.Name;
        raw ??= template.Id.ToString("N");
        var invalid = Path.GetInvalidFileNameChars();
        var cleaned = new string(raw.Select(c => Array.IndexOf(invalid, c) >= 0 ? '_' : c).ToArray()).Trim();
        if (string.IsNullOrWhiteSpace(cleaned))
            cleaned = template.Id.ToString("N");
        return cleaned.EndsWith(".json", StringComparison.OrdinalIgnoreCase)
            ? cleaned
            : cleaned + ".json";
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveInline([FromBody] MeasureTemplate model, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(model.Name))
            return BadRequest("Measure name is required.");
        if (!Enum.IsDefined(model.GenerationFamily))
            return BadRequest("A supported generation type is required.");

        var existing = await store.GetByIdAsync(model.Id, ct);
        if (existing is { IsSystem: true })
            return StatusCode(StatusCodes.Status403Forbidden, "Forbidden: system measure cannot be modified.");

        try
        {
            var parsed = MeasureBundleParser.Parse(model.BundleJson);
            MeasureBundleParser.ApplyMetadata(model, parsed);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }

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
            return StatusCode(StatusCodes.Status403Forbidden, "Forbidden: system measure cannot be deleted.");

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

        var clone = new MeasureTemplate
        {
            Id = Guid.NewGuid(),
            Name = $"{source.Name} (Copy)",
            Description = source.Description,
            IsSystem = false,
            UpdatedAt = DateTimeOffset.UtcNow,
            GenerationFamily = source.GenerationFamily,
            BundleJson = source.BundleJson,
            MeasureId = source.MeasureId,
            CanonicalUrl = source.CanonicalUrl,
            Version = source.Version,
            MeasureDate = source.MeasureDate,
            Status = source.Status
        };

        await store.UpsertAsync(clone, ct);
        return Json(new { id = clone.Id });
    }
}
