using Link.UI.Models;
using Link.UI.Services;
using Microsoft.AspNetCore.Mvc;

namespace Link.UI.Controllers;

[Route("Configuration")]
public sealed class ConfigurationController : Controller
{
    private readonly ConfigurationService _configuration;

    public ConfigurationController(ConfigurationService configuration)
    {
        _configuration = configuration;
    }

    [HttpGet("")]
    public IActionResult Index()
    {
        ViewData["Title"] = "Configuration";
        return View(_configuration.LoadHome());
    }

    [HttpGet("Measures")]
    public async Task<IActionResult> Measures(CancellationToken cancellationToken)
    {
        Section("measures", "Measure definitions");
        return View(await _configuration.LoadMeasuresAsync(cancellationToken));
    }

    [HttpGet("Measures/{id}")]
    public async Task<IActionResult> Measure(string id, CancellationToken cancellationToken)
    {
        Section("measures", "Measure definition");
        return View(await _configuration.LoadMeasureAsync(id, cancellationToken));
    }

    [HttpPost("Measures")]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(ConfigurationRules.MaxUploadBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = ConfigurationRules.MaxUploadBytes)]
    public async Task<IActionResult> SaveMeasure(IFormFile? bundle, CancellationToken cancellationToken)
    {
        await using var stream = bundle?.OpenReadStream();
        Temp(await _configuration.SaveMeasureAsync(stream, bundle?.Length ?? 0, cancellationToken));
        return RedirectToAction(nameof(Measures));
    }

    [HttpGet("Vendors")]
    public async Task<IActionResult> Vendors(CancellationToken cancellationToken)
    {
        Section("vendors", "Vendors");
        return View(await _configuration.LoadVendorsAsync(cancellationToken));
    }

    [HttpPost("Vendors")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveVendor(Guid? id, string? name, string? secret, CancellationToken cancellationToken)
    {
        Temp(await _configuration.SaveVendorAsync(id, name, secret, cancellationToken));
        return RedirectToAction(nameof(Vendors));
    }

    [HttpPost("Vendors/{id:guid}/delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteVendor(Guid id, CancellationToken cancellationToken)
    {
        Temp(await _configuration.DeleteVendorAsync(id, cancellationToken));
        return RedirectToAction(nameof(Vendors));
    }

    [HttpGet("Vendors/{vendorId:guid}/versions")]
    public async Task<IActionResult> Versions(Guid vendorId, CancellationToken cancellationToken)
    {
        Section("vendors", "Vendor versions");
        return View(await _configuration.LoadVersionsAsync(vendorId, cancellationToken));
    }

    [HttpPost("Vendors/{vendorId:guid}/versions")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveVersion(Guid vendorId, Guid? versionId, string? version, CancellationToken cancellationToken)
    {
        Temp(await _configuration.SaveVersionAsync(vendorId, versionId, version, cancellationToken));
        return RedirectToAction(nameof(Versions), new { vendorId });
    }

    [HttpPost("Vendors/versions/{versionId:guid}/delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteVersion(Guid versionId, Guid vendorId, CancellationToken cancellationToken)
    {
        Temp(await _configuration.DeleteVersionAsync(versionId, cancellationToken));
        return RedirectToAction(nameof(Versions), new { vendorId });
    }

    [HttpGet("Validation")]
    public async Task<IActionResult> Validation(CancellationToken cancellationToken)
    {
        Section("validation", "Validation");
        return View(await _configuration.LoadValidationAsync(cancellationToken));
    }

    [HttpPost("Validation/initialize")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Initialize(bool categories, CancellationToken cancellationToken)
    {
        Temp(await _configuration.InitializeAsync(categories, cancellationToken));
        return RedirectToAction(nameof(Validation));
    }

    [HttpGet("Validation/categories/{id}")]
    public async Task<IActionResult> Category(string id, CancellationToken cancellationToken)
    {
        Section("validation", "Validation category");
        return View(await _configuration.LoadCategoryAsync(id, cancellationToken));
    }

    [HttpPost("Validation/categories")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveCategory(CategoryForm form, CancellationToken cancellationToken)
    {
        Temp(await _configuration.SaveCategoryAsync(form, cancellationToken));
        return string.IsNullOrWhiteSpace(form.Id)
            ? RedirectToAction(nameof(Validation))
            : RedirectToAction(nameof(Category), new { id = form.Id.Trim() });
    }

    [HttpPost("Validation/packages")]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(ConfigurationRules.MaxUploadBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = ConfigurationRules.MaxUploadBytes)]
    public async Task<IActionResult> UploadPackage(string? name, IFormFile? package, CancellationToken cancellationToken)
    {
        await using var stream = package?.OpenReadStream();
        Temp(await _configuration.UploadPackageAsync(name, stream, package?.Length ?? 0, cancellationToken));
        return RedirectToAction(nameof(Validation));
    }

    [HttpGet("Validation/packages/{name}")]
    public async Task<IActionResult> Package(string name, CancellationToken cancellationToken)
    {
        Section("validation", "Validation package");
        return View(await _configuration.LoadPackageAsync(name, cancellationToken));
    }

    [HttpGet("Validation/dependencies")]
    public async Task<IActionResult> Dependencies(CancellationToken cancellationToken)
    {
        Section("validation", "Terminology dependencies");
        return View(await _configuration.LoadDependenciesAsync(cancellationToken));
    }

    [HttpGet("Validation/category-export")]
    public async Task<IActionResult> ExportCategories(CancellationToken cancellationToken)
    {
        var file = await _configuration.ExportCategoriesAsync(cancellationToken);
        if (file.Bytes is null)
        {
            Temp(ConfigurationAction.Fail(file.Error ?? "The category export failed."));
            return RedirectToAction(nameof(Validation));
        }

        return File(file.Bytes, "application/json", "validation-categories.json");
    }

    [HttpPost("Validation/category-import")]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(ConfigurationRules.MaxUploadBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = ConfigurationRules.MaxUploadBytes)]
    public async Task<IActionResult> ImportCategories(IFormFile? file, CancellationToken cancellationToken)
    {
        await using var stream = file?.OpenReadStream();
        Temp(await _configuration.ImportCategoriesAsync(stream, file?.Length ?? 0, cancellationToken));
        return RedirectToAction(nameof(Validation));
    }

    [HttpPost("Validation/categories/{id}/rules")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveRule(string id, string? field, string? regex, bool inverted, string? matcher, CancellationToken cancellationToken)
    {
        Temp(await _configuration.SaveRuleAsync(id, field, regex, inverted, matcher, cancellationToken));
        return RedirectToAction(nameof(Category), new { id });
    }

    [HttpPost("Validation/rules/{ruleId:long}/delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteRule(long ruleId, string? id, CancellationToken cancellationToken)
    {
        Temp(await _configuration.DeleteRuleAsync(ruleId, cancellationToken));
        return string.IsNullOrWhiteSpace(id)
            ? RedirectToAction(nameof(Validation))
            : RedirectToAction(nameof(Category), new { id = id.Trim() });
    }

    [HttpGet("Measures/{id}/cql")]
    public async Task<IActionResult> MeasureCql(string id, string? libraryId, string? range, bool submitted, CancellationToken cancellationToken)
    {
        Section("measures", "Measure CQL");
        return View(await _configuration.LoadMeasureCqlAsync(id, libraryId, range, submitted, cancellationToken));
    }

    [HttpGet("Measures/{id}/evaluate")]
    public async Task<IActionResult> MeasureEvaluate(string id, CancellationToken cancellationToken)
    {
        Section("measures", "Evaluate measure");
        return View(await _configuration.LoadMeasureEvaluateAsync(id, cancellationToken));
    }

    [HttpPost("Measures/{id}/evaluate")]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(ConfigurationRules.MaxUploadBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = ConfigurationRules.MaxUploadBytes)]
    public async Task<IActionResult> EvaluateMeasure(string id, string? parameters, string? debug, IFormFile? parametersFile, CancellationToken cancellationToken)
    {
        Section("measures", "Evaluate measure");
        await using var stream = parametersFile?.OpenReadStream();
        return View("MeasureEvaluate", await _configuration.EvaluateMeasureAsync(id, parameters, debug, stream, parametersFile?.Length ?? 0, cancellationToken));
    }

    [HttpPost("Notifications")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SendNotification(NotificationSendForm form, CancellationToken cancellationToken)
    {
        Temp(await _configuration.SendNotificationAsync(form, cancellationToken));
        return RedirectToAction(nameof(Notifications));
    }

    [HttpGet("QueryPlans")]
    public async Task<IActionResult> QueryPlans(string? facilityId, CancellationToken cancellationToken)
    {
        Section("plans", "Query plans");
        return View(await _configuration.LoadQueryPlansAsync(facilityId, cancellationToken));
    }

    [HttpGet("Terminology")]
    public async Task<IActionResult> Terminology(CancellationToken cancellationToken)
    {
        Section("terminology", "Terminology");
        return View(await _configuration.LoadTerminologyAsync(cancellationToken));
    }

    [HttpGet("Terminology/codes")]
    public async Task<IActionResult> Codes(CodeQuery query, bool submitted, CancellationToken cancellationToken)
    {
        Section("terminology", "Terminology codes");
        return View(await _configuration.LoadCodesAsync(query, submitted, cancellationToken));
    }

    [HttpGet("Hsloc")]
    public async Task<IActionResult> Hsloc(HslocQuery query, CancellationToken cancellationToken)
    {
        Section("hsloc", "HSLOC");
        return View(await _configuration.LoadHslocAsync(query, cancellationToken));
    }

    [HttpPost("Hsloc")]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(ConfigurationRules.MaxUploadBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = ConfigurationRules.MaxUploadBytes)]
    public async Task<IActionResult> ReplaceHsloc(string? oldVersion, string? newVersion, IFormFile? csv, HslocQuery query, CancellationToken cancellationToken)
    {
        await using var stream = csv?.OpenReadStream();
        Temp(await _configuration.ReplaceHslocAsync(oldVersion, newVersion, stream, csv?.Length ?? 0, csv?.FileName, cancellationToken));
        return RedirectToAction(nameof(Hsloc), new { query.Text, query.Version, query.Page });
    }

    [HttpPost("Hsloc/{id:guid}/delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteHsloc(Guid id, HslocQuery query, CancellationToken cancellationToken)
    {
        Temp(await _configuration.DeleteHslocAsync(id, cancellationToken));
        return RedirectToAction(nameof(Hsloc), new { query.Text, query.Version, query.Page });
    }

    [HttpGet("Operations")]
    public async Task<IActionResult> Operations(OperationQuery query, CancellationToken cancellationToken)
    {
        Section("operations", "Normalization operations");
        return View(await _configuration.LoadOperationsAsync(query, cancellationToken));
    }

    [HttpPost("Operations/vendor")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveVendorOperation(NormalizationOperationInput input, CancellationToken cancellationToken)
    {
        Section("operations", "Normalization operations");
        var page = await _configuration.SaveVendorOperationAsync(input, cancellationToken);
        if (!string.IsNullOrWhiteSpace(page.ActionMessage))
        {
            TempData["Message"] = page.ActionMessage;
            return RedirectToAction(nameof(Operations));
        }

        return View("Operations", page);
    }

    [HttpPost("Operations/vendor/extensions")]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(256 * 1024)]
    [RequestFormLimits(MultipartBodyLengthLimit = 256 * 1024)]
    public async Task<IActionResult> ImportVendorExtensions(
        List<string>? vendorVersionIds,
        IFormFile? csv,
        CancellationToken cancellationToken)
    {
        Section("operations", "Normalization operations");
        string? text = null;
        var tooLarge = csv is { Length: > FacilityNormalizationRules.MaxImportCharacters };
        if (csv is { Length: > 0 } && !tooLarge)
        {
            using var reader = new StreamReader(csv.OpenReadStream());
            text = await reader.ReadToEndAsync(cancellationToken);
        }

        var page = await _configuration.ImportVendorExtensionsAsync(vendorVersionIds, text, tooLarge, cancellationToken);
        if (!string.IsNullOrWhiteSpace(page.ActionMessage))
        {
            TempData["Message"] = page.ActionMessage;
            return RedirectToAction(nameof(Operations));
        }

        return View("Operations", page);
    }

    [HttpPost("Operations/vendor/delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteVendorOperation(
        string? operationId,
        string? vendorVersionId,
        CancellationToken cancellationToken)
    {
        Temp(await _configuration.DeleteVendorOperationAsync(operationId, vendorVersionId, cancellationToken));
        return RedirectToAction(nameof(Operations));
    }

    [HttpGet("Notifications")]
    public async Task<IActionResult> Notifications(NotificationQuery query, CancellationToken cancellationToken)
    {
        Section("notifications", "Notifications");
        return View(await _configuration.LoadNotificationsAsync(query, cancellationToken));
    }

    [HttpGet("Notifications/{id:guid}")]
    public async Task<IActionResult> Notification(Guid id, CancellationToken cancellationToken)
    {
        Section("notifications", "Notification");
        return View(await _configuration.LoadNotificationAsync(id, cancellationToken));
    }

    [HttpGet("Notifications/configurations")]
    public async Task<IActionResult> Configurations(NotificationConfigQuery query, CancellationToken cancellationToken)
    {
        Section("notifications", "Notification configuration");
        return View(await _configuration.LoadConfigurationsAsync(query, cancellationToken));
    }

    [HttpPost("Notifications/configurations")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveConfiguration(NotificationConfigForm form, CancellationToken cancellationToken)
    {
        Temp(await _configuration.SaveConfigurationAsync(form, cancellationToken));
        return RedirectToAction(nameof(Configurations), new { edit = form.Id });
    }

    [HttpPost("Notifications/configurations/delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfiguration(string? id, CancellationToken cancellationToken)
    {
        Temp(await _configuration.DeleteConfigurationAsync(id, cancellationToken));
        return RedirectToAction(nameof(Configurations));
    }

    [HttpGet("MeasureMappings")]
    public async Task<IActionResult> MeasureMappings(MappingQuery query, CancellationToken cancellationToken)
    {
        Section("mappings", "Measure mappings");
        return View(await _configuration.LoadMappingsAsync(query, cancellationToken));
    }

    [HttpPost("MeasureMappings")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveMapping(MappingForm form, CancellationToken cancellationToken)
    {
        Temp(await _configuration.SaveMappingAsync(form, cancellationToken));
        return RedirectToAction(nameof(MeasureMappings));
    }

    [HttpPost("MeasureMappings/delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteMapping(string? id, CancellationToken cancellationToken)
    {
        Temp(await _configuration.DeleteMappingAsync(id, cancellationToken));
        return RedirectToAction(nameof(MeasureMappings));
    }

    private void Section(string section, string title)
    {
        ViewData["Title"] = title;
        ViewData["ConfigurationSection"] = section;
    }

    private void Temp(ConfigurationAction result)
    {
        if (result.Succeeded)
            TempData["Message"] = result.Message;
        else
            TempData["Error"] = result.Message;
    }
}
