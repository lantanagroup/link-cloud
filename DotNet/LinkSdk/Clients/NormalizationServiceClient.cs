﻿using Flurl.Http;
using Flurl.Http.Configuration;
using Flurl.Http.Content;
using LantanaGroup.Link.Sdk.ApiClient;
using System.Net.Http;
using System.Text;
using LantanaGroup.Link.Shared.Application.Extensions.Security;
using LantanaGroup.Link.Shared.Application.Interfaces.Services.Security.Token;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using LantanaGroup.Link.Shared.Application.Models.Integration.Normalization;
using LantanaGroup.Link.Shared.Application.Models.Responses;
using Microsoft.Extensions.Options;

namespace LantanaGroup.Link.Sdk.Clients;

public class NormalizationServiceClient : LinkApiClientBase, INormalizationServiceClient
{
    public NormalizationServiceClient(
        IOptions<ServiceRegistry> serviceRegistry,
        IOptions<BackendAuthenticationServiceExtension.LinkBearerServiceOptions> bearerOptions,
        IOptions<LinkTokenServiceSettings> tokenServiceSettings,
        ICreateSystemToken tokenService)
        : base(
            serviceRegistry.Value.NormalizationServiceApiUrl
                ?? throw new InvalidOperationException("Normalization service URL is not configured in ServiceRegistry."),
            bearerOptions, tokenServiceSettings, tokenService)
    { }

    /// <summary>Sends this client's calls through Admin.BFF with the same relative paths.</summary>
    public NormalizationServiceClient(
        AdminBffRoute route,
        IOptions<BackendAuthenticationServiceExtension.LinkBearerServiceOptions> bearerOptions,
        IOptions<LinkTokenServiceSettings> tokenServiceSettings,
        ICreateSystemToken tokenService)
        : base(route, bearerOptions, tokenServiceSettings, tokenService)
    { }

    public Task<LinkApiResponse<PagedConfigModel<NormalizationOperationApiModel>>> SearchFacilityOperationsAsync(
        string facilityId,
        bool includeDisabled = true,
        int pageSize = 100,
        int pageNumber = 1,
        CancellationToken cancellationToken = default,
        string? resourceType = null,
        Guid? operationId = null,
        string? sortBy = null,
        string? sortOrder = null)
    {
        var request = Request($"normalization/Operations/facility/{facilityId}")
            .SetQueryParam("includeDisabled", includeDisabled)
            .SetQueryParam("pageSize", pageSize)
            .SetQueryParam("pageNumber", pageNumber);
        if (!string.IsNullOrWhiteSpace(resourceType))
            request = request.SetQueryParam("resourceType", resourceType);
        if (operationId.HasValue)
            request = request.SetQueryParam("operationId", operationId.Value);
        if (!string.IsNullOrWhiteSpace(sortBy))
            request = request.SetQueryParam("sortBy", sortBy);
        if (!string.IsNullOrWhiteSpace(sortOrder))
            request = request.SetQueryParam("sortOrder", sortOrder);

        return SendAsync<PagedConfigModel<NormalizationOperationApiModel>>(() => request.GetAsync(cancellationToken: cancellationToken));
    }

    public Task<LinkApiResponse<PagedConfigModel<NormalizationOperationApiModel>>> SearchVendorVersionOperationsAsync(
        Guid vendorVersionId,
        bool includeDisabled = true,
        int pageSize = 100,
        int pageNumber = 1,
        CancellationToken cancellationToken = default) =>
        SendAsync<PagedConfigModel<NormalizationOperationApiModel>>(() => Request($"normalization/Operations/vendor-version/{vendorVersionId}")
            .SetQueryParam("includeDisabled", includeDisabled)
            .SetQueryParam("pageSize", pageSize)
            .SetQueryParam("pageNumber", pageNumber)
            .GetAsync(cancellationToken: cancellationToken));

    public Task<LinkApiResponse> CreateOperationAsync(
        CreateNormalizationOperationRequestApiModel requestBody,
        CancellationToken cancellationToken = default) =>
        SendAsync(() => Request("normalization/Operations")
            .PostJsonAsync(requestBody, cancellationToken: cancellationToken));

    public Task<LinkApiResponse> UpdateOperationAsync(
        UpdateNormalizationOperationRequestApiModel requestBody,
        CancellationToken cancellationToken = default) =>
        SendAsync(() => Request("normalization/Operations")
            .PutJsonAsync(requestBody, cancellationToken: cancellationToken));

    public Task<LinkApiResponse<List<NormalizationResourceApiModel>>> GetResourcesAsync(
        CancellationToken cancellationToken = default) =>
        SendAsync<List<NormalizationResourceApiModel>>(() => Request("normalization/resource/resources")
            .GetAsync(cancellationToken: cancellationToken));

    public Task<LinkApiResponse<string>> TestOperationAsync(
        Guid operationId,
        string facilityId,
        string resourceJson,
        CancellationToken cancellationToken = default) =>
        SendStringAsync(() => Request($"normalization/operations/{operationId}/test")
            .SetQueryParam("facilityId", facilityId)
            .PostAsync(new StringContent(resourceJson, Encoding.UTF8, "application/json"), cancellationToken: cancellationToken));

    public Task<LinkApiResponse> DeleteFacilityOperationsAsync(
        string facilityId,
        CancellationToken cancellationToken = default) =>
        SendAsync(() => Request($"normalization/operations/facility/{facilityId}")
            .DeleteAsync(cancellationToken: cancellationToken));

    public Task<LinkApiResponse> DeleteFacilityOperationAsync(
        string facilityId,
        Guid operationId,
        CancellationToken cancellationToken = default) =>
        SendAsync(() => Request($"normalization/operations/facility/{facilityId}")
            .SetQueryParam("operationId", operationId)
            .DeleteAsync(cancellationToken: cancellationToken));

    public Task<LinkApiResponse> DeleteVendorVersionOperationsAsync(
        Guid vendorVersionId,
        CancellationToken cancellationToken = default) =>
        SendAsync(() => Request($"normalization/operations/vendor-version/{vendorVersionId}")
            .DeleteAsync(cancellationToken: cancellationToken));

    public Task<LinkApiResponse> DeleteVendorVersionOperationAsync(
        Guid vendorVersionId,
        Guid operationId,
        CancellationToken cancellationToken = default) =>
        SendAsync(() => Request($"normalization/operations/vendor-version/{vendorVersionId}")
            .SetQueryParam("operationId", operationId)
            .DeleteAsync(cancellationToken: cancellationToken));

    public Task<LinkApiResponse<List<NormalizationOperationSequenceApiModel>>> GetOperationSequencesAsync(
        string facilityId,
        CancellationToken cancellationToken = default) =>
        SendAsync<List<NormalizationOperationSequenceApiModel>>(() => Request("normalization/OperationSequence")
            .SetQueryParam("facilityId", facilityId)
            .GetAsync(cancellationToken: cancellationToken));

    public Task<LinkApiResponse> CreateOperationSequencesAsync(
        string facilityId,
        string resourceType,
        List<CreateNormalizationOperationSequenceApiModel> sequences,
        CancellationToken cancellationToken = default) =>
        SendAsync(() => Request("normalization/OperationSequence")
            .SetQueryParam("facilityId", facilityId)
            .SetQueryParam("resourceType", resourceType)
            .PostJsonAsync(sequences, cancellationToken: cancellationToken));

    public Task<LinkApiResponse> DeleteOperationSequencesAsync(
        string facilityId,
        string? resourceType = null,
        CancellationToken cancellationToken = default)
    {
        var req = Request("normalization/OperationSequence")
            .SetQueryParam("facilityId", facilityId);
        if (!string.IsNullOrWhiteSpace(resourceType)) req = req.SetQueryParam("resourceType", resourceType);
        return SendAsync(() => req.DeleteAsync(cancellationToken: cancellationToken));
    }

    public Task<LinkApiResponse<NormalizationVendorVersionOperationPresetApiModel>> CreateVendorVersionOperationPresetAsync(
        CreateNormalizationVendorVersionOperationPresetRequestApiModel request,
        CancellationToken cancellationToken = default) =>
        SendAsync<NormalizationVendorVersionOperationPresetApiModel>(() => Request("normalization/vendor-version-operation-presets")
            .PostJsonAsync(request, cancellationToken: cancellationToken));

    public Task<LinkApiResponse<List<NormalizationVendorVersionOperationPresetApiModel>>> GetVendorVersionOperationPresetsAsync(
        Guid? vendorVersionId = null,
        string? resource = null,
        CancellationToken cancellationToken = default)
    {
        var req = Request("normalization/vendor-version-operation-presets");
        if (vendorVersionId.HasValue) req = req.SetQueryParam("vendorVersionId", vendorVersionId.Value);
        if (!string.IsNullOrWhiteSpace(resource)) req = req.SetQueryParam("resource", resource);
        return SendAsync<List<NormalizationVendorVersionOperationPresetApiModel>>(() => req.GetAsync(cancellationToken: cancellationToken));
    }

    public Task<LinkApiResponse> DeleteVendorVersionOperationPresetAsync(
        Guid vendorVersionId,
        Guid presetId,
        CancellationToken cancellationToken = default) =>
        SendAsync(() => Request($"normalization/vendor-version-operation-presets/{vendorVersionId}/{presetId}")
            .DeleteAsync(cancellationToken: cancellationToken));

    public Task<LinkApiResponse<PagedConfigModel<FacilityLocationTreeApiModel>>> GetFacilityLocationsAsync(
        string facilityId,
        CancellationToken cancellationToken = default) =>
        SendAsync<PagedConfigModel<FacilityLocationTreeApiModel>>(() => Request($"normalization/facility-locations/facilities/{facilityId}/locations")
            .GetAsync(cancellationToken: cancellationToken));

    public Task<LinkApiResponse<FacilityLocationApiModel>> GetFacilityLocationAsync(
        string facilityId,
        string locationId,
        CancellationToken cancellationToken = default) =>
        SendAsync<FacilityLocationApiModel>(() => Request($"normalization/facility-locations/facilities/{facilityId}/locations/{locationId}")
            .GetAsync(cancellationToken: cancellationToken));

    public Task<LinkApiResponse<FacilityLocationApiModel>> CreateFacilityLocationAsync(
        string facilityId,
        CreateFacilityLocationRequestApiModel request,
        CancellationToken cancellationToken = default) =>
        SendAsync<FacilityLocationApiModel>(() => Request($"normalization/facility-locations/facilities/{facilityId}/locations")
            .PostJsonAsync(request, cancellationToken: cancellationToken));

    public Task<LinkApiResponse<PagedConfigModel<FacilityLocationLocalCodeMappingApiModel>>> SearchFacilityLocationLocalCodeMappingsAsync(
        SearchFacilityLocationLocalCodeMappingsRequestApiModel request,
        CancellationToken cancellationToken = default)
    {
        var apiRequest = Request("normalization/hsloc-mappings/search");
        if (!string.IsNullOrWhiteSpace(request.Id)) apiRequest = apiRequest.SetQueryParam("id", request.Id);
        if (!string.IsNullOrWhiteSpace(request.FacilityId)) apiRequest = apiRequest.SetQueryParam("facilityId", request.FacilityId);
        if (!string.IsNullOrWhiteSpace(request.LocationId)) apiRequest = apiRequest.SetQueryParam("locationId", request.LocationId);
        if (!string.IsNullOrWhiteSpace(request.LocalCodeSystem)) apiRequest = apiRequest.SetQueryParam("localCodeSystem", request.LocalCodeSystem);
        if (!string.IsNullOrWhiteSpace(request.LocalCode)) apiRequest = apiRequest.SetQueryParam("localCode", request.LocalCode);
        if (request.HSLOCId.HasValue) apiRequest = apiRequest.SetQueryParam("HSLOCId", request.HSLOCId.Value);
        if (request.Unmapped.HasValue) apiRequest = apiRequest.SetQueryParam("unmapped", request.Unmapped.Value);
        if (request.PageSize.HasValue) apiRequest = apiRequest.SetQueryParam("pageSize", request.PageSize.Value);
        if (request.PageNumber.HasValue) apiRequest = apiRequest.SetQueryParam("pageNumber", request.PageNumber.Value);

        return SendAsync<PagedConfigModel<FacilityLocationLocalCodeMappingApiModel>>(() => apiRequest
            .GetAsync(cancellationToken: cancellationToken));
    }

    public Task<LinkApiResponse<FacilityLocationLocalCodeMappingApiModel>> GetFacilityLocationLocalCodeMappingAsync(
        string mappingId,
        CancellationToken cancellationToken = default) =>
        SendAsync<FacilityLocationLocalCodeMappingApiModel>(() => Request($"normalization/hsloc-mappings/{mappingId}")
            .GetAsync(cancellationToken: cancellationToken));

    public Task<LinkApiResponse<FacilityLocationLocalCodeMappingApiModel>> CreateFacilityLocationLocalCodeMappingAsync(
        string facilityId,
        CreateFacilityLocationLocalCodeMappingRequestApiModel request,
        CancellationToken cancellationToken = default) =>
        SendAsync<FacilityLocationLocalCodeMappingApiModel>(() => Request($"normalization/hsloc-mappings/facilities/{facilityId}")
            .PostJsonAsync(request, cancellationToken: cancellationToken));

    public Task<LinkApiResponse<FacilityLocationLocalCodeMappingApiModel>> UpdateFacilityLocationLocalCodeMappingAsync(
        string mappingId,
        UpdateFacilityLocationLocalCodeMappingRequestApiModel request,
        CancellationToken cancellationToken = default) =>
        SendAsync<FacilityLocationLocalCodeMappingApiModel>(() => Request($"normalization/hsloc-mappings/{mappingId}")
            .PutJsonAsync(request, cancellationToken: cancellationToken));

    public Task<LinkApiResponse> DeleteFacilityLocationLocalCodeMappingAsync(
        string mappingId,
        CancellationToken cancellationToken = default) =>
        SendAsync(() => Request($"normalization/hsloc-mappings/{mappingId}")
            .DeleteAsync(cancellationToken: cancellationToken));

    public Task<LinkApiResponse> DeleteFacilityLocationLocalCodeMappingsForFacilityAsync(
        string facilityId,
        CancellationToken cancellationToken = default) =>
        SendAsync(() => Request($"normalization/hsloc-mappings/facilities/{facilityId}")
            .DeleteAsync(cancellationToken: cancellationToken));

    public Task<LinkApiResponse<PagedConfigModel<NormalizationOperationApiModel>>> SearchOperationsAsync(
        string? facilityId = null,
        string? operationType = null,
        string? resourceType = null,
        Guid? operationId = null,
        bool includeDisabled = false,
        Guid? vendorVersionId = null,
        string? sortBy = null,
        string? sortOrder = null,
        int pageSize = 10,
        int pageNumber = 1,
        CancellationToken cancellationToken = default)
    {
        var request = Request("normalization/Operations")
            .SetQueryParam("includeDisabled", includeDisabled)
            .SetQueryParam("pageSize", pageSize)
            .SetQueryParam("pageNumber", pageNumber);
        if (!string.IsNullOrWhiteSpace(facilityId))
            request = request.SetQueryParam("facilityId", facilityId);
        if (!string.IsNullOrWhiteSpace(operationType))
            request = request.SetQueryParam("operationType", operationType);
        if (!string.IsNullOrWhiteSpace(resourceType))
            request = request.SetQueryParam("resourceType", resourceType);
        if (operationId.HasValue)
            request = request.SetQueryParam("operationId", operationId.Value);
        if (vendorVersionId.HasValue)
            request = request.SetQueryParam("vendorVersionId", vendorVersionId.Value);
        if (!string.IsNullOrWhiteSpace(sortBy))
            request = request.SetQueryParam("sortBy", sortBy);
        if (!string.IsNullOrWhiteSpace(sortOrder))
            request = request.SetQueryParam("sortOrder", sortOrder);

        return SendAsync<PagedConfigModel<NormalizationOperationApiModel>>(() =>
            request.GetAsync(cancellationToken: cancellationToken));
    }

    public Task<LinkApiResponse<List<HslocCodeApiModel>>> GetHslocCodesAsync(
        bool includeInactive = false,
        CancellationToken cancellationToken = default) =>
        SendAsync<List<HslocCodeApiModel>>(() => Request("normalization/HSLOC")
            .SetQueryParam("includeInactive", includeInactive)
            .GetAsync(cancellationToken: cancellationToken));

    public Task<LinkApiResponse> UpdateHslocCodesAsync(
        string oldVersion,
        string newVersion,
        Stream csvFile,
        string fileName = "hsloc.csv",
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(csvFile);
        var request = Request("normalization/HSLOC");
        var content = new CapturedMultipartContent(new FlurlHttpSettings());
        content.AddString("OldVersion", oldVersion ?? string.Empty);
        content.AddString("NewVersion", newVersion ?? string.Empty);
        content.AddFile("CsvFile", csvFile, string.IsNullOrWhiteSpace(fileName) ? "hsloc.csv" : fileName, "text/csv");
        return SendAsync(() => request.SendAsync(HttpMethod.Put, content, cancellationToken: cancellationToken));
    }

    public Task<LinkApiResponse> DeleteAllHslocCodesAsync(
        CancellationToken cancellationToken = default) =>
        SendAsync(() => Request("normalization/HSLOC")
            .DeleteAsync(cancellationToken: cancellationToken));

    public Task<LinkApiResponse> DeleteHslocCodeAsync(
        Guid id,
        CancellationToken cancellationToken = default) =>
        SendAsync(() => Request($"normalization/HSLOC/{id}")
            .DeleteAsync(cancellationToken: cancellationToken));
}