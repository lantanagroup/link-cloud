namespace LantanaGroup.Link.Report.Domain.Enums;

/// <summary>
/// What a retention participant's data is tied to, which decides how many results Report tracks for it.
/// </summary>
public enum RetentionParticipantScope
{
    /// <summary>
    /// The participant deletes what it holds for each listed report, and replies with one result per report.
    /// </summary>
    Report = 0,

    /// <summary>
    /// The participant's data is not tied to a report. It acts on the cleanup's cutoff and replies with one
    /// result for the whole cleanup, which never holds back a report's <c>Scrubbed</c> state.
    /// </summary>
    Cutoff = 1
}
