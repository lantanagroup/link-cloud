using LantanaGroup.Link.Nhsn.App.Bff.Application.Models.Onboarding;

namespace LantanaGroup.Link.Nhsn.App.Bff.Application.Interfaces.Infrastructure;

/// <summary>
/// The Report service, in our vocabulary. Reference port for the gateway pattern — no Link type
/// crosses this boundary, so the Application layer never sees <c>ReportScheduleApiModel</c> or
/// <c>LinkApiResponse</c>.
/// </summary>
public interface IReportGateway
{
    /// <summary>
    /// Reads the facility's most recently created report schedule, or null when Report has none
    /// for it yet.
    /// </summary>
    Task<ReportScheduleSummary?> GetLatestScheduleAsync(string facilityId, CancellationToken cancellationToken = default);
}
