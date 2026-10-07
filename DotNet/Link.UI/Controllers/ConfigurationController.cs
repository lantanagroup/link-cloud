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
