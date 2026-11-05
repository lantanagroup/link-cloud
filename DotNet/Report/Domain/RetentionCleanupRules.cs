using LantanaGroup.Link.Report.Data.Entities;
using LantanaGroup.Link.Report.Domain.Enums;
using LantanaGroup.Link.Shared.Application.Models.Retention;

namespace LantanaGroup.Link.Report.Domain;

/// <summary>
/// How a participant's reply changes a stored result, and how the results add up to the cleanup's status.
/// See dev-docs/retention-cleanup-record.md.
/// </summary>
public static class RetentionCleanupRules
{
    /// <summary>
    /// The longest error message stored on a result.
    /// </summary>
    public const int MaxErrorMessageLength = 2000;

    /// <summary>
    /// The cleanup's overall status from all of its results.
    /// </summary>
    /// <exception cref="ArgumentException">There are no results.</exception>
    public static RetentionCleanupStatus CalculateStatus(IReadOnlyCollection<RetentionCleanupResultStatus> results)
    {
        ArgumentNullException.ThrowIfNull(results);

        if (results.Count == 0)
        {
            throw new ArgumentException("A cleanup status needs at least one result.", nameof(results));
        }

        var anyCompleted = results.Contains(RetentionCleanupResultStatus.Completed);

        if (results.Contains(RetentionCleanupResultStatus.Failed))
        {
            return anyCompleted ? RetentionCleanupStatus.Partial : RetentionCleanupStatus.Failed;
        }

        return results.Contains(RetentionCleanupResultStatus.Pending)
            ? RetentionCleanupStatus.InProgress
            : RetentionCleanupStatus.Completed;
    }

    /// <summary>
    /// Applies one reply to a result. Returns false when the reply is a duplicate or stale and changes nothing.
    /// </summary>
    /// <remarks>
    /// A completed result never changes again: no request asks a participant again about a report it
    /// finished, so a later <c>Failed</c> can only be a stale or duplicate reply. A <c>Completed</c> reply
    /// always replaces a failure, including one Report recorded itself. A failure replaces an earlier failure
    /// only when it finished later.
    /// </remarks>
    public static bool ApplyReply(RetentionCleanupResult result, RetentionReportResult reply)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(reply);

        if (result.Status == RetentionCleanupResultStatus.Completed)
        {
            return false;
        }

        if (reply.Status == RetentionResultStatus.Failed &&
            result.Status == RetentionCleanupResultStatus.Failed &&
            reply.CompletedDateTime <= result.CompletedDateTime)
        {
            return false;
        }

        var completed = reply.Status == RetentionResultStatus.Completed;
        result.Status = completed ? RetentionCleanupResultStatus.Completed : RetentionCleanupResultStatus.Failed;
        result.RecordsAffected = reply.RecordsAffected;
        result.ErrorMessage = completed ? null : Truncate(reply.ErrorMessage ?? "No error message was given.");
        result.CompletedDateTime = reply.CompletedDateTime;

        return true;
    }

    /// <summary>
    /// Records a pending result as failed because the participant's completion left it out. Returns false,
    /// changing nothing, when the result already has an answer.
    /// </summary>
    /// <remarks>
    /// Only a pending result can be missing. A retry asks only about failed reports, so a completed or failed
    /// result left out of a later completion was not in that request.
    /// </remarks>
    public static bool ApplyMissing(RetentionCleanupResult result,
                                    string serviceName,
                                    DateTimeOffset completedDateTime)
    {
        ArgumentNullException.ThrowIfNull(result);

        if (result.Status != RetentionCleanupResultStatus.Pending)
        {
            return false;
        }

        result.Status = RetentionCleanupResultStatus.Failed;
        result.RecordsAffected = 0;
        result.ErrorMessage = Truncate($"Missing from the {serviceName} completion.");
        result.CompletedDateTime = completedDateTime;

        return true;
    }

    private static string Truncate(string text)
    {
        return text.Length <= MaxErrorMessageLength ? text : text[..MaxErrorMessageLength];
    }
}
