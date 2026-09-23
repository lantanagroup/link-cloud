using Automation.UI.Models;
using Automation.UI.Services.Persistence;
using Microsoft.AspNetCore.Mvc;

namespace Automation.UI.Controllers;

public class FacilityTemplatesController(
    IFacilityTemplateStore store,
    IQueryPlanTemplateStore queryPlanTemplateStore,
    INormalizationStore normalizationStore,
    IOrganizationResourceMapTemplateStore organizationResourceMapTemplateStore,
    IPatientConfigurationStore patientConfigurationStore,
    IScenarioStore scenarioStore) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        ViewBag.QueryPlanTemplates = await queryPlanTemplateStore.GetAllAsync(ct);
        ViewBag.NormalizationSuites = await normalizationStore.GetAllSuitesAsync(ct);
        ViewBag.OrganizationResourceMaps = await organizationResourceMapTemplateStore.GetAllAsync(ct);
        ViewBag.PatientConfigurations = await patientConfigurationStore.GetAllAsync(ct);
        return View(await store.GetAllAsync(ct));
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
    public async Task<IActionResult> SaveInline([FromBody] FacilityTemplate? model, CancellationToken ct)
    {
        if (model is null)
            return BadRequest("Facility template is required.");

        if (string.IsNullOrWhiteSpace(model.Name))
            return BadRequest("Template name is required.");

        model.Name = model.Name.Trim();
        model.Description = string.IsNullOrWhiteSpace(model.Description) ? null : model.Description.Trim();
        model.VendorName = string.IsNullOrWhiteSpace(model.VendorName) ? null : model.VendorName.Trim();
        model.AllowedPatientConfigurationIds = (model.AllowedPatientConfigurationIds ?? [])
            .Where(id => id != Guid.Empty)
            .Distinct()
            .ToList();

        if (model.EnableOrganizationLocationMapping && !model.OrganizationResourceMapTemplateId.HasValue)
            return BadRequest("Organization location mapping is on, so an organization resource map is required.");

        if (!model.AllowPatientConfigurationsOutsideSet && model.AllowedPatientConfigurationIds.Count == 0)
            return BadRequest("A closed patient configuration set needs at least one patient configuration.");

        if (!model.EnableOrganizationLocationMapping)
            model.OrganizationResourceMapTemplateId = null;

        var templates = await store.GetAllAsync(ct);
        if (templates.Any(t => t.Id != model.Id && string.Equals(t.Name, model.Name, StringComparison.OrdinalIgnoreCase)))
            return Conflict($"A facility template named '{model.Name}' already exists.");

        var existing = await store.GetByIdAsync(model.Id, ct);
        if (existing is { IsSystem: true })
            return StatusCode(StatusCodes.Status403Forbidden, "System template cannot be modified.");

        var referenceError = await ValidateReferencesAsync(model, ct);
        if (referenceError != null)
            return BadRequest(referenceError);

        model.IsSystem = false;
        model.IsDefault = false;
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
        if (template == null) return NotFound();
        if (template.IsSystem)
            return StatusCode(StatusCodes.Status403Forbidden, "System template cannot be deleted.");
        if (template.IsDefault)
            return Conflict("The system facility config cannot be deleted.");

        var usedBy = (await scenarioStore.GetAllAsync(ct))
            .Where(s => s.FacilityConfigurationMode == FacilityConfigurationMode.Facility
                && s.FacilityTemplateId == request.Id)
            .Select(s => s.Name)
            .ToList();
        if (usedBy.Count > 0)
            return Conflict($"This facility template is used by {string.Join(", ", usedBy)}. Remove it from those scenarios before deleting it.");

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

        var templates = await store.GetAllAsync(ct);
        var cloneName = source.Name.Trim() + " (Copy)";
        var copyNumber = 2;
        while (templates.Any(t => string.Equals(t.Name, cloneName, StringComparison.OrdinalIgnoreCase)))
        {
            cloneName = $"{source.Name.Trim()} (Copy {copyNumber})";
            copyNumber++;
        }

        var clone = new FacilityTemplate
        {
            Id = Guid.NewGuid(),
            Name = cloneName,
            Description = source.Description,
            VendorName = source.VendorName,
            QueryPlanTemplateId = source.QueryPlanTemplateId,
            NormalizationSuiteId = source.NormalizationSuiteId,
            OrganizationResourceMapTemplateId = source.OrganizationResourceMapTemplateId,
            EnableOrganizationLocationMapping = source.EnableOrganizationLocationMapping,
            AllowedPatientConfigurationIds = [.. source.AllowedPatientConfigurationIds],
            AllowPatientConfigurationsOutsideSet = source.AllowPatientConfigurationsOutsideSet,
            IsSystem = false,
            IsDefault = false,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        await store.UpsertAsync(clone, ct);
        return CreatedAtAction(nameof(GetJson), new { id = clone.Id }, new { id = clone.Id });
    }

    private async Task<string?> ValidateReferencesAsync(FacilityTemplate model, CancellationToken ct)
    {
        if (model.QueryPlanTemplateId.HasValue
            && await queryPlanTemplateStore.GetByIdAsync(model.QueryPlanTemplateId.Value, ct) == null)
            return "Query plan was not found.";

        if (model.NormalizationSuiteId.HasValue
            && await normalizationStore.GetSuiteByIdAsync(model.NormalizationSuiteId.Value, ct) == null)
            return "Normalization suite was not found.";

        if (model.EnableOrganizationLocationMapping && model.OrganizationResourceMapTemplateId.HasValue
            && await organizationResourceMapTemplateStore.GetByIdAsync(model.OrganizationResourceMapTemplateId.Value, ct) == null)
            return "Organization resource map was not found.";

        if (model.AllowedPatientConfigurationIds.Count == 0)
            return null;

        var configs = await patientConfigurationStore.GetAllAsync(ct);
        var known = configs.Select(c => c.Id).ToHashSet();
        if (model.AllowedPatientConfigurationIds.Any(id => !known.Contains(id)))
            return "One or more patient configurations were not found.";

        return null;
    }
}
