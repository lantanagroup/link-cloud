using LantanaGroup.Link.Sdk.ApiClient;
using LantanaGroup.Link.Shared.Application.Models.Integration.Normalization;
using LantanaGroup.Link.Shared.Application.Models.Responses;

namespace LantanaGroup.Link.Sdk.Clients;

public interface INormalizationServiceClient
{
    Task<LinkApiResponse<PagedConfigModel<NormalizationOperationApiModel>>> SearchFacilityOperationsAsync(string facilityId, bool includeDisabled = true, int pageSize = 100, int pageNumber = 1, CancellationToken cancellationToken = default);
    Task<LinkApiResponse<PagedConfigModel<NormalizationOperationApiModel>>> SearchVendorVersionOperationsAsync(Guid vendorVersionId, bool includeDisabled = true, int pageSize = 100, int pageNumber = 1, CancellationToken cancellationToken = default);
    Task<LinkApiResponse> CreateOperationAsync(CreateNormalizationOperationRequestApiModel requestBody, CancellationToken cancellationToken = default);
    Task<LinkApiResponse> DeleteFacilityOperationsAsync(string facilityId, CancellationToken cancellationToken = default);
    Task<LinkApiResponse> DeleteVendorVersionOperationsAsync(Guid vendorVersionId, CancellationToken cancellationToken = default);
    Task<LinkApiResponse<List<NormalizationOperationSequenceApiModel>>> GetOperationSequencesAsync(string facilityId, CancellationToken cancellationToken = default);
    Task<LinkApiResponse> CreateOperationSequencesAsync(string facilityId, string resourceType, List<CreateNormalizationOperationSequenceApiModel> sequences, CancellationToken cancellationToken = default);
    Task<LinkApiResponse> DeleteOperationSequencesAsync(string facilityId, string? resourceType = null, CancellationToken cancellationToken = default);
    Task<LinkApiResponse<NormalizationVendorVersionOperationPresetApiModel>> CreateVendorVersionOperationPresetAsync(CreateNormalizationVendorVersionOperationPresetRequestApiModel request, CancellationToken cancellationToken = default);
    Task<LinkApiResponse<List<NormalizationVendorVersionOperationPresetApiModel>>> GetVendorVersionOperationPresetsAsync(Guid? vendorVersionId = null, string? resource = null, CancellationToken cancellationToken = default);
    Task<LinkApiResponse> DeleteVendorVersionOperationPresetAsync(Guid vendorVersionId, Guid presetId, CancellationToken cancellationToken = default);
    Task<LinkApiResponse<FacilityLocationApiModel>> GetFacilityLocationAsync(string facilityId, string locationId, CancellationToken cancellationToken = default);
    Task<LinkApiResponse<FacilityLocationApiModel>> CreateFacilityLocationAsync(string facilityId, CreateFacilityLocationRequestApiModel request, CancellationToken cancellationToken = default);
    Task<LinkApiResponse<PagedConfigModel<FacilityLocationLocalCodeMappingApiModel>>> SearchFacilityLocationLocalCodeMappingsAsync(SearchFacilityLocationLocalCodeMappingsRequestApiModel request, CancellationToken cancellationToken = default);
    Task<LinkApiResponse<FacilityLocationLocalCodeMappingApiModel>> GetFacilityLocationLocalCodeMappingAsync(string mappingId, CancellationToken cancellationToken = default);
    Task<LinkApiResponse<FacilityLocationLocalCodeMappingApiModel>> CreateFacilityLocationLocalCodeMappingAsync(string facilityId, CreateFacilityLocationLocalCodeMappingRequestApiModel request, CancellationToken cancellationToken = default);
    Task<LinkApiResponse<FacilityLocationLocalCodeMappingApiModel>> UpdateFacilityLocationLocalCodeMappingAsync(string mappingId, UpdateFacilityLocationLocalCodeMappingRequestApiModel request, CancellationToken cancellationToken = default);
    Task<LinkApiResponse> DeleteFacilityLocationLocalCodeMappingAsync(string mappingId, CancellationToken cancellationToken = default);
    Task<LinkApiResponse> DeleteFacilityLocationLocalCodeMappingsForFacilityAsync(string facilityId, CancellationToken cancellationToken = default);
}
