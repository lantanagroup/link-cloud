using System.Text;
using LantanaGroup.Link.Sdk.ApiClient;
using LantanaGroup.Link.Sdk.Clients;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using LantanaGroup.Link.Shared.Application.Models.Integration.DMRP;
using LantanaGroup.Link.Shared.Application.Models.Integration.Normalization;
using LantanaGroup.Link.Shared.Application.Models.Integration.Validation;
using LantanaGroup.Link.Shared.Application.Models.Tenant;
using Link.UI.Models;
using Microsoft.Extensions.Options;

namespace Link.UI.Services;

/// <summary>
/// Configuration pages: measure definitions, vendors, validation, query plans, terminology,
/// HSLOC, the global normalization search, notifications, and DMRP measure mappings.
/// Package upload, category rules, CQL, evaluation, and sending a notification use the same clients.
/// Reads and writes go through LinkSDK. Query-plan editors and facility normalization stay on the facility page.
/// Vendor-owned operations are edited from the operation search.
/// </summary>
public sealed partial class ConfigurationService
{
    public const string MeasuresNotConfigured = "MeasureEval service URL is not configured (ServiceRegistry:MeasureServiceUrl).";
    public const string TenantNotConfigured = "Tenant service URL is not configured (ServiceRegistry:TenantService:TenantServiceUrl).";
    public const string ValidationNotConfigured = "Validation service URL is not configured (ServiceRegistry:ValidationServiceUrl).";
    public const string AcquisitionNotConfigured = "Data acquisition service URL is not configured (ServiceRegistry:DataAcquisitionServiceUrl).";
    public const string TerminologyNotConfigured = "Terminology service URL is not configured (ServiceRegistry:TerminologyServiceUrl).";
    public const string NormalizationNotConfigured = "Normalization service URL is not configured (ServiceRegistry:NormalizationServiceUrl).";
    public const string NotificationNotConfigured = "Notification service URL is not configured (ServiceRegistry:NotificationServiceUrl).";
    public const string DmrpOff = "DMRP is off for this environment (DMRP:Enabled).";

    private readonly IMeasureEvalServiceClient? _measures;
    private readonly IFacilityServiceClient? _tenant;
    private readonly IValidationServiceClient? _validation;
    private readonly IDataAcquisitionServiceClient? _acquisition;
    private readonly ITerminologyServiceClient? _terminology;
    private readonly INormalizationServiceClient? _normalization;
    private readonly INotificationServiceClient? _notification;
    private readonly IDmrpServiceClient? _dmrp;
    private readonly LinkUiFeatureOptions _options;
    private readonly ILogger<ConfigurationService> _logger;

    public ConfigurationService(
        IMeasureEvalServiceClient? measures,
        IFacilityServiceClient? tenant,
        IValidationServiceClient? validation,
        IDataAcquisitionServiceClient? acquisition,
        ITerminologyServiceClient? terminology,
        INormalizationServiceClient? normalization,
        INotificationServiceClient? notification,
        IDmrpServiceClient? dmrp,
        IOptions<LinkUiFeatureOptions> options,
        ILogger<ConfigurationService> logger)
    {
        _measures = measures;
        _tenant = tenant;
        _validation = validation;
        _acquisition = acquisition;
        _terminology = terminology;
        _normalization = normalization;
        _notification = notification;
        _dmrp = dmrp;
        _options = options.Value;
        _logger = logger;
    }

    public static ConfigurationService Create(IServiceProvider services)
    {
        var registry = services.GetRequiredService<IOptions<ServiceRegistry>>().Value;
        return new ConfigurationService(
            Client<IMeasureEvalServiceClient>(services, registry.MeasureServiceUrl),
            Client<IFacilityServiceClient>(services, registry.TenantService?.TenantServiceUrl),
            Client<IValidationServiceClient>(services, registry.ValidationServiceUrl),
            Client<IDataAcquisitionServiceClient>(services, registry.DataAcquisitionServiceUrl),
            Client<ITerminologyServiceClient>(services, registry.TerminologyServiceUrl),
            Client<INormalizationServiceClient>(services, registry.NormalizationServiceUrl),
            Client<INotificationServiceClient>(services, registry.NotificationServiceUrl),
            Client<IDmrpServiceClient>(services, registry.TenantService?.TenantServiceUrl),
            services.GetRequiredService<IOptions<LinkUiFeatureOptions>>(),
            services.GetRequiredService<ILogger<ConfigurationService>>());
    }

    public ConfigurationHomePage LoadHome() => new()
    {
        MeasuresConfigured = _measures is not null,
        TenantConfigured = _tenant is not null,
        ValidationConfigured = _validation is not null,
        AcquisitionConfigured = _acquisition is not null,
        TerminologyConfigured = _terminology is not null,
        NormalizationConfigured = _normalization is not null,
        NotificationConfigured = _notification is not null,
        DmrpEnabled = _options.DmrpEnabled
    };

    public async Task<MeasureListPage> LoadMeasuresAsync(CancellationToken cancellationToken)
    {
        var page = new MeasureListPage { Configured = _measures is not null };
        if (_measures is null)
        {
            page.LoadError = MeasuresNotConfigured;
            return page;
        }

        try
        {
            var response = await _measures.GetAllMeasureDefinitionsAsync(cancellationToken);
            if (!Ok(response))
            {
                page.LoadError = Fail("MeasureEval", response.StatusCode, response.RawBody);
                return page;
            }

            var rows = ConfigurationRules.ReadMeasures(response.Body);
            if (rows is null)
            {
                page.LoadError = "Measure definitions were not a list.";
                return page;
            }

            page.Measures = rows.OrderBy(row => row.Id, StringComparer.OrdinalIgnoreCase).ToList();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Measure definition list failed");
            page.LoadError = "MeasureEval service call failed: " + ex.Message;
        }

        return page;
    }

    public async Task<MeasureDetailPage> LoadMeasureAsync(string? id, CancellationToken cancellationToken)
    {
        var page = new MeasureDetailPage { Configured = _measures is not null, Id = id?.Trim() ?? string.Empty };
        var idError = ConfigurationRules.CheckMeasureId(page.Id);
        if (idError is not null)
        {
            page.LoadError = idError;
            return page;
        }

        if (_measures is null)
        {
            page.LoadError = MeasuresNotConfigured;
            return page;
        }

        try
        {
            var measureTask = _measures.GetMeasureDefinitionAsync(page.Id, cancellationToken);
            var artifactTask = _measures.GetRelatedArtifactsAsync(page.Id, cancellationToken);
            await Task.WhenAll(measureTask, artifactTask);
            var measure = await measureTask;
            var artifacts = await artifactTask;
            if (!Ok(measure))
            {
                page.LoadError = measure.StatusCode == 404
                    ? "That measure definition was not found."
                    : Fail("MeasureEval", measure.StatusCode, measure.RawBody);
            }
            else
            {
                ConfigurationRules.ReadMeasure(measure.Body, page);
            }

            if (!Ok(artifacts))
                page.ArtifactError = Fail("MeasureEval", artifacts.StatusCode, artifacts.RawBody);
            else
                page.Artifacts = ConfigurationRules.ReadArtifacts(artifacts.Body);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Measure definition load failed");
            page.LoadError = "MeasureEval service call failed: " + ex.Message;
        }

        return page;
    }

    public async Task<ConfigurationAction> SaveMeasureAsync(Stream? bundle, long length, CancellationToken cancellationToken)
    {
        if (_measures is null)
            return ConfigurationAction.Fail(MeasuresNotConfigured);
        var read = await ReadUploadAsync(bundle, length, cancellationToken);
        if (read.Error is not null)
            return ConfigurationAction.Fail(read.Error);
        var error = ConfigurationRules.CheckBundle(read.Text, out var id);
        if (error is not null)
            return ConfigurationAction.Fail(error);

        try
        {
            var response = await _measures.PutMeasureDefinitionAsync(read.Text!, cancellationToken);
            if (!Ok(response))
                return ConfigurationAction.Fail(Fail("MeasureEval", response.StatusCode, response.RawBody));
            return ConfigurationAction.Ok("Measure definition " + id + " was saved.");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Measure definition save failed");
            return ConfigurationAction.Fail("MeasureEval service call failed: " + ex.Message);
        }
    }

    public async Task<VendorListPage> LoadVendorsAsync(CancellationToken cancellationToken)
    {
        var page = new VendorListPage { Configured = _tenant is not null };
        if (_tenant is null)
        {
            page.LoadError = TenantNotConfigured;
            return page;
        }

        try
        {
            var response = await _tenant.GetVendorsAsync(cancellationToken);
            if (!Ok(response))
            {
                page.LoadError = Fail("Tenant", response.StatusCode, response.RawBody);
                return page;
            }

            page.Vendors = (response.Body ?? new List<VendorModel>())
                .Where(vendor => vendor.Id.HasValue)
                .Select(vendor => new VendorRow
                {
                    Id = vendor.Id!.Value,
                    Name = vendor.Name ?? string.Empty,
                    SecretId = vendor.Authentication?.SigningKeySecretId ?? string.Empty
                })
                .OrderBy(vendor => vendor.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Vendor list failed");
            page.LoadError = "Tenant service call failed: " + ex.Message;
        }

        return page;
    }

    public async Task<ConfigurationAction> SaveVendorAsync(Guid? id, string? name, string? secret, CancellationToken cancellationToken)
    {
        if (_tenant is null)
            return ConfigurationAction.Fail(TenantNotConfigured);
        var nameError = ConfigurationRules.CheckName(name, out var cleanName);
        if (nameError is not null)
            return ConfigurationAction.Fail(nameError);
        var secretError = ConfigurationRules.CheckSecret(secret, out var cleanSecret);
        if (secretError is not null)
            return ConfigurationAction.Fail(secretError);

        var authentication = cleanSecret is null ? null : new VendorAuthenticationSettings { SigningKeySecretId = cleanSecret };
        try
        {
            if (id is null)
            {
                var created = await _tenant.CreateVendorAsync(new CreateVendorModel { Name = cleanName, Authentication = authentication }, cancellationToken);
                return Ok(created)
                    ? ConfigurationAction.Ok("Vendor " + cleanName + " was created.")
                    : ConfigurationAction.Fail(Fail("Tenant", created.StatusCode, created.RawBody));
            }

            var updated = await _tenant.UpdateVendorAsync(id.Value, new UpdateVendorModel { Name = cleanName, Authentication = authentication }, cancellationToken);
            return Ok(updated)
                ? ConfigurationAction.Ok("Vendor " + cleanName + " was updated.")
                : ConfigurationAction.Fail(Fail("Tenant", updated.StatusCode, updated.RawBody));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Vendor save failed");
            return ConfigurationAction.Fail("Tenant service call failed: " + ex.Message);
        }
    }

    public async Task<ConfigurationAction> DeleteVendorAsync(Guid id, CancellationToken cancellationToken)
    {
        if (_tenant is null)
            return ConfigurationAction.Fail(TenantNotConfigured);
        if (id == Guid.Empty)
            return ConfigurationAction.Fail("Vendor id is not a valid id.");

        try
        {
            var response = await _tenant.DeleteVendorAsync(id, cancellationToken);
            return Ok(response)
                ? ConfigurationAction.Ok("Vendor was deleted.")
                : ConfigurationAction.Fail(Fail("Tenant", response.StatusCode, response.RawBody));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Vendor delete failed");
            return ConfigurationAction.Fail("Tenant service call failed: " + ex.Message);
        }
    }

    public async Task<VendorVersionPage> LoadVersionsAsync(Guid vendorId, CancellationToken cancellationToken)
    {
        var page = new VendorVersionPage { Configured = _tenant is not null, VendorId = vendorId };
        if (vendorId == Guid.Empty)
        {
            page.LoadError = "Vendor id is not a valid id.";
            return page;
        }

        if (_tenant is null)
        {
            page.LoadError = TenantNotConfigured;
            return page;
        }

        try
        {
            var vendorTask = _tenant.GetVendorAsync(vendorId, cancellationToken);
            var versionTask = _tenant.GetVendorVersionsAsync(vendorId, cancellationToken);
            await Task.WhenAll(vendorTask, versionTask);
            var vendor = await vendorTask;
            var versions = await versionTask;
            page.VendorName = Ok(vendor) ? vendor.Body?.Name ?? string.Empty : string.Empty;
            if (!Ok(vendor) && vendor.StatusCode == 404)
            {
                page.LoadError = "That vendor was not found.";
                return page;
            }

            if (!Ok(versions))
            {
                page.LoadError = Fail("Tenant", versions.StatusCode, versions.RawBody);
                return page;
            }

            page.Versions = (versions.Body ?? new List<VendorVersionModel>())
                .Where(version => version.Id.HasValue)
                .Select(version => new VendorVersionRow { Id = version.Id!.Value, Version = version.Version ?? string.Empty })
                .OrderBy(version => version.Version, StringComparer.OrdinalIgnoreCase)
                .ToList();
            if (!Ok(vendor))
                page.LoadError = Fail("Tenant", vendor.StatusCode, vendor.RawBody);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Vendor version list failed");
            page.LoadError = "Tenant service call failed: " + ex.Message;
        }

        return page;
    }

    public async Task<ConfigurationAction> SaveVersionAsync(Guid vendorId, Guid? versionId, string? version, CancellationToken cancellationToken)
    {
        if (_tenant is null)
            return ConfigurationAction.Fail(TenantNotConfigured);
        if (vendorId == Guid.Empty)
            return ConfigurationAction.Fail("Vendor id is not a valid id.");
        var error = ConfigurationRules.CheckVersion(version, out var clean);
        if (error is not null)
            return ConfigurationAction.Fail(error);

        try
        {
            if (versionId is null)
            {
                var created = await _tenant.CreateVendorVersionAsync(new CreateVendorVersionModel { VendorId = vendorId, Version = clean }, cancellationToken);
                return Ok(created)
                    ? ConfigurationAction.Ok("Version " + clean + " was created.")
                    : ConfigurationAction.Fail(Fail("Tenant", created.StatusCode, created.RawBody));
            }

            var updated = await _tenant.UpdateVendorVersionAsync(versionId.Value, new UpdateVendorVersionModel { Version = clean }, cancellationToken);
            return Ok(updated)
                ? ConfigurationAction.Ok("Version " + clean + " was updated.")
                : ConfigurationAction.Fail(Fail("Tenant", updated.StatusCode, updated.RawBody));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Vendor version save failed");
            return ConfigurationAction.Fail("Tenant service call failed: " + ex.Message);
        }
    }

    public async Task<ConfigurationAction> DeleteVersionAsync(Guid versionId, CancellationToken cancellationToken)
    {
        if (_tenant is null)
            return ConfigurationAction.Fail(TenantNotConfigured);
        if (versionId == Guid.Empty)
            return ConfigurationAction.Fail("Version id is not a valid id.");

        try
        {
            var response = await _tenant.DeleteVendorVersionAsync(versionId, cancellationToken);
            return Ok(response)
                ? ConfigurationAction.Ok("Version was deleted.")
                : ConfigurationAction.Fail(Fail("Tenant", response.StatusCode, response.RawBody));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Vendor version delete failed");
            return ConfigurationAction.Fail("Tenant service call failed: " + ex.Message);
        }
    }

    public async Task<ValidationListPage> LoadValidationAsync(CancellationToken cancellationToken)
    {
        var page = new ValidationListPage { Configured = _validation is not null };
        if (_validation is null)
        {
            page.LoadError = ValidationNotConfigured;
            return page;
        }

        try
        {
            var artifactTask = _validation.GetArtifactsAsync(cancellationToken);
            var categoryTask = _validation.GetCategoriesAsync(cancellationToken);
            await Task.WhenAll(artifactTask, categoryTask);
            var artifacts = await artifactTask;
            var categories = await categoryTask;
            if (!Ok(artifacts))
                page.LoadError = Fail("Validation", artifacts.StatusCode, artifacts.RawBody);
            else
            {
                page.Artifacts = (artifacts.Body ?? new List<ValidationArtifactApiModel>())
                    .Select(item => new ValidationArtifactRow { Id = item.Id, Name = item.Name, Type = item.Type })
                    .OrderBy(item => item.Type, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
                    .ToList();
            }

            if (!Ok(categories))
                page.CategoryError = Fail("Validation", categories.StatusCode, categories.RawBody);
            else
            {
                page.Categories = (categories.Body ?? new List<ValidationCategoryApiModel>())
                    .Select(item => new ConfigurationCategoryRow
                    {
                        Id = item.Id,
                        Title = item.Title,
                        Severity = item.Severity ?? string.Empty,
                        Acceptable = item.Acceptable,
                        Reserved = string.Equals(item.Id, ConfigurationRules.ReservedCategory, StringComparison.OrdinalIgnoreCase)
                    })
                    .OrderBy(item => item.Title, StringComparer.OrdinalIgnoreCase)
                    .ToList();
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Validation configuration load failed");
            page.LoadError = "Validation service call failed: " + ex.Message;
        }

        return page;
    }

    public async Task<ConfigurationAction> InitializeAsync(bool categories, CancellationToken cancellationToken)
    {
        if (_validation is null)
            return ConfigurationAction.Fail(ValidationNotConfigured);

        try
        {
            var response = categories
                ? await _validation.InitializeCategoriesAsync(cancellationToken)
                : await _validation.InitializeArtifactsAsync(cancellationToken);
            if (!Ok(response))
                return ConfigurationAction.Fail(Fail("Validation", response.StatusCode, response.RawBody));
            return ConfigurationAction.Ok(categories
                ? "Validation categories were initialized."
                : "Implementation guides were initialized.");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Validation initialize failed");
            return ConfigurationAction.Fail("Validation service call failed: " + ex.Message);
        }
    }

    public async Task<CategoryPage> LoadCategoryAsync(string? id, CancellationToken cancellationToken)
    {
        var page = new CategoryPage { Configured = _validation is not null, Form = new CategoryForm { Id = id?.Trim() } };
        if (string.Equals(page.Form.Id, ConfigurationRules.ReservedCategory, StringComparison.OrdinalIgnoreCase))
        {
            page.Reserved = true;
            page.LoadError = "uncategorized is reserved and cannot be edited.";
            return page;
        }

        var idError = ConfigurationRules.CheckCategory(
            new CategoryForm { Id = page.Form.Id, Title = "placeholder", Severity = "ERROR", Guidance = "placeholder" },
            out _);
        if (idError is not null)
        {
            page.LoadError = idError;
            return page;
        }

        page.ShowForm = true;
        if (_validation is null)
        {
            page.LoadError = ValidationNotConfigured;
            return page;
        }

        try
        {
            var response = await _validation.GetCategoryAsync(page.Form.Id!, cancellationToken);
            if (response.StatusCode == 404)
            {
                page.Missing = true;
                page.Form = new CategoryForm { Id = page.Form.Id, Submit = true, Review = true };
                return page;
            }

            if (!Ok(response) || response.Body is null)
            {
                page.LoadError = Fail("Validation", response.StatusCode, response.RawBody);
                return page;
            }

            page.Form = new CategoryForm
            {
                Id = response.Body.Id,
                Title = response.Body.Title,
                Severity = response.Body.Severity,
                Acceptable = response.Body.Acceptable,
                Submit = response.Body.Submit,
                Review = response.Body.Review,
                Guidance = response.Body.Guidance
            };
            await LoadRulesAsync(page, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Validation category load failed");
            page.LoadError = "Validation service call failed: " + ex.Message;
        }

        return page;
    }

    public async Task<ConfigurationAction> UploadPackageAsync(string? name, Stream? content, long length, CancellationToken cancellationToken)
    {
        var nameError = ConfigurationRules.CheckPackageName(name, out var packageName);
        if (nameError is not null)
            return ConfigurationAction.Fail(nameError);
        var read = await ReadBytesAsync(content, length, "A package file is required.", cancellationToken);
        if (read.Error is not null)
            return ConfigurationAction.Fail(read.Error);
        if (_validation is null)
            return ConfigurationAction.Fail(ValidationNotConfigured);

        try
        {
            var response = await _validation.UploadPackageAsync(packageName, read.Bytes!, cancellationToken);
            return Ok(response)
                ? ConfigurationAction.Ok("Package " + packageName + " was uploaded.")
                : ConfigurationAction.Fail(Fail("Validation", response.StatusCode, response.RawBody));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Validation package upload failed");
            return ConfigurationAction.Fail("Validation service call failed: " + ex.Message);
        }
    }

    public async Task<PackagePage> LoadPackageAsync(string? name, CancellationToken cancellationToken)
    {
        var page = new PackagePage { Configured = _validation is not null };
        var nameError = ConfigurationRules.CheckPackageName(name, out var packageName);
        page.Name = packageName;
        if (nameError is not null)
        {
            page.LoadError = nameError;
            return page;
        }

        if (_validation is null)
        {
            page.LoadError = ValidationNotConfigured;
            return page;
        }

        try
        {
            var detailTask = _validation.GetPackageDetailsAsync(packageName, cancellationToken);
            var dependencyTask = _validation.GetPackageDependenciesAsync(packageName, cancellationToken);
            await Task.WhenAll(detailTask, dependencyTask);
            var details = await detailTask;
            var dependencies = await dependencyTask;
            if (details.StatusCode == 404)
                page.LoadError = "That package was not found.";
            else if (!Ok(details))
                page.LoadError = Fail("Validation", details.StatusCode, details.RawBody);
            else
                ConfigurationRules.ReadPackage(details.Body, page);

            if (dependencies.StatusCode == 404)
                page.DependencyError = "That package was not found.";
            else if (!Ok(dependencies))
                page.DependencyError = Fail("Validation", dependencies.StatusCode, dependencies.RawBody);
            else
            {
                var rows = ConfigurationRules.ReadDependencies(dependencies.Body, out var shortened);
                if (rows is null)
                    page.DependencyError = "Terminology dependencies were not a list.";
                else
                {
                    page.Dependencies = rows;
                    if (shortened)
                        page.ListNote = "Showing the first 200 dependencies.";
                }
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Validation package load failed");
            page.LoadError = "Validation service call failed: " + ex.Message;
        }

        return page;
    }

    public async Task<DependencyPage> LoadDependenciesAsync(CancellationToken cancellationToken)
    {
        var page = new DependencyPage { Configured = _validation is not null };
        if (_validation is null)
        {
            page.LoadError = ValidationNotConfigured;
            return page;
        }

        try
        {
            var response = await _validation.GetAllDependenciesAsync(cancellationToken);
            if (!Ok(response))
            {
                page.LoadError = Fail("Validation", response.StatusCode, response.RawBody);
                return page;
            }

            var rows = ConfigurationRules.ReadDependencies(response.Body, out var shortened);
            if (rows is null)
            {
                page.LoadError = "Terminology dependencies were not a list.";
                return page;
            }

            page.Dependencies = rows;
            if (shortened)
                page.ListNote = "Showing the first 200 dependencies.";
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Validation dependency load failed");
            page.LoadError = "Validation service call failed: " + ex.Message;
        }

        return page;
    }

    public async Task<(byte[]? Bytes, string? Error)> ExportCategoriesAsync(CancellationToken cancellationToken)
    {
        if (_validation is null)
            return (null, ValidationNotConfigured);

        try
        {
            var response = await _validation.ExportCategoriesAsync(cancellationToken);
            if (!Ok(response))
                return (null, Fail("Validation", response.StatusCode, response.RawBody));
            return (Encoding.UTF8.GetBytes(response.Body ?? "[]"), null);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Validation category export failed");
            return (null, "Validation service call failed: " + ex.Message);
        }
    }

    public async Task<ConfigurationAction> ImportCategoriesAsync(Stream? content, long length, CancellationToken cancellationToken)
    {
        var read = await ReadTextUploadAsync(content, length, "A category export file is required.", cancellationToken);
        if (read.Error is not null)
            return ConfigurationAction.Fail(read.Error);
        var error = ConfigurationRules.CheckBulkImport(read.Text);
        if (error is not null)
            return ConfigurationAction.Fail(error);
        if (_validation is null)
            return ConfigurationAction.Fail(ValidationNotConfigured);

        try
        {
            var response = await _validation.ImportCategoriesAsync(read.Text!, cancellationToken);
            return Ok(response)
                ? ConfigurationAction.Ok("Categories were imported. Categories that were not in the file were removed.")
                : ConfigurationAction.Fail(Fail("Validation", response.StatusCode, response.RawBody));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Validation category import failed");
            return ConfigurationAction.Fail("Validation service call failed: " + ex.Message);
        }
    }

    public async Task<ConfigurationAction> SaveRuleAsync(string? id, string? field, string? regex, bool inverted, string? matcher, CancellationToken cancellationToken)
    {
        if (string.Equals(id?.Trim(), ConfigurationRules.ReservedCategory, StringComparison.OrdinalIgnoreCase))
            return ConfigurationAction.Fail("uncategorized is reserved and cannot be edited.");
        var idError = ConfigurationRules.CheckCategory(
            new CategoryForm { Id = id, Title = "placeholder", Severity = "ERROR", Guidance = "placeholder" },
            out var category);
        if (idError is not null)
            return ConfigurationAction.Fail(idError);
        var ruleError = ConfigurationRules.CheckRule(field, regex, inverted, matcher, out var json);
        if (ruleError is not null)
            return ConfigurationAction.Fail(ruleError);
        if (_validation is null)
            return ConfigurationAction.Fail(ValidationNotConfigured);

        try
        {
            var response = await _validation.SaveCategoryRuleAsync(category.Id!, json, cancellationToken);
            return Ok(response)
                ? ConfigurationAction.Ok("Category rule was saved.")
                : ConfigurationAction.Fail(Fail("Validation", response.StatusCode, response.RawBody));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Validation category rule save failed");
            return ConfigurationAction.Fail("Validation service call failed: " + ex.Message);
        }
    }

    public async Task<ConfigurationAction> DeleteRuleAsync(long ruleId, CancellationToken cancellationToken)
    {
        if (ruleId <= 0)
            return ConfigurationAction.Fail("Rule id is not a valid id.");
        if (_validation is null)
            return ConfigurationAction.Fail(ValidationNotConfigured);

        try
        {
            var response = await _validation.DeleteCategoryRuleAsync(ruleId, cancellationToken);
            return Ok(response)
                ? ConfigurationAction.Ok("Category rule was deleted.")
                : ConfigurationAction.Fail(Fail("Validation", response.StatusCode, response.RawBody));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Validation category rule delete failed");
            return ConfigurationAction.Fail("Validation service call failed: " + ex.Message);
        }
    }

    public async Task<MeasureCqlPage> LoadMeasureCqlAsync(string? id, string? libraryId, string? range, bool submitted, CancellationToken cancellationToken)
    {
        var page = new MeasureCqlPage
        {
            Configured = _measures is not null,
            Id = id?.Trim() ?? string.Empty,
            LibraryId = libraryId?.Trim(),
            Range = range?.Trim()
        };
        var idError = ConfigurationRules.CheckMeasureId(page.Id);
        if (idError is not null)
        {
            page.LoadError = idError;
            return page;
        }

        if (submitted)
        {
            var cqlError = ConfigurationRules.CheckCql(page.LibraryId, page.Range, out var library, out var cleanRange);
            page.LibraryId = library;
            page.Range = cleanRange;
            if (cqlError is not null)
                page.CqlError = cqlError;
        }

        if (_measures is null)
        {
            page.LoadError = MeasuresNotConfigured;
            return page;
        }

        try
        {
            var measure = await _measures.GetMeasureDefinitionAsync(page.Id, cancellationToken);
            if (measure.StatusCode == 404)
                page.LibraryError = "That measure definition was not found.";
            else if (!Ok(measure))
                page.LibraryError = Fail("MeasureEval", measure.StatusCode, measure.RawBody);
            else
            {
                var detail = new MeasureDetailPage();
                ConfigurationRules.ReadMeasure(measure.Body, detail);
                page.Libraries = detail.Libraries;
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Measure library lookup failed");
            page.LibraryError = "MeasureEval service call failed: " + ex.Message;
        }

        if (!submitted || page.CqlError is not null)
            return page;

        try
        {
            var response = await _measures.GetMeasureCqlAsync(page.Id, page.LibraryId!, page.Range, cancellationToken);
            if (response.StatusCode == 404)
            {
                page.CqlError = "CQL was not found for that library.";
                return page;
            }

            if (!Ok(response))
            {
                page.CqlError = Fail("MeasureEval", response.StatusCode, response.RawBody);
                return page;
            }

            page.Cql = ConfigurationRules.Cap(response.Body);
            page.Truncated = (response.Body?.Length ?? 0) > ConfigurationRules.PreviewLength;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Measure CQL load failed");
            page.CqlError = "MeasureEval service call failed: " + ex.Message;
        }

        return page;
    }

    public Task<MeasureEvalPage> LoadMeasureEvaluateAsync(string? id, CancellationToken cancellationToken)
    {
        var page = new MeasureEvalPage { Configured = _measures is not null, Id = id?.Trim() ?? string.Empty };
        var idError = ConfigurationRules.CheckMeasureId(page.Id);
        if (idError is not null)
            page.LoadError = idError;
        else if (_measures is null)
            page.LoadError = MeasuresNotConfigured;
        return Task.FromResult(page);
    }

    public async Task<MeasureEvalPage> EvaluateMeasureAsync(string? id, string? parameters, string? debug, Stream? file, long length, CancellationToken cancellationToken)
    {
        var page = new MeasureEvalPage { Configured = _measures is not null, Id = id?.Trim() ?? string.Empty, Debug = debug?.Trim() };
        var idError = ConfigurationRules.CheckMeasureId(page.Id);
        if (idError is not null)
        {
            page.LoadError = idError;
            return page;
        }

        string? text = parameters;
        if (file is not null && length > 0)
        {
            var read = await ReadTextUploadAsync(file, length, "Parameters are required.", cancellationToken);
            if (read.Error is not null)
            {
                page.ResultError = read.Error;
                return page;
            }

            text = read.Text;
            page.ParametersOmitted = true;
        }
        else if ((text?.Length ?? 0) <= 8000)
        {
            page.Parameters = text;
        }
        else
        {
            page.ParametersOmitted = true;
        }

        var error = ConfigurationRules.CheckEvaluate(text, debug, out var cleanDebug);
        page.Debug = cleanDebug ?? (string.IsNullOrWhiteSpace(debug) ? null : debug.Trim());
        if (error is not null)
        {
            page.ResultError = error;
            return page;
        }

        if (_measures is null)
        {
            page.LoadError = MeasuresNotConfigured;
            return page;
        }

        try
        {
            var response = await _measures.EvaluateMeasureAsync(page.Id, text!, cleanDebug, cancellationToken);
            if (response.StatusCode == 404)
            {
                page.ResultError = "That measure definition was not found.";
                return page;
            }

            if (!Ok(response))
            {
                page.ResultError = Fail("MeasureEval", response.StatusCode, response.RawBody);
                return page;
            }

            page.Result = ConfigurationRules.Cap(response.Body);
            page.Truncated = (response.Body?.Length ?? 0) > ConfigurationRules.PreviewLength;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Measure evaluation failed");
            page.ResultError = "MeasureEval service call failed: " + ex.Message;
        }

        return page;
    }

    public async Task<ConfigurationAction> SendNotificationAsync(NotificationSendForm form, CancellationToken cancellationToken)
    {
        var error = ConfigurationRules.CheckSend(form, _options.NumericOnlyFacilityId, out var clean, out var recipients, out var bcc);
        if (error is not null)
            return ConfigurationAction.Fail(error);
        if (_notification is null)
            return ConfigurationAction.Fail(NotificationNotConfigured);

        try
        {
            var response = await _notification.CreateNotificationAsync(new NotificationMessageApiModel
            {
                NotificationType = clean.NotificationType,
                FacilityId = clean.FacilityId,
                Subject = clean.Subject,
                Body = clean.Body,
                Recipients = recipients,
                Bcc = bcc
            }, cancellationToken);
            if (!Ok(response))
                return ConfigurationAction.Fail(Fail("Notification", response.StatusCode, response.RawBody));
            var created = ConfigurationRules.ReadCreatedId(response.Body);
            return ConfigurationAction.Ok(created is null ? "Notification was sent." : "Notification " + created + " was sent.");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Notification send failed");
            return ConfigurationAction.Fail("Notification service call failed: " + ex.Message);
        }
    }

    public async Task<ConfigurationAction> SaveCategoryAsync(CategoryForm form, CancellationToken cancellationToken)
    {
        if (_validation is null)
            return ConfigurationAction.Fail(ValidationNotConfigured);
        var error = ConfigurationRules.CheckCategory(form, out var clean);
        if (error is not null)
            return ConfigurationAction.Fail(error);

        try
        {
            var response = await _validation.UpdateCategoryAsync(new ValidationCategoryApiModel
            {
                Id = clean.Id!,
                Title = clean.Title!,
                Severity = clean.Severity,
                Acceptable = clean.Acceptable,
                Submit = clean.Submit,
                Review = clean.Review,
                Guidance = clean.Guidance
            }, cancellationToken);
            return Ok(response)
                ? ConfigurationAction.Ok("Category " + clean.Id + " was saved.")
                : ConfigurationAction.Fail(Fail("Validation", response.StatusCode, response.RawBody));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Validation category save failed");
            return ConfigurationAction.Fail("Validation service call failed: " + ex.Message);
        }
    }

    public async Task<QueryPlanPage> LoadQueryPlansAsync(string? facilityId, CancellationToken cancellationToken)
    {
        var page = new QueryPlanPage { Configured = _acquisition is not null, FacilityId = facilityId };
        if (facilityId is null)
            return page;

        var error = ConfigurationRules.CheckFacility(facilityId, _options.NumericOnlyFacilityId, required: true, out var clean);
        page.FacilityId = clean ?? facilityId.Trim();
        if (error is not null)
        {
            page.LoadError = error;
            return page;
        }

        if (_acquisition is null)
        {
            page.LoadError = AcquisitionNotConfigured;
            return page;
        }

        try
        {
            var tasks = FacilityAcquisitionRules.QueryPlanTypes
                .Select(type => LoadPlanAsync(clean!, type, cancellationToken))
                .ToArray();
            page.Plans = await Task.WhenAll(tasks);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Query plan lookup failed");
            page.LoadError = "Data acquisition service call failed: " + ex.Message;
        }

        return page;
    }

    public async Task<TerminologyPage> LoadTerminologyAsync(CancellationToken cancellationToken)
    {
        var page = new TerminologyPage { Configured = _terminology is not null };
        if (_terminology is null)
        {
            page.ValueSetError = TerminologyNotConfigured;
            page.CodeSystemError = TerminologyNotConfigured;
            return page;
        }

        try
        {
            var valueSetsTask = _terminology.GetValueSetSummariesAsync(cancellationToken);
            var codeSystemsTask = _terminology.GetCodeSystemSummariesAsync(cancellationToken);
            await Task.WhenAll(valueSetsTask, codeSystemsTask);
            page.ValueSets = ReadResources(await valueSetsTask, "ValueSet", "Value sets were not a bundle.", out var valueSetError);
            page.CodeSystems = ReadResources(await codeSystemsTask, "CodeSystem", "Code systems were not a bundle.", out var codeSystemError);
            page.ValueSetError = valueSetError;
            page.CodeSystemError = codeSystemError;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Terminology list failed");
            page.ValueSetError = "Terminology service call failed: " + ex.Message;
        }

        return page;
    }

    public async Task<CodeSearchPage> LoadCodesAsync(CodeQuery? query, bool submitted, CancellationToken cancellationToken)
    {
        var page = new CodeSearchPage { Configured = _terminology is not null, Query = query ?? new CodeQuery() };
        if (!submitted)
            return page;

        var error = ConfigurationRules.CheckCodeSearch(query, out var clean);
        page.Searched = true;
        if (error is not null)
            page.Query = query ?? new CodeQuery();
        else
            page.Query = clean;
        if (error is not null)
        {
            page.LoadError = error;
            return page;
        }

        if (_terminology is null)
        {
            page.LoadError = TerminologyNotConfigured;
            return page;
        }

        try
        {
            var response = await _terminology.SearchCodesAsync(
                clean.Search,
                clean.CodeSystem,
                clean.ValueSet,
                clean.Version,
                clean.ExcludeInactive,
                clean.Page ?? 1,
                clean.PageSize ?? ConfigurationRules.DefaultPageSize,
                cancellationToken);
            if (response.StatusCode == 204)
            {
                page.Paging = ConfigurationRules.Bar(clean.Page ?? 1, clean.PageSize ?? ConfigurationRules.DefaultPageSize, 0, 1);
                return page;
            }

            if (!Ok(response) || response.Body is null)
            {
                page.LoadError = Fail("Terminology", response.StatusCode, response.RawBody);
                return page;
            }

            page.Codes = response.Body.Records.Select(code => new CodeRow
            {
                System = code.System,
                Code = code.Code,
                Display = code.Display,
                Status = code.Status.ToString()
            }).ToList();
            var metadata = response.Body.Metadata;
            page.Paging = ConfigurationRules.Bar(
                metadata?.PageNumber > 0 ? metadata.PageNumber : clean.Page ?? 1,
                metadata?.PageSize > 0 ? metadata.PageSize : clean.PageSize ?? ConfigurationRules.DefaultPageSize,
                metadata?.TotalCount ?? page.Codes.Count,
                metadata?.TotalPages > 0 ? (int)metadata.TotalPages : null);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Terminology code search failed");
            page.LoadError = "Terminology service call failed: " + ex.Message;
        }

        return page;
    }

    public async Task<HslocPage> LoadHslocAsync(HslocQuery? query, CancellationToken cancellationToken)
    {
        var page = new HslocPage { Configured = _normalization is not null, Query = query ?? new HslocQuery() };
        if (_normalization is null)
        {
            page.LoadError = NormalizationNotConfigured;
            return page;
        }

        try
        {
            var response = await _normalization.GetHslocCodesAsync(includeInactive: true, cancellationToken);
            if (!Ok(response))
            {
                page.LoadError = Fail("Normalization", response.StatusCode, response.RawBody);
                return page;
            }

            var rows = (response.Body ?? new List<HslocCodeApiModel>())
                .Select(code => new HslocRow
                {
                    Id = code.Id,
                    Code = code.HSLOCCode,
                    CdcCode = code.CDCCode,
                    ShortDescription = code.ShortDescription,
                    LongDescription = code.LongDescription,
                    Version = code.Version,
                    Active = code.IsActive
                })
                .OrderBy(code => code.Code, StringComparer.OrdinalIgnoreCase)
                .ToList();
            var paged = ConfigurationRules.PageHsloc(rows, query);
            page.Rows = paged.Page;
            page.Paging = paged.Bar;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "HSLOC list failed");
            page.LoadError = "Normalization service call failed: " + ex.Message;
        }

        return page;
    }

    public async Task<ConfigurationAction> ReplaceHslocAsync(string? oldVersion, string? newVersion, Stream? csv, long length, string? fileName, CancellationToken cancellationToken)
    {
        if (_normalization is null)
            return ConfigurationAction.Fail(NormalizationNotConfigured);
        var error = ConfigurationRules.CheckHslocVersions(oldVersion, newVersion, out var oldValue, out var newValue);
        if (error is not null)
            return ConfigurationAction.Fail(error);
        if (csv is null || length <= 0)
            return ConfigurationAction.Fail("A non-empty HSLOC CSV file is required.");
        if (length > ConfigurationRules.MaxUploadBytes)
            return ConfigurationAction.Fail("The CSV is larger than 8 MB.");

        try
        {
            var response = await _normalization.UpdateHslocCodesAsync(oldValue, newValue, csv, SafeFileName(fileName), cancellationToken);
            return Ok(response)
                ? ConfigurationAction.Ok("HSLOC version " + oldValue + " was replaced with " + newValue + ".")
                : ConfigurationAction.Fail(Fail("Normalization", response.StatusCode, response.RawBody));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "HSLOC replace failed");
            return ConfigurationAction.Fail("Normalization service call failed: " + ex.Message);
        }
    }

    public async Task<ConfigurationAction> DeleteHslocAsync(Guid id, CancellationToken cancellationToken)
    {
        if (_normalization is null)
            return ConfigurationAction.Fail(NormalizationNotConfigured);
        if (id == Guid.Empty)
            return ConfigurationAction.Fail("HSLOC id is not a valid id.");

        try
        {
            var response = await _normalization.DeleteHslocCodeAsync(id, cancellationToken);
            if (Ok(response))
                return ConfigurationAction.Ok("HSLOC code was deleted.");
            var detail = response.RawBody ?? string.Empty;
            if (detail.Contains("FK_FacilityLocationLocalCodeMapping_HSLOC", StringComparison.Ordinal))
                return ConfigurationAction.Fail("This HSLOC code is used by a facility location mapping. Update or remove that mapping before deleting the code.");
            return ConfigurationAction.Fail(Fail("Normalization", response.StatusCode, response.RawBody));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "HSLOC delete failed");
            return ConfigurationAction.Fail("Normalization service call failed: " + ex.Message);
        }
    }

    public async Task<OperationSearchPage> LoadOperationsAsync(OperationQuery? query, CancellationToken cancellationToken)
    {
        var page = new OperationSearchPage { Configured = _normalization is not null, Query = query ?? new OperationQuery() };
        var error = ConfigurationRules.CheckOperation(query, _options.NumericOnlyFacilityId, out var clean);
        if (error is null)
            page.Query = clean;
        if (error is not null)
            page.LoadError = error;
        else if (_normalization is null)
            page.LoadError = NormalizationNotConfigured;
        else
        {
            try
            {
            var resources = await _normalization.GetResourcesAsync(cancellationToken);
            if (Ok(resources))
            {
                page.ResourceTypes = (resources.Body ?? new List<NormalizationResourceApiModel>())
                    .Select(item => item.ResourceName)
                    .Where(name => !string.IsNullOrWhiteSpace(name))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
                    .ToList();
            }
            else
            {
                page.ResourceWarning = Fail("Normalization", resources.StatusCode, resources.RawBody);
            }

            Guid? operationId = string.IsNullOrWhiteSpace(clean.OperationId) ? null : Guid.Parse(clean.OperationId);
            Guid? vendorVersionId = string.IsNullOrWhiteSpace(clean.VendorVersionId) ? null : Guid.Parse(clean.VendorVersionId);
            var response = await _normalization.SearchOperationsAsync(
                clean.FacilityId,
                clean.OperationType,
                clean.ResourceType,
                operationId,
                clean.IncludeDisabled,
                vendorVersionId,
                clean.SortBy,
                ConfigurationRules.SortOrder(clean.SortDir),
                clean.PageSize ?? ConfigurationRules.DefaultPageSize,
                clean.Page ?? 1,
                cancellationToken);
            if (!Ok(response) || response.Body is null)
            {
                page.LoadError = Fail("Normalization", response.StatusCode, response.RawBody);
            }
            else
            {
                page.Operations = response.Body.Records.Select(item => new OperationRow
                {
                    Id = item.Id,
                    Name = item.Name,
                    OperationType = item.OperationType,
                    FacilityId = item.FacilityId,
                    Disabled = item.IsDisabled,
                    Resources = string.Join(", ", item.OperationResourceTypes
                        .Select(type => type.Resource?.ResourceName)
                        .Where(name => !string.IsNullOrWhiteSpace(name))),
                    VendorVersionIds = (item.VendorPresets ?? [])
                        .Select(preset => preset.VendorVersionId)
                        .Where(id => id != Guid.Empty)
                        .Distinct()
                        .ToList()
                }).ToList();
                var metadata = response.Body.Metadata;
                page.Paging = ConfigurationRules.Bar(
                    metadata?.PageNumber > 0 ? metadata.PageNumber : clean.Page ?? 1,
                    metadata?.PageSize > 0 ? metadata.PageSize : clean.PageSize ?? ConfigurationRules.DefaultPageSize,
                    metadata?.TotalCount ?? page.Operations.Count,
                    metadata?.TotalPages > 0 ? (int)metadata.TotalPages : null);
            }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Normalization operation search failed");
                page.LoadError = "Normalization service call failed: " + ex.Message;
            }
        }

        if (_normalization is not null)
            await AttachVendorEditorAsync(page, cancellationToken);

        return page;
    }

    public async Task<NotificationListPage> LoadNotificationsAsync(NotificationQuery? query, CancellationToken cancellationToken)
    {
        var page = new NotificationListPage { Configured = _notification is not null, Query = query ?? new NotificationQuery() };
        var error = ConfigurationRules.CheckNotification(query, _options.NumericOnlyFacilityId, out var clean);
        if (error is null)
            page.Query = clean;
        if (error is not null)
        {
            page.LoadError = error;
            return page;
        }

        if (_notification is null)
        {
            page.LoadError = NotificationNotConfigured;
            return page;
        }

        try
        {
            var response = await _notification.SearchNotificationsAsync(
                clean.SearchText,
                clean.FacilityId,
                clean.NotificationType,
                clean.CreatedOnStart,
                clean.CreatedOnEnd,
                clean.SentOnStart,
                clean.SentOnEnd,
                "CreatedOn",
                "Descending",
                clean.PageSize ?? ConfigurationRules.DefaultPageSize,
                clean.Page ?? 1,
                cancellationToken);
            if (!Ok(response) || response.Body is null)
            {
                page.LoadError = Fail("Notification", response.StatusCode, response.RawBody);
                return page;
            }

            page.Notifications = response.Body.Records.Select(item => new NotificationRow
            {
                Id = item.Id ?? string.Empty,
                Type = item.NotificationType ?? string.Empty,
                FacilityId = item.FacilityId ?? string.Empty,
                Subject = item.Subject ?? string.Empty,
                CreatedOn = Stamp(item.CreatedOn)
            }).ToList();
            page.Paging = Bar(response.Body.Metadata, clean.Page ?? 1, clean.PageSize ?? ConfigurationRules.DefaultPageSize, page.Notifications.Count);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Notification search failed");
            page.LoadError = "Notification service call failed: " + ex.Message;
        }

        return page;
    }

    public async Task<NotificationDetailPage> LoadNotificationAsync(Guid id, CancellationToken cancellationToken)
    {
        var page = new NotificationDetailPage { Configured = _notification is not null, Id = id.ToString() };
        if (id == Guid.Empty)
        {
            page.LoadError = "Notification id is not a valid id.";
            return page;
        }

        if (_notification is null)
        {
            page.LoadError = NotificationNotConfigured;
            return page;
        }

        try
        {
            var response = await _notification.GetNotificationAsync(id, cancellationToken);
            if (response.StatusCode == 404)
            {
                page.LoadError = "That notification was not found.";
                return page;
            }

            if (!Ok(response) || response.Body is null)
            {
                page.LoadError = Fail("Notification", response.StatusCode, response.RawBody);
                return page;
            }

            var item = response.Body;
            page.Type = item.NotificationType ?? string.Empty;
            page.FacilityId = item.FacilityId ?? string.Empty;
            page.CorrelationId = item.CorrelationId ?? string.Empty;
            page.Subject = item.Subject ?? string.Empty;
            page.Body = item.Body ?? string.Empty;
            page.Recipients = string.Join(", ", item.Recipients ?? new List<string>());
            page.Bcc = string.Join(", ", item.Bcc ?? new List<string>());
            page.CreatedOn = Stamp(item.CreatedOn);
            page.SentOn = string.Join(", ", (item.SentOn ?? new List<DateTime>()).Select(sent => Stamp(sent)));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Notification load failed");
            page.LoadError = "Notification service call failed: " + ex.Message;
        }

        return page;
    }

    public async Task<FacilityNotificationSection> LoadFacilityNotificationAsync(string facilityId, CancellationToken cancellationToken)
    {
        var section = new FacilityNotificationSection { Configured = _notification is not null };
        if (_notification is null)
            return section;

        var page = await LoadConfigurationsAsync(
            new NotificationConfigQuery { FacilityId = facilityId, Page = 1, PageSize = 20 },
            cancellationToken);
        if (!string.IsNullOrWhiteSpace(page.LoadError) && page.Configurations.Count == 0)
        {
            section.Error = page.LoadError;
            return section;
        }

        var match = page.Configurations.FirstOrDefault(row =>
            string.Equals(row.FacilityId, facilityId, StringComparison.OrdinalIgnoreCase));
        if (match is null)
            return section;

        section.Exists = true;
        section.Id = match.Id;
        section.Emails = match.Emails;
        section.EmailEnabled = match.EmailEnabled;
        return section;
    }

    public bool AcceptFacilityReturn(string? facilityId) =>
        FacilityFormRules.IsValidFacilityId(facilityId, _options.NumericOnlyFacilityId);

    public async Task<NotificationConfigPage> LoadConfigurationsAsync(NotificationConfigQuery? query, CancellationToken cancellationToken)
    {
        var page = new NotificationConfigPage { Configured = _notification is not null, Query = query ?? new NotificationConfigQuery() };
        var error = ConfigurationRules.CheckConfigSearch(query, _options.NumericOnlyFacilityId, out var clean);
        if (error is null)
            page.Query = clean;
        if (error is not null)
        {
            page.LoadError = error;
            return page;
        }

        if (_notification is null)
        {
            page.LoadError = NotificationNotConfigured;
            return page;
        }

        try
        {
            var response = await _notification.SearchConfigurationsAsync(
                clean.SearchText,
                clean.FacilityId,
                "FacilityId",
                "Ascending",
                clean.PageSize ?? ConfigurationRules.DefaultPageSize,
                clean.Page ?? 1,
                cancellationToken);
            if (!Ok(response) || response.Body is null)
            {
                page.LoadError = Fail("Notification", response.StatusCode, response.RawBody);
                return page;
            }

            page.Configurations = response.Body.Records.Select(MapConfig).ToList();
            page.Paging = Bar(response.Body.Metadata, clean.Page ?? 1, clean.PageSize ?? ConfigurationRules.DefaultPageSize, page.Configurations.Count);
            if (clean.Edit is not null)
            {
                var existing = await _notification.GetConfigurationAsync(clean.Edit, cancellationToken);
                if (Ok(existing) && existing.Body is not null)
                    page.Form = FormFrom(existing.Body);
                else
                    page.FormError = existing.StatusCode == 404
                        ? "That notification configuration was not found."
                        : Fail("Notification", existing.StatusCode, existing.RawBody);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Notification configuration list failed");
            page.LoadError = "Notification service call failed: " + ex.Message;
        }

        return page;
    }

    public async Task<ConfigurationAction> SaveConfigurationAsync(NotificationConfigForm form, CancellationToken cancellationToken)
    {
        if (_notification is null)
            return ConfigurationAction.Fail(NotificationNotConfigured);
        var facilityError = ConfigurationRules.CheckFacility(form.FacilityId, _options.NumericOnlyFacilityId, required: true, out var facilityId);
        if (facilityError is not null)
            return ConfigurationAction.Fail(facilityError);
        var emailError = ConfigurationRules.CheckEmails(form.Emails, form.EmailEnabled, out var emails);
        if (emailError is not null)
            return ConfigurationAction.Fail(emailError);

        var creating = string.IsNullOrWhiteSpace(form.Id);
        if (!creating && !Guid.TryParse(form.Id, out _))
            return ConfigurationAction.Fail("Configuration id is not a valid id.");

        try
        {
            var channels = new List<NotificationChannelApiModel>
            {
                new() { Name = ConfigurationRules.EmailChannel, Enabled = form.EmailEnabled }
            };
            List<EnabledNotificationApiModel>? enabled = null;
            if (!creating)
            {
                var existing = await _notification.GetConfigurationAsync(form.Id!.Trim(), cancellationToken);
                if (!Ok(existing) || existing.Body is null)
                {
                    return ConfigurationAction.Fail(existing.StatusCode == 404
                        ? "That notification configuration was not found."
                        : Fail("Notification", existing.StatusCode, existing.RawBody));
                }

                enabled = existing.Body.EnabledNotifications;
                foreach (var channel in existing.Body.Channels ?? new List<NotificationChannelApiModel>())
                {
                    if (!string.Equals(channel.Name, ConfigurationRules.EmailChannel, StringComparison.OrdinalIgnoreCase))
                        channels.Add(channel);
                }
            }

            var model = new NotificationConfigurationApiModel
            {
                Id = creating ? null : form.Id!.Trim(),
                FacilityId = facilityId,
                EmailAddresses = emails,
                EnabledNotifications = enabled,
                Channels = channels
            };
            var response = creating
                ? await _notification.CreateConfigurationAsync(model, cancellationToken)
                : await _notification.UpdateConfigurationAsync(model, cancellationToken);
            return Ok(response)
                ? ConfigurationAction.Ok(creating
                    ? "Notification configuration for " + facilityId + " was created."
                    : "Notification configuration for " + facilityId + " was updated.")
                : ConfigurationAction.Fail(Fail("Notification", response.StatusCode, response.RawBody));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Notification configuration save failed");
            return ConfigurationAction.Fail("Notification service call failed: " + ex.Message);
        }
    }

    public async Task<ConfigurationAction> DeleteConfigurationAsync(string? id, CancellationToken cancellationToken)
    {
        if (_notification is null)
            return ConfigurationAction.Fail(NotificationNotConfigured);
        if (!Guid.TryParse(id, out var parsed) || parsed == Guid.Empty)
            return ConfigurationAction.Fail("Configuration id is not a valid id.");

        try
        {
            var response = await _notification.DeleteConfigurationAsync(parsed.ToString(), cancellationToken);
            return Ok(response)
                ? ConfigurationAction.Ok("Notification configuration was deleted.")
                : ConfigurationAction.Fail(Fail("Notification", response.StatusCode, response.RawBody));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Notification configuration delete failed");
            return ConfigurationAction.Fail("Notification service call failed: " + ex.Message);
        }
    }

    public async Task<MappingPage> LoadMappingsAsync(MappingQuery? query, CancellationToken cancellationToken)
    {
        var page = new MappingPage
        {
            Enabled = _options.DmrpEnabled,
            Configured = _dmrp is not null,
            Query = query ?? new MappingQuery()
        };
        if (!_options.DmrpEnabled)
        {
            page.LoadError = DmrpOff;
            return page;
        }

        var error = ConfigurationRules.CheckMappingSearch(query, out var clean);
        if (error is null)
            page.Query = clean;
        if (error is not null)
        {
            page.LoadError = error;
            return page;
        }

        if (_dmrp is null)
        {
            page.LoadError = TenantNotConfigured;
            return page;
        }

        try
        {
            Frequency? frequency = string.IsNullOrWhiteSpace(clean.Frequency) ? null : Enum.Parse<Frequency>(clean.Frequency);
            var response = await _dmrp.SearchMeasureMappingsAsync(
                clean.Measure,
                clean.Dqm,
                frequency,
                clean.PageSize ?? ConfigurationRules.DefaultPageSize,
                clean.Page ?? 1,
                cancellationToken);
            if (response.StatusCode == 204)
            {
                page.Paging = ConfigurationRules.Bar(clean.Page ?? 1, clean.PageSize ?? ConfigurationRules.DefaultPageSize, 0, 1);
                return page;
            }

            if (!Ok(response) || response.Body is null)
            {
                page.LoadError = Fail("DMRP", response.StatusCode, response.RawBody);
                return page;
            }

            page.Mappings = response.Body.Records.Select(item => new MappingRow
            {
                Id = item.Id ?? string.Empty,
                Measure = item.Measure ?? string.Empty,
                Dqm = item.DQM ?? string.Empty,
                Frequency = item.Frequency?.ToString() ?? string.Empty
            }).ToList();
            var metadata = response.Body.Metadata;
            page.Paging = ConfigurationRules.Bar(
                metadata?.PageNumber > 0 ? metadata.PageNumber : clean.Page ?? 1,
                metadata?.PageSize > 0 ? metadata.PageSize : clean.PageSize ?? ConfigurationRules.DefaultPageSize,
                metadata?.TotalCount ?? page.Mappings.Count,
                metadata?.TotalPages > 0 ? (int)metadata.TotalPages : null);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Measure mapping search failed");
            page.LoadError = "DMRP service call failed: " + ex.Message;
        }

        return page;
    }

    public async Task<ConfigurationAction> SaveMappingAsync(MappingForm form, CancellationToken cancellationToken)
    {
        if (!_options.DmrpEnabled)
            return ConfigurationAction.Fail(DmrpOff);
        if (_dmrp is null)
            return ConfigurationAction.Fail(TenantNotConfigured);
        var creating = string.IsNullOrWhiteSpace(form.Id);
        var error = ConfigurationRules.CheckMapping(form, creating, out var clean);
        if (error is not null)
            return ConfigurationAction.Fail(error);

        var model = new MeasureMappingModel
        {
            Id = creating ? null : clean.Id,
            Measure = clean.Measure,
            DQM = clean.Dqm,
            Frequency = Enum.Parse<Frequency>(clean.Frequency!)
        };

        try
        {
            var response = creating
                ? await _dmrp.CreateMeasureMappingAsync(model, cancellationToken)
                : await _dmrp.UpdateMeasureMappingAsync(clean.Id!, model, cancellationToken);
            return Ok(response)
                ? ConfigurationAction.Ok(creating ? "Measure mapping was created." : "Measure mapping was updated.")
                : ConfigurationAction.Fail(Fail("DMRP", response.StatusCode, response.RawBody));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Measure mapping save failed");
            return ConfigurationAction.Fail("DMRP service call failed: " + ex.Message);
        }
    }

    public async Task<ConfigurationAction> DeleteMappingAsync(string? id, CancellationToken cancellationToken)
    {
        if (!_options.DmrpEnabled)
            return ConfigurationAction.Fail(DmrpOff);
        if (_dmrp is null)
            return ConfigurationAction.Fail(TenantNotConfigured);
        if (!Guid.TryParse(id, out var parsed) || parsed == Guid.Empty)
            return ConfigurationAction.Fail("Mapping id is not a valid id.");

        try
        {
            var response = await _dmrp.DeleteMeasureMappingAsync(parsed.ToString(), cancellationToken);
            return Ok(response)
                ? ConfigurationAction.Ok("Measure mapping was deleted.")
                : ConfigurationAction.Fail(Fail("DMRP", response.StatusCode, response.RawBody));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Measure mapping delete failed");
            return ConfigurationAction.Fail("DMRP service call failed: " + ex.Message);
        }
    }

    private async Task<QueryPlanRow> LoadPlanAsync(string facilityId, string type, CancellationToken cancellationToken)
    {
        var response = await _acquisition!.GetQueryPlanAsync(facilityId, type, cancellationToken);
        if (response.StatusCode is 404 or 204)
            return new QueryPlanRow { Type = type };
        if (!Ok(response))
            return new QueryPlanRow { Type = type, Error = Fail("Data acquisition", response.StatusCode, response.RawBody) };
        return new QueryPlanRow { Type = type, Saved = true };
    }

    private static IReadOnlyList<TerminologyResourceRow> ReadResources(
        LinkApiResponse<string> response,
        string resourceType,
        string invalidMessage,
        out string? error)
    {
        if (!Ok(response))
        {
            error = Fail("Terminology", response.StatusCode, response.RawBody);
            return Array.Empty<TerminologyResourceRow>();
        }

        var rows = ConfigurationRules.ReadTerminology(response.Body, resourceType);
        if (rows is null)
        {
            error = invalidMessage;
            return Array.Empty<TerminologyResourceRow>();
        }

        error = null;
        return rows;
    }

    private static NotificationConfigRow MapConfig(NotificationConfigurationApiModel item)
    {
        var email = item.Channels?.FirstOrDefault(channel =>
            string.Equals(channel.Name, ConfigurationRules.EmailChannel, StringComparison.OrdinalIgnoreCase));
        return new NotificationConfigRow
        {
            Id = item.Id ?? string.Empty,
            FacilityId = item.FacilityId ?? string.Empty,
            Emails = string.Join(", ", item.EmailAddresses ?? new List<string>()),
            EmailEnabled = email?.Enabled == true
        };
    }

    private static NotificationConfigForm FormFrom(NotificationConfigurationApiModel item)
    {
        var email = item.Channels?.FirstOrDefault(channel =>
            string.Equals(channel.Name, ConfigurationRules.EmailChannel, StringComparison.OrdinalIgnoreCase));
        return new NotificationConfigForm
        {
            Id = item.Id,
            FacilityId = item.FacilityId,
            Emails = string.Join(Environment.NewLine, item.EmailAddresses ?? new List<string>()),
            EmailEnabled = email?.Enabled == true
        };
    }

    private static PageBar Bar(NotificationPageMetadata? metadata, int page, int pageSize, int count) =>
        ConfigurationRules.Bar(
            metadata?.PageNumber > 0 ? metadata.PageNumber : page,
            metadata?.PageSize > 0 ? metadata.PageSize : pageSize,
            metadata?.TotalCount ?? count,
            metadata?.TotalPages > 0 ? (int)metadata.TotalPages : null);

    private async Task LoadRulesAsync(CategoryPage page, CancellationToken cancellationToken)
    {
        if (_validation is null || string.IsNullOrWhiteSpace(page.Form.Id))
            return;

        try
        {
            var response = await _validation.GetCategoryRuleHistoryAsync(page.Form.Id, cancellationToken);
            if (response.StatusCode == 404)
                return;
            if (!Ok(response))
            {
                page.RuleError = Fail("Validation", response.StatusCode, response.RawBody);
                return;
            }

            var rows = ConfigurationRules.ReadRules(response.Body);
            if (rows is null)
            {
                page.RuleError = "Category rules were not a list.";
                return;
            }

            page.Rules = rows;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Validation category rule history failed");
            page.RuleError = "Validation service call failed: " + ex.Message;
        }
    }

    private static async Task<(string? Text, string? Error)> ReadUploadAsync(Stream? stream, long length, CancellationToken cancellationToken)
    {
        if (stream is null || length <= 0)
            return (null, "A measure definition bundle file is required.");
        if (length > ConfigurationRules.MaxUploadBytes)
            return (null, "The bundle is larger than 8 MB.");

        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: true);
        var text = await reader.ReadToEndAsync(cancellationToken);
        if (text.Length > ConfigurationRules.MaxUploadBytes)
            return (null, "The bundle is larger than 8 MB.");
        return (text, null);
    }

    private static async Task<(string? Text, string? Error)> ReadTextUploadAsync(Stream? stream, long length, string missing, CancellationToken cancellationToken)
    {
        if (stream is null || length <= 0)
            return (null, missing);
        if (length > ConfigurationRules.MaxUploadBytes)
            return (null, "The file is larger than 8 MB.");

        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: true);
        var text = await reader.ReadToEndAsync(cancellationToken);
        if (text.Length > ConfigurationRules.MaxUploadBytes)
            return (null, "The file is larger than 8 MB.");
        return (text, null);
    }

    private static async Task<(byte[]? Bytes, string? Error)> ReadBytesAsync(Stream? stream, long length, string missing, CancellationToken cancellationToken)
    {
        if (stream is null || length <= 0)
            return (null, missing);
        if (length > ConfigurationRules.MaxUploadBytes)
            return (null, "The file is larger than 8 MB.");

        using var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer, cancellationToken);
        if (buffer.Length == 0)
            return (null, missing);
        if (buffer.Length > ConfigurationRules.MaxUploadBytes)
            return (null, "The file is larger than 8 MB.");
        return (buffer.ToArray(), null);
    }

    private static string SafeFileName(string? fileName)
    {
        var name = Path.GetFileName(fileName ?? string.Empty);
        if (string.IsNullOrWhiteSpace(name) || name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            return "hsloc.csv";
        return name.Length <= 120 ? name : name[..120];
    }

    private static string Stamp(DateTime? value)
    {
        if (value is null)
            return string.Empty;
        return LinkUiTime.IsoUtc(value);
    }

    private static T? Client<T>(IServiceProvider services, string? url) where T : class =>
        string.IsNullOrWhiteSpace(url) ? null : services.GetRequiredService<T>();

    private static bool Ok(LinkApiResponse response) => response.StatusCode is >= 200 and < 300;

    private static bool Ok<T>(LinkApiResponse<T> response) => response.StatusCode is >= 200 and < 300;

    private static string Fail(string service, int statusCode, string? body) =>
        FacilityFormRules.ServiceMessage(service, statusCode, body);
}
