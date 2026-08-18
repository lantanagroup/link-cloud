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

    Task<LinkApiResponse<FacilityReportingPlanModel>> CreateFacilityReportingPlanAsync(FacilityReportingPlanRequest request, CancellationToken cancellationToken = default);
    Task<LinkApiResponse<FacilityReportingPlanModel>> GetFacilityReportingPlanAsync(string id, CancellationToken cancellationToken = default);
    Task<LinkApiResponse<FacilityReportingPlanModel>> UpdateFacilityReportingPlanAsync(string id, FacilityReportingPlanUpdateRequest request, CancellationToken cancellationToken = default);
    Task<LinkApiResponse> DeleteFacilityReportingPlanAsync(string id, CancellationToken cancellationToken = default);
    Task<LinkApiResponse<PagedConfigModel<FacilityReportingPlanModel>>> SearchFacilityReportingPlansAsync(int pageSize = 10, int pageNumber = 1, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a facility's reporting plans, optionally narrowed to a reporting period or to whether the
    /// facility was reporting.
    /// </summary>
    Task<LinkApiResponse<List<FacilityReportingPlanModel>>> GetFacilityReportingPlansForFacilityAsync(string facilityId,
        int? month = null, int? year = null, bool? isReporting = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Searches reporting plans across facilities, measure mappings and reporting periods.
    /// </summary>
    Task<LinkApiResponse<PagedConfigModel<FacilityReportingPlanModel>>> SearchFacilityReportingPlansAsync(
        string? facilityId, string? measureMappingId = null, int? month = null, int? year = null,
        bool? isReporting = null, int pageSize = 10, int pageNumber = 1, CancellationToken cancellationToken = default);

    /// <summary>Deletes every reporting plan.</summary>
    Task<LinkApiResponse> DeleteFacilityReportingPlansAsync(CancellationToken cancellationToken = default);

    /// <summary>Deletes every reporting plan belonging to a facility.</summary>
    Task<LinkApiResponse> DeleteFacilityReportingPlansForFacilityAsync(string facilityId, CancellationToken cancellationToken = default);
}
