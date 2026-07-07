using LantanaGroup.Link.Sdk.ApiClient;
using LantanaGroup.Link.Shared.Application.Models.Integration.DataAcquisition;
using LantanaGroup.Link.Shared.Application.Models.Responses;

namespace LantanaGroup.Link.Sdk.Clients;

public interface IDataAcquisitionServiceClient
{
    Task<LinkApiResponse> GetFhirQueryConfigurationAsync(string facilityId, CancellationToken cancellationToken = default);
    Task<LinkApiResponse> CreateFhirQueryConfigurationAsync(CreateFhirQueryConfigurationRequestApiModel request, CancellationToken cancellationToken = default);
    Task<LinkApiResponse> DeleteFhirQueryConfigurationAsync(string facilityId, CancellationToken cancellationToken = default);
    Task<LinkApiResponse> GetQueryPlanAsync(string facilityId, string type, CancellationToken cancellationToken = default);
    Task<LinkApiResponse> CreateQueryPlanAsync(string facilityId, CreateQueryPlanRequestApiModel request, CancellationToken cancellationToken = default);
    Task<LinkApiResponse> DeleteQueryPlanAsync(string facilityId, string type, CancellationToken cancellationToken = default);
    Task<LinkApiResponse> SoftDeleteLogsByFacilityAsync(string facilityId, CancellationToken cancellationToken = default);
    Task<LinkApiResponse<PagedConfigModel<DataAcquisitionLogApiModel>>> SearchAcquisitionLogsAsync(
        string facilityId,
        string reportId,
        int pageSize = 100,
        int pageNumber = 1,
        string sortBy = "Id",
        string sortOrder = "Ascending",
        string? searchTerm = null,
        CancellationToken cancellationToken = default);
    Task<LinkApiResponse<DataAcquisitionLogApiModel>> GetAcquisitionLogByIdAsync(long id, CancellationToken cancellationToken = default);
    Task<LinkApiResponse<List<string>>> GetAcquisitionLogNotesAsync(long id, CancellationToken cancellationToken = default);
    Task<LinkApiResponse<DataAcquisitionLogStatusStatisticsApiModel>> GetReportStatusCountsAsync(string reportId, CancellationToken cancellationToken = default);
    Task<LinkApiResponse> GetReportStatisticsAsync(string reportId, CancellationToken cancellationToken = default);
    Task<LinkApiResponse<DataAcquisitionReportSummaryApiModel>> GetReportSummaryAsync(string reportId, CancellationToken cancellationToken = default);
    Task<LinkApiResponse<List<string>>> GetAcquiredResourceIdsForReportAsync(string facilityId, string reportId, CancellationToken cancellationToken = default);
    Task<LinkApiResponse<PagedConfigModel<ReferenceResourceApiModel>>> GetReferenceResourcesForLogAsync(long logId, int pageSize = 100, int pageNumber = 1, CancellationToken cancellationToken = default);
    Task<LinkApiResponse> ProcessAcquisitionLogAsync(long id, CancellationToken cancellationToken = default);
    Task<LinkApiResponse> ProcessAcquisitionLogsBulkAsync(List<long> ids, CancellationToken cancellationToken = default);
    Task<LinkApiResponse<DataAcquisitionBulkActionResultApiModel>> CancelAcquisitionLogsBulkAsync(List<long> ids, int minAgeHours = 24, CancellationToken cancellationToken = default);
    Task<LinkApiResponse> ProcessAcquisitionLogsByFilterAsync(object filter, CancellationToken cancellationToken = default);
    Task<LinkApiResponse<DataAcquisitionBulkActionResultApiModel>> CancelAcquisitionLogsByFilterAsync(object filter, int minAgeHours = 24, CancellationToken cancellationToken = default);
    Task<LinkApiResponse> DeleteAcquisitionLogAsync(long id, CancellationToken cancellationToken = default);
    Task<LinkApiResponse> SoftDeleteLogsByReportTrackingIdAsync(string reportTrackingId, CancellationToken cancellationToken = default);
    Task<LinkApiResponse> RestoreLogsByReportTrackingIdAsync(string reportTrackingId, CancellationToken cancellationToken = default);
    Task<LinkApiResponse> RestoreLogsByFacilityAsync(string facilityId, CancellationToken cancellationToken = default);

    Task<LinkApiResponse<List<OrganizationLocationConfigurationApiModel>>> GetOrganizationLocationConfigurationsAsync(
        string facilityId,
        CancellationToken cancellationToken = default);

    Task<LinkApiResponse<OrganizationLocationConfigurationApiModel>> CreateOrganizationLocationConfigurationAsync(
        string facilityId,
        CreateOrganizationLocationConfigurationApiModel request,
        CancellationToken cancellationToken = default);

    Task<LinkApiResponse<List<OrganizationLocationMappingApiModel>>> GetOrganizationLocationMappingsAsync(
        string facilityId,
        CancellationToken cancellationToken = default);
}
