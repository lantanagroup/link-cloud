using LantanaGroup.Link.Sdk.ApiClient;
using LantanaGroup.Link.Shared.Application.Models;
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

    /// <summary>
    /// Searches measure mappings by NHSN measure, dQM and frequency.
    /// </summary>
    /// <remarks>
    /// Every filter is optional and they combine with AND. Answers 204 with no body when nothing
    /// matches, so an empty result is a status code rather than an empty <c>Records</c> list.
    /// </remarks>
    Task<LinkApiResponse<PagedConfigModel<MeasureMappingModel>>> SearchMeasureMappingsAsync(
        string? measure, string? dqm = null, Frequency? frequency = null,
        int pageSize = 10, int pageNumber = 1, CancellationToken cancellationToken = default);

    Task<LinkApiResponse<FacilityReportingPlanModel>> CreateFacilityReportingPlanAsync(FacilityReportingPlanRequest request, CancellationToken cancellationToken = default);
    Task<LinkApiResponse<FacilityReportingPlanModel>> GetFacilityReportingPlanAsync(string id, CancellationToken cancellationToken = default);
    Task<LinkApiResponse<FacilityReportingPlanModel>> UpdateFacilityReportingPlanAsync(string id, FacilityReportingPlanUpdateRequest request, CancellationToken cancellationToken = default);
    Task<LinkApiResponse> DeleteFacilityReportingPlanAsync(string id, CancellationToken cancellationToken = default);
    Task<LinkApiResponse<PagedConfigModel<FacilityReportingPlanModel>>> SearchFacilityReportingPlansAsync(int pageSize = 10, int pageNumber = 1, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a facility's reporting plans, optionally narrowed to a reporting period, a look-ahead
    /// window, or to whether the facility was reporting.
    /// </summary>
    /// <param name="monthsAhead">
    /// A window of 1 to 24 reporting periods counting the current one. Cannot be combined with
    /// <paramref name="month"/> or <paramref name="year"/>; doing so is refused with a 400.
    /// </param>
    /// <param name="refresh">
    /// Asks DMRP for the plan before answering, so the result reflects what DMRP says now rather
    /// than what Link last recorded. A refresh that fails answers 502 rather than returning stored
    /// rows, so a caller that would rather have stale data than none should leave this false.
    /// </param>
    Task<LinkApiResponse<List<FacilityReportingPlanModel>>> GetFacilityReportingPlansForFacilityAsync(string facilityId,
        int? month = null, int? year = null, bool? isReporting = null, int? monthsAhead = null, bool refresh = false,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a facility's reporting plan as a calendar: one entry per reporting period, carrying the
    /// measures enrolled in it and the schedule Link will run for them.
    /// </summary>
    /// <remarks>
    /// This is the read behind the facility-facing reporting plan table. A period the facility has no
    /// plan on record for is derived from its current enrollment and flagged <c>IsProjected</c>;
    /// recorded periods always win over projected ones.
    /// </remarks>
    /// <param name="monthsAhead">
    /// A window of 1 to 24 periods counting the current one - <c>6</c> is this month and the next
    /// five. Omit to return every period the facility has a plan for, and to project nothing.
    /// </param>
    /// <param name="isReporting">
    /// Defaults to true on this operation, so the answer is what the facility currently owes rather
    /// than its whole history.
    /// </param>
    /// <param name="refresh">
    /// Asks DMRP for the plan before answering. Refreshes the current period once whatever the
    /// window, since every projected month is derived from that enrollment.
    /// </param>
    Task<LinkApiResponse<PagedConfigModel<FacilityReportingPlanPeriodModel>>> GetFacilityReportingPlanPeriodsAsync(
        string facilityId, int? monthsAhead = null, bool? isReporting = null, bool refresh = false,
        int pageSize = 10, int pageNumber = 1, CancellationToken cancellationToken = default);

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
