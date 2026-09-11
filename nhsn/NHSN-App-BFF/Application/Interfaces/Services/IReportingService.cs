using LantanaGroup.Link.Nhsn.App.Bff.Application.Models;
using LantanaGroup.Link.Nhsn.App.Bff.Application.Models.Reporting;

namespace LantanaGroup.Link.Nhsn.App.Bff.Application.Interfaces.Services;

/// <summary>
/// The reporting step: requesting an ad hoc report and reading back its results. Facility comes
/// from the token, so no method takes one.
/// </summary>
public interface IReportingService
{
    /// <summary>
    /// Requests an ad hoc report and returns it with a Pending status.
    /// </summary>
    /// <remarks>
    /// A report already in flight does not refuse a second one. An ad hoc report from this flow
    /// bypasses submission, and Link only reports a schedule complete once it has been submitted,
    /// so "still pending" is not a state that clears on its own — refusing on it would let the
    /// first test report block every one after it. The UI warns and allows.
    /// </remarks>
    Task<ReportSummary> RequestReportAsync(ReportRequest request, CancellationToken cancellationToken = default);

    /// <summary>Lists the current facility's reports, newest first.</summary>
    Task<Paged<ReportSummary>> ListReportsAsync(int page, int pageSize, CancellationToken cancellationToken = default);

    /// <summary>Reads one report's full detail, or null when Report has no schedule for that id.</summary>
    Task<ReportDetail?> GetReportAsync(string reportId, CancellationToken cancellationToken = default);

    /// <summary>Reads the per-patient rows behind the Report Details patient table.</summary>
    Task<List<ReportPatientEntry>> GetReportPatientsAsync(string reportId, CancellationToken cancellationToken = default);

    /// <summary>Reads the real evidence behind one patient's mapping indicators.</summary>
    Task<PatientMappingEvidence?> GetPatientMappingEvidenceAsync(string reportId, string patientId, CancellationToken cancellationToken = default);

    /// <summary>Reads the facility's configured query plan for the report's vendor.</summary>
    Task<QueryPlan?> GetQueryPlanAsync(string reportId, CancellationToken cancellationToken = default);

    /// <summary>Reads DataAcquisition's acquisition log entries recorded for this report.</summary>
    Task<List<AcquisitionLogEntry>> GetAcquisitionLogsAsync(string reportId, CancellationToken cancellationToken = default);

    /// <summary>Reads DataAcquisition's own summary counts for this report, for the export action.</summary>
    Task<AcquisitionReportSummary?> GetAcquisitionSummaryAsync(string reportId, CancellationToken cancellationToken = default);

    /// <summary>Reads one patient's measure-report export data for the given report type, for the patient report download action.</summary>
    Task<PatientMeasureReportExport?> GetPatientMeasureReportExportAsync(string reportId, string patientId, string reportType, CancellationToken cancellationToken = default);
}
