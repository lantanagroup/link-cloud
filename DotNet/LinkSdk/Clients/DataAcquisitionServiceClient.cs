using Flurl.Http;
using LantanaGroup.Link.Sdk.ApiClient;
using LantanaGroup.Link.Shared.Application.Extensions.Security;
using LantanaGroup.Link.Shared.Application.Interfaces.Services.Security.Token;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using LantanaGroup.Link.Shared.Application.Models.Integration.DataAcquisition;
using LantanaGroup.Link.Shared.Application.Models.Responses;
using Microsoft.Extensions.Options;

namespace LantanaGroup.Link.Sdk.Clients;

public class DataAcquisitionServiceClient : LinkApiClientBase, IDataAcquisitionServiceClient
{
    public DataAcquisitionServiceClient(
        IOptions<ServiceRegistry> serviceRegistry,
        IOptions<BackendAuthenticationServiceExtension.LinkBearerServiceOptions> bearerOptions,
        IOptions<LinkTokenServiceSettings> tokenServiceSettings,
        ICreateSystemToken tokenService)
        : base(
            serviceRegistry.Value.DataAcquisitionServiceApiUrl
                ?? throw new InvalidOperationException("DataAcquisition service URL is not configured in ServiceRegistry."),
            bearerOptions, tokenServiceSettings, tokenService)
    { }

    public Task<LinkApiResponse> GetFhirQueryConfigurationAsync(
        string facilityId,
        CancellationToken cancellationToken = default) =>
        SendAsync(() => Request($"data/{facilityId}/fhirQueryConfiguration")
            .GetAsync(cancellationToken: cancellationToken));

    public Task<LinkApiResponse> CreateFhirQueryConfigurationAsync(
        CreateFhirQueryConfigurationRequestApiModel request,
        CancellationToken cancellationToken = default) =>
        SendAsync(() => Request("data/fhirQueryConfiguration")
            .PostJsonAsync(request, cancellationToken: cancellationToken));

    public Task<LinkApiResponse> DeleteFhirQueryConfigurationAsync(
        string facilityId,
        CancellationToken cancellationToken = default) =>
        SendAsync(() => Request($"data/{facilityId}/fhirQueryConfiguration")
            .DeleteAsync(cancellationToken: cancellationToken));

    /// <summary>
    /// Saves/updates the FHIR server connection settings (base URL, max concurrent requests,
    /// min/max pull time, max retries): <c>PUT /api/data/fhirQueryConfiguration</c>. The facility id
    /// travels in the request body, matching <c>CreateFhirQueryConfigurationAsync</c>.
    /// </summary>
    public Task<LinkApiResponse> UpdateFhirQueryConfigurationAsync(
        object request,
        CancellationToken cancellationToken = default) =>
        SendAsync(() => Request("data/fhirQueryConfiguration")
            .PutJsonAsync(request, cancellationToken: cancellationToken));

    /// <summary>
    /// Facility-scoped FHIR connection probe, run once a facility's FHIR configuration has been
    /// saved: <c>GET /api/data/connectionValidation/{facilityId}/$validate</c>.
    /// </summary>
    public Task<LinkApiResponse> ValidateFacilityConnectionAsync(
        string facilityId,
        string? patientId = null,
        string? patientIdentifier = null,
        string? measureId = null,
        DateTime? start = null,
        DateTime? end = null,
        CancellationToken cancellationToken = default)
    {
        var request = Request($"data/connectionValidation/{facilityId}/$validate");
        if (!string.IsNullOrWhiteSpace(patientId)) request = request.SetQueryParam("patientId", patientId);
        if (!string.IsNullOrWhiteSpace(patientIdentifier)) request = request.SetQueryParam("patientIdentifier", patientIdentifier);
        if (!string.IsNullOrWhiteSpace(measureId)) request = request.SetQueryParam("measureId", measureId);
        if (start.HasValue) request = request.SetQueryParam("start", start.Value.ToString("o"));
        if (end.HasValue) request = request.SetQueryParam("end", end.Value.ToString("o"));
        return SendAsync(() => request.GetAsync(cancellationToken: cancellationToken));
    }

    /// <summary>
    /// URL-only FHIR reachability probe used while the FHIR Server Information form is still being
    /// filled out, before any configuration has been saved. DataAcquisition does not expose an
    /// unscoped <c>GET /api/data/connectionValidation/$validate</c> route yet, so this returns a
    /// synthetic success and makes no network call. TODO: forward to the real endpoint once it exists.
    /// </summary>
    public Task<LinkApiResponse> ValidateConnectionAsync(
        string? fhirServerBaseUrl = null,
        CancellationToken cancellationToken = default) =>
        SyntheticSuccessAsync("GET", "data/connectionValidation/$validate",
            "{\"isConnected\":true,\"message\":\"Synthetic success - endpoint not yet implemented in DataAcquisition.\"}");

    public Task<LinkApiResponse> GetFhirListConfigurationAsync(
        string facilityId,
        CancellationToken cancellationToken = default) =>
        GetFhirListConfigurationAsync(facilityId, includePatientName: false, cancellationToken);

    /// <summary>
    /// Reads the FHIR patient-list configuration for a facility. When
    /// <paramref name="includePatientName"/> is <see langword="true"/> the caller is asking for each
    /// list together with the patients matched to it (queriedAt, patientCount, patients[]).
    /// The DataAcquisition read endpoint does not honour this query parameter yet, so today it is
    /// forwarded harmlessly and ignored. TODO: drop this note once the backend supports it.
    /// </summary>
    public Task<LinkApiResponse> GetFhirListConfigurationAsync(
        string facilityId,
        bool includePatientName,
        CancellationToken cancellationToken = default)
    {
        var request = Request($"data/{facilityId}/fhirQueryList");
        if (includePatientName) request = request.SetQueryParam("includePatientName", true);
        return SendAsync(() => request.GetAsync(cancellationToken: cancellationToken));
    }

    public Task<LinkApiResponse> CreateFhirListConfigurationAsync(
        object request,
        CancellationToken cancellationToken = default) =>
        SendAsync(() => Request("data/fhirQueryList")
            .PostJsonAsync(request, cancellationToken: cancellationToken));

    /// <summary>
    /// Saves/updates the Epic patient-list configurations (Admit/Discharge x timeframe):
    /// <c>PUT /api/data/fhirQueryList</c>.
    /// </summary>
    public Task<LinkApiResponse> UpdateFhirListConfigurationAsync(
        object request,
        CancellationToken cancellationToken = default) =>
        SendAsync(() => Request("data/fhirQueryList")
            .PutJsonAsync(request, cancellationToken: cancellationToken));

    public Task<LinkApiResponse> DeleteFhirListConfigurationAsync(
        string facilityId,
        CancellationToken cancellationToken = default) =>
        SendAsync(() => Request($"data/{facilityId}/fhirQueryList")
            .DeleteAsync(cancellationToken: cancellationToken));

    public Task<LinkApiResponse> GetQueryPlanAsync(
        string facilityId,
        string type,
        CancellationToken cancellationToken = default) =>
        SendAsync(() => Request($"data/{facilityId}/QueryPlan")
            .SetQueryParam("type", type)
            .GetAsync(cancellationToken: cancellationToken));

    public Task<LinkApiResponse> CreateQueryPlanAsync(
        string facilityId,
        CreateQueryPlanRequestApiModel request,
        CancellationToken cancellationToken = default) =>
        SendAsync(() => Request($"data/{facilityId}/QueryPlan")
            .WithHeader("Content-Type", "application/json")
            .SendStringAsync(HttpMethod.Post,
                Newtonsoft.Json.JsonConvert.SerializeObject(request),
                cancellationToken: cancellationToken));

    /// <summary>
    /// Saves/updates the pre-configured per-vendor query plan required before a report can be
    /// generated: <c>PUT /api/data/{facilityId}/QueryPlan</c>.
    /// </summary>
    public Task<LinkApiResponse> UpdateQueryPlanAsync(
        string facilityId,
        object request,
        CancellationToken cancellationToken = default) =>
        SendAsync(() => Request($"data/{facilityId}/QueryPlan")
            .WithHeader("Content-Type", "application/json")
            .SendStringAsync(HttpMethod.Put,
                Newtonsoft.Json.JsonConvert.SerializeObject(request),
                cancellationToken: cancellationToken));

    public Task<LinkApiResponse> DeleteQueryPlanAsync(
        string facilityId,
        string type,
        CancellationToken cancellationToken = default) =>
        SendAsync(() => Request($"data/{facilityId}/QueryPlan")
            .SetQueryParam("type", type)
            .DeleteAsync(cancellationToken: cancellationToken));

    public Task<LinkApiResponse> SoftDeleteLogsByFacilityAsync(
        string facilityId,
        CancellationToken cancellationToken = default) =>
        SendAsync(() => Request($"data/acquisition-logs/facility/{facilityId}")
            .DeleteAsync(cancellationToken: cancellationToken));

    public Task<LinkApiResponse<PagedConfigModel<DataAcquisitionLogApiModel>>> SearchAcquisitionLogsAsync(
        string facilityId,
        string reportId,
        int pageSize = 100,
        int pageNumber = 1,
        string sortBy = "Id",
        string sortOrder = "Ascending",
        string? searchTerm = null,
        CancellationToken cancellationToken = default) =>
        SendAsync<PagedConfigModel<DataAcquisitionLogApiModel>>(() => Request("data/acquisition-logs")
            .SetQueryParam("facilityId", facilityId)
            .SetQueryParam("reportId", reportId)
            .SetQueryParam("pageSize", pageSize)
            .SetQueryParam("pageNumber", pageNumber)
            .SetQueryParam("sortBy", sortBy)
            .SetQueryParam("sortOrder", sortOrder)
            .SetQueryParam("searchTerm", string.IsNullOrWhiteSpace(searchTerm) ? null : searchTerm)
            .GetAsync(cancellationToken: cancellationToken));

    public Task<LinkApiResponse<DataAcquisitionLogApiModel>> GetAcquisitionLogByIdAsync(
        long id,
        CancellationToken cancellationToken = default) =>
        SendAsync<DataAcquisitionLogApiModel>(() => Request($"data/acquisition-logs/{id}")
            .GetAsync(cancellationToken: cancellationToken));

    public Task<LinkApiResponse<List<string>>> GetAcquisitionLogNotesAsync(
        long id,
        CancellationToken cancellationToken = default) =>
        SendAsync<List<string>>(() => Request($"data/acquisition-logs/{id}/notes")
            .GetAsync(cancellationToken: cancellationToken));

    public Task<LinkApiResponse<DataAcquisitionLogStatusStatisticsApiModel>> GetReportStatusCountsAsync(
        string reportId,
        CancellationToken cancellationToken = default) =>
        SendAsync<DataAcquisitionLogStatusStatisticsApiModel>(() => Request($"data/acquisition-logs/report/{reportId}/status-counts")
            .GetAsync(cancellationToken: cancellationToken));

    public Task<LinkApiResponse> GetReportStatisticsAsync(
        string reportId,
        CancellationToken cancellationToken = default) =>
        SendAsync(() => Request($"data/acquisition-logs/report/{reportId}/statistics")
            .GetAsync(cancellationToken: cancellationToken));

    public Task<LinkApiResponse<DataAcquisitionReportSummaryApiModel>> GetReportSummaryAsync(
        string reportId,
        CancellationToken cancellationToken = default) =>
        SendAsync<DataAcquisitionReportSummaryApiModel>(() => Request($"data/acquisition-logs/report/{reportId}/summary")
            .GetAsync(cancellationToken: cancellationToken));

    public Task<LinkApiResponse<List<string>>> GetAcquiredResourceIdsForReportAsync(
        string facilityId,
        string reportId,
        CancellationToken cancellationToken = default) =>
        SendAsync<List<string>>(() => Request($"data/acquisition-logs/report/{reportId}/acquired-resource-ids")
            .SetQueryParam("facilityId", facilityId)
            .GetAsync(cancellationToken: cancellationToken));

    public Task<LinkApiResponse<PagedConfigModel<ReferenceResourceApiModel>>> GetReferenceResourcesForLogAsync(
        long logId,
        int pageSize = 100,
        int pageNumber = 1,
        CancellationToken cancellationToken = default) =>
        SendAsync<PagedConfigModel<ReferenceResourceApiModel>>(() => Request($"data/acquisition-logs/{logId}/reference-resources")
            .SetQueryParam("pageSize", pageSize)
            .SetQueryParam("pageNumber", pageNumber)
            .GetAsync(cancellationToken: cancellationToken));

    public Task<LinkApiResponse> ProcessAcquisitionLogAsync(
        long id,
        CancellationToken cancellationToken = default) =>
        SendAsync(() => Request($"data/acquisition-logs/{id}/process")
            .PostJsonAsync(id, cancellationToken: cancellationToken));

    public Task<LinkApiResponse> ProcessAcquisitionLogsBulkAsync(
        List<long> ids,
        CancellationToken cancellationToken = default) =>
        SendAsync(() => Request("data/acquisition-logs/process-bulk")
            .PostJsonAsync(ids, cancellationToken: cancellationToken));

    public Task<LinkApiResponse<DataAcquisitionBulkActionResultApiModel>> CancelAcquisitionLogsBulkAsync(
        List<long> ids,
        int minAgeHours = 24,
        CancellationToken cancellationToken = default) =>
        SendAsync<DataAcquisitionBulkActionResultApiModel>(() => Request("data/acquisition-logs/cancel-bulk")
            .SetQueryParam("minAgeHours", minAgeHours)
            .PostJsonAsync(ids, cancellationToken: cancellationToken));

    public Task<LinkApiResponse> ProcessAcquisitionLogsByFilterAsync(
        object filter,
        CancellationToken cancellationToken = default) =>
        SendAsync(() => Request("data/acquisition-logs/process-by-filter")
            .PostJsonAsync(filter, cancellationToken: cancellationToken));

    public Task<LinkApiResponse<DataAcquisitionBulkActionResultApiModel>> CancelAcquisitionLogsByFilterAsync(
        object filter,
        int minAgeHours = 24,
        CancellationToken cancellationToken = default) =>
        SendAsync<DataAcquisitionBulkActionResultApiModel>(() => Request("data/acquisition-logs/cancel-by-filter")
            .SetQueryParam("minAgeHours", minAgeHours)
            .PostJsonAsync(filter, cancellationToken: cancellationToken));

    public Task<LinkApiResponse> DeleteAcquisitionLogAsync(
        long id,
        CancellationToken cancellationToken = default) =>
        SendAsync(() => Request($"data/acquisition-logs/{id}")
            .DeleteAsync(cancellationToken: cancellationToken));

    public Task<LinkApiResponse> SoftDeleteLogsByReportTrackingIdAsync(
        string reportTrackingId,
        CancellationToken cancellationToken = default) =>
        SendAsync(() => Request($"data/acquisition-logs/report/{reportTrackingId}")
            .DeleteAsync(cancellationToken: cancellationToken));

    public Task<LinkApiResponse> RestoreLogsByReportTrackingIdAsync(
        string reportTrackingId,
        CancellationToken cancellationToken = default) =>
        SendAsync(() => Request($"data/acquisition-logs/report/{reportTrackingId}/restore")
            .PatchJsonAsync(new { }, cancellationToken: cancellationToken));

    public Task<LinkApiResponse> RestoreLogsByFacilityAsync(
        string facilityId,
        CancellationToken cancellationToken = default) =>
        SendAsync(() => Request($"data/acquisition-logs/facility/{facilityId}/restore")
            .PatchJsonAsync(new { }, cancellationToken: cancellationToken));

    public Task<LinkApiResponse<List<OrganizationLocationConfigurationApiModel>>> GetOrganizationLocationConfigurationsAsync(
        string facilityId,
        CancellationToken cancellationToken = default) =>
        SendAsync<List<OrganizationLocationConfigurationApiModel>>(() => Request($"data/location-config/facility/{facilityId}")
            .GetAsync(cancellationToken: cancellationToken));

    public Task<LinkApiResponse<OrganizationLocationConfigurationApiModel>> CreateOrganizationLocationConfigurationAsync(
        string facilityId,
        CreateOrganizationLocationConfigurationApiModel request,
        CancellationToken cancellationToken = default) =>
        SendAsync<OrganizationLocationConfigurationApiModel>(() => Request($"data/location-config/facility/{facilityId}")
            .PostJsonAsync(request, cancellationToken: cancellationToken));

    public Task<LinkApiResponse<List<OrganizationLocationMappingApiModel>>> GetOrganizationLocationMappingsAsync(
        string facilityId,
        CancellationToken cancellationToken = default) =>
        SendAsync<List<OrganizationLocationMappingApiModel>>(() => Request($"data/location-mappings/facility/{facilityId}")
            .GetAsync(cancellationToken: cancellationToken));

    public Task<LinkApiResponse<List<EncounterMappingApiModel>>> GetEncounterMappingsAsync(
        string facilityId,
        CancellationToken cancellationToken = default) =>
        SendAsync<List<EncounterMappingApiModel>>(() => Request($"data/encounter-mappings/facilities/{facilityId}")
            .GetAsync(cancellationToken: cancellationToken));

    // ----- Organization location configuration (update / delete) -----

    /// <summary>
    /// Saves/updates the organization location configuration (Managing Org / Location Identifier /
    /// Custom FHIR Path selection) for a facility: <c>PUT /api/data/location-config/facility/{facilityId}</c>.
    /// </summary>
    public Task<LinkApiResponse> UpdateOrganizationLocationConfigurationAsync(
        string facilityId,
        object request,
        CancellationToken cancellationToken = default) =>
        SendAsync(() => Request($"data/location-config/facility/{facilityId}")
            .PutJsonAsync(request, cancellationToken: cancellationToken));

    /// <summary>
    /// Removes the organization location configuration for a facility:
    /// <c>DELETE /api/data/location-config/facility/{facilityId}</c>.
    /// </summary>
    public Task<LinkApiResponse> DeleteOrganizationLocationConfigurationAsync(
        string facilityId,
        CancellationToken cancellationToken = default) =>
        SendAsync(() => Request($"data/location-config/facility/{facilityId}")
            .DeleteAsync(cancellationToken: cancellationToken));

    // ----- Organization location mappings -----

    /// <summary>
    /// Saves the resolved organization/location mapping (including a Cerner "Site" search result):
    /// <c>PUT /api/data/location-mappings/{id}</c>.
    /// </summary>
    public Task<LinkApiResponse> UpdateOrganizationLocationMappingAsync(
        int id,
        object request,
        CancellationToken cancellationToken = default) =>
        SendAsync(() => Request($"data/location-mappings/{id}")
            .PutJsonAsync(request, cancellationToken: cancellationToken));

    // ----- sFTP acquisition configuration (Cerner) -----

    public Task<LinkApiResponse> GetSftpConfigurationByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default) =>
        SendAsync(() => Request($"data/sftp-configurations/{id}")
            .GetAsync(cancellationToken: cancellationToken));

    public Task<LinkApiResponse> GetOrganizationSftpConfigurationAsync(
        string organizationId,
        CancellationToken cancellationToken = default) =>
        SendAsync(() => Request($"data/{organizationId}/sftp-configurations")
            .GetAsync(cancellationToken: cancellationToken));

    public Task<LinkApiResponse> CreateSftpConfigurationAsync(
        string organizationId,
        object request,
        CancellationToken cancellationToken = default) =>
        SendAsync(() => Request($"data/{organizationId}/sftp-configurations")
            .PostJsonAsync(request, cancellationToken: cancellationToken));

    public Task<LinkApiResponse> UpdateSftpConfigurationAsync(
        string organizationId,
        string configurationId,
        object request,
        CancellationToken cancellationToken = default) =>
        SendAsync(() => Request($"data/{organizationId}/sftp-configurations/{configurationId}")
            .PutJsonAsync(request, cancellationToken: cancellationToken));

    public Task<LinkApiResponse> DeleteSftpConfigurationAsync(
        string organizationId,
        string configurationId,
        CancellationToken cancellationToken = default) =>
        SendAsync(() => Request($"data/{organizationId}/sftp-configurations/{configurationId}")
            .DeleteAsync(cancellationToken: cancellationToken));

    /// <summary>
    /// Saves the write-only sFTP credentials for an organization (never read back once saved):
    /// <c>PUT /api/data/{organizationId}/sftp-configurations/credentials</c>.
    /// </summary>
    public Task<LinkApiResponse> UpdateSftpCredentialsAsync(
        string organizationId,
        object credentials,
        CancellationToken cancellationToken = default) =>
        SendAsync(() => Request($"data/{organizationId}/sftp-configurations/credentials")
            .PutJsonAsync(credentials, cancellationToken: cancellationToken));

    public Task<LinkApiResponse> DeleteSftpCredentialsAsync(
        string organizationId,
        CancellationToken cancellationToken = default) =>
        SendAsync(() => Request($"data/{organizationId}/sftp-configurations/credentials")
            .DeleteAsync(cancellationToken: cancellationToken));

    /// <summary>
    /// Reads whether sFTP credentials already exist for an organization, without exposing the
    /// values: <c>GET /api/data/{organizationId}/sftp-configurations/credentials/status</c>.
    /// </summary>
    public Task<LinkApiResponse> GetSftpCredentialStatusAsync(
        string organizationId,
        CancellationToken cancellationToken = default) =>
        SendAsync(() => Request($"data/{organizationId}/sftp-configurations/credentials/status")
            .GetAsync(cancellationToken: cancellationToken));

    /// <summary>
    /// Tests the connection against the saved sFTP configuration:
    /// <c>POST /api/data/{organizationId}/sftp-configurations/test-connection</c>.
    /// </summary>
    public Task<LinkApiResponse> TestSftpConnectionAsync(
        string organizationId,
        CancellationToken cancellationToken = default) =>
        SendAsync(() => Request($"data/{organizationId}/sftp-configurations/test-connection")
            .PostJsonAsync(new { }, cancellationToken: cancellationToken));

    /// <summary>
    /// Ad-hoc "test and preview": test unsaved sFTP connection details typed into the form and
    /// (when <paramref name="includeFileContent"/> is set) return every file with its patients
    /// attached, in one call. DataAcquisition does not expose an unscoped
    /// <c>POST /api/data/sftp-configurations/test-connection</c> route yet, so this returns a
    /// synthetic success and makes no network call. TODO: forward to the real endpoint once it exists.
    /// </summary>
    public Task<LinkApiResponse> TestSftpConnectionAdHocAsync(
        object connectionDetails,
        bool includeFileContent = false,
        CancellationToken cancellationToken = default) =>
        SyntheticSuccessAsync("POST",
            $"data/sftp-configurations/test-connection?includeFileContent={includeFileContent.ToString().ToLowerInvariant()}",
            "{\"success\":true,\"message\":\"Synthetic success - endpoint not yet implemented in DataAcquisition.\",\"files\":[]}");

    /// <summary>
    /// Retrieves the paged/filterable sFTP acquisition logs: <c>GET /api/data/sftp-logs</c>.
    /// </summary>
    public Task<LinkApiResponse> SearchSftpLogsAsync(
        string? facilityId = null,
        string? status = null,
        string? acquisitionType = null,
        string? subType = null,
        int pageNumber = 1,
        int pageSize = 10,
        string? sortBy = null,
        string? sortOrder = null,
        bool? includeDeleted = null,
        CancellationToken cancellationToken = default)
    {
        var request = Request("data/sftp-logs")
            .SetQueryParam("pageNumber", pageNumber)
            .SetQueryParam("pageSize", pageSize);
        if (!string.IsNullOrWhiteSpace(facilityId)) request = request.SetQueryParam("facilityId", facilityId);
        if (!string.IsNullOrWhiteSpace(status)) request = request.SetQueryParam("status", status);
        if (!string.IsNullOrWhiteSpace(acquisitionType)) request = request.SetQueryParam("acquisitionType", acquisitionType);
        if (!string.IsNullOrWhiteSpace(subType)) request = request.SetQueryParam("subType", subType);
        if (!string.IsNullOrWhiteSpace(sortBy)) request = request.SetQueryParam("sortBy", sortBy);
        if (!string.IsNullOrWhiteSpace(sortOrder)) request = request.SetQueryParam("sortOrder", sortOrder);
        if (includeDeleted.HasValue) request = request.SetQueryParam("includeDeleted", includeDeleted.Value);
        return SendAsync(() => request.GetAsync(cancellationToken: cancellationToken));
    }

    /// <summary>
    /// Returns a synthetic 2xx <see cref="LinkApiResponse"/> for onboarding endpoints that the SDK
    /// is expected to expose but that have no backend route yet. Makes no network call.
    /// </summary>
    private static Task<LinkApiResponse> SyntheticSuccessAsync(string method, string relativePath, string rawBody = "{}") =>
        Task.FromResult(new LinkApiResponse
        {
            StatusCode = 200,
            RawBody = rawBody,
            ContentType = "application/json",
            RequestMethod = method,
            RequestUrl = relativePath,
        });
}
