namespace LantanaGroup.Link.Report.Domain.Enums;

/// <summary>
/// What Report did with one <c>RetentionCheckComplete</c> message.
/// </summary>
public enum RetentionCompletionOutcome
{
    /// <summary>
    /// The completion was applied to the cleanup, even if it changed nothing because it was a duplicate.
    /// </summary>
    Applied = 0,

    /// <summary>
    /// No cleanup has the completion's id, so nothing changed.
    /// </summary>
    UnknownCleanup = 1,

    /// <summary>
    /// The sender is not one of the cleanup's expected participants, so nothing changed.
    /// </summary>
    UnexpectedService = 2
}
