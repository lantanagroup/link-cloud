using LantanaGroup.Link.Sdk.ApiClient;
using LantanaGroup.Link.Shared.Application.Models.Integration.Report;
using LantanaGroup.Link.Shared.Application.Models.Responses;
using LantanaGroup.Link.Shared.Application.Models.Tenant;

namespace LantanaGroup.Link.Sdk.Clients;

public interface IReportServiceClient
{
    // --- Schedules ---
    Task<LinkApiResponse<ReportScheduleApiModel>> GetScheduleAsync(string reportId, CancellationToken cancellationToken = default);
    Task<LinkApiResponse<List<ReportScheduleApiModel>>> GetSchedulesByFacilityAsync(string facilityId, bool? active = null, bool blocking = false, bool includeDeleted = false, CancellationToken cancellationToken = default);
    Task<LinkApiResponse<PagedConfigModel<ReportScheduleApiModel>>> SearchSchedulesAsync(string reportId, CancellationToken cancellationToken = default);
    Task<LinkApiResponse> SoftDeleteScheduleAsync(string reportId, CancellationToken cancellationToken = default);
    Task<LinkApiResponse> RestoreScheduleAsync(string reportId, CancellationToken cancellationToken = default);
    Task<LinkApiResponse> SetReportsDeletedStatusForFacilityAsync(string facilityId, bool deleted, CancellationToken cancellationToken = default);

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
