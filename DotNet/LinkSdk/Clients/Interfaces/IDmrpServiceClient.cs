using LantanaGroup.Link.Sdk.ApiClient;
using LantanaGroup.Link.Shared.Application.Models.Integration.DMRP;
using LantanaGroup.Link.Shared.Application.Models.Responses;

namespace LantanaGroup.Link.Sdk.Clients;

public interface IDmrpServiceClient
{
    Task<LinkApiResponse<MeasureMappingModel>> CreateMeasureMappingAsync(MeasureMappingModel request, CancellationToken cancellationToken = default);
    Task<LinkApiResponse<MeasureMappingModel>> GetMeasureMappingAsync(string id, CancellationToken cancellationToken = default);
    Task<LinkApiResponse<MeasureMappingModel>> UpdateMeasureMappingAsync(string id, MeasureMappingModel request, CancellationToken cancellationToken = default);
    Task<LinkApiResponse> DeleteMeasureMappingAsync(string id, CancellationToken cancellationToken = default);
    Task<LinkApiResponse<PagedConfigModel<MeasureMappingModel>>> SearchMeasureMappingsAsync(int pageSize = 10, int pageNumber = 1, CancellationToken cancellationToken = default);

    Task<LinkApiResponse<FacilityReportingPlanModel>> CreateFacilityReportingPlanAsync(FacilityReportingPlanModel request, CancellationToken cancellationToken = default);
    Task<LinkApiResponse<FacilityReportingPlanModel>> GetFacilityReportingPlanAsync(string id, CancellationToken cancellationToken = default);
    Task<LinkApiResponse<FacilityReportingPlanModel>> UpdateFacilityReportingPlanAsync(string id, FacilityReportingPlanModel request, CancellationToken cancellationToken = default);
    Task<LinkApiResponse> DeleteFacilityReportingPlanAsync(string id, CancellationToken cancellationToken = default);
    Task<LinkApiResponse<PagedConfigModel<FacilityReportingPlanModel>>> SearchFacilityReportingPlansAsync(int pageSize = 10, int pageNumber = 1, CancellationToken cancellationToken = default);
}
