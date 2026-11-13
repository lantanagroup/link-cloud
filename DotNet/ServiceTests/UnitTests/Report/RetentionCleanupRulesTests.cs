using LantanaGroup.Link.Report.Data.Entities;
using LantanaGroup.Link.Report.Domain;
using LantanaGroup.Link.Report.Domain.Enums;
using LantanaGroup.Link.Shared.Application.Models.Retention;

namespace UnitTests.Report;

/// <summary>
/// Covers the cleanup status table and how a reply changes a stored result, including duplicate, stale and
/// late replies.
/// </summary>
[Trait("Category", "UnitTests")]
public class RetentionCleanupRulesTests
{
    private const RetentionCleanupResultStatus Pending = RetentionCleanupResultStatus.Pending;
    private const RetentionCleanupResultStatus Completed = RetentionCleanupResultStatus.Completed;
    private const RetentionCleanupResultStatus Failed = RetentionCleanupResultStatus.Failed;

    private static readonly DateTimeOffset Earlier = new(2026, 11, 5, 2, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Later = Earlier.AddHours(1);

    public static TheoryData<RetentionCleanupResultStatus[], RetentionCleanupStatus> StatusCases => new()
    {
        { [Pending, Pending], RetentionCleanupStatus.InProgress },
        { [Pending, Completed], RetentionCleanupStatus.InProgress },
        { [Completed, Completed], RetentionCleanupStatus.Completed },
        { [Completed, Failed], RetentionCleanupStatus.Partial },
        { [Pending, Completed, Failed], RetentionCleanupStatus.Partial },
        { [Pending, Failed], RetentionCleanupStatus.Failed },
        { [Failed, Failed], RetentionCleanupStatus.Failed }
    };

    [Theory]
    [MemberData(nameof(StatusCases))]
    public void CalculateStatus_Results_FollowsTheStatusTable(RetentionCleanupResultStatus[] results,
                                                              RetentionCleanupStatus expected)
    {
        Assert.Equal(expected, RetentionCleanupRules.CalculateStatus(results));
    }

    [Fact]
    public void CalculateStatus_NoResults_Throws()
    {
        Assert.Throws<ArgumentException>(() => RetentionCleanupRules.CalculateStatus([]));
    }

    [Fact]
    public void ApplyReply_PendingAndCompleted_RecordsTheReply()
    {
        var result = Result(Pending);

        var changed = RetentionCleanupRules.ApplyReply(result, Reply(RetentionResultStatus.Completed, 12, Earlier));

        Assert.True(changed);
        Assert.Equal(Completed, result.Status);
        Assert.Equal(12, result.RecordsAffected);
        Assert.Null(result.ErrorMessage);
        Assert.Equal(Earlier, result.CompletedDateTime);
    }

    [Fact]
    public void ApplyReply_PendingAndFailed_RecordsTheCountAndMessage()
    {
        var result = Result(Pending);

        var changed = RetentionCleanupRules.ApplyReply(
            result,
            Reply(RetentionResultStatus.Failed, 3, Earlier, "Delete failed (SqlException)."));

        Assert.True(changed);
        Assert.Equal(Failed, result.Status);
        Assert.Equal(3, result.RecordsAffected);
        Assert.Equal("Delete failed (SqlException).", result.ErrorMessage);
    }

    [Fact]
    public void ApplyReply_FailedWithoutMessage_StoresAPlaceholder()
    {
        var result = Result(Pending);

        RetentionCleanupRules.ApplyReply(result, Reply(RetentionResultStatus.Failed, 0, Earlier));

        Assert.False(string.IsNullOrWhiteSpace(result.ErrorMessage));
    }

    [Fact]
    public void ApplyReply_LongErrorMessage_IsTruncated()
    {
        var result = Result(Pending);
        var message = new string('x', RetentionCleanupRules.MaxErrorMessageLength + 10);

        RetentionCleanupRules.ApplyReply(result, Reply(RetentionResultStatus.Failed, 0, Earlier, message));

        Assert.Equal(RetentionCleanupRules.MaxErrorMessageLength, result.ErrorMessage.Length);
    }

    [Fact]
    public void ApplyReply_FailedThenCompleted_ReplacesTheFailure()
    {
        var result = Result(Failed, Later, "No response from Validation after 3 re-sends");

        var changed = RetentionCleanupRules.ApplyReply(result, Reply(RetentionResultStatus.Completed, 5, Earlier));

        Assert.True(changed);
        Assert.Equal(Completed, result.Status);
        Assert.Null(result.ErrorMessage);
        Assert.Equal(Earlier, result.CompletedDateTime);
    }

    [Fact]
    public void ApplyReply_CompletedThenFailed_ChangesNothing()
    {
        var result = Result(Completed, Earlier, recordsAffected: 7);

        var changed = RetentionCleanupRules.ApplyReply(result, Reply(RetentionResultStatus.Failed, 0, Later, "x"));

        Assert.False(changed);
        Assert.Equal(Completed, result.Status);
        Assert.Equal(7, result.RecordsAffected);
    }

    [Fact]
    public void ApplyReply_CompletedTwice_KeepsTheFirstCount()
    {
        var result = Result(Completed, Earlier, recordsAffected: 7);

        var changed = RetentionCleanupRules.ApplyReply(result, Reply(RetentionResultStatus.Completed, 0, Later));

        Assert.False(changed);
        Assert.Equal(7, result.RecordsAffected);
        Assert.Equal(Earlier, result.CompletedDateTime);
    }

    [Fact]
    public void ApplyReply_NewerFailure_ReplacesTheOlderOne()
    {
        var result = Result(Failed, Earlier, "first");

        var changed = RetentionCleanupRules.ApplyReply(result, Reply(RetentionResultStatus.Failed, 2, Later, "second"));

        Assert.True(changed);
        Assert.Equal("second", result.ErrorMessage);
        Assert.Equal(Later, result.CompletedDateTime);
    }

    [Fact]
    public void ApplyReply_SameOrOlderFailure_ChangesNothing()
    {
        var result = Result(Failed, Later, "current");

        Assert.False(RetentionCleanupRules.ApplyReply(result, Reply(RetentionResultStatus.Failed, 0, Later, "dup")));
        Assert.False(RetentionCleanupRules.ApplyReply(result, Reply(RetentionResultStatus.Failed, 0, Earlier, "old")));
        Assert.Equal("current", result.ErrorMessage);
    }

    [Fact]
    public void ApplyMissing_Pending_RecordsFailed()
    {
        var result = Result(Pending);

        var changed = RetentionCleanupRules.ApplyMissing(result, "MeasureEval", Earlier);

        Assert.True(changed);
        Assert.Equal(Failed, result.Status);
        Assert.Equal(0, result.RecordsAffected);
        Assert.Contains("MeasureEval", result.ErrorMessage);
        Assert.Equal(Earlier, result.CompletedDateTime);
    }

    [Theory]
    [InlineData(RetentionCleanupResultStatus.Completed)]
    [InlineData(RetentionCleanupResultStatus.Failed)]
    public void ApplyMissing_AlreadyAnswered_ChangesNothing(RetentionCleanupResultStatus status)
    {
        var result = Result(status, Earlier, "kept");

        Assert.False(RetentionCleanupRules.ApplyMissing(result, "MeasureEval", Later));
        Assert.Equal(status, result.Status);
        Assert.Equal(Earlier, result.CompletedDateTime);
    }

    [Fact]
    public void HasDefinedStatuses_CompletedAndFailed_ReturnsTrue()
    {
        var completion = Completion(Reply(RetentionResultStatus.Completed, 1, Later),
                                    Reply(RetentionResultStatus.Failed, 0, Later, "x"));

        Assert.True(RetentionCleanupRules.HasDefinedStatuses(completion));
    }

    [Fact]
    public void HasDefinedStatuses_UndefinedReportStatus_ReturnsFalse()
    {
        var completion = Completion(Reply(RetentionResultStatus.Completed, 1, Later),
                                    Reply((RetentionResultStatus)2, 0, Later));

        Assert.False(RetentionCleanupRules.HasDefinedStatuses(completion));
    }

    [Fact]
    public void HasDefinedStatuses_UndefinedSummaryStatus_ReturnsFalse()
    {
        var completion = Completion();
        completion.Reports = null;
        completion.Summary = Reply((RetentionResultStatus)2, 0, Later);

        Assert.False(RetentionCleanupRules.HasDefinedStatuses(completion));
    }

    [Fact]
    public void HasDefinedStatuses_NullReportEntry_ReturnsFalse()
    {
        var completion = Completion(new RetentionReportResult[] { null! });

        Assert.False(RetentionCleanupRules.HasDefinedStatuses(completion));
    }

    private static RetentionCheckCompleteValue Completion(params RetentionReportResult[] reports)
    {
        return new RetentionCheckCompleteValue
        {
            RetentionCleanupId = Guid.NewGuid().ToString(),
            ServiceName = "MeasureEval",
            CompletedDateTime = Later,
            Reports = [.. reports]
        };
    }

    private static RetentionCleanupResult Result(RetentionCleanupResultStatus status,
                                                 DateTimeOffset? completedDateTime = null,
                                                 string? errorMessage = null,
                                                 long? recordsAffected = null)
    {
        return new RetentionCleanupResult
        {
            Id = Guid.NewGuid(),
            Status = status,
            CompletedDateTime = completedDateTime,
            ErrorMessage = errorMessage,
            RecordsAffected = recordsAffected
        };
    }

    private static RetentionReportResult Reply(RetentionResultStatus status,
                                               long recordsAffected,
                                               DateTimeOffset completedDateTime,
                                               string? errorMessage = null)
    {
        return new RetentionReportResult
        {
            Status = status,
            RecordsAffected = recordsAffected,
            CompletedDateTime = completedDateTime,
            ErrorMessage = errorMessage
        };
    }
}
