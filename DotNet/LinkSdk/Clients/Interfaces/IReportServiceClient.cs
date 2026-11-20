using LantanaGroup.Link.Sdk.ApiClient;
using LantanaGroup.Link.Shared.Application.Enums;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Models.Integration.Report;
using LantanaGroup.Link.Shared.Application.Models.Responses;
using LantanaGroup.Link.Shared.Application.Models.Tenant;

namespace LantanaGroup.Link.Sdk.Clients;

public interface IReportServiceClient
{
    // --- Schedules ---
    Task<LinkApiResponse<ReportScheduleApiModel>> GetScheduleAsync(string reportId, CancellationToken cancellationToken = default, bool includeDeleted = false);
    Task<LinkApiResponse<List<ReportScheduleApiModel>>> GetSchedulesByFacilityAsync(string facilityId, bool? active = null, bool blocking = false, bool includeDeleted = false, CancellationToken cancellationToken = default);
    Task<LinkApiResponse<PagedConfigModel<ReportScheduleApiModel>>> SearchSchedulesAsync(string reportId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Paged schedule search used by a facility's report list:
    /// <c>GET /api/schedules/search</c>.
    /// </summary>
    Task<LinkApiResponse<PagedConfigModel<ReportScheduleApiModel>>> SearchFacilitySchedulesAsync(
        ReportScheduleSearch query,
        CancellationToken cancellationToken = default);

    /// <summary>Status totals and created-per-day counts: <c>POST /api/schedules/counts</c>.</summary>
    Task<LinkApiResponse<ReportActivityCounts>> GetActivityCountsAsync(
        ReportActivityCountRequest request,
        CancellationToken cancellationToken = default);
    Task<LinkApiResponse<PagedConfigModel<ReportSummaryApiModel>>> GetReportSummariesAsync(string? facilityId = null, ReportStatus? status = null, string? sortBy = null, SortOrder? sortOrder = null, int pageSize = 10, int pageNumber = 1, CancellationToken cancellationToken = default);
    Task<LinkApiResponse<ReportSummaryApiModel>> GetReportSummaryAsync(string reportScheduleId, CancellationToken cancellationToken = default);
    Task<LinkApiResponse> SoftDeleteScheduleAsync(string reportId, CancellationToken cancellationToken = default, bool allowInProgress = false);
    Task<LinkApiResponse> RestoreScheduleAsync(string reportId, CancellationToken cancellationToken = default);
    Task<LinkApiResponse> SetReportsDeletedStatusForFacilityAsync(string facilityId, bool deleted, CancellationToken cancellationToken = default);

    /// <summary>
    /// Generates a schedule's manifest on demand (<c>GET /schedules/{id}/manifest</c>). Nothing is
    /// uploaded or submitted. Patients still in flight are included and flagged in the manifest's
    /// OperationOutcome.
    /// </summary>
    /// <param name="reportScheduleId">The report schedule id.</param>
    /// <param name="format">NDJSON (the default) or a FHIR JSON Bundle.</param>
    /// <param name="force">Generate even though the reporting period has not closed; otherwise that is a 409.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>
    /// The manifest as text, exactly as the service sent it: NDJSON lines or a Bundle document, as
    /// <see cref="LinkApiResponse{T}.ContentType"/> reports. It is not deserialized, because this
    /// client's JSON settings cannot read FHIR and NDJSON is not a single JSON document. Parse it with
    /// the FHIR serializer if you need resources. On failure, <see cref="LinkApiResponse{T}.RawBody"/>
    /// holds the Problem Details.
    /// </returns>
    Task<LinkApiResponse<string>> GetScheduleManifestAsync(string reportScheduleId, ReportManifestFormat format = ReportManifestFormat.Ndjson, bool force = false, CancellationToken cancellationToken = default);

    // --- Entries ---
    Task<LinkApiResponse<ReportEntryApiModel>> GetEntryByIdAsync(string id, CancellationToken cancellationToken = default);
    Task<LinkApiResponse<List<ReportEntryApiModel>>> GetEntriesByScheduleAsync(string reportId, CancellationToken cancellationToken = default);
    Task<LinkApiResponse<List<ReportEntryApiModel>>> GetEntriesByPatientAsync(string patientId, CancellationToken cancellationToken = default);
    Task<LinkApiResponse<int>> GetEntryCountByScheduleAsync(string reportScheduleId, CancellationToken cancellationToken = default);
    Task<LinkApiResponse<ReportEntrySummaryApiModel>> GetEntrySummaryByScheduleAsync(string reportScheduleId, CancellationToken cancellationToken = default);
    /// <summary>
    /// Gets one patient's entry within a schedule, including the evidence behind its mapping indicators.
    /// </summary>
    Task<LinkApiResponse<ReportEntryDetailApiModel>> GetEntryByScheduleAndPatientAsync(string reportScheduleId, string patientId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Searches report entries, returning the mapping indicators alongside the reporting and submission
    /// status for each patient. This is the operation behind the report detail patient table.
    /// </summary>
    /// <remarks>
    /// Carries no mapping detail -- call <see cref="GetEntryByScheduleAndPatientAsync"/> for the counts and
    /// the unmapped codes behind a single patient's indicator.
    /// </remarks>
    Task<LinkApiResponse<PagedConfigModel<ReportEntryApiModel>>> SearchEntriesAsync(
        string? facilityId = null,
        string? patientId = null,
        string? reportScheduleId = null,
        string? reportType = null,
        string? sortBy = null,
        int pageSize = 10,
        int pageNumber = 1,
        SortOrder? sortOrder = null,
        CancellationToken cancellationToken = default);

    // --- Resources ---
    Task<LinkApiResponse<ReportResourceApiModel>> GetResourceByIdAsync(string id, CancellationToken cancellationToken = default);
    Task<LinkApiResponse<List<ReportResourceApiModel>>> GetResourcesByScheduleAsync(string reportScheduleId, CancellationToken cancellationToken = default);
    Task<LinkApiResponse<List<ReportResourceApiModel>>> GetResourcesByScheduleAndPatientAsync(string reportScheduleId, string patientId, CancellationToken cancellationToken = default);
    Task<LinkApiResponse<List<ReportResourceApiModel>>> GetResourcesByPatientAsync(string patientId, CancellationToken cancellationToken = default);
    Task<LinkApiResponse<PagedConfigModel<ReportResourceApiModel>>> SearchResourcesAsync(string facilityId, string reportId, int pageSize = 5000, int pageNumber = 1, CancellationToken cancellationToken = default);

    // --- Populations ---
    Task<LinkApiResponse<ReportPopulationApiModel>> GetPopulationByIdAsync(string id, CancellationToken cancellationToken = default);
    Task<LinkApiResponse<List<ReportPopulationApiModel>>> GetPopulationsByScheduleAsync(string reportId, string? reportType = null, CancellationToken cancellationToken = default);
    Task<LinkApiResponse<int>> GetInitialPopulationCountAsync(string reportScheduleId, CancellationToken cancellationToken = default);
}
