using LantanaGroup.Link.Sdk.ApiClient;
using LantanaGroup.Link.Shared.Application.Models.Integration.Normalization;
using LantanaGroup.Link.Shared.Application.Models.Responses;

namespace LantanaGroup.Link.Sdk.Clients;

public interface INormalizationServiceClient
{
    Task<LinkApiResponse<PagedConfigModel<NormalizationOperationApiModel>>> SearchFacilityOperationsAsync(string facilityId, bool includeDisabled = true, int pageSize = 100, int pageNumber = 1, CancellationToken cancellationToken = default);
    Task<LinkApiResponse> CreateOperationAsync(CreateNormalizationOperationRequestApiModel requestBody, CancellationToken cancellationToken = default);
    Task<LinkApiResponse> DeleteFacilityOperationsAsync(string facilityId, CancellationToken cancellationToken = default);
    Task<LinkApiResponse<List<NormalizationOperationSequenceApiModel>>> GetOperationSequencesAsync(string facilityId, CancellationToken cancellationToken = default);
    Task<LinkApiResponse> CreateOperationSequencesAsync(string facilityId, string resourceType, List<CreateNormalizationOperationSequenceApiModel> sequences, CancellationToken cancellationToken = default);
    Task<LinkApiResponse> DeleteOperationSequencesAsync(string facilityId, string? resourceType = null, CancellationToken cancellationToken = default);
    Task<LinkApiResponse<NormalizationVendorApiModel>> CreateVendorAsync(string vendorName, CancellationToken cancellationToken = default);
    Task<LinkApiResponse<List<NormalizationVendorApiModel>>> GetVendorAsync(string vendor, CancellationToken cancellationToken = default);
    Task<LinkApiResponse<List<NormalizationVendorApiModel>>> GetAllVendorsAsync(CancellationToken cancellationToken = default);
    Task<LinkApiResponse> DeleteVendorAsync(string vendor, CancellationToken cancellationToken = default);
    Task<LinkApiResponse<NormalizationVendorPresetApiModel>> CreateVendorPresetAsync(CreateNormalizationVendorPresetRequestApiModel request, CancellationToken cancellationToken = default);
    Task<LinkApiResponse<List<NormalizationVendorPresetApiModel>>> GetVendorPresetsAsync(string vendor, string? resource = null, CancellationToken cancellationToken = default);
    Task<LinkApiResponse> DeleteVendorPresetAsync(string vendor, Guid presetId, CancellationToken cancellationToken = default);
}
