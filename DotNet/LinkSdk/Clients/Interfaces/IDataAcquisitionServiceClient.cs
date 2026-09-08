using LantanaGroup.Link.Sdk.ApiClient;
using LantanaGroup.Link.Shared.Application.Models.Integration.DataAcquisition;
using LantanaGroup.Link.Shared.Application.Models.Responses;

namespace LantanaGroup.Link.Sdk.Clients;

public interface IDataAcquisitionServiceClient
{
    Task<LinkApiResponse> GetFhirQueryConfigurationAsync(string facilityId, CancellationToken cancellationToken = default);
    Task<LinkApiResponse> CreateFhirQueryConfigurationAsync(CreateFhirQueryConfigurationRequestApiModel request, CancellationToken cancellationToken = default);

    /// <summary>Saves/updates the FHIR server connection settings: <c>PUT /api/data/fhirQueryConfiguration</c>.</summary>
    Task<LinkApiResponse> UpdateFhirQueryConfigurationAsync(object request, CancellationToken cancellationToken = default);

    Task<LinkApiResponse> DeleteFhirQueryConfigurationAsync(string facilityId, CancellationToken cancellationToken = default);

    /// <summary>Facility-scoped FHIR connection probe: <c>GET /api/data/connectionValidation/{facilityId}/$validate</c>.</summary>
    Task<LinkApiResponse> ValidateFacilityConnectionAsync(
        string facilityId,
        string? patientId = null,
        string? patientIdentifier = null,
        string? measureId = null,
        DateTime? start = null,
        DateTime? end = null,
        CancellationToken cancellationToken = default);

    /// <summary>URL-only FHIR reachability probe before any configuration is saved. No backend route yet — returns a synthetic success.</summary>
    Task<LinkApiResponse> ValidateConnectionAsync(string? fhirServerBaseUrl = null, CancellationToken cancellationToken = default);

    Task<LinkApiResponse> GetFhirListConfigurationAsync(string facilityId, CancellationToken cancellationToken = default);

    /// <summary>Reads the FHIR patient-list configuration; pass <paramref name="includePatientName"/> to request matched patients per list.</summary>
    Task<LinkApiResponse> GetFhirListConfigurationAsync(string facilityId, bool includePatientName, CancellationToken cancellationToken = default);

    Task<LinkApiResponse> CreateFhirListConfigurationAsync(object request, CancellationToken cancellationToken = default);

    /// <summary>Saves/updates the Epic patient-list configurations: <c>PUT /api/data/fhirQueryList</c>.</summary>
    Task<LinkApiResponse> UpdateFhirListConfigurationAsync(object request, CancellationToken cancellationToken = default);

    Task<LinkApiResponse> DeleteFhirListConfigurationAsync(string facilityId, CancellationToken cancellationToken = default);
    Task<LinkApiResponse> GetQueryPlanAsync(string facilityId, string type, CancellationToken cancellationToken = default);
    Task<LinkApiResponse> CreateQueryPlanAsync(string facilityId, CreateQueryPlanRequestApiModel request, CancellationToken cancellationToken = default);

    /// <summary>Saves/updates the pre-configured per-vendor query plan: <c>PUT /api/data/{facilityId}/QueryPlan</c>.</summary>
    Task<LinkApiResponse> UpdateQueryPlanAsync(string facilityId, object request, CancellationToken cancellationToken = default);

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

    Task<LinkApiResponse<List<EncounterMappingApiModel>>> GetEncounterMappingsAsync(
        string facilityId,
        CancellationToken cancellationToken = default);

    // Organization location configuration (update / delete)
    Task<LinkApiResponse> UpdateOrganizationLocationConfigurationAsync(string facilityId, object request, CancellationToken cancellationToken = default);
    Task<LinkApiResponse> DeleteOrganizationLocationConfigurationAsync(string facilityId, CancellationToken cancellationToken = default);

    // Organization location mappings
    /// <summary>Saves the resolved organization/location mapping: <c>PUT /api/data/location-mappings/{id}</c>.</summary>
    Task<LinkApiResponse> UpdateOrganizationLocationMappingAsync(int id, object request, CancellationToken cancellationToken = default);

    // sFTP acquisition configuration (Cerner)
    Task<LinkApiResponse> GetSftpConfigurationByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<LinkApiResponse> GetOrganizationSftpConfigurationAsync(string organizationId, CancellationToken cancellationToken = default);
    Task<LinkApiResponse> CreateSftpConfigurationAsync(string organizationId, object request, CancellationToken cancellationToken = default);
    Task<LinkApiResponse> UpdateSftpConfigurationAsync(string organizationId, string configurationId, object request, CancellationToken cancellationToken = default);
    Task<LinkApiResponse> DeleteSftpConfigurationAsync(string organizationId, string configurationId, CancellationToken cancellationToken = default);
    Task<LinkApiResponse> UpdateSftpCredentialsAsync(string organizationId, object credentials, CancellationToken cancellationToken = default);
    Task<LinkApiResponse> DeleteSftpCredentialsAsync(string organizationId, CancellationToken cancellationToken = default);
    Task<LinkApiResponse> GetSftpCredentialStatusAsync(string organizationId, CancellationToken cancellationToken = default);
    Task<LinkApiResponse> TestSftpConnectionAsync(string organizationId, CancellationToken cancellationToken = default);

    /// <summary>Ad-hoc test-and-preview against unsaved sFTP details. No unscoped backend route yet — returns a synthetic success.</summary>
    Task<LinkApiResponse> TestSftpConnectionAdHocAsync(object connectionDetails, bool includeFileContent = false, CancellationToken cancellationToken = default);

    Task<LinkApiResponse> SearchSftpLogsAsync(
        string? facilityId = null,
        string? status = null,
        string? acquisitionType = null,
        string? subType = null,
        int pageNumber = 1,
        int pageSize = 10,
        string? sortBy = null,
        string? sortOrder = null,
        bool? includeDeleted = null,
        CancellationToken cancellationToken = default);
}
