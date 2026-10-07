using LantanaGroup.Link.Sdk.ApiClient;
using LantanaGroup.Link.Shared.Application.Enums;
using LantanaGroup.Link.Shared.Application.Models.Integration.DataAcquisition;
using LantanaGroup.Link.Shared.Application.Models.Responses;

namespace LantanaGroup.Link.Sdk.Clients;

public interface IDataAcquisitionServiceClient
{
    Task<LinkApiResponse> GetFhirQueryConfigurationAsync(string facilityId, CancellationToken cancellationToken = default);
    Task<LinkApiResponse> CreateFhirQueryConfigurationAsync(CreateFhirQueryConfigurationRequestApiModel request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a FHIR query configuration, including authentication when the body carries it.
    /// <c>POST /api/data/fhirQueryConfiguration</c>.
    /// </summary>
    Task<LinkApiResponse> CreateFhirQueryConfigurationAsync(object request, CancellationToken cancellationToken = default);

    /// <summary>Saves/updates the FHIR server connection settings: <c>PUT /api/data/fhirQueryConfiguration</c>.</summary>
    Task<LinkApiResponse> UpdateFhirQueryConfigurationAsync(object request, CancellationToken cancellationToken = default);

    Task<LinkApiResponse> DeleteFhirQueryConfigurationAsync(string facilityId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads the "Other" vendor generic OAuth configuration:
    /// <c>GET /api/data-acquisition/facilities/{facilityId}/fhir-authentication-configuration</c>.
    /// 404 when the facility has none. The client secret is never returned.
    /// </summary>
    Task<LinkApiResponse> GetFhirAuthenticationConfigurationAsync(string facilityId,
                                                                  CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates or replaces the "Other" vendor generic OAuth configuration:
    /// <c>PUT /api/data-acquisition/facilities/{facilityId}/fhir-authentication-configuration</c>.
    /// The request body carries the client secret, so it is not captured in the response.
    /// </summary>
    Task<LinkApiResponse> UpdateFhirAuthenticationConfigurationAsync(string facilityId,
                                                                     object request,
                                                                     CancellationToken cancellationToken = default);

    /// <summary>Facility-scoped FHIR connection probe: <c>GET /api/data/connectionValidation/{facilityId}/$validate</c>.</summary>
    Task<LinkApiResponse> ValidateFacilityConnectionAsync(
        string facilityId,
        string? patientId = null,
        string? patientIdentifier = null,
        string? measureId = null,
        DateTime? start = null,
        DateTime? end = null,
        CancellationToken cancellationToken = default);

    Task<LinkApiResponse> GetFhirListConfigurationAsync(string facilityId, bool includePatients = false, CancellationToken cancellationToken = default);
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
        CancellationToken cancellationToken = default,
        string? patientId = null);

    /// <summary>
    /// Searches acquisition logs with the filters the log API accepts.
    /// Blank filters are omitted. Status values are repeated <c>statuses</c> query parameters.
    /// </summary>
    Task<LinkApiResponse<PagedConfigModel<DataAcquisitionLogSummaryApiModel>>> SearchAcquisitionLogsAsync(
        AcquisitionLogQuery query,
        CancellationToken cancellationToken = default);

    /// <summary>Day totals and the failed total: <c>POST /api/data/acquisition-logs/counts</c>.</summary>
    Task<LinkApiResponse<AcquisitionActivityCounts>> GetActivityCountsAsync(
        AcquisitionActivityCountRequest request,
        CancellationToken cancellationToken = default);

    Task<LinkApiResponse<DataAcquisitionLogApiModel>> GetAcquisitionLogByIdAsync(long id, CancellationToken cancellationToken = default);
    Task<LinkApiResponse<List<string>>> GetAcquisitionLogNotesAsync(long id, CancellationToken cancellationToken = default);
    Task<LinkApiResponse<DataAcquisitionLogStatusStatisticsApiModel>> GetReportStatusCountsAsync(
        string reportId,
        CancellationToken cancellationToken = default,
        string? patientId = null);
    Task<LinkApiResponse> GetReportStatisticsAsync(string reportId, CancellationToken cancellationToken = default);
    Task<LinkApiResponse<DataAcquisitionReportSummaryApiModel>> GetReportSummaryAsync(string reportId, CancellationToken cancellationToken = default);
    Task<LinkApiResponse<List<string>>> GetAcquiredResourceIdsForReportAsync(string facilityId, string reportId, CancellationToken cancellationToken = default);
    Task<LinkApiResponse<List<AcquiredResourceCountByPatientApiModel>>> GetAcquiredResourceCountsByPatientAsync(string? facilityId, string reportId, CancellationToken cancellationToken = default);
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

    Task<LinkApiResponse> DeleteOrganizationLocationConfigurationsAsync(
        string facilityId,
        CancellationToken cancellationToken = default);

    Task<LinkApiResponse<List<OrganizationLocationMappingApiModel>>> GetOrganizationLocationMappingsAsync(
        string facilityId,
        CancellationToken cancellationToken = default);

    Task<LinkApiResponse<List<EncounterMappingApiModel>>> GetEncounterMappingsAsync(
        string facilityId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Paged location mappings for one facility:
    /// <c>GET /api/data/location-mappings/facility/{facilityId}/search</c>.
    /// </summary>
    Task<LinkApiResponse<PagedConfigModel<OrganizationLocationMappingApiModel>>> SearchOrganizationLocationMappingsAsync(
        string facilityId,
        string? locationId = null,
        string? locationName = null,
        string? locationAlias = null,
        string? partOfValue = null,
        bool? isOrgLocation = null,
        bool? isActive = null,
        string? sortBy = null,
        SortOrder? sortOrder = null,
        int pageSize = 10,
        int pageNumber = 1,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Paged encounter mappings for one facility:
    /// <c>GET /api/data/encounter-mappings/facilities/{facilityId}/search</c>.
    /// </summary>
    Task<LinkApiResponse<PagedConfigModel<EncounterMappingApiModel>>> SearchEncounterMappingsAsync(
        string facilityId,
        string? encounterId = null,
        string? patientId = null,
        bool? mappedToOrg = null,
        string? sortBy = null,
        SortOrder? sortOrder = null,
        int pageSize = 10,
        int pageNumber = 1,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates connectivity to a FHIR server using only its base URL, without requiring an
    /// existing facility configuration.
    /// </summary>
    Task<LinkApiResponse<FhirServerConnectionResult>> ValidateFhirServerConnectionAsync(
        string fhirServerUrl,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Tests SFTP connection details without a saved configuration, optionally previewing the patients in
    /// each Cerner extract. The request body is not captured in the response, because it carries the password.
    /// </summary>
    Task<LinkApiResponse<SftpTestConnectionResultApiModel>> TestSftpConnectionAsync(
        SftpTestConnectionRequestApiModel request,
        bool includeFileContent = false,
        CancellationToken cancellationToken = default);

    // Organization location configuration (update)
    Task<LinkApiResponse> UpdateOrganizationLocationConfigurationAsync(string facilityId, object request, CancellationToken cancellationToken = default);

    /// <summary>Updates one reporting-organization configuration: <c>PUT /api/data/location-config/{id}</c>.</summary>
    Task<LinkApiResponse> UpdateOrganizationLocationConfigurationByIdAsync(int id, object request, CancellationToken cancellationToken = default);

    /// <summary>Deletes one reporting-organization configuration: <c>DELETE /api/data/location-config/{id}</c>.</summary>
    Task<LinkApiResponse> DeleteOrganizationLocationConfigurationByIdAsync(int id, CancellationToken cancellationToken = default);

    // Organization location mappings
    /// <summary>Reads one organization/location mapping: <c>GET /api/data/location-mappings/{id}</c>.</summary>
    Task<LinkApiResponse<OrganizationLocationMappingApiModel>> GetOrganizationLocationMappingAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>Deletes one organization/location mapping: <c>DELETE /api/data/location-mappings/{id}</c>.</summary>
    Task<LinkApiResponse> DeleteOrganizationLocationMappingAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>Saves the resolved organization/location mapping: <c>PUT /api/data/location-mappings/{id}</c>.</summary>
    Task<LinkApiResponse> UpdateOrganizationLocationMappingAsync(int id, object request, CancellationToken cancellationToken = default);

    // sFTP acquisition configuration (Cerner)
    Task<LinkApiResponse> GetSftpConfigurationByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<LinkApiResponse> GetOrganizationSftpConfigurationAsync(string organizationId, CancellationToken cancellationToken = default);
    Task<LinkApiResponse> CreateSftpConfigurationAsync(string organizationId, object request, CancellationToken cancellationToken = default);
    Task<LinkApiResponse> UpdateSftpConfigurationAsync(string organizationId, string configurationId, object request, CancellationToken cancellationToken = default);
    Task<LinkApiResponse> DeleteSftpConfigurationAsync(string organizationId, string configurationId, CancellationToken cancellationToken = default);
    /// <summary>Saves the write-only sFTP credentials. The request body is not captured in the response, because it carries the password.</summary>
    Task<LinkApiResponse> UpdateSftpCredentialsAsync(string organizationId, object credentials, CancellationToken cancellationToken = default);
    Task<LinkApiResponse> DeleteSftpCredentialsAsync(string organizationId, CancellationToken cancellationToken = default);
    Task<LinkApiResponse> GetSftpCredentialStatusAsync(string organizationId, CancellationToken cancellationToken = default);
    Task<LinkApiResponse> TestSavedSftpConnectionAsync(string organizationId, CancellationToken cancellationToken = default);

    /// <summary>Reads one sFTP acquisition log: <c>GET /api/data/sftp-logs/{logId}</c>.</summary>
    Task<LinkApiResponse<SftpLogApiModel>> GetSftpLogAsync(string logId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Resets an sFTP log so it can be retried: <c>POST /api/data/sftp-logs/{logId}/reset</c>.
    /// Only ConfigurationRequired and MaxRetriesReached logs reset.
    /// </summary>
    Task<LinkApiResponse> ResetSftpLogAsync(string logId, CancellationToken cancellationToken = default);

    /// <summary>Typed sFTP log search: <c>GET /api/data/sftp-logs</c>.</summary>
    Task<LinkApiResponse<PagedConfigModel<SftpLogApiModel>>> SearchSftpAcquisitionLogsAsync(
        string? facilityId = null,
        string? status = null,
        string? acquisitionType = null,
        string? subType = null,
        int pageNumber = 1,
        int pageSize = 10,
        string? sortBy = null,
        string? sortOrder = null,
        CancellationToken cancellationToken = default);

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

    /// <summary>Records an sFTP acquisition log: <c>POST /api/data/sftp-logs</c>.</summary>
    Task<LinkApiResponse> CreateSftpLogAsync(object request, CancellationToken cancellationToken = default);
}
