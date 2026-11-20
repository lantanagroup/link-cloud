using LantanaGroup.Link.Report.Domain.Enums;

namespace LantanaGroup.Link.Report.Domain
{
    /// <summary>
    /// The statuses at which a <c>ReportEntry</c> has finished processing. The completion gate
    /// (<see cref="Managers.IReportEntryManager.AreAllEntriesCompleteAsync"/>) and the manifest's
    /// stalled-patient warnings both read these sets, so the two cannot disagree.
    /// </summary>
    /// <remarks>
    /// LEGPROG-497 replaces <see cref="ReportingStatus"/> and <see cref="SubmissionStatus"/>
    /// (LEGLINK-1224). When that migration lands, redefine "terminal" here and nowhere else.
    /// </remarks>
    public static class ReportEntryTerminalStatuses
    {
        // Arrays rather than sets: EF Core translates Contains on these into SQL IN.
        public static readonly ReportingStatus[] Reporting =
        [
            ReportingStatus.NotReportable,
            ReportingStatus.PassedValidation,
            ReportingStatus.FailedValidation
        ];

        public static readonly SubmissionStatus[] Submission =
        [
            SubmissionStatus.Submitted,
            SubmissionStatus.NotEligable,
            SubmissionStatus.NotSubmitted,
        ];

        public static bool IsTerminal(ReportingStatus reportingStatus, SubmissionStatus? submissionStatus) =>
            Reporting.Contains(reportingStatus)
            && submissionStatus.HasValue
            && Submission.Contains(submissionStatus.Value);
    }
}
