using LantanaGroup.Link.Shared.Application.Models.Integration.Normalization;
using LantanaGroup.Link.Shared.Application.Models.Tenant;
using Link.UI.Models;
using Microsoft.AspNetCore.Http;

namespace Link.UI.Services;

public sealed partial class ConfigurationService
{
    public async Task<OperationSearchPage> SaveVendorOperationAsync(
        NormalizationOperationInput input,
        CancellationToken cancellationToken)
    {
        var page = await LoadOperationsAsync(new OperationQuery(), cancellationToken);
        if (page.VendorEditor is null)
        {
            page.LoadError = NormalizationNotConfigured;
            return page;
        }

        var editor = page.VendorEditor;
        editor.Normalization.VendorOwned = true;
        editor.Normalization.EditorOpen = true;
        editor.Normalization.Editor = FacilityNormalizationRules.WithBlanks(input);
        editor.FormError = null;

        if (input.ParseFailed)
        {
            editor.FormError = "This operation could not be read, so it was not saved.";
            return page;
        }

        if (string.Equals(
                FacilityNormalizationRules.CanonicalType(input.OperationType),
                "HSLOCMap",
                StringComparison.OrdinalIgnoreCase))
        {
            editor.FormError = "An HSLOC map cannot be assigned to a vendor version.";
            return page;
        }

        Guid? id = null;
        var existingIds = new List<Guid>();
        if (!string.IsNullOrWhiteSpace(input.OperationId))
        {
            if (!Guid.TryParse(input.OperationId, out var parsed) || parsed == Guid.Empty)
            {
                editor.FormError = "Invalid operation id.";
                return page;
            }

            var found = await FindOperationAsync(parsed, cancellationToken);
            if (found.Error is not null)
            {
                editor.FormError = found.Error;
                return page;
            }

            if (!string.IsNullOrWhiteSpace(found.Model!.FacilityId))
            {
                editor.FormError = "This operation belongs to a facility. Open that facility to edit it.";
                return page;
            }

            var existingType = FacilityNormalizationRules.CanonicalType(found.Model.OperationType) ?? found.Model.OperationType;
            var postedType = FacilityNormalizationRules.CanonicalType(input.OperationType);
            if (!string.Equals(existingType, postedType, StringComparison.OrdinalIgnoreCase))
            {
                editor.FormError = "Operation type cannot be changed.";
                return page;
            }

            existingIds.AddRange(VendorIds(found.Model));
            id = parsed;
        }

        if (editor.Normalization.ResourceTypes.Count == 0)
        {
            editor.FormError = page.ResourceWarning ?? "Normalization has no resource types yet.";
            return page;
        }

        if (!FacilityNormalizationRules.TryBuild(
                input,
                string.Empty,
                editor.Normalization.ResourceTypes,
                out var request,
                out var isDisabled,
                out var error))
        {
            editor.FormError = error;
            return page;
        }

        if (!FacilityNormalizationRules.TryVendorOwners(
                input.VendorPresetsPosted,
                editor.VendorListLoaded,
                input.VendorVersionIds,
                editor.Vendors.Select(vendor => vendor.Id),
                existingIds,
                out var owners,
                out var ownerError))
        {
            editor.FormError = ownerError;
            return page;
        }

        request!.FacilityId = null;
        request.VendorVersionIds = owners!;

        try
        {
            if (id is null)
            {
                var created = await _normalization!.CreateOperationAsync(request, cancellationToken);
                if (!Ok(created))
                {
                    editor.FormError = Fail("Normalization", created.StatusCode, created.RawBody);
                    return page;
                }
            }
            else
            {
                var updated = await _normalization!.UpdateOperationAsync(new UpdateNormalizationOperationRequestApiModel
                {
                    Id = id,
                    FacilityId = null,
                    ResourceTypes = request.ResourceTypes,
                    Operation = request.Operation,
                    IsDisabled = isDisabled,
                    VendorVersionIds = owners
                }, cancellationToken);
                if (!Ok(updated))
                {
                    editor.FormError = Fail("Normalization", updated.StatusCode, updated.RawBody);
                    return page;
                }
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Vendor operation save failed");
            editor.FormError = "Normalization service call failed: " + ex.Message;
            return page;
        }

        page.ActionMessage = "Vendor operation saved.";
        return page;
    }

    public async Task<OperationSearchPage> ImportVendorExtensionsAsync(
        IReadOnlyList<string>? vendorVersionIds,
        string? csv,
        bool fileTooLarge,
        CancellationToken cancellationToken)
    {
        var page = await LoadOperationsAsync(new OperationQuery(), cancellationToken);
        var posted = vendorVersionIds?.Where(id => !string.IsNullOrWhiteSpace(id)).ToList() ?? [];
        if (page.VendorEditor is null)
        {
            page.LoadError = NormalizationNotConfigured;
            return page;
        }

        page.VendorEditor.Normalization.ImportVendorIds = posted;
        if (fileTooLarge)
        {
            page.VendorEditor.FormError = "The CSV must be 64 KB or smaller.";
            return page;
        }

        if (page.VendorEditor.Normalization.ResourceTypes.Count == 0)
        {
            page.VendorEditor.FormError = page.ResourceWarning ?? "Normalization has no resource types yet.";
            return page;
        }

        if (!FacilityNormalizationRules.TryVendorOwners(
                posted: true,
                page.VendorEditor.VendorListLoaded,
                posted,
                page.VendorEditor.Vendors.Select(vendor => vendor.Id),
                [],
                out var owners,
                out var ownerError))
        {
            page.VendorEditor.FormError = ownerError;
            return page;
        }

        if (!FacilityNormalizationRules.TryParseExtensionCsv(
                csv,
                page.VendorEditor.Normalization.ResourceTypes,
                out var groups,
                out var parseError))
        {
            page.VendorEditor.FormError = parseError;
            return page;
        }

        var records = new List<NormalizationOperationApiModel>();
        try
        {
            foreach (var owner in owners!)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var found = await _normalization!.SearchOperationsAsync(
                    operationType: "RemoveExtensions",
                    includeDisabled: true,
                    vendorVersionId: owner,
                    pageSize: FacilityNormalizationRules.MaxImportUrls,
                    pageNumber: 1,
                    cancellationToken: cancellationToken);
                if (found.StatusCode == StatusCodes.Status204NoContent)
                    continue;
                if (!Ok(found) || found.Body is null)
                {
                    page.VendorEditor.FormError = Fail("Normalization", found.StatusCode, found.RawBody);
                    return page;
                }

                var pageRecords = found.Body.Records ?? [];
                var metadata = found.Body.Metadata;
                if (metadata is not null && (metadata.TotalPages > 1 || metadata.TotalCount > pageRecords.Count))
                {
                    page.VendorEditor.FormError = "Too many remove-extensions operations to check for conflicts, so nothing was imported.";
                    return page;
                }

                records.AddRange(pageRecords);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Vendor extension import search failed");
            page.VendorEditor.FormError = "Normalization service call failed: " + ex.Message;
            return page;
        }

        if (FacilityNormalizationRules.HasExtensionConflict(records, groups, out var conflict))
        {
            page.VendorEditor.FormError = conflict;
            return page;
        }

        var created = 0;
        try
        {
            foreach (var group in groups)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var input = new NormalizationOperationInput
                {
                    OperationType = "RemoveExtensions",
                    Name = "Remove Extensions - " + group.ResourceType,
                    Description = "Imported extension URLs.",
                    ResourceTypes = [group.ResourceType],
                    ExtensionUrls = group.Urls.Select(url => new ExtensionUrlInput { Url = url }).ToList()
                };
                if (!FacilityNormalizationRules.TryBuild(
                        input,
                        string.Empty,
                        page.VendorEditor.Normalization.ResourceTypes,
                        out var request,
                        out _,
                        out var buildError))
                {
                    return await ImportStoppedAsync(posted, created, buildError, cancellationToken);
                }

                request!.FacilityId = null;
                request.VendorVersionIds = owners!;
                var response = await _normalization!.CreateOperationAsync(request, cancellationToken);
                if (!Ok(response))
                {
                    var message = Fail("Normalization", response.StatusCode, response.RawBody);
                    return await ImportStoppedAsync(posted, created, message, cancellationToken);
                }

                created++;
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Vendor extension import failed");
            return await ImportStoppedAsync(posted, created, "Normalization service call failed: " + ex.Message, cancellationToken);
        }

        page.ActionMessage = created == 1
            ? "Imported 1 remove-extensions operation."
            : $"Imported {created} remove-extensions operations.";
        return page;
    }

    public async Task<ConfigurationAction> DeleteVendorOperationAsync(
        string? operationId,
        string? vendorVersionId,
        CancellationToken cancellationToken)
    {
        if (_normalization is null)
            return ConfigurationAction.Fail(NormalizationNotConfigured);
        if (!Guid.TryParse(operationId, out var id) || id == Guid.Empty)
            return ConfigurationAction.Fail("Invalid operation id.");

        if (!Guid.TryParse(vendorVersionId, out var versionId) || versionId == Guid.Empty)
        {
            var found = await FindOperationAsync(id, cancellationToken);
            if (found.Error is not null)
                return ConfigurationAction.Fail(found.Error);
            if (!string.IsNullOrWhiteSpace(found.Model!.FacilityId))
                return ConfigurationAction.Fail("This operation belongs to a facility. Open that facility to edit it.");

            versionId = VendorIds(found.Model).FirstOrDefault();
            if (versionId == Guid.Empty)
                return ConfigurationAction.Fail("This operation has no vendor version, so it cannot be deleted from here.");
        }

        try
        {
            var response = await _normalization.DeleteVendorVersionOperationAsync(versionId, id, cancellationToken);
            if (response.StatusCode == StatusCodes.Status404NotFound)
                return ConfigurationAction.Fail("That operation was not found.");
            if (!Ok(response))
                return ConfigurationAction.Fail(Fail("Normalization", response.StatusCode, response.RawBody));
            return ConfigurationAction.Ok("Vendor operation deleted.");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Vendor operation delete failed");
            return ConfigurationAction.Fail("Normalization service call failed: " + ex.Message);
        }
    }

    private async Task AttachVendorEditorAsync(OperationSearchPage page, CancellationToken cancellationToken)
    {
        if (page.ResourceTypes.Count == 0 && page.ResourceWarning is null)
            await LoadOperationResourcesAsync(page, cancellationToken);

        var vendors = await LoadVendorOptionsAsync(cancellationToken);
        var panel = new NormalizationPanel
        {
            VendorOwned = true,
            ResourceTypes = page.ResourceTypes,
            ResourceWarning = page.ResourceWarning
        };
        var editor = new FacilityHubViewModel
        {
            NormalizationConfigured = true,
            VendorListLoaded = vendors.Loaded,
            VendorWarning = vendors.Warning,
            Vendors = vendors.Options,
            Normalization = panel
        };

        if (!string.IsNullOrWhiteSpace(page.Query.EditId))
        {
            if (!Guid.TryParse(page.Query.EditId, out var editId) || editId == Guid.Empty)
            {
                editor.FormError = "Operation id is not a valid id.";
            }
            else
            {
                var found = await FindOperationAsync(editId, cancellationToken);
                if (found.Error is not null)
                {
                    editor.FormError = found.Error;
                }
                else if (!string.IsNullOrWhiteSpace(found.Model!.FacilityId))
                {
                    editor.FormError = "This operation belongs to a facility. Open that facility to edit it.";
                }
                else
                {
                    panel.Editor = FacilityNormalizationRules.FromOperation(found.Model);
                    panel.EditorOpen = true;
                }
            }
        }
        else
        {
            var editType = FacilityNormalizationRules.CanonicalType(page.Query.EditType);
            if (editType == "HSLOCMap")
                editor.FormError = "An HSLOC map cannot be assigned to a vendor version.";
            else if (editType is not null)
            {
                panel.Editor = FacilityNormalizationRules.Blank(editType);
                panel.EditorOpen = true;
            }
        }

        page.VendorEditor = editor;
    }

    private async Task LoadOperationResourcesAsync(OperationSearchPage page, CancellationToken cancellationToken)
    {
        try
        {
            var resources = await _normalization!.GetResourcesAsync(cancellationToken);
            if (Ok(resources))
            {
                page.ResourceTypes = (resources.Body ?? [])
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
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Normalization resource list failed");
            page.ResourceWarning = "Normalization service call failed: " + ex.Message;
        }
    }

    private async Task<(bool Loaded, IReadOnlyList<VendorOption> Options, string? Warning)> LoadVendorOptionsAsync(
        CancellationToken cancellationToken)
    {
        if (_tenant is null)
            return (false, [], TenantNotConfigured);

        try
        {
            var response = await _tenant.GetVendorVersionsAsync(cancellationToken: cancellationToken);
            if (response.StatusCode == StatusCodes.Status204NoContent || (Ok(response) && response.Body is null))
                return (true, [], null);
            if (!Ok(response) || response.Body is null)
                return (false, [], Fail("Tenant", response.StatusCode, response.RawBody));

            var options = response.Body
                .Where(version => version.Id is Guid id && id != Guid.Empty)
                .OrderBy(version => version.VendorName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(version => version.Version, StringComparer.OrdinalIgnoreCase)
                .Select(version => new VendorOption
                {
                    Id = version.Id!.Value,
                    Label = string.IsNullOrWhiteSpace(version.VendorName)
                        ? version.Version ?? version.Id.ToString()!
                        : $"{version.VendorName} - {version.Version}"
                })
                .ToList();
            return (true, options, null);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Vendor version list failed");
            return (false, [], "Tenant service call failed while loading vendors.");
        }
    }

    private async Task<(NormalizationOperationApiModel? Model, string? Error)> FindOperationAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        try
        {
            var response = await _normalization!.SearchOperationsAsync(
                operationId: id,
                includeDisabled: true,
                pageSize: 5,
                pageNumber: 1,
                cancellationToken: cancellationToken);
            if (response.StatusCode == StatusCodes.Status204NoContent)
                return (null, "That operation was not found.");
            if (!Ok(response) || response.Body is null)
                return (null, Fail("Normalization", response.StatusCode, response.RawBody));

            var match = (response.Body.Records ?? []).FirstOrDefault(item => item.Id == id);
            return match is null
                ? (null, "That operation was not found.")
                : (match, null);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Vendor operation lookup failed");
            return (null, "Normalization service call failed: " + ex.Message);
        }
    }

    private async Task<OperationSearchPage> ImportStoppedAsync(
        IReadOnlyList<string> posted,
        int created,
        string? reason,
        CancellationToken cancellationToken)
    {
        var message = created == 0
            ? reason ?? "The import did not create an operation."
            : $"Imported {created} operation(s), then the next one was not created: {reason}";
        var page = await LoadOperationsAsync(new OperationQuery(), cancellationToken);
        if (page.VendorEditor is null)
        {
            page.LoadError = message;
            return page;
        }

        page.VendorEditor.FormError = message;
        page.VendorEditor.Normalization.ImportVendorIds = posted;
        return page;
    }

    private static List<Guid> VendorIds(NormalizationOperationApiModel model) =>
        (model.VendorPresets ?? [])
            .Select(preset => preset.VendorVersionId)
            .Where(id => id != Guid.Empty)
            .Distinct()
            .ToList();
}
