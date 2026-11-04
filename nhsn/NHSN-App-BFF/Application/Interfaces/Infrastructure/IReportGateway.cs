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
    /// summary (status, patient count) for the Report Results list. Returns each row's measure
    /// mapping too (not just ReportSummary's fields), so the list can resolve friendly measure
    /// names the same way the detail view does.
    /// </summary>
    Task<Paged<ReportDetail>> ListReportsAsync(string facilityId, int page, int pageSize, CancellationToken cancellationToken = default);

    /// <summary>Reads one report's full detail, or null when Report has no schedule for that id.</summary>
    Task<ReportDetail?> GetReportAsync(string reportId, CancellationToken cancellationToken = default);

    /// <summary>Reads the per-patient rows behind the Report Details patient table.</summary>
    Task<List<ReportPatientEntry>> GetReportPatientsAsync(string reportId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads the distinct patient ids across every one of a facility's report schedules, newest
    /// schedule first -- the "patients involved in the facility's generated reports" set the MRN
    /// Identifier Intake rule builder's patient list is drawn from.
    /// </summary>
    Task<IReadOnlyList<string>> GetFacilityPatientIdsAsync(string facilityId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads the real evidence behind one patient's Location Org / HSLOC / Encounter mapping
    /// indicators, or null when Report has no entry for that patient in this report.
    /// </summary>
    Task<PatientMappingEvidence?> GetPatientMappingEvidenceAsync(string reportId, string patientId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Resolves the blob storage location of one patient's generated report for a report type --
    /// the per-measure report if one has been recorded, falling back to the patient's whole
    /// aggregate report otherwise -- or null when Report has no entry for this patient, or no
    /// report URI recorded yet (not through submission).
    /// </summary>
    Task<PatientReportBlobReference?> GetPatientReportBlobReferenceAsync(string reportId, string patientId, string reportType, CancellationToken cancellationToken = default);
}
