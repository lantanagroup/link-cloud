﻿using Flurl.Http;
using LantanaGroup.Link.Sdk.ApiClient;
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

    public Task<LinkApiResponse<PagedConfigModel<NormalizationOperationApiModel>>> SearchFacilityOperationsAsync(
        string facilityId,
        bool includeDisabled = true,
        int pageSize = 100,
        int pageNumber = 1,
        CancellationToken cancellationToken = default) =>
        SendAsync<PagedConfigModel<NormalizationOperationApiModel>>(() => Request($"normalization/Operations/facility/{facilityId}")
            .SetQueryParam("includeDisabled", includeDisabled)
            .SetQueryParam("pageSize", pageSize)
            .SetQueryParam("pageNumber", pageNumber)
            .GetAsync(cancellationToken: cancellationToken));

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

    public Task<LinkApiResponse> DeleteFacilityOperationsAsync(
        string facilityId,
        CancellationToken cancellationToken = default) =>
        SendAsync(() => Request($"normalization/operations/facility/{facilityId}")
            .DeleteAsync(cancellationToken: cancellationToken));

    public Task<LinkApiResponse> DeleteVendorVersionOperationsAsync(
        Guid vendorVersionId,
        CancellationToken cancellationToken = default) =>
        SendAsync(() => Request($"normalization/operations/vendor-version/{vendorVersionId}")
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
}