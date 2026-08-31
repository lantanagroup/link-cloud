using LantanaGroup.Link.Nhsn.App.Bff.Application.Interfaces.Infrastructure;
using LantanaGroup.Link.Nhsn.App.Bff.Application.Models.Onboarding;
using LantanaGroup.Link.Sdk.Clients;

namespace LantanaGroup.Link.Nhsn.App.Bff.Infrastructure.Link;

// IReportGateway over LinkSdk's IReportServiceClient.
internal sealed class ReportGateway : IReportGateway
{
    private const string ServiceName = "Report";

    private readonly IReportServiceClient _reportClient;

    public ReportGateway(IReportServiceClient reportClient)
    {
        _reportClient = reportClient;
    }

    public async Task<ReportScheduleSummary?> GetLatestScheduleAsync(string facilityId, CancellationToken cancellationToken = default)
    {
        var response = await _reportClient.GetSchedulesByFacilityAsync(facilityId, cancellationToken: cancellationToken);
        var schedules = LinkResponseHandler.Optional(response, ServiceName, nameof(GetLatestScheduleAsync));
        if (schedules is null || schedules.Count == 0)
        {
            return null;
        }

        var latest = schedules.OrderByDescending(schedule => schedule.CreateDate ?? DateTime.MinValue).First();
        return new ReportScheduleSummary
        {
            ReportId = latest.Id.ToString(),
            Measures = latest.ReportTypes
        };
    }
}
