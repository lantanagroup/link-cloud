using LantanaGroup.Link.Nhsn.App.Bff.Application.Models;
using LantanaGroup.Link.Nhsn.App.Bff.Application.Models.Onboarding;
using LantanaGroup.Link.Nhsn.App.Bff.Application.Models.Reporting;

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

    /// <summary>
    /// Reads a facility's report schedules, newest first, combining each schedule with its report
    /// summary (status, patient count) for the Report Results list.
    /// </summary>
    Task<Paged<ReportSummary>> ListReportsAsync(string facilityId, int page, int pageSize, CancellationToken cancellationToken = default);

    /// <summary>Reads one report's full detail, or null when Report has no schedule for that id.</summary>
    Task<ReportDetail?> GetReportAsync(string reportId, CancellationToken cancellationToken = default);

    /// <summary>Reads the per-patient rows behind the Report Details patient table.</summary>
    Task<List<ReportPatientEntry>> GetReportPatientsAsync(string reportId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads the real evidence behind one patient's Location Org / HSLOC / Encounter mapping
    /// indicators, or null when Report has no entry for that patient in this report.
    /// </summary>
    Task<PatientMappingEvidence?> GetPatientMappingEvidenceAsync(string reportId, string patientId, CancellationToken cancellationToken = default);
}
