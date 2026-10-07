using LantanaGroup.Link.Sdk.ApiClient;
using LantanaGroup.Link.Sdk.Clients;
using LantanaGroup.Link.Shared.Application.Models.Integration.Normalization;
using LantanaGroup.Link.Shared.Application.Models.Responses;
using LantanaGroup.Link.Shared.Application.Services.Security;
using Link.UI.Models;

namespace Link.UI.Services;

/// <summary>
/// Loads and saves facility normalization operations through LinkSDK.
/// </summary>
public sealed class FacilityNormalizationService
{
    public const string NotConfigured =
        "Normalization service URL is not configured (ServiceRegistry:NormalizationServiceUrl).";

    private readonly INormalizationServiceClient _client;
    private readonly ILogger _logger;

    public FacilityNormalizationService(INormalizationServiceClient client, ILogger logger)
    {
        _client = client;
        _logger = logger;
    }

    public async Task LoadAsync(
        FacilityHubViewModel page,
        string? operationId,
        string? operationType,
        string? sequenceType,
        int operationPage,
        CancellationToken cancellationToken)
    {
        var facilityId = page.FacilityId!;
        try
        {
            var pageNumber = FacilityNormalizationRules.ClampPage(operationPage);
            var listTask = _client.SearchFacilityOperationsAsync(
                facilityId,
                includeDisabled: true,
                pageSize: FacilityNormalizationRules.PageSize,
                pageNumber: pageNumber,
                cancellationToken: cancellationToken,
                sortBy: "OperationType",
                sortOrder: "ascending");
            var resourceTask = _client.GetResourcesAsync(cancellationToken);
            var sequenceTask = _client.GetOperationSequencesAsync(facilityId, cancellationToken);

            var list = await listTask;
            if (!ApplyList(page, list, pageNumber, facilityId))
                return;

            ApplyResources(page, await resourceTask, facilityId);
            var sequences = await ReadSequencesAsync(page, facilityId, sequenceTask);
            await ApplyEditorAsync(page, facilityId, operationId, operationType, cancellationToken);
            await ApplySequenceAsync(page, facilityId, sequenceType, sequences, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Normalization load failed. FacilityId={FacilityId}", facilityId.Sanitize());
            page.Normalization.ReadFailed = true;
            page.NormalizationError = "Normalization service call failed: " + ex.Message;
        }
    }

    public async Task<string?> SaveAsync(
        FacilityHubViewModel page,
        NormalizationOperationInput input,
        CancellationToken cancellationToken)
    {
        page.Normalization.Editor = FacilityNormalizationRules.WithBlanks(input);
        page.Normalization.EditorOpen = true;
        var facilityId = page.FacilityId!;

        if (page.Normalization.ResourceTypes.Count == 0)
            return page.Normalization.ResourceWarning ?? "Normalization has no resource types yet.";

        Guid? id = null;
        var existingPresetIds = new List<Guid>();
        if (!string.IsNullOrWhiteSpace(input.OperationId))
        {
            if (!Guid.TryParse(input.OperationId, out var parsed))
                return "Invalid operation id.";

            var existing = await FindAsync(facilityId, parsed, cancellationToken);
            if (existing is null)
                return "That operation was not found.";

            var existingType = FacilityNormalizationRules.CanonicalType(existing.OperationType) ?? existing.OperationType;
            var postedType = FacilityNormalizationRules.CanonicalType(input.OperationType);
            if (!string.Equals(existingType, postedType, StringComparison.OrdinalIgnoreCase))
                return "Operation type cannot be changed.";

            existingPresetIds.AddRange(existing.VendorPresets.Select(preset => preset.VendorVersionId));
            id = parsed;
        }

        if (!FacilityNormalizationRules.TryBuild(
                input,
                facilityId,
                page.Normalization.ResourceTypes,
                out var request,
                out var isDisabled,
                out var error))
            return error;

        if (!FacilityNormalizationRules.TryVendorPresets(
                input.VendorPresetsPosted,
                page.VendorListLoaded,
                id is not null,
                input.VendorVersionIds,
                page.Vendors.Select(vendor => vendor.Id),
                existingPresetIds,
                out var vendorIds,
                out var vendorError))
            return vendorError;

        if (id is null)
        {
            request!.VendorVersionIds = vendorIds ?? [];
            var created = await _client.CreateOperationAsync(request, cancellationToken);
            if (!created.IsSuccessStatusCode)
            {
                Log("Normalization operation create", facilityId, created);
                return FacilityFormRules.ServiceMessage("Normalization", created.StatusCode, created.RawBody);
            }

            return null;
        }

        var updated = await _client.UpdateOperationAsync(new UpdateNormalizationOperationRequestApiModel
        {
            Id = id,
            FacilityId = request!.FacilityId,
            ResourceTypes = request.ResourceTypes,
            Operation = request.Operation,
            IsDisabled = isDisabled,
            VendorVersionIds = vendorIds
        }, cancellationToken);
        if (!updated.IsSuccessStatusCode)
        {
            Log("Normalization operation update", facilityId, updated);
            return FacilityFormRules.ServiceMessage("Normalization", updated.StatusCode, updated.RawBody);
        }

        return null;
    }

    public async Task<string?> ImportExtensionUrlsAsync(
        FacilityHubViewModel page,
        string? csv,
        bool fileTooLarge,
        CancellationToken cancellationToken)
    {
        if (fileTooLarge)
            return "The CSV must be 64 KB or smaller.";

        if (page.Normalization.ResourceTypes.Count == 0)
            return page.Normalization.ResourceWarning ?? "Normalization has no resource types yet.";

        if (!FacilityNormalizationRules.TryParseExtensionCsv(
                csv,
                page.Normalization.ResourceTypes,
                out var groups,
                out var error))
            return error;

        var facilityId = page.FacilityId!;
        var found = await _client.SearchOperationsAsync(
            facilityId: facilityId,
            operationType: "RemoveExtensions",
            includeDisabled: true,
            pageSize: FacilityNormalizationRules.MaxImportUrls,
            pageNumber: 1,
            cancellationToken: cancellationToken);
        List<NormalizationOperationApiModel> records;
        if (found.StatusCode == StatusCodes.Status204NoContent)
        {
            records = [];
        }
        else if (!found.IsSuccessStatusCode || found.Body is null)
        {
            Log("Normalization extension import", facilityId, found);
            return FacilityFormRules.ServiceMessage("Normalization", found.StatusCode, found.RawBody);
        }
        else
        {
            records = found.Body.Records ?? [];
            var metadata = found.Body.Metadata;
            if (metadata is not null && (metadata.TotalPages > 1 || metadata.TotalCount > records.Count))
                return "Too many remove-extensions operations to check for conflicts, so nothing was imported.";
        }

        if (FacilityNormalizationRules.HasExtensionConflict(records, groups, out var conflict))
            return conflict;

        var created = 0;
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
                    facilityId,
                    page.Normalization.ResourceTypes,
                    out var request,
                    out _,
                    out var buildError))
            {
                return created == 0
                    ? buildError
                    : $"Imported {created} operation(s), then the next one was not created: {buildError}";
            }

            var response = await _client.CreateOperationAsync(request!, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                Log("Normalization extension import", facilityId, response);
                var message = FacilityFormRules.ServiceMessage("Normalization", response.StatusCode, response.RawBody);
                return created == 0
                    ? message
                    : $"Imported {created} operation(s), then create failed: {message}";
            }

            created++;
        }

        return null;
    }

    public async Task<string?> DeleteAsync(string facilityId, string? operationId, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(operationId, out var id))
            return "Invalid operation id.";

        var response = await _client.DeleteFacilityOperationAsync(facilityId, id, cancellationToken);
        if (response.StatusCode == StatusCodes.Status404NotFound)
            return "That operation was not found.";

        if (!response.IsSuccessStatusCode)
        {
            Log("Normalization operation delete", facilityId, response);
            return FacilityFormRules.ServiceMessage("Normalization", response.StatusCode, response.RawBody);
        }

        return null;
    }

    public async Task<string?> SaveSequenceAsync(
        FacilityHubViewModel page,
        NormalizationSequenceInput input,
        CancellationToken cancellationToken)
    {
        var facilityId = page.FacilityId!;
        if (input.Rows is not null)
            page.Normalization.Sequence = input.Rows;
        if (!string.IsNullOrWhiteSpace(input.ResourceType))
            page.Normalization.SequenceType = input.ResourceType.Trim();

        if (page.Normalization.ResourceTypes.Count == 0)
            return page.Normalization.ResourceWarning ?? "Normalization has no resource types yet.";

        if (!FacilityNormalizationRules.TryBuildSequence(
                input.ResourceType,
                page.Normalization.ResourceTypes,
                input.Rows,
                out var sequences,
                out var clear,
                out var error))
            return error;

        var resourceType = page.Normalization.ResourceTypes.First(item =>
            string.Equals(item, input.ResourceType?.Trim(), StringComparison.OrdinalIgnoreCase));
        var listed = await _client.SearchFacilityOperationsAsync(
            facilityId,
            includeDisabled: true,
            pageSize: FacilityNormalizationRules.SequencePageSize,
            pageNumber: 1,
            cancellationToken: cancellationToken,
            resourceType: resourceType);
        if (!listed.IsSuccessStatusCode || listed.Body is null)
        {
            Log("Normalization sequence operation list", facilityId, listed);
            return FacilityFormRules.ServiceMessage("Normalization", listed.StatusCode, listed.RawBody);
        }

        if (listed.Body.Metadata is { } metadata && metadata.TotalCount > listed.Body.Records.Count)
            return "Not every operation for this resource type could be listed, so the sequence was not saved.";

        if (clear)
            return await ClearSequenceAsync(facilityId, resourceType, cancellationToken);

        var saved = await _client.CreateOperationSequencesAsync(facilityId, resourceType, sequences, cancellationToken);
        if (!saved.IsSuccessStatusCode)
        {
            Log("Normalization sequence save", facilityId, saved);
            return FacilityFormRules.ServiceMessage("Normalization", saved.StatusCode, saved.RawBody);
        }

        return null;
    }

    public async Task<string?> DeleteSequenceAsync(
        FacilityHubViewModel page,
        string? resourceType,
        CancellationToken cancellationToken)
    {
        var facilityId = page.FacilityId!;
        var canonical = page.Normalization.ResourceTypes.FirstOrDefault(item =>
            string.Equals(item, resourceType?.Trim(), StringComparison.OrdinalIgnoreCase));
        if (canonical is null)
            return "Choose a resource type for the sequence.";

        return await ClearSequenceAsync(facilityId, canonical, cancellationToken);
    }

    public async Task<string?> TestAsync(
        FacilityHubViewModel page,
        string? operationId,
        string? resourceJson,
        CancellationToken cancellationToken)
    {
        if (!page.Normalization.EditorOpen)
            return "That operation was not found.";

        page.Normalization.Editor.TestResource = resourceJson;
        if (!FacilityNormalizationRules.TryTestResource(resourceJson, out var json, out var error))
            return error;

        if (!Guid.TryParse(operationId, out var id))
            return "Invalid operation id.";

        var response = await _client.TestOperationAsync(id, page.FacilityId!, json!, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            Log("Normalization operation test", page.FacilityId, response);
            page.Normalization.TestFailed = true;
            page.Normalization.TestResult = FacilityFormRules.ServiceMessage("Normalization", response.StatusCode, response.RawBody);
            return null;
        }

        page.Normalization.TestFailed = false;
        page.Normalization.TestResult = FacilityNormalizationRules.Cap(response.Body ?? response.RawBody);
        return null;
    }

    private async Task<string?> ClearSequenceAsync(string facilityId, string resourceType, CancellationToken cancellationToken)
    {
        var response = await _client.DeleteOperationSequencesAsync(facilityId, resourceType, cancellationToken);
        if (response.StatusCode is StatusCodes.Status204NoContent or StatusCodes.Status404NotFound)
            return null;

        Log("Normalization sequence delete", facilityId, response);
        return FacilityFormRules.ServiceMessage("Normalization", response.StatusCode, response.RawBody);
    }

    private bool ApplyList(FacilityHubViewModel page, LinkApiResponse<PagedConfigModel<NormalizationOperationApiModel>> list, int pageNumber, string facilityId)
    {
        if (list.StatusCode == StatusCodes.Status204NoContent)
        {
            page.Normalization.PageNumber = pageNumber;
            page.Normalization.PageSize = FacilityNormalizationRules.PageSize;
            return true;
        }

        if (!list.IsSuccessStatusCode || list.Body is null)
        {
            Log("Normalization operation list", facilityId, list);
            page.Normalization.ReadFailed = true;
            page.NormalizationError = FacilityFormRules.ServiceMessage("Normalization", list.StatusCode, list.RawBody);
            return false;
        }

        page.Normalization.Operations = list.Body.Records.Select(MapRow).ToList();
        var metadata = list.Body.Metadata;
        page.Normalization.PageNumber = metadata?.PageNumber > 0 ? metadata.PageNumber : pageNumber;
        page.Normalization.PageSize = metadata?.PageSize > 0 ? metadata.PageSize : FacilityNormalizationRules.PageSize;
        page.Normalization.TotalCount = metadata?.TotalCount ?? page.Normalization.Operations.Count;
        page.Normalization.TotalPages = metadata?.TotalPages > 0 ? metadata.TotalPages : 1;
        return true;
    }

    private void ApplyResources(FacilityHubViewModel page, LinkApiResponse<List<NormalizationResourceApiModel>> resources, string facilityId)
    {
        if (resources.StatusCode == StatusCodes.Status204NoContent
            || (resources.IsSuccessStatusCode && resources.Body is { Count: 0 }))
        {
            page.Normalization.ResourceWarning = "Normalization has no resource types yet.";
            return;
        }

        if (!resources.IsSuccessStatusCode || resources.Body is null)
        {
            Log("Normalization resource types", facilityId, resources);
            page.Normalization.ResourceWarning = FacilityFormRules.ServiceMessage("Normalization", resources.StatusCode, resources.RawBody);
            return;
        }

        page.Normalization.ResourceTypes = resources.Body
            .Select(resource => resource.ResourceName)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private async Task<List<NormalizationOperationSequenceApiModel>> ReadSequencesAsync(
        FacilityHubViewModel page,
        string facilityId,
        Task<LinkApiResponse<List<NormalizationOperationSequenceApiModel>>> sequenceTask)
    {
        var response = await sequenceTask;
        if (response.StatusCode == StatusCodes.Status204NoContent)
            return [];

        if (!response.IsSuccessStatusCode || response.Body is null)
        {
            Log("Normalization sequences", facilityId, response);
            page.NormalizationError ??= FacilityFormRules.ServiceMessage("Normalization", response.StatusCode, response.RawBody);
            return [];
        }

        return response.Body;
    }

    private async Task ApplyEditorAsync(
        FacilityHubViewModel page,
        string facilityId,
        string? operationId,
        string? operationType,
        CancellationToken cancellationToken)
    {
        if (Guid.TryParse(operationId, out var id))
        {
            var found = await FindAsync(facilityId, id, cancellationToken);
            if (found is null)
            {
                page.NormalizationError ??= "That operation was not found.";
                return;
            }

            page.Normalization.Editor = FacilityNormalizationRules.FromOperation(found);
            page.Normalization.EditorOpen = true;
            return;
        }

        var type = FacilityNormalizationRules.CanonicalType(operationType);
        if (type is null)
            return;

        page.Normalization.Editor = FacilityNormalizationRules.Blank(type);
        page.Normalization.EditorOpen = true;
    }

    private async Task ApplySequenceAsync(
        FacilityHubViewModel page,
        string facilityId,
        string? sequenceType,
        List<NormalizationOperationSequenceApiModel> sequences,
        CancellationToken cancellationToken)
    {
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (page.Normalization.TotalCount <= page.Normalization.Operations.Count)
        {
            foreach (var row in page.Normalization.Operations)
            {
                foreach (var name in row.ResourceTypes.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
                    names.Add(name);
            }
        }
        else
        {
            var wide = await _client.SearchFacilityOperationsAsync(
                facilityId,
                includeDisabled: true,
                pageSize: FacilityNormalizationRules.SequencePageSize,
                pageNumber: 1,
                cancellationToken: cancellationToken,
                sortBy: "OperationType",
                sortOrder: "ascending");
            if (wide.IsSuccessStatusCode && wide.Body is not null)
            {
                foreach (var record in wide.Body.Records)
                {
                    foreach (var name in ResourceNames(record))
                        names.Add(name);
                }
            }
        }

        foreach (var sequence in sequences)
        {
            var name = sequence.OperationResourceType?.Resource?.ResourceName;
            if (!string.IsNullOrWhiteSpace(name))
                names.Add(name);
        }

        if (page.Normalization.ResourceTypes.Count > 0)
        {
            names.RemoveWhere(name => page.Normalization.ResourceTypes.All(catalog =>
                !string.Equals(catalog, name, StringComparison.OrdinalIgnoreCase)));
        }

        var ordered = names.OrderBy(name => name, StringComparer.OrdinalIgnoreCase).ToList();
        page.Normalization.SequenceResourceTypes = ordered;
        var selected = ordered.FirstOrDefault(name =>
            string.Equals(name, sequenceType?.Trim(), StringComparison.OrdinalIgnoreCase))
            ?? ordered.FirstOrDefault();
        page.Normalization.SequenceType = selected;
        if (selected is null)
            return;

        var listed = await _client.SearchFacilityOperationsAsync(
            facilityId,
            includeDisabled: true,
            pageSize: FacilityNormalizationRules.SequencePageSize,
            pageNumber: 1,
            cancellationToken: cancellationToken,
            resourceType: selected,
            sortBy: "OperationType",
            sortOrder: "ascending");
        if (!listed.IsSuccessStatusCode || listed.Body is null)
        {
            Log("Normalization sequence operation list", facilityId, listed);
            page.NormalizationError ??= FacilityFormRules.ServiceMessage("Normalization", listed.StatusCode, listed.RawBody);
            return;
        }

        page.Normalization.SequenceIncomplete = listed.Body.Metadata is { } metadata
            && metadata.TotalCount > listed.Body.Records.Count;

        var rows = new Dictionary<Guid, NormalizationSequenceEntryInput>();
        foreach (var record in listed.Body.Records)
        {
            rows[record.Id] = new NormalizationSequenceEntryInput
            {
                OperationId = record.Id,
                Name = record.Name,
                OperationType = record.OperationType,
                IsDisabled = record.IsDisabled
            };
        }

        foreach (var sequence in sequences)
        {
            if (!string.Equals(sequence.OperationResourceType?.Resource?.ResourceName, selected, StringComparison.OrdinalIgnoreCase))
                continue;

            var operation = sequence.OperationResourceType?.Operation;
            if (operation is null || operation.Id == Guid.Empty)
                continue;

            if (!rows.TryGetValue(operation.Id, out var row))
            {
                row = new NormalizationSequenceEntryInput
                {
                    OperationId = operation.Id,
                    Name = operation.Name,
                    OperationType = operation.OperationType,
                    IsDisabled = operation.IsDisabled
                };
                rows[operation.Id] = row;
            }

            row.Sequence = sequence.Sequence;
        }

        page.Normalization.Sequence = rows.Values
            .OrderBy(row => row.Sequence ?? int.MaxValue)
            .ThenBy(row => row.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private async Task<NormalizationOperationApiModel?> FindAsync(
        string facilityId,
        Guid operationId,
        CancellationToken cancellationToken)
    {
        var response = await _client.SearchFacilityOperationsAsync(
            facilityId,
            includeDisabled: true,
            pageSize: 1,
            pageNumber: 1,
            cancellationToken: cancellationToken,
            operationId: operationId);
        if (!response.IsSuccessStatusCode || response.Body is null)
        {
            Log("Normalization operation read", facilityId, response);
            return null;
        }

        return response.Body.Records.FirstOrDefault(record => record.Id == operationId);
    }

    private static NormalizationOperationRow MapRow(NormalizationOperationApiModel model) => new()
    {
        Id = model.Id,
        OperationType = model.OperationType,
        Name = model.Name,
        Description = model.Description,
        IsDisabled = model.IsDisabled,
        ResourceTypes = string.Join(", ", ResourceNames(model)),
        OperationJson = model.OperationJson ?? string.Empty
    };

    private static IEnumerable<string> ResourceNames(NormalizationOperationApiModel model) =>
        model.OperationResourceTypes
            .Select(row => row.Resource?.ResourceName)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Select(name => name!);

    private void Log(string operation, string? facilityId, LinkApiResponse response)
    {
        _logger.LogWarning(
            "{Operation} failed with status {StatusCode}. FacilityId={FacilityId} TraceId={TraceId}",
            operation,
            response.StatusCode,
            facilityId?.Sanitize(),
            response.TraceId);
    }
}
