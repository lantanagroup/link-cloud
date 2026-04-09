using LantanaGroup.Link.Shared.Application.Models.Integration.DataAcquisition;
using LantanaGroup.Link.Shared.Application.Models.Integration.DataAcquisition;
using LantanaGroup.Link.Shared.Application.Models.Responses;

namespace LantanaGroup.Link.Sdk.Clients;

public interface IDataAcquisitionServiceClient
{
    Task GetFhirQueryConfigurationAsync(string facilityId, CancellationToken cancellationToken = default);
    Task<bool> HasFhirQueryConfigurationAsync(string facilityId, CancellationToken cancellationToken = default);
    Task<bool> CreateFhirQueryConfigurationAsync(CreateFhirQueryConfigurationRequestApiModel request, CancellationToken cancellationToken = default);
    Task DeleteFhirQueryConfigurationAsync(string facilityId, CancellationToken cancellationToken = default);
    Task GetQueryPlanAsync(string facilityId, string type, CancellationToken cancellationToken = default);
    Task<bool> HasQueryPlanAsync(string facilityId, string type, CancellationToken cancellationToken = default);
    Task<bool> CreateQueryPlanAsync(string facilityId, CreateQueryPlanRequestApiModel request, CancellationToken cancellationToken = default);
    Task DeleteQueryPlanAsync(string facilityId, string type, CancellationToken cancellationToken = default);
    Task SoftDeleteLogsByFacilityAsync(string facilityId, CancellationToken cancellationToken = default);
    Task<PagedConfigModel<DataAcquisitionLogApiModel>> SearchAcquisitionLogsAsync(
        string facilityId,
        string reportId,
        int pageSize = 100,
        int pageNumber = 1,
        string sortBy = "Id",
        string sortOrder = "Ascending",
        CancellationToken cancellationToken = default);
    Task<DataAcquisitionLogApiModel?> GetAcquisitionLogByIdAsync(long id, CancellationToken cancellationToken = default);
    Task<DataAcquisitionLogStatusStatisticsApiModel?> GetReportStatusCountsAsync(string reportId, CancellationToken cancellationToken = default);
    Task<DataAcquisitionReportSummaryApiModel?> GetReportSummaryAsync(string reportId, CancellationToken cancellationToken = default);
    Task<List<string>> GetAcquiredResourceIdsForReportAsync(string facilityId, string reportId, CancellationToken cancellationToken = default);
    Task<PagedConfigModel<ReferenceResourceApiModel>> GetReferenceResourcesForLogAsync(long logId, int pageSize = 100, int pageNumber = 1, CancellationToken cancellationToken = default);
}
