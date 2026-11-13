using Automation.UI.Models;
using Link.UI.Services;
using Automation.UI.Services.Persistence;
using Microsoft.AspNetCore.Mvc;

namespace Link.UI.Controllers;

[Route("Automation/FacilityTemplates/[action]")]
public class FacilityTemplatesController : AutomationTemplateController
{
    private IFacilityTemplateStore store = null!;
    private IQueryPlanTemplateStore queryPlanTemplateStore = null!;
    private INormalizationStore normalizationStore = null!;
    private IOrganizationResourceMapTemplateStore organizationResourceMapTemplateStore = null!;
    private IPatientConfigurationStore patientConfigurationStore = null!;
    private IScenarioStore scenarioStore = null!;

    public FacilityTemplatesController(LinkAutomationEngineStatus engine, IServiceProvider services)
        : base(engine, services)
    {
    }

    protected override bool AttachStores() =>
        Resolve(out store)
        && Resolve(out queryPlanTemplateStore)
        && Resolve(out normalizationStore)
        && Resolve(out organizationResourceMapTemplateStore)
        && Resolve(out patientConfigurationStore)
        && Resolve(out scenarioStore);

    [HttpGet]
    public async Task<IActionResult> Index(
        int page = 1,
        int pageSize = FacilityTemplateListRules.DefaultPageSize,
        CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        if (!FacilityTemplateListRules.PageSizes.Contains(pageSize))
            pageSize = FacilityTemplateListRules.DefaultPageSize;

        var totalCount = await store.CountAsync(ct);
        var totalPages = Math.Max(1, (int)Math.Ceiling(totalCount / (double)pageSize));
        page = Math.Min(page, totalPages);
        var templates = await store.GetPageAsync((page - 1) * pageSize, pageSize, ct);

        ViewBag.CurrentPage = page;
        ViewBag.PageSize = pageSize;
        ViewBag.TotalCount = totalCount;
        ViewBag.TotalPages = totalPages;
        ViewBag.QueryPlanTemplates = await queryPlanTemplateStore.GetAllAsync(ct);
        ViewBag.NormalizationSuites = await normalizationStore.GetAllSuitesAsync(ct);
        ViewBag.OrganizationResourceMaps = await organizationResourceMapTemplateStore.GetAllAsync(ct);
        ViewBag.PatientConfigurations = await patientConfigurationStore.GetAllAsync(ct);
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

        if (!model.QueryPlanTemplateId.HasValue)
            return BadRequest("A facility template needs a query plan.");

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

        return await DeleteOneAsync(request.Id, ct);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteBulk([FromBody] BulkDeleteRequest? request, CancellationToken ct)
    {
        if (request?.Ids == null || request.Ids.Count == 0)
            return BadRequest("Select at least one facility template.");
        if (request.Ids.Count > FacilityTemplateListRules.MaxBulkDelete)
            return BadRequest($"A maximum of {FacilityTemplateListRules.MaxBulkDelete} templates can be deleted at once.");
        if (request.Ids.Any(id => id == Guid.Empty))
            return BadRequest("All template IDs must be non-empty GUIDs.");

        var scenarios = await scenarioStore.GetAllAsync(ct);
        var deletedIds = new List<Guid>();
        var failures = new List<object>();
        foreach (var id in request.Ids.Distinct())
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                var result = await TryDeleteAsync(id, scenarios, ct);
                if (result == DeleteTemplateResult.Deleted)
                {
                    deletedIds.Add(id);
                    continue;
                }

                failures.Add(new { id, reason = await FailureReasonAsync(id, result, scenarios, ct) });
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception)
            {
                failures.Add(new { id, reason = "An unexpected error occurred while deleting this template." });
            }
        }

        return Ok(new { deletedIds, failures });
    }

    private async Task<IActionResult> DeleteOneAsync(Guid id, CancellationToken ct)
    {
        var scenarios = await scenarioStore.GetAllAsync(ct);
        var result = await TryDeleteAsync(id, scenarios, ct);
        return result switch
        {
            DeleteTemplateResult.Deleted => Ok(),
            DeleteTemplateResult.NotFound => NotFound(),
            DeleteTemplateResult.SystemProtected => StatusCode(StatusCodes.Status403Forbidden, "System template cannot be deleted."),
            DeleteTemplateResult.DefaultProtected => Conflict("The system facility config cannot be deleted."),
            DeleteTemplateResult.InUse => Conflict(await FailureReasonAsync(id, result, scenarios, ct)),
            _ => StatusCode(StatusCodes.Status500InternalServerError)
        };
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

    private enum DeleteTemplateResult
    {
        Deleted,
        NotFound,
        SystemProtected,
        DefaultProtected,
        InUse
    }

    private async Task<DeleteTemplateResult> TryDeleteAsync(
        Guid id,
        IReadOnlyList<TestScenarioDefinition> scenarios,
        CancellationToken ct)
    {
        var template = await store.GetByIdAsync(id, ct);
        if (template == null)
            return DeleteTemplateResult.NotFound;
        if (template.IsSystem)
            return DeleteTemplateResult.SystemProtected;
        if (template.IsDefault)
            return DeleteTemplateResult.DefaultProtected;
        if (scenarios.Any(s => s.FacilityConfigurationMode == FacilityConfigurationMode.Facility && s.FacilityTemplateId == id))
            return DeleteTemplateResult.InUse;

        await store.DeleteAsync(id, ct);
        return DeleteTemplateResult.Deleted;
    }

    private static Task<string> FailureReasonAsync(
        Guid id,
        DeleteTemplateResult result,
        IReadOnlyList<TestScenarioDefinition> scenarios,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (result == DeleteTemplateResult.InUse)
        {
            var names = scenarios
                .Where(s => s.FacilityConfigurationMode == FacilityConfigurationMode.Facility && s.FacilityTemplateId == id)
                .Select(s => s.Name);
            return Task.FromResult($"This facility template is used by {string.Join(", ", names)}. Remove it from those scenarios before deleting it.");
        }

        var reason = result switch
        {
            DeleteTemplateResult.NotFound => "Facility template was not found.",
            DeleteTemplateResult.SystemProtected => "System template cannot be deleted.",
            DeleteTemplateResult.DefaultProtected => "The system facility config cannot be deleted.",
            _ => "Deletion failed."
        };
        return Task.FromResult(reason);
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
