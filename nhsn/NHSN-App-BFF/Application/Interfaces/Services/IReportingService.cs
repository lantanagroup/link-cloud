using LantanaGroup.Link.Nhsn.App.Bff.Application.Models.Reporting;

namespace LantanaGroup.Link.Nhsn.App.Bff.Application.Interfaces.Services;

/// <summary>
/// The reporting step: requesting an ad hoc report. Facility comes from the token, so no method
/// takes one.
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
}
