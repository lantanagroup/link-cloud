using LantanaGroup.Link.Shared.Application.Models.Integration.Normalization;
using LantanaGroup.Link.Shared.Application.Models.Responses;

namespace LantanaGroup.Link.Sdk.Clients;

public interface INormalizationServiceClient
{
    Task<PagedConfigModel<NormalizationOperationApiModel>> SearchFacilityOperationsAsync(string facilityId, bool includeDisabled = true, int pageSize = 100, int pageNumber = 1, CancellationToken cancellationToken = default);
    Task CreateOperationAsync(CreateNormalizationOperationRequestApiModel requestBody, CancellationToken cancellationToken = default);
    Task DeleteFacilityOperationsAsync(string facilityId, CancellationToken cancellationToken = default);
    Task<List<NormalizationOperationSequenceApiModel>> GetOperationSequencesAsync(string facilityId, CancellationToken cancellationToken = default);
}
