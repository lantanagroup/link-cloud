using LantanaGroup.Link.Sdk.ApiClient;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Models.Kafka;
using LantanaGroup.Link.Shared.Application.Models.Tenant;

namespace LantanaGroup.Link.Sdk.Clients;

public interface IAdminBffIntegrationClient
{
    Task<LinkApiResponse<string>> GetHealthAsync(CancellationToken cancellationToken = default);

    Task<LinkApiResponse<FacilityModel>> CreateFacilityAsync(
        FacilityModel request,
        CancellationToken cancellationToken = default);

    Task<LinkApiResponse> DeleteFacilityAsync(
        string facilityId,
        CancellationToken cancellationToken = default);

    Task<LinkApiResponse> SoftDeleteAggregateFacilityAsync(
        string facilityId,
        CancellationToken cancellationToken = default);

    Task<LinkApiResponse> RestoreAggregateFacilityAsync(
        string facilityId,
        CancellationToken cancellationToken = default);

    Task<LinkApiResponse<string>> GetReportSummariesAsync(CancellationToken cancellationToken = default);

    Task<LinkApiResponse<string>> GetReportSummaryAsync(
        string reportScheduleId,
        CancellationToken cancellationToken = default);

    Task<LinkApiResponse> DeleteAggregateReportAsync(
        string reportScheduleId,
        CancellationToken cancellationToken = default);

    Task<LinkApiResponse> RestoreAggregateReportAsync(
        string reportScheduleId,
        CancellationToken cancellationToken = default);

    Task<LinkApiResponse> CreateReportScheduledAsync(
        string facilityId,
        Frequency frequency,
        IReadOnlyList<string> reportTypes,
        DateTime startDateUtc,
        int delayMinutes,
        string reportTrackingId,
        CancellationToken cancellationToken = default);

    Task<LinkApiResponse> CreatePatientListAcquiredAsync(
        string facilityId,
        IReadOnlyList<PatientListItem> patientLists,
        Guid reportTrackingId,
        CancellationToken cancellationToken = default);
}
