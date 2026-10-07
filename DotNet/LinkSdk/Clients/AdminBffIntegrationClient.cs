using Flurl.Http;
using LantanaGroup.Link.Sdk.ApiClient;
using LantanaGroup.Link.Shared.Application.Extensions.Security;
using LantanaGroup.Link.Shared.Application.Interfaces.Services.Security.Token;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using LantanaGroup.Link.Shared.Application.Models.Kafka;
using LantanaGroup.Link.Shared.Application.Models.Tenant;
using Microsoft.Extensions.Options;

namespace LantanaGroup.Link.Sdk.Clients;

public sealed class AdminBffIntegrationClient : LinkApiClientBase, IAdminBffIntegrationClient
{
    public AdminBffIntegrationClient(
        IOptions<ServiceRegistry> serviceRegistry,
        IOptions<BackendAuthenticationServiceExtension.LinkBearerServiceOptions> bearerOptions,
        IOptions<LinkTokenServiceSettings> tokenServiceSettings,
        ICreateSystemToken tokenService)
        : base(
            serviceRegistry.Value.AdminBffServiceApiUrl
                ?? throw new InvalidOperationException("Admin.BFF service URL is not configured in ServiceRegistry."),
            bearerOptions, tokenServiceSettings, tokenService)
    {
    }

    public Task<LinkApiResponse<string>> GetHealthAsync(CancellationToken cancellationToken = default) =>
        SendStringAsync(() => Request("/monitor/health").GetAsync(cancellationToken: cancellationToken));

    public Task<LinkApiResponse<string>> GetServiceHealthAsync(string service, CancellationToken cancellationToken = default) =>
        SendStringAsync(() => Request($"/monitor/health/{Uri.EscapeDataString(service)}").GetAsync(cancellationToken: cancellationToken));

    public Task<LinkApiResponse<string>> GetServiceInformationAsync(CancellationToken cancellationToken = default) =>
        SendStringAsync(() => Request("/info").GetAsync(cancellationToken: cancellationToken));

    public Task<LinkApiResponse<FacilityModel>> CreateFacilityAsync(FacilityModel request, CancellationToken cancellationToken = default) =>
        SendAsync<FacilityModel>(() => Request("/Facility").PostJsonAsync(request, cancellationToken: cancellationToken));

    public Task<LinkApiResponse> DeleteFacilityAsync(string facilityId, CancellationToken cancellationToken = default) =>
        SendAsync(() => Request($"/Facility/{facilityId}").DeleteAsync(cancellationToken: cancellationToken));

    public Task<LinkApiResponse> SoftDeleteAggregateFacilityAsync(string facilityId, CancellationToken cancellationToken = default) =>
        SendAsync(() => Request($"/aggregate/facility/{facilityId}").DeleteAsync(cancellationToken: cancellationToken));

    public Task<LinkApiResponse> RestoreAggregateFacilityAsync(string facilityId, CancellationToken cancellationToken = default) =>
        SendAsync(() => Request($"/aggregate/facility/{facilityId}/restore").PatchAsync(cancellationToken: cancellationToken));

    public Task<LinkApiResponse<string>> GetReportSummariesAsync(CancellationToken cancellationToken = default) =>
        SendStringAsync(() => Request("/aggregate/reports/summaries").GetAsync(cancellationToken: cancellationToken));

    public Task<LinkApiResponse<string>> GetReportSummaryAsync(string reportScheduleId, CancellationToken cancellationToken = default) =>
        SendStringAsync(() => Request($"/aggregate/reports/summaries/{reportScheduleId}").GetAsync(cancellationToken: cancellationToken));

    public Task<LinkApiResponse> DeleteAggregateReportAsync(string reportScheduleId, CancellationToken cancellationToken = default) =>
        SendAsync(() => Request($"/aggregate/reports/{reportScheduleId}").DeleteAsync(cancellationToken: cancellationToken));

    public Task<LinkApiResponse> AbortAggregateReportAsync(string reportScheduleId, CancellationToken cancellationToken = default) =>
        SendAsync(() => Request($"/aggregate/reports/{reportScheduleId}/abort").PostAsync(cancellationToken: cancellationToken));

    public Task<LinkApiResponse> RestoreAggregateReportAsync(string reportScheduleId, CancellationToken cancellationToken = default) =>
        SendAsync(() => Request($"/aggregate/reports/{reportScheduleId}/restore").PatchAsync(cancellationToken: cancellationToken));

    public Task<LinkApiResponse> CreateReportScheduledAsync(
        string facilityId,
        Frequency frequency,
        IReadOnlyList<string> reportTypes,
        DateTime startDateUtc,
        int delayMinutes,
        string reportTrackingId,
        CancellationToken cancellationToken = default)
    {
        var body = new
        {
            facilityId,
            frequency,
            reportTypes,
            startDate = DateTime.SpecifyKind(startDateUtc, DateTimeKind.Utc),
            delay = delayMinutes.ToString(),
            reportTrackingId
        };

        return SendAsync(() => Request("/integration/report-scheduled")
            .PostJsonAsync(body, cancellationToken: cancellationToken));
    }

    public Task<LinkApiResponse> CreatePatientListAcquiredAsync(
        string facilityId,
        IReadOnlyList<PatientListItem> patientLists,
        Guid reportTrackingId,
        CancellationToken cancellationToken = default)
    {
        var body = new
        {
            facilityId,
            patientLists,
            reportTrackingId
        };

        return SendAsync(() => Request("/integration/patient-list-acquired")
            .PostJsonAsync(body, cancellationToken: cancellationToken));
    }

    public Task<LinkApiResponse> CreatePatientEventAsync(
        string facilityId,
        string patientId,
        string eventType,
        CancellationToken cancellationToken = default)
    {
        var body = new { key = facilityId, patientId, eventType };
        return SendAsync(() => Request("/integration/patient-event")
            .PostJsonAsync(body, cancellationToken: cancellationToken));
    }

    public Task<LinkApiResponse> CreateDataAcquisitionRequestedAsync(
        string facilityId,
        string patientId,
        string queryType,
        IReadOnlyList<string> reportTypes,
        DateTime startDateUtc,
        DateTime endDateUtc,
        CancellationToken cancellationToken = default)
    {
        var body = new
        {
            key = facilityId,
            patientId,
            queryType,
            scheduledReports = new[]
            {
                new
                {
                    reportTypes,
                    startDate = DateTime.SpecifyKind(startDateUtc, DateTimeKind.Utc),
                    endDate = DateTime.SpecifyKind(endDateUtc, DateTimeKind.Utc)
                }
            }
        };
        return SendAsync(() => Request("/integration/data-acquisition-requested")
            .PostJsonAsync(body, cancellationToken: cancellationToken));
    }

    public Task<LinkApiResponse> CreatePatientAcquiredAsync(
        string facilityId,
        IReadOnlyList<string> patientIds,
        string reportTrackingId,
        CancellationToken cancellationToken = default)
    {
        var body = new { facilityId, patientIds, reportTrackingId };
        return SendAsync(() => Request("/integration/patient-acquired")
            .PostJsonAsync(body, cancellationToken: cancellationToken));
    }

    public Task<LinkApiResponse> StartConsumersAsync(
        string correlationId,
        CancellationToken cancellationToken = default) =>
        SendAsync(() => Request("/integration/start-consumers")
            .PostJsonAsync(new { correlationId }, cancellationToken: cancellationToken));

    public Task<LinkApiResponse<string>> ReadConsumersAsync(
        string correlationId,
        CancellationToken cancellationToken = default) =>
        SendStringAsync(() => Request("/integration/read-consumers")
            .PostJsonAsync(new { correlationId }, cancellationToken: cancellationToken));

    public Task<LinkApiResponse> StopConsumersAsync(
        string correlationId,
        CancellationToken cancellationToken = default) =>
        SendAsync(() => Request("/integration/stop-consumers")
            .PostJsonAsync(new { correlationId }, cancellationToken: cancellationToken));
}
