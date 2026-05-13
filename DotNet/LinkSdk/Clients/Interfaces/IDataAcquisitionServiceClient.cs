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
        string? searchTerm = null,
        CancellationToken cancellationToken = default);
    Task<DataAcquisitionLogApiModel?> GetAcquisitionLogByIdAsync(long id, CancellationToken cancellationToken = default);
    Task<List<string>> GetAcquisitionLogNotesAsync(long id, CancellationToken cancellationToken = default);
    Task<DataAcquisitionLogStatusStatisticsApiModel?> GetReportStatusCountsAsync(string reportId, CancellationToken cancellationToken = default);
    Task GetReportStatisticsAsync(string reportId, CancellationToken cancellationToken = default);
    Task<DataAcquisitionReportSummaryApiModel?> GetReportSummaryAsync(string reportId, CancellationToken cancellationToken = default);
    Task<List<string>> GetAcquiredResourceIdsForReportAsync(string facilityId, string reportId, CancellationToken cancellationToken = default);
    Task<PagedConfigModel<ReferenceResourceApiModel>> GetReferenceResourcesForLogAsync(long logId, int pageSize = 100, int pageNumber = 1, CancellationToken cancellationToken = default);
    Task ProcessAcquisitionLogAsync(long id, CancellationToken cancellationToken = default);
    Task ProcessAcquisitionLogsBulkAsync(List<long> ids, CancellationToken cancellationToken = default);
    Task<DataAcquisitionBulkActionResultApiModel?> CancelAcquisitionLogsBulkAsync(List<long> ids, int minAgeHours = 24, CancellationToken cancellationToken = default);
    Task ProcessAcquisitionLogsByFilterAsync(object filter, CancellationToken cancellationToken = default);
    Task<DataAcquisitionBulkActionResultApiModel?> CancelAcquisitionLogsByFilterAsync(object filter, int minAgeHours = 24, CancellationToken cancellationToken = default);
    Task DeleteAcquisitionLogAsync(long id, CancellationToken cancellationToken = default);
    Task SoftDeleteLogsByReportTrackingIdAsync(string reportTrackingId, CancellationToken cancellationToken = default);
    Task RestoreLogsByReportTrackingIdAsync(string reportTrackingId, CancellationToken cancellationToken = default);
    Task RestoreLogsByFacilityAsync(string facilityId, CancellationToken cancellationToken = default);
}
